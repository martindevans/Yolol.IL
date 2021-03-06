﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Compiler.Emitter;
using Type = System.Type;
using ExceptionBlock = Yolol.IL.Compiler.Emitter.Instructions.ExceptionBlock;

namespace Yolol.IL.Extensions
{
    public readonly struct ConvertResult
    {
        public readonly Type OnStack;
        public readonly bool IsFallible;
        public readonly Value? StaticValue;

        public ConvertResult(Type onStack, bool isFallible, Value? staticValue)
        {
            OnStack = onStack;
            IsFallible = isFallible;
            StaticValue = staticValue;
        }

        public void Deconstruct(out Type type, out bool fallible)
        {
            type = OnStack;
            fallible = IsFallible;
        }
    }

    internal static class ExpressionExtensions
    {
        private readonly struct Parameter
        {
            /// <summary>
            /// The local which stores this value
            /// </summary>
            public readonly Local Local;

            /// <summary>
            /// If this parameter is constant, it's value. Otherwise null.
            /// </summary>
            public readonly Value? Constant;

            public Parameter(Local local, Value? constant)
            {
                Local = local;
                Constant = constant;
            }
        }

        #region top level converters
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
        /// <param name="leftConst">Indicates if the left parameter is a constant</param>
        /// <param name="rightConst">Indicates if the right parameter is a constant</param>
        /// <returns>A tuple, indicating the type left on the stack by this expression and if the expression **is** potentially fallible (i.e. errorLabel may be jumped to)</returns>
        public static ConvertResult ConvertBinary<TInLeft, TInRight, TOut, TEmit>(
            this Expression<Func<TInLeft, TInRight, TOut>> expr,
            OptimisingEmitter<TEmit> emitter,
            ExceptionBlock errorLabel,
            Value? leftConst,
            Value? rightConst
        )
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertBinaryFastPath(expr, emitter, errorLabel, leftConst, rightConst);
            if (fast != null)
                return fast.Value;

            // Put the parameters into local, ready to be used later
            using var localRight = emitter.DeclareLocal(typeof(TInRight), "ConvertBinary_Right", false);
            emitter.StoreLocal(localRight);
            using var localLeft = emitter.DeclareLocal(typeof(TInLeft), "ConvertBinary_Left", false);
            emitter.StoreLocal(localLeft);
            var parameters = new Dictionary<string, Parameter> {
                { expr.Parameters[0].Name!, new Parameter(localLeft, leftConst) },
                { expr.Parameters[1].Name!, new Parameter(localRight, rightConst) },
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel);
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
        public static ConvertResult ConvertUnary<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, OptimisingEmitter<TEmit> emitter, ExceptionBlock errorLabel)
        {
            // Try to convert expression without putting things into locals. Only works with certain expressions.
            var fast = TryConvertUnaryFastPath(expr, emitter, errorLabel);
            if (fast != null)
                return fast.Value;

            // Put the parameter into a local, ready to be used later
            using var parameter = emitter.DeclareLocal(typeof(TIn), "ConvertUnary_In", false);
            emitter.StoreLocal(parameter);
            var parameters = new Dictionary<string, Parameter> {
                { expr.Parameters[0].Name!, new Parameter(parameter, null) }
            };

            return ConvertExpression(expr.Body, emitter, parameters, errorLabel);
        }
        #endregion

        private static ConvertResult ConvertExpression<TEmit>(
            Expression expr,
            OptimisingEmitter<TEmit> emitter,
            IReadOnlyDictionary<string, Parameter> parameters,
            ExceptionBlock errorLabel
        )
        {
            switch (expr.NodeType)
            {
                case ExpressionType.New: {
                    var n = (NewExpression)expr;
                    var fallible = false;
                    foreach (var arg in n.Arguments)
                    {
                        var result = ConvertExpression(arg, emitter, parameters, errorLabel);
                        fallible |= result.IsFallible;
                    }

                    emitter.NewObject(n.Constructor);

                    return new ConvertResult(n.Type, fallible, null);
                }

                case ExpressionType.Constant: {
                    var constant = (ConstantExpression)expr;
                    if (constant.Type == typeof(string))
                    {
                        emitter.LoadConstant((string)constant.Value!);
                        return new ConvertResult(typeof(string), false, (string)constant.Value!);
                    }

                    if (constant.Type == typeof(bool))
                    {
                        emitter.LoadConstant((bool)constant.Value!);
                        return new ConvertResult(typeof(bool), false, (Number)(bool)constant.Value!);
                    }

                    if (constant.Type == typeof(int))
                    {
                        emitter.LoadConstant((int)constant.Value!);
                        return new ConvertResult(typeof(int), false, (Number)(int)constant.Value!);
                    }
                    
                    throw new NotSupportedException($"Constant type: `{constant.Type.Name}`");
                }

                case ExpressionType.Parameter: {
                    var paramExpr = (ParameterExpression)expr;
                    var param = parameters[paramExpr.Name!];
                    emitter.LoadLocal(param.Local);
                    return new ConvertResult(expr.Type, false, param.Constant);
                }

                case ExpressionType.Not: {
                    var unary = (UnaryExpression)expr;
                    var result = ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.LogicalNot))
                        : unary.Operand.Type.GetMethod("op_LogicalNot");
                    emitter.Call(m!);
                    return new ConvertResult(m!.ReturnType, result.IsFallible, null);
                }

