using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Pop
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Pop();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "Pop()";
        }
    }
}
