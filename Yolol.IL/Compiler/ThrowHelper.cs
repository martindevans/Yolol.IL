using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Yolol.IL.Compiler.Exceptions;

namespace Yolol.IL.Compiler
{
    [ExcludeFromCodeCoverage]
    internal static class ThrowHelper
    {
        [ContractAnnotation("assertion:false => halt")]
        public static void Check(bool assertion, string message)
        {
            if (assertion)
                return;

            throw new CompilerAssertionFailedException(message);
        }

        [ContractAnnotation("item:null => halt")]
        public static T CheckNotNull<T>(T? item, string message)
            where T : class
        {
            if (item == null)
                throw new CompilerAssertionFailedException(message);

            return item;
        }

        [ContractAnnotation(" => halt")]
        public static Exception NotImplemented(string message)
        {
            throw new NotImplementedException(message);
        }

        [ContractAnnotation(" => halt")]
        public static Exception Invalid(string message)
        {
            throw new InvalidOperationException(message);
        }
    }
}
