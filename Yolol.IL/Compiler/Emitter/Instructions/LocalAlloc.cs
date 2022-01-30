using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LocalAlloc
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LocalAllocate();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "LocalAllocate()";
        }
    }
}
