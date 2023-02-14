using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Or
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Or();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "Or()";
        }
    }
}
