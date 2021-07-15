using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal abstract class BaseInstruction
    {
        public abstract void Emit<TEmit>(Emit<TEmit> emitter);
    }
}
