using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Duplicate
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Duplicate();
        }

        public override string ToString()
        {
            return $"Duplicate()";
        }
    }
}
