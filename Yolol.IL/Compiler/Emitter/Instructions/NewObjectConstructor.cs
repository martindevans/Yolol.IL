using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class NewObject<TA, TB>
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.NewObject<TA, TB>();
        }

        public override string ToString()
        {
            return $"NewObject<{typeof(TA)},{typeof(TB)}>()";
        }
    }
}
