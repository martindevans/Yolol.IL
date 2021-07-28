using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadArgumentAddress
        : BaseInstruction
    {
        private readonly ushort _arg;

        public LoadArgumentAddress(ushort arg)
        {
            _arg = arg;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadArgumentAddress(_arg);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"LoadArgAddr({_arg})";
        }
    }
}
