using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Leave
        : BaseInstruction
    {
        private readonly ExceptionBlock _block;

        public Leave(ExceptionBlock block)
        {
            _block = block;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            var block = ThrowHelper.CheckNotNull(_block.Block, "Cannot leave block that has not been opened yet");
            emitter.Leave(block.Label);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "Leave()";
        }
    }
}