                case ExpressionType.Negate: {
                    var unary = (UnaryExpression)expr;
                    var result = ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.BoolNegate))
                        : unary.Operand.Type.GetMethod("op_UnaryNegation");
                    emitter.Call(m!);
                    return new ConvertResult(m!.ReturnType, result.IsFallible, null);
                }

                case ExpressionType.Convert: {
                    var unary = (UnaryExpression)expr;
                    var result = ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    emitter.Call(unary.Method);
                    return new ConvertResult(unary.Method!.ReturnType, result.IsFallible, null);
                }

                case ExpressionType.Call: {
                    var c = (MethodCallExpression)expr;
                    if (c.Method.IsStatic)
                    {
                        var idx = 0;
                        var types = new Type[c.Arguments.Count];
                        var staticValues = new Value?[c.Arguments.Count];
                        var fallible = false;
                        foreach (var arg in c.Arguments)
                        {
                            var result = ConvertExpression(arg, emitter, parameters, errorLabel);
                            types[idx] = result.OnStack;
                            staticValues[idx] = result.StaticValue;
                            idx++;
                            fallible |= result.IsFallible;
                        }

                        var (tc, ec) = ConvertCallWithErrorHandling(types, staticValues, c.Method, emitter, errorLabel);
                        return new ConvertResult(tc, ec | fallible, null);
                    }
                    else
                    {
                        var (_, fallible) = ConvertExpression(c.Object!, emitter, parameters, errorLabel);

                        using (var local = emitter.DeclareLocal(c.Object!.Type, "ConvertExpression_NonStaticCall", false))
                        {
                            emitter.StoreLocal(local);
                            emitter.LoadLocalAddress(local);
                            foreach (var arg in c.Arguments)
                            {
                                var (_, e) = ConvertExpression(arg, emitter, parameters, errorLabel);
                                fallible |= e;
                            }

                            emitter.Call(c.Method);
                            return new ConvertResult(c.Method.ReturnType, fallible, null);
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
                    var leftResult = ConvertExpression(binary.Left, emitter, parameters, errorLabel);
                    var rightResult = ConvertExpression(binary.Right, emitter, parameters, errorLabel);

                    var finalResult = ConvertCallWithErrorHandling(
                        new[] { leftResult.OnStack, rightResult.OnStack },
                        new[] { leftResult.StaticValue, rightResult.StaticValue },
                        binary.Method!,
                        emitter,
                        errorLabel
                    );

                    var err = finalResult.IsFallible | leftResult.IsFallible | rightResult.IsFallible;
                    return new ConvertResult(finalResult.OnStack, err, finalResult.StaticValue);
                }

                case ExpressionType.And: {
                    var binary = (BinaryExpression)expr;

                    // Put left and right values on the stack
                    var leftResult = ConvertExpression(binary.Left, emitter, parameters, errorLabel);
                    var rightResult = ConvertExpression(binary.Right, emitter, parameters, errorLabel);


                    var staticVal = (Value?)null;
                    if (leftResult.StaticValue.HasValue && rightResult.StaticValue.HasValue)
                        staticVal = (Number)(leftResult.StaticValue.Value.ToBool() & rightResult.StaticValue.Value.ToBool());

                    emitter.And();

                    return new ConvertResult(typeof(bool), false, staticVal);
                }

                default:
                    throw new NotImplementedException(expr.NodeType + " " + expr.GetType());
            }
        }

        private static ConvertResult ConvertCallWithErrorHandling<TEmit>(
            Type[] parameterTypes,
            Value?[]? staticValues,
            MethodInfo method,
            OptimisingEmitter<TEmit> emitter,
            ExceptionBlock errorLabel
        )
        {
            if (method.DeclaringType == null)
                throw new InvalidOperationException("Cannot convert call with null `DeclaringType`");

            // Check for errors, early out if this is guaranteed to be an error
            var errorData = method.TryGetErrorMetadata(parameterTypes);
            var infallible = (errorData?.IsInfallible ?? false);
            if (errorData != null)
                infallible |= CheckForRuntimeError(errorData.Value, parameterTypes, staticValues, emitter, errorLabel);

            // If the error metadata specifies an alternative implementation to use (which does not include runtime checks
            // for the case already checked by `WillThrow`) use that alternative instead.
            if (errorData != null && errorData.Value.UnsafeAlternative != null)
            {
                emitter.Call(errorData.Value.UnsafeAlternative);
                return new ConvertResult(errorData.Value.UnsafeAlternative!.ReturnType, !infallible, null);
            }
            else
            {
                emitter.Call(method);
                return new ConvertResult(method.ReturnType, !infallible, null);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="errorData"></param>
        /// <param name="parameterTypes"></param>
        /// <param name="staticValues"></param>
        /// <param name="emitter"></param>
        /// <param name="errorLabel"></param>
        /// <returns>If the method is infallible (i.e. is statically known not to throw)</returns>
        private static bool CheckForRuntimeError<TEmit>(
            MethodInfoExtensions.ErrorMetadata errorData,
            Type[] parameterTypes,
            Value?[]? staticValues,
            OptimisingEmitter<TEmit> emitter,
            ExceptionBlock errorLabel
        )
        {
            if (staticValues != null && staticValues.Length != parameterTypes.Length)
                throw new ArgumentException("incorrect number of static values");

            // If it's infallible there's no point checking for errors!
            if (errorData.IsInfallible)
                return true;

            // Determine it we can statically determine whether this is an error
            if (staticValues != null)
            {
                var staticError = errorData.StaticWillThrow(staticValues);
                if (staticError.HasValue)
                {
                    // If we statically know it's not going to error return now, the rest of the error handling code is unnecessary.
                    if (!staticError.Value)
                        return true;

                    // We statically know that this _will_ trigger an error. In principle this could clear up the stack and early exit
                    // but it's such a rare case it's not worth the extra complexity.
                }
            }

            if (errorData.WillThrow == null)
                throw new InvalidOperationException("Null `WillThrow` method");

            // Save the parameters into locals
            var parameterLocals = new List<Local>();
            for (var i = parameterTypes.Length - 1; i >= 0; i--)
            {
                var local = emitter.DeclareLocal(parameterTypes[i], $"ConvertCallWithErrorHandling_{parameterLocals.Count}", false);
                emitter.StoreLocal(local);
                parameterLocals.Add(local);
            }

            errorData.EmitDynamicWillThrow(emitter, errorLabel, parameterLocals);

            // Put the parameters back onto the stack
            for (var i = parameterLocals.Count - 1; i >= 0; i--)
                emitter.LoadLocal(parameterLocals[i]);

            // Dispose locals used for holding parameters
            foreach (var parameterLocal in parameterLocals)
                parameterLocal.Dispose();

            return false;
        }

        #region fast path
        private static ConvertResult? TryConvertBinaryFastPath<TLeft, TRight, TOut, TEmit>(
            this Expression<Func<TLeft, TRight, TOut>> expr,
            OptimisingEmitter<TEmit> emitter,
            ExceptionBlock errorLabel,
            Value? leftConst,
            Value? rightConst
        )
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
                case ExpressionType.GreaterThanOrEqual: {
                    var binary = (BinaryExpression)expr.Body;
                    if (binary.Method == null)
                        return null;

                    // Fast path conversion only works if the two arguments are parameters, consumed in the order they were given
                    if (!(binary.Left is ParameterExpression pl) || pl.Name != expr.Parameters[0].Name)
                        return null;
                    if (!(binary.Right is ParameterExpression pr) || pr.Name != expr.Parameters[1].Name)
                        return null;

                    return ConvertCallWithErrorHandling(
                        new[] { expr.Parameters[0].Type, expr.Parameters[1].Type },
                        new Value?[] {leftConst, rightConst},
                        binary.Method!,
                        emitter,
                        errorLabel
                    );
                }

                case ExpressionType.Call: {
                    return TryConvertCallFastPath(expr, emitter, errorLabel);
                }

                default:
                    return null;
            }
        }

        private static ConvertResult? TryConvertUnaryFastPath<TIn, TOut, TEmit>(this Expression<Func<TIn, TOut>> expr, OptimisingEmitter<TEmit> emitter, ExceptionBlock errorLabel)
        {
            switch (expr.Body.NodeType)
            {
                case ExpressionType.Parameter:
                    var p = (ParameterExpression)expr.Body;
                    return new ConvertResult(p.Type, false, null);

                case ExpressionType.Constant:
                    emitter.Pop();
                    return ConvertExpression(expr.Body, emitter, new Dictionary<string, Parameter>(), errorLabel);

                case ExpressionType.Call: {
                    return TryConvertCallFastPath(expr, emitter, errorLabel);
                }

                default:
                    return null;
            }
        }

        private static ConvertResult? TryConvertCallFastPath<TExpr, TEmit>(this Expression<TExpr> expr, OptimisingEmitter<TEmit> emitter, ExceptionBlock errorLabel)
        {
            if (expr.Body.NodeType != ExpressionType.Call)
                return null;

            var call = (MethodCallExpression)expr.Body;
            if (call.Method == null || !call.Method.IsStatic)
                return null;

            // Fast path conversion only works if the arguments are parameters, consumed in the order they were given
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                var arg = call.Arguments[i];
                if (!(arg is ParameterExpression pl) || pl.Name != expr.Parameters[i].Name)
                    return null;
            }

            return ConvertCallWithErrorHandling(
                call.Arguments.Select(a => a.Type).ToArray(),
                null,
                call.Method,
                emitter,
                errorLabel
            );
        }
        #endregion
    }
}
