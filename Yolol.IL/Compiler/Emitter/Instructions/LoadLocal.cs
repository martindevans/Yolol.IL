using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadLocal
        : BaseInstruction
    {
        public readonly Local Local;

        public LoadLocal(Local local)
        {
            Local = local;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadLocal(Local);
        }

        public override string ToString()
        {
            return $"LoadLocal({Local.Name})";
        }
    }
}
