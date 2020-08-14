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
        public static Type ConvertBinary<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = ConvertBinaryFastPath(expr, emitter);
            if (fast != null)
                return fast;

            // Put the parameters into local, ready to be used later
            var parameterRight = emitter.DeclareLocal(typeof(TInRight));
            emitter.StoreLocal(parameterRight);
            var parameterLeft = emitter.DeclareLocal(typeof(TInLeft));
            emitter.StoreLocal(parameterLeft);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name, parameterLeft },
                { expr.Parameters[1].Name, parameterRight },
            };

            try
            {
                return ConvertExpression(expr.Body, emitter, parameters);
            }
            finally
            {
                foreach (var local in parameters)
                    local.Value.Dispose();
            }
        }

        public static Type ConvertUnary<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = ConvertUnaryFastPath(expr, emitter);
            if (fast != null)
                return fast;

            // Put the parameter into a local, ready to be used later
            var parameter = emitter.DeclareLocal(typeof(TIn));
            emitter.StoreLocal(parameter);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name, parameter }
            };

            try
            {
                return ConvertExpression(expr.Body, emitter, parameters);
            }
            finally
            {
                foreach (var local in parameters)
                    local.Value.Dispose();
            }
        }

        private static Type ConvertExpression<TEmit>(Expression expr, Emit<TEmit> emitter, IReadOnlyDictionary<string, Local> parameters)
        {
            switch (expr.NodeType)
            {
                case ExpressionType.New: {
                    var n = (NewExpression)expr;
                    foreach (var arg in n.Arguments)
                        ConvertExpression(arg, emitter, parameters);
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
                    ConvertExpression(unary.Operand, emitter, parameters);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.LogicalNot))
                        : unary.Operand.Type.GetMethod("op_LogicalNot");
                    emitter.Call(m);
                    return m.ReturnType;
                }

                case ExpressionType.Negate: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.BoolNegate))
                        : unary.Operand.Type.GetMethod("op_UnaryNegation");
                    emitter.Call(m);
                    return m.ReturnType;
                }

                case ExpressionType.Convert: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters);
                    emitter.Call(unary.Method);
                    return unary.Method.ReturnType;
                }

                case ExpressionType.Call: {
                    var c = (MethodCallExpression)expr;
                    if (c.Method.IsStatic)
                    {
                        foreach (var arg in c.Arguments)
                            ConvertExpression(arg, emitter, parameters);
                        emitter.Call(c.Method);
                        return c.Method.ReturnType;
                    }
                    else
                    {
                        ConvertExpression(c.Object, emitter, parameters);
                        using (var local = emitter.DeclareLocal(c.Object.Type))
                        {
                            emitter.StoreLocal(local);
                            emitter.LoadLocalAddress(local);
                            foreach (var arg in c.Arguments)
                                ConvertExpression(arg, emitter, parameters);
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
                    ConvertExpression(binary.Left, emitter, parameters);
                    ConvertExpression(binary.Right, emitter, parameters);
                    emitter.Call(binary.Method);
                    return binary.Method.ReturnType;
                }

                default:
                    throw new NotImplementedException(expr.NodeType + " " + expr.GetType());
            }
        }

        private static Type? ConvertBinaryFastPath<TInLeft, TInRight, TOut, TEmit>(this Expression<Func<TInLeft, TInRight, TOut>> expr, Emit<TEmit> emitter)
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
                    if (!(binary.Left is ParameterExpression pl) || pl.Name != expr.Parameters[0].Name)
                        return null;
                    if (!(binary.Right is ParameterExpression pr) || pr.Name != expr.Parameters[1].Name)
                        return null;
                    emitter.Call(binary.Method);
                    return binary.Method.ReturnType;

                default:
                    return null;
            }
        }

        private static Type? ConvertUnaryFastPath<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter)
        {
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Parameter:
                    var p = (ParameterExpression)expr.Body;
                    return p.Type;

                case ExpressionType.Constant:
                    emitter.Pop();
                    return ConvertExpression(expr.Body, emitter, new Dictionary<string, Local>());

                default:
                    return null;
            }
        }
    }
}
