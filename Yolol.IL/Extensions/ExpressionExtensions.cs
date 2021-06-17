using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Sigil;
using Yolol.IL.Compiler;
using Type = System.Type;

namespace Yolol.IL.Extensions
{
    internal static class ExpressionExtensions
    {
        /// <summary>
        /// Emit IL for the given expression
        /// </summary>
        /// <typeparam name="TInLeft"></typeparam>
        /// <typeparam name="TInRight"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="expr"></param>
        /// <param name="emitter"></param>
        /// <param name="errorLabel"></param>
        /// <returns>A tuple, indicating the type left on the stack by this expression and if the expression **is** potentially fallible (i.e. errorLabel may be jumped to)</returns>
        public static (Type, bool) ConvertBinary<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertBinaryFastPath(expr, emitter, errorLabel);
            if (fast != null)
                return fast.Value;

            // Put the parameters into local, ready to be used later
            using var parameterRight = emitter.DeclareLocal(typeof(TInRight), "ConvertBinary_Right", false);
            emitter.StoreLocal(parameterRight);
            using var parameterLeft = emitter.DeclareLocal(typeof(TInLeft), "ConvertBinary_Left", false);
            emitter.StoreLocal(parameterLeft);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name!, parameterLeft },
                { expr.Parameters[1].Name!, parameterRight },
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel, 0);
        }

        /// <summary>
        /// Emit IL for the given expression
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="expr"></param>
        /// <param name="emitter"></param>
        /// <param name="errorLabel"></param>
        /// <returns>A tuple, indicating the type left on the stack by this expression and if the expression **is** potentially fallible (i.e. errorLabel may be jumped to)</returns>
        public static (Type, bool) ConvertUnary<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertUnaryFastPath(expr, emitter, errorLabel);
            if (fast != null)
                return fast.Value;

            // Put the parameter into a local, ready to be used later
            using var parameter = emitter.DeclareLocal(typeof(TIn), "ConvertUnary_In", false);
            emitter.StoreLocal(parameter);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name!, parameter }
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel, 0);
        }

        private static (Type, bool) ConvertExpression<TEmit>(Expression expr, Emit<TEmit> emitter, IReadOnlyDictionary<string, Local> parameters, Label errorLabel, int stackSize)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.New: {
                    var n = (NewExpression)expr;
                    var fallible = false;
                    foreach (var arg in n.Arguments)
                    {
                        var (_, e) = ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                        fallible |= e;
                        stackSize++;
                    }

                    emitter.NewObject(n.Constructor);
                    return (n.Type, fallible);
                }

                case ExpressionType.Constant: {
                    var constant = (ConstantExpression)expr;
                    if (constant.Type == typeof(string))
                    {
                        emitter.LoadConstant((string)constant.Value!);
                        return (typeof(string), false);
                    }

                    if (constant.Type == typeof(bool))
                    {
                        emitter.LoadConstant((bool)constant.Value!);
                        return (typeof(bool), false);
                    }

                    if (constant.Type == typeof(int))
                    {
                        emitter.LoadConstant((int)constant.Value!);
                        return (typeof(int), false);
                    }
                    
                    throw new NotSupportedException($"Constant type: `{constant.Type.Name}`");
                }

                case ExpressionType.Parameter: {
                    var param = (ParameterExpression)expr;
                    emitter.LoadLocal(parameters[param.Name!]);
                    return (expr.Type, false);
                }

                case ExpressionType.Not: {
                    var unary = (UnaryExpression)expr;
                    var (_, e) = ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.LogicalNot))
                        : unary.Operand.Type.GetMethod("op_LogicalNot");
                    emitter.Call(m);
                    return (m!.ReturnType, e);
                }

                case ExpressionType.Negate: {
                    var unary = (UnaryExpression)expr;
                    var (_, e) = ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.BoolNegate))
                        : unary.Operand.Type.GetMethod("op_UnaryNegation");
                    emitter.Call(m);
                    return (m!.ReturnType, e);
                }

                case ExpressionType.Convert: {
                    var unary = (UnaryExpression)expr;
                    var (_, e) = ConvertExpression(unary.Operand, emitter, parameters, errorLabel, stackSize);
                    emitter.Call(unary.Method);
                    return (unary.Method!.ReturnType, e);
                }

                case ExpressionType.Call: {
                    var c = (MethodCallExpression)expr;
                    if (c.Method.IsStatic)
                    {
                        var fallible = false;
                        foreach (var arg in c.Arguments)
                        {
                            var (_, e) = ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                            fallible |= e;
                            stackSize++;
                        }

                        emitter.Call(c.Method);
                        return (c.Method.ReturnType, fallible);
                    }
                    else
                    {
                        var (_, fallible) = ConvertExpression(c.Object!, emitter, parameters, errorLabel, stackSize);
                        stackSize++;

                        using (var local = emitter.DeclareLocal(c.Object!.Type, "ConvertExpression_NonStaticCall", false))
                        {
                            emitter.StoreLocal(local);
                            emitter.LoadLocalAddress(local);
                            foreach (var arg in c.Arguments)
                            {
                                var (_, e) = ConvertExpression(arg, emitter, parameters, errorLabel, stackSize);
                                fallible |= e;
                                stackSize++;
                            }

                            emitter.Call(c.Method);
                            return (c.Method.ReturnType, fallible);
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
                    var (lt, el) = ConvertExpression(binary.Left, emitter, parameters, errorLabel, stackSize);
                    var (rt, er) = ConvertExpression(binary.Right, emitter, parameters, errorLabel, stackSize + 1);

                    var (t, e) = ConvertBinaryWithErrorHandling(lt, rt, binary, emitter, errorLabel, stackSize + 2);

                    var err = e | el | er;
                    return (t, err);
                }

                default:
                    throw new NotImplementedException(expr.NodeType + " " + expr.GetType());
            }
        }

        /// <summary>
        /// Convert a binary method, assuming the two inputs are already on the stack
        /// </summary>
        /// <returns></returns>
        private static (Type, bool) ConvertBinaryWithErrorHandling<TEmit, TExpr>(Type leftType, Type rightType, TExpr binary, Emit<TEmit> emitter, Label errorLabel, int stackSize)
            where TExpr : BinaryExpression
        {
            if (binary.Method == null)
                throw new InvalidOperationException("Cannot convert binary method with null `Method`");
            if (binary.Method.DeclaringType == null)
                throw new InvalidOperationException("Cannot convert binary method with null `DeclaringType`");

            var errorData = binary.Method.TryGetErrorMetadata(leftType, rightType);

            if (errorData != null)
            {
                if (errorData.Value.WillThrow == null)
                    throw new NotImplementedException("Null `WillThrow` method");

                // Save the left and right parameters into locals
                using var rl = emitter.DeclareLocal(rightType, "ConvertBinaryWithErrorHandling_Right", false);
                emitter.StoreLocal(rl);
                using var ll = emitter.DeclareLocal(leftType, "ConvertBinaryWithErrorHandling_Left", false);
                emitter.StoreLocal(ll);

                // Put left and right parameters back onto stack
                emitter.LoadLocal(ll);
                emitter.LoadLocal(rl);

                // Invoke the `will throw` method to discover if this invocation would trigger a runtime error
                emitter.Call(errorData.Value.WillThrow);

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

            // If the error metadata specifies an alternative implementation to use (which does not include runtime checks
            // for the case already checked by `WillThrow`) use that alternative instead.
            if (errorData != null && errorData.Value.UnsafeAlternative != null)
            {
                emitter.Call(errorData.Value.UnsafeAlternative);
                return (errorData.Value.UnsafeAlternative!.ReturnType, true);
            }
            else
            {
                emitter.Call(binary.Method);
                return (binary.Method!.ReturnType, errorData != null);
            }


        }

        private static (Type, bool)? TryConvertBinaryFastPath<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
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

        private static (Type, bool)? TryConvertUnaryFastPath<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter, Label errorLabel)
        {
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Parameter:
                    var p = (ParameterExpression)expr.Body;
                    return (p.Type, false);

                case ExpressionType.Constant:
                    emitter.Pop();
                    return ConvertExpression(expr.Body, emitter, new Dictionary<string, Local>(), errorLabel, 0);

                default:
                    return null;
            }
        }
    }
}
