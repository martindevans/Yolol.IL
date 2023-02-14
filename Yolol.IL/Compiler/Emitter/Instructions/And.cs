using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class And
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.And();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "And()";
        }
    }
}
