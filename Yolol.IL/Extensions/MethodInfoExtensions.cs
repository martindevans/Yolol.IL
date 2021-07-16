using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.Execution.Attributes;
using Yolol.IL.Compiler.Emitter;
using Type = System.Type;

namespace Yolol.IL.Extensions
{
    internal static class MethodInfoExtensions
    {
        public readonly struct ErrorMetadata
        {
            public readonly MethodInfo OriginalMethod;
            public readonly MethodInfo? UnsafeAlternative;

            public readonly MethodInfo? WillThrow;
            public readonly bool IsInfallible;

            private readonly IReadOnlyList<bool>? _ignoreParams;

            public ErrorMetadata(MethodInfo original, bool infallible, MethodInfo? willThrow, MethodInfo? unsafeAlternative)
            {
                OriginalMethod = original;
                WillThrow = willThrow;
                UnsafeAlternative = unsafeAlternative;
                IsInfallible = infallible;

                if (willThrow != null)
                {
                    _ignoreParams = (
                        from parameter in willThrow.GetParameters()
                        let attr = parameter.GetCustomAttribute<IgnoreParamAttribute>()
                        select attr != null
                    ).ToArray();
                }
                else
                    _ignoreParams = null;
            }

            /// <summary>
            /// Emit code to dynamically check for errors. If an error would occur clear out the stack and jump away to the given label
            /// </summary>
            /// <typeparam name="TEmit"></typeparam>
            /// <param name="emitter"></param>
            /// <param name="errorLabel"></param>
            /// <param name="stackSize"></param>
            /// <param name="parameters"></param>
            public void EmitDynamicWillThrow<TEmit>(OptimisingEmitter<TEmit> emitter, Compiler.Emitter.Instructions.ExceptionBlock errorLabel, int stackSize, IReadOnlyList<Local> parameters)
            {
                if (WillThrow == null)
                    return;

                // Load parameters back onto stack
                for (var i = parameters.Count - 1; i >= 0; i--)
                    emitter.LoadLocal(parameters[i]);

                // Invoke the `will throw` method to discover if this invocation would trigger a runtime error
                emitter.Call(WillThrow);

                // Create a label to jump past the error handling for the normal case
                var noThrowLabel = emitter.DefineLabel();

                // Jump past error handling if this is ok
                emitter.BranchIfFalse(noThrowLabel);

                // If execution reaches here it means an error would occur in this operation. First empty out the stack and then jump
                // to the error handling label for this expression.
                // There are N less things on the stack than indicated by stackSize because the N parameters to this method have already been taken off the stack.
                for (var i = 0; i < stackSize - parameters.Count; i++)
                    emitter.Pop();
                emitter.Leave(errorLabel);

                emitter.MarkLabel(noThrowLabel);
            }

            /// <summary>
            /// Statically check if the method will throw if called with the given parameters
            /// </summary>
            /// <param name="values"></param>
            /// <returns></returns>
            public bool? StaticWillThrow(Value?[] values)
            {
                if (IsInfallible)
                    return false;
                if (WillThrow == null || _ignoreParams == null)
                    return null;

                var parameters = WillThrow.GetParameters();
                if (parameters.Length != values.Length)
                    throw new ArgumentException("Incorrect number of args supplied", nameof(values));

                // Check that there is a value for all non-ignored arguments
                for (var i = 0; i < values.Length; i++)
                    if (!_ignoreParams[i] && values[i] == null)
                        return null;

                // Build a list of args to call the method with
                var args = new object?[values.Length];
                for (var i = 0; i < values.Length; i++)
                {
                    var v = values[i];
                    if (v != null)
                    {
                        args[i] = v.Value.Coerce(parameters[i].ParameterType.ToStackType());
                    }
                    else
                    {
                        var type = parameters[i].ParameterType;
                        args[i] = type.GetTypeInfo().IsValueType ? Activator.CreateInstance(type) : null;
                    }
                }

                var result = (bool)WillThrow.Invoke(null, args);
                return result;
            }
        }

        public static ErrorMetadata? TryGetErrorMetadata(this MethodInfo method, params Type[] parameters)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            if (method.DeclaringType == null)
                throw new InvalidOperationException("Cannot find will throw on method with null `DeclaringType`");

            // Reflect out the error metadata, this can tell us in advance if the method will throw
            var attrs = method.GetCustomAttributes(typeof(ErrorMetadataAttribute), false);

            if (attrs.Length > 1)
                throw new InvalidOperationException("Method has more than one `ErrorMetadataAttribute`");

            if (attrs.Length == 0)
                return null;
            
            var attr = (ErrorMetadataAttribute)attrs[0];

            // Get the `will throw` method which tells us if a given pair of values would cause a runtime error
            var willThrow = attr.WillThrow == null ? null : GetMethod(method.DeclaringType, attr.WillThrow, typeof(bool), parameters);

            // Get the `unsafe alternative` method which implements the same behaviour but without runtime checks
            var alternativeImpl = attr.UnsafeAlternative == null ? null : GetMethod(method.DeclaringType, attr.UnsafeAlternative, null, parameters);

            // If both are null there's nothing useful to return
            if (willThrow == null && alternativeImpl == null)
                return null;

            if (attr.IsInfallible)
            {
                // There's no point returning will throw since it's infallible
                return new ErrorMetadata(method, true, null, alternativeImpl);
            }
            else
            {
                // Method is not infallible, so return both
                return new ErrorMetadata(method, false, willThrow, alternativeImpl);
            }
        }

        private static MethodInfo GetMethod(Type declaringType, string name, Type? requiredReturnType, Type[] parameters)
        {
            var method = declaringType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
            if (method == null)
                throw new InvalidOperationException($"ErrorMetadataAttribute references an invalid method: `{name}`");
            if (requiredReturnType != null && method.ReturnType != requiredReturnType)
                throw new InvalidOperationException($"ErrorMetadataAttribute references an method which does not return {requiredReturnType.Name}: `{name}`");

            return method;
        }
    }
}
