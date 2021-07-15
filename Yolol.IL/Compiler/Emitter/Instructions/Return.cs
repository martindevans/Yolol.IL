using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Return
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Return();
        }

        public override string ToString()
        {
            return $"Return()";
        }
    }
}
