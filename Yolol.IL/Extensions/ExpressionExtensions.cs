using System;
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
    internal readonly struct ConvertResult
    {
        /// <summary>
        /// The type of the value left on the stack
        /// </summary>
        public readonly Type OnStack;

        /// <summary>
        /// The value which this expression produced (if it can be statically determined, otherwise null)
        /// </summary>
        public readonly Value? StaticValue;

        /// <summary>
        /// The type which the inputs to this expression must have for the expression not to error. Since an error will
        /// have caused execution to jump away, the types can be assumed to be these types.
        /// </summary>
        public readonly IReadOnlyList<StackType>? Implications;

        /// <summary>
        /// Indicates if the output of this expression is guaranteed to be trimmed.
        /// </summary>
        public readonly bool Trimmed;

        public ConvertResult(Type onStack, Value? staticValue, IReadOnlyList<StackType>? implications, bool trimmed)
        {
            OnStack = onStack;
            StaticValue = staticValue;
            Implications = implications;
            Trimmed = trimmed;
        }
    }

    internal readonly struct ConstantInt32
    {
        public readonly int Value;

        public ConstantInt32(int value)
        {
            Value = value;
        }

        public static implicit operator int(ConstantInt32 value)
        {
            return value.Value;
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
            if (expr.ReturnType == typeof(StaticError))
                return new ConvertResult(typeof(StaticError), null, null, true);

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
            if (expr.ReturnType == typeof(StaticError))
                return new ConvertResult(typeof(StaticError), null, null, true);

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
                    foreach (var arg in n.Arguments)
                        ConvertExpression(arg, emitter, parameters, errorLabel);

                    emitter.NewObject(n.Constructor!);

                    return new ConvertResult(n.Type, null, null, false);
                }

                case ExpressionType.Constant: {
                    var constant = (ConstantExpression)expr;
                    if (constant.Type == typeof(string))
                    {
                        emitter.LoadConstant((string)constant.Value!);
                        return new ConvertResult(typeof(string), (string)constant.Value!, null, false);
                    }

                    if (constant.Type == typeof(bool))
                    {
                        emitter.LoadConstant((bool)constant.Value!);
                        return new ConvertResult(typeof(bool), (Number)(bool)constant.Value!, null, true);
                    }

                    if (constant.Type == typeof(int))
                    {
                        emitter.LoadConstant((int)constant.Value!);
                        return new ConvertResult(typeof(int), (Number)(int)constant.Value!, null, true);
                    }

                    throw ThrowHelper.NotImplemented($"Constant type: `{constant.Type.Name}`");
                }

                case ExpressionType.Parameter: {
                    var paramExpr = (ParameterExpression)expr;
                    var param = parameters[paramExpr.Name!];
                    emitter.LoadLocal(param.Local);
                    return new ConvertResult(expr.Type, param.Constant, null, false);
                }

                case ExpressionType.Not: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.LogicalNot))
                        : unary.Operand.Type.GetMethod("op_LogicalNot");
                    emitter.Call(m!);
                    return new ConvertResult(m!.ReturnType, null, null, true);
                }

                case ExpressionType.Negate: {
                    var unary = (UnaryExpression)expr;
                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    var m = unary.Operand.Type == typeof(bool)
                        ? typeof(Runtime).GetMethod(nameof(Runtime.BoolNegate))
                        : unary.Operand.Type.GetMethod("op_UnaryNegation");
                    return ConvertCallWithErrorHandling(new[] { unary.Operand.Type }, null, m!, emitter, errorLabel);
                }

                case ExpressionType.Convert: {
                    var unary = (UnaryExpression)expr;

                    // Check if a constant value is being loaded, if so evaluate it now
                    var from = unary.Operand;
                    if (from.NodeType == ExpressionType.New && from.Type == typeof(ConstantInt32))
                    {
                        var e = (int)Expression.Lambda(unary).Compile().DynamicInvoke()!;
                        emitter.LoadConstant(e);
                        return new ConvertResult(typeof(int), (Number)e, null, true);
                    }

                    ConvertExpression(unary.Operand, emitter, parameters, errorLabel);
                    emitter.Call(unary.Method!);
                    return new ConvertResult(unary.Method!.ReturnType, null, null, true);
                }

                case ExpressionType.Call: {
                    var c = (MethodCallExpression)expr;
                    if (c.Method.IsStatic)
                    {
                        var idx = 0;
                        var types = new Type[c.Arguments.Count];
                        var staticValues = new Value?[c.Arguments.Count];
                        foreach (var arg in c.Arguments)
                        {
                            var result = ConvertExpression(arg, emitter, parameters, errorLabel);
                            types[idx] = result.OnStack;
                            staticValues[idx] = result.StaticValue;
                            idx++;
                        }

                        var call = ConvertCallWithErrorHandling(types, staticValues, c.Method, emitter, errorLabel);
                        return call;
                    }
                    else
                    {
                        ConvertExpression(c.Object!, emitter, parameters, errorLabel);

                        using (var local = emitter.DeclareLocal(c.Object!.Type, "ConvertExpression_NonStaticCall", false))
                        {
                            emitter.StoreLocal(local);
                            emitter.LoadLocalAddress(local, false);
                            foreach (var arg in c.Arguments)
                                ConvertExpression(arg, emitter, parameters, errorLabel);

                            emitter.Call(c.Method);
                            return new ConvertResult(c.Method.ReturnType, null, null, false);
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

                    return new ConvertResult(finalResult.OnStack, finalResult.StaticValue, null, true);
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

                    return new ConvertResult(typeof(bool), staticVal, null, true);
                }

                default:
                    throw ThrowHelper.NotImplemented(expr.NodeType + " " + expr.GetType());
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
            ThrowHelper.CheckNotNull(method.DeclaringType, "Cannot convert call with null `DeclaringType`");

            // Check for errors, early out if this is guaranteed to be an error
            var errorData = method.TryGetErrorMetadata(parameterTypes);
            if (errorData != null)
                CheckForRuntimeError(errorData.Value, parameterTypes, staticValues, emitter, errorLabel);

            var implications = method.GetTypeImplications();

            // If the error metadata specifies an alternative implementation to use (which does not include runtime checks
            // for the case already checked by `WillThrow`) use that alternative instead.
            if (errorData != null && errorData.Value.UnsafeAlternative != null)
            {
                emitter.Call(errorData.Value.UnsafeAlternative);

                var ts = errorData.Value.UnsafeAlternative.IsTrimSafe();
                return new ConvertResult(errorData.Value.UnsafeAlternative!.ReturnType, null, implications, ts);
            }
            else
            {
                emitter.Call(method);

                var ts = method.IsTrimSafe();
                return new ConvertResult(method.ReturnType, null, implications, ts);
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
            IReadOnlyList<Type> parameterTypes,
            Value?[]? staticValues,
            OptimisingEmitter<TEmit> emitter,
            ExceptionBlock errorLabel
        )
        {
            ThrowHelper.Check(staticValues == null || staticValues.Length == parameterTypes.Count, "Incorrect number of static values");

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

            // Save the parameters into locals
            var parameterLocals = new List<Local>();
            for (var i = parameterTypes.Count - 1; i >= 0; i--)
            {
                var local = emitter.DeclareLocal(parameterTypes[i], $"ConvertCallWithErrorHandling_{emitter.InstructionCount}", false);
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
                    var method = ThrowHelper.CheckNotNull(binary.Method, "Cannot convert null method");

                    // Fast path conversion only works if the two arguments are parameters, consumed in the order they were given
                    if (binary.Left is not ParameterExpression pl || pl.Name != expr.Parameters[0].Name)
                        return null;
                    if (binary.Right is not ParameterExpression pr || pr.Name != expr.Parameters[1].Name)
                        return null;

                    return ConvertCallWithErrorHandling(
                        new[] { expr.Parameters[0].Type, expr.Parameters[1].Type },
                        new[] { leftConst, rightConst },
                        method,
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
                    return new ConvertResult(p.Type, null, null, true);

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
            // This shouldn't ever happen, fail gracefully.
            if (expr.Body.NodeType != ExpressionType.Call)
                return null;

            var call = (MethodCallExpression)expr.Body;
            if (!call.Method.IsStatic)
                return null;

            // Fast path conversion only works if the arguments are parameters, consumed in the order they were given
            for (var i = 0; i < call.Arguments.Count; i++)
            {
                var arg = call.Arguments[i];
                if (arg is not ParameterExpression pl || pl.Name != expr.Parameters[i].Name)
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
