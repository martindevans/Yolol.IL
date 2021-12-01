using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.Execution.Attributes;
using Yolol.IL.Compiler;
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
            public readonly MethodInfo WillThrow;

            /// <summary>
            /// A set of flags which indicate if the parameter is required in the WillThrow method.
            /// If this flag is false then the argument in that position can be set to the `default` value when
            /// calling WillThrow.
            /// </summary>
            private readonly IReadOnlyList<bool> _ignoreParams;

            public ErrorMetadata(MethodInfo original, MethodInfo willThrow, MethodInfo? unsafeAlternative)
            {
                OriginalMethod = original;
                WillThrow = willThrow;
                UnsafeAlternative = unsafeAlternative;

                _ignoreParams = (
                    from parameter in willThrow.GetParameters()
                    let attr = parameter.GetCustomAttribute<IgnoreParamAttribute>()
                    select attr != null
                ).ToArray();
                
            }

            /// <summary>
            /// Emit code to dynamically check for errors. If an error would occur clear out the stack and jump away to the given label
            /// </summary>
            /// <typeparam name="TEmit"></typeparam>
            /// <param name="emitter"></param>
            /// <param name="errorLabel"></param>
            /// <param name="parameters"></param>
            public void EmitDynamicWillThrow<TEmit>(OptimisingEmitter<TEmit> emitter, Compiler.Emitter.Instructions.ExceptionBlock errorLabel, IReadOnlyList<Local> parameters)
            {
                // Load parameters back onto stack
                for (var i = parameters.Count - 1; i >= 0; i--)
                    emitter.LoadLocal(parameters[i]);

                // Invoke the `will throw` method to discover if this invocation would trigger a runtime error
                emitter.Call(WillThrow);

                // Create a label to jump past the error handling for the normal case
                var noThrowLabel = emitter.DefineLabel();

                // Jump past error handling if this is ok
                emitter.BranchIfFalse(noThrowLabel);

                // If execution reaches here it means an error would occur in this operation
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
                var parameters = WillThrow.GetParameters();
                ThrowHelper.Check(parameters.Length == values.Length, "Incorrect number of args supplied");

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

                var result = (bool)WillThrow.Invoke(null, args)!;
                return result;
            }
        }

        public static ErrorMetadata? TryGetErrorMetadata(this MethodInfo method, params Type[] parameters)
        {
            // Nb. Is parameters _always_ equal to method.GetParameters()? If so, it can be removed and replaced with this:
            // parameters = method.GetParameters().Select(a => a.ParameterType).ToArray();

            Debug.Assert(method.DeclaringType != null);

            // Reflect out the error metadata, this can tell us in advance if the method will throw
            var attr = method.GetCustomAttributes(typeof(ErrorMetadataAttribute), false).OfType<ErrorMetadataAttribute>().SingleOrDefault();
            if (attr == null)
                return null;
            
            // Get the `will throw` method which tells us if a given pair of values would cause a runtime error
            var willThrow = GetMethod(method.DeclaringType, attr.WillThrow, typeof(bool), parameters);

            // Get the `unsafe alternative` method which implements the same behaviour but without runtime checks
            var alternativeImpl = attr.UnsafeAlternative == null ? null : GetMethod(method.DeclaringType, attr.UnsafeAlternative, null, parameters);

            // Method is not infallible, so return both
            return new ErrorMetadata(method, willThrow, alternativeImpl);

        }

        /// <summary>
        /// Get a set of "type implications" for the given method - these types are correct if the method does not throw a runtime error
        /// </summary>
        /// <param name="method"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static IReadOnlyList<StackType> GetTypeImplications(this MethodInfo method)
        {
            Debug.Assert(method.DeclaringType != null);

            return (from parameter in method.GetParameters()
                    let impl = parameter.GetCustomAttribute<TypeImplicationAttribute>()
                    select impl?.Type.ToStackType() ?? parameter.ParameterType.ToStackType()).ToList();
        }

        private static MethodInfo GetMethod(Type declaringType, string name, Type? requiredReturnType, Type[] parameters)
        {
            var method = declaringType.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
            method = ThrowHelper.CheckNotNull(method, $"ErrorMetadataAttribute references an invalid method: `{name}`");

            ThrowHelper.Check(
                requiredReturnType == null || method.ReturnType == requiredReturnType,
                $"ErrorMetadataAttribute references an method which does not return {requiredReturnType?.Name}: `{name}`"
            );

            return method;
        }
    }
}
