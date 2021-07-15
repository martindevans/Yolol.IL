using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadLocalAddress
        : BaseInstruction
    {
        private readonly Local _local;

        public LoadLocalAddress(Local local)
        {
            _local = local;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadLocalAddress(_local);
        }

        public override string ToString()
        {
            return $"LoadLocalAddr({_local.Name}):";
        }
    }
}