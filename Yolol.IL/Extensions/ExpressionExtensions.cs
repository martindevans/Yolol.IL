using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Sigil;
using Yolol.IL.Compiler;

namespace Yolol.IL.Extensions
{
    internal static class ExpressionExtensions
    {
        public static Type ConvertUnary<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, Emit<TEmit> emitter)
        {
            // Special case the "Parameter" type to do nothing. There is only one parameter and it is already on the stack
            if (expr.Body.NodeType == ExpressionType.Parameter)
            {
                var p = (ParameterExpression)expr.Body;
                return p.Type;
            }

            // Put the parameter into a local, ready to be used later
            var parameter = emitter.DeclareLocal(typeof(TIn));
            emitter.StoreLocal(parameter);
            var parameters = new Dictionary<string, Local> {
                { expr.Parameters[0].Name, parameter }
            };
            
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Not:
                case ExpressionType.Negate:
                case ExpressionType.Equal:
                    return ConvertExpression(expr.Body, emitter, parameters);

                case ExpressionType.Call:
                    var c = (MethodCallExpression)expr.Body;
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

                case ExpressionType.New:
                    var n = (NewExpression)expr.Body;
                    foreach (var arg in n.Arguments)
                        ConvertExpression(arg, emitter, parameters);
                    emitter.NewObject(n.Constructor);
                    return n.Type;

                case ExpressionType.Constant:
                    ConvertExpression(expr.Body, emitter, parameters);
                    return expr.Body.Type;


                default:
                    throw new NotImplementedException(expr.Body.NodeType.ToString() + " " + expr.Body.GetType());
            }
        }

        private static Type ConvertExpression<TEmit>(Expression expr, Emit<TEmit> emitter, IReadOnlyDictionary<string, Local> parameters)
        {
            switch (expr.NodeType)
            {
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

                case ExpressionType.Equal: {
                    var binary = (BinaryExpression)expr;
                    ConvertExpression(binary.Left, emitter, parameters);
                    ConvertExpression(binary.Right, emitter, parameters);
                    emitter.Call(binary.Method);
                    return binary.Method.ReturnType;
                }

                default:
                    throw new NotImplementedException(expr.NodeType.ToString() + " " + expr.GetType());
            }
        }
    }
}
