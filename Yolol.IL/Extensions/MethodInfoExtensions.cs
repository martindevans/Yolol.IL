using System;
using System.Collections.Generic;
using System.Reflection;
using Yolol.Execution.Attributes;

namespace Yolol.IL.Extensions
{
    internal static class MethodInfoExtensions
    {
        public readonly struct ErrorMetadata
        {
            public readonly MethodInfo? WillThrow;
            public readonly MethodInfo? UnsafeAlternative;

            public ErrorMetadata(MethodInfo? willThrow, MethodInfo? unsafeAlternative)
            {
                WillThrow = willThrow;
                UnsafeAlternative = unsafeAlternative;
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
                return new ErrorMetadata(null, alternativeImpl);
            }
            else
            {
                // Method is not infallible, so return both
                return new ErrorMetadata(willThrow, alternativeImpl);
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
