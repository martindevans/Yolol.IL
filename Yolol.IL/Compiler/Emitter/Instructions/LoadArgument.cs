using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadArgument
        : BaseInstruction
    {
        private readonly ushort _arg;

        public LoadArgument(ushort arg)
        {
            _arg = arg;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadArgument(_arg);
        }

        public override string ToString()
        {
            return $"LoadArg({_arg})";
        }
    }
}
