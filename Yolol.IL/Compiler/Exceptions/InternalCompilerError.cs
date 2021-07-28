using System;
using System.Diagnostics.CodeAnalysis;

namespace Yolol.IL.Compiler.Exceptions
{
    /// <summary>
    /// Something went wrong inside the compiler
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class InternalCompilerException
        : Exception
    {
        public InternalCompilerException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Something impossible happened
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class CompilerAssertionFailedException
        : InternalCompilerException
    {
        public CompilerAssertionFailedException(string message)
            : base(message)
        {
        }
    }
}
