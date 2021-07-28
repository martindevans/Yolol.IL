using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class WriteLine
        : BaseInstruction
    {
        private readonly string _format;
        private readonly Local[] _args;

        public WriteLine(string format, Local[] args)
        {
            _format = format;
            _args = args;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.WriteLine(_format, _args);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            var args = string.Join<Local>(",", _args);
            return $"WriteLine({args})";
        }
    }
}
