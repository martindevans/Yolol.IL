using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Sigil;
using Yolol.Execution.Attributes;
using Yolol.IL.Compiler;
using Type = System.Type;

namespace Yolol.IL.Extensions
{
    internal static class ExpressionExtensions
    {
        public static Type ConvertBinary<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertBinaryFastPath(expr, emitter, errorLabel);
            if (fast != null)
                return fast;

            // Put the parameters into local, ready to be used later
            using var parameterRight = emitter.DeclareLocal(typeof(TInRight));
            emitter.StoreLocal(parameterRight);
            using var parameterLeft = emitter.DeclareLocal(typeof(TInLeft));
            emitter.StoreLocal(parameterLeft);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name, parameterLeft },
                { expr.Parameters[1].Name, parameterRight },
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel, 0);
        }

        public static Type ConvertUnary<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertUnaryFastPath(expr, emitter, errorLabel);
            if (fast != null)
                return fast;

            // Put the parameter into a local, ready to be used later
            using var parameter = emitter.DeclareLocal(typeof(TIn));
            emitter.StoreLocal(parameter);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name, parameter }
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel, 0);
        }

        private static Type ConvertExpression<TEmit>(Expression expr, Emit<TEmit> emitter, IReadOnlyDictionary<string, Local> parameters, Label errorLabel, int stackSize)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.New: {
                    var n = (NewExpression)expr;
                    foreach (var arg in n.Arguments)
                    {
                        ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                        stackSize++;
                    }

                    emitter.NewObject(n.Constructor);
                    return n.Type;
                }

                case ExpressionType.Constant: {
                    var constant = (ConstantExpression)expr;
                    if (constant.Type == typeof(string))
                    {
                        emitter.LoadConstant((string)constant.Value);
                        return typeof(string);
                    }

                    if (constant.Type == typeof(bool))
                    {
                        emitter.LoadConstant((bool)constant.Value);
                        return typeof(bool);
                    }

                    if (constant.Type == typeof(int))
                    {
                        emitter.LoadConstant((int)constant.Value);
                        return typeof(int);
                    }
                    
                    throw new NotSupportedException($"Constant type: `{constant.Type.Name}`");
                }

                case ExpressionType.Parameter: {
                    var param = (ParameterExpression)expr;
                    emitter.LoadLocal(parameters[param.Name]);
                    return expr.Type;
                }

                case ExpressionType.Not: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.LogicalNot))
                        : unary.Operand.Type.GetMethod("op_LogicalNot");
                    emitter.Call(m);
                    return m!.ReturnType;
                }

                case ExpressionType.Negate: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.BoolNegate))
                        : unary.Operand.Type.GetMethod("op_UnaryNegation");
                    emitter.Call(m);
                    return m!.ReturnType;
                }

                case ExpressionType.Convert: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    emitter.Call(unary.Method);
                    return unary.Method.ReturnType;
                }

                case ExpressionType.Call: {
                    var c = (MethodCallExpression)expr;
                    if (c.Method.IsStatic)
                    {
                        foreach (var arg in c.Arguments)
                        {
                            ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                            stackSize++;
                        }

                        emitter.Call(c.Method);
                        return c.Method.ReturnType;
                    }
                    else
                    {
                        ConvertExpression(c.Object!, emitter, parameters, errorLabel, stackSize);
                        stackSize++;

                        using (var local = emitter.DeclareLocal(c.Object!.Type))
                        {
                            emitter.StoreLocal(local);
                            emitter.LoadLocalAddress(local);
                            foreach (var arg in c.Arguments)
                            {
                                ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                                stackSize++;
                            }

                            emitter.Call(c.Method);
                            return c.Method.ReturnType;
                        }
                    }
                }

                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual: {
                    var binary = (BinaryExpression)expr;

                    // Put left and right values on the stack
                    var lt = ConvertExpression(binary.Left, emitter, parameters, errorLabel, stackSize);
                    var rt = ConvertExpression(binary.Right, emitter, parameters, errorLabel, stackSize + 1);

                    return ConvertBinaryWithErrorHandling(lt, rt, binary, emitter, errorLabel, stackSize + 2);

                    //if (binary.Method == null)
                    //    throw new InvalidOperationException("Cannot convert binary method with null `Method`");
                    //if (binary.Method.DeclaringType == null)
                    //    throw new InvalidOperationException("Cannot convert binary method with null `DeclaringType`");
                    //emitter.Call(binary.Method);
                    //return binary.Method!.ReturnType;
                }

                default:
                    throw new NotImplementedException(expr.NodeType + " " + expr.GetType());
            }
        }

        /// <summary>
        /// Convert a binary method, assuming the two inputs are already on the stack
        /// </summary>
        /// <returns></returns>
        private static Type ConvertBinaryWithErrorHandling<TEmit, TExpr>(Type leftType, Type rightType, TExpr binary, Emit<TEmit> emitter, Label errorLabel, int stackSize)
            where TExpr : BinaryExpression
        {
            if (binary.Method == null)
                throw new InvalidOperationException("Cannot convert binary method with null `Method`");
            if (binary.Method.DeclaringType == null)
                throw new InvalidOperationException("Cannot convert binary method with null `DeclaringType`");

            // Reflect out the error metadata, this can tell us in advance if the method will throw
            var attrs = binary.Method.GetCustomAttributes(typeof(ErrorMetadataAttribute), false);
            if (attrs.Length > 1)
                throw new InvalidOperationException("Method has more than one `ErrorMetadataAttribute`");
            if (attrs.Length > 0)
            {
                var attr = (ErrorMetadataAttribute)attrs[0];
                if (!attr.IsInfallible && attr.WillThrow != null)
                {
                    // Save the left and right parameters into locals
                    using var rl = emitter.DeclareLocal(rightType);
                    emitter.StoreLocal(rl);
                    using var ll = emitter.DeclareLocal(leftType);
                    emitter.StoreLocal(ll);

                    // Put left and right parameters back onto stack
                    emitter.LoadLocal(ll);
                    emitter.LoadLocal(rl);

                    // Get the `will throw` method which tells us if a given pair of values would cause a runtime error
                    var willThrow = binary.Method.DeclaringType.GetMethod(attr.WillThrow, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { leftType, rightType }, null);
                    if (willThrow == null)
                        throw new InvalidOperationException($"ErrorMetadataAttribute references an invalid method: `{attr.WillThrow}`");
                    if (willThrow.ReturnType != typeof(bool))
                        throw new InvalidOperationException($"ErrorMetadataAttribute references an method which does not return bool: `{attr.WillThrow}`");

                    // Invoke the `will throw` method to discover if this invocation would trigger a runtime error
                    emitter.Call(willThrow);

                    // Create a label to jump past the error handling for the normal case
                    var noThrowLabel = emitter.DefineLabel();

                    // Jump past error handling if this is ok
                    emitter.BranchIfFalse(noThrowLabel);

                    // If execution reaches here it means an error would occur in this operation. First empty out the stack and then jump
                    // to the error handling label for this expression.
                    // There are two less things on the stack than indicated by stackSize because the two parameter to this method have already been taken off the stack.
                    for (var i = 0; i < stackSize - 2; i++)
                        emitter.Pop();
                    emitter.Branch(errorLabel);

                    // Put the left and right sides back onto the stack
                    emitter.MarkLabel(noThrowLabel);
                    emitter.LoadLocal(ll);
                    emitter.LoadLocal(rl);
                }
            }

            emitter.Call(binary.Method);
            return binary.Method!.ReturnType;
        }

        private static Type? TryConvertBinaryFastPath<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Add:
                case ExpressionType.Subtract:
                case ExpressionType.Multiply:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    var binary = (BinaryExpression)expr.Body;
                    if (binary.Method == null)
                        return null;

                    // Fast path conversion only works if the two arguments are parameters, consumed in the order they were given
                    if (!(binary.Left is ParameterExpression pl) || pl.Name != expr.Parameters[0].Name)
                        return null;
                    if (!(binary.Right is ParameterExpression pr) || pr.Name != expr.Parameters[1].Name)
                        return null;

                    return ConvertBinaryWithErrorHandling(expr.Parameters[0].Type, expr.Parameters[1].Type, binary, emitter, errorLabel, 2);

                default:
                    return null;
            }
        }

        private static Type? TryConvertUnaryFastPath<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Parameter:
                    var p = (ParameterExpression)expr.Body;
                    return p.Type;

                case ExpressionType.Constant:
                    emitter.Pop();
                    return ConvertExpression(expr.Body, emitter, new Dictionary<string, Local>(), errorLabel, 0);

                default:
                    return null;
            }
        }
    }
}
