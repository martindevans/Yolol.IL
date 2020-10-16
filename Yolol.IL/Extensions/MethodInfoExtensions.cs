using System;
using System.Reflection;
using Yolol.Execution.Attributes;

namespace Yolol.IL.Extensions
{
    internal static class MethodInfoExtensions
    {
        public static MethodInfo? TryGetWillThrowMethod(this MethodInfo method, params Type[] parameters)
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
            if (attr.IsInfallible || attr.WillThrow == null)
                return null;

            // Get the `will throw` method which tells us if a given pair of values would cause a runtime error
            var willThrow = method.DeclaringType.GetMethod(attr.WillThrow, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
            if (willThrow == null)
                throw new InvalidOperationException($"ErrorMetadataAttribute references an invalid method: `{attr.WillThrow}`");
            if (willThrow.ReturnType != typeof(bool))
                throw new InvalidOperationException($"ErrorMetadataAttribute references an method which does not return bool: `{attr.WillThrow}`");

            return willThrow;
        }
    }
}
