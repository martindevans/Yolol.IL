using System;
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
            if (_block.Block == null)
                throw new InvalidOperationException("Cannot leave block that has not been opened yet");

            emitter.Leave(_block.Block.Label);
        }

        public override string ToString()
        {
            return $"Leave()";
        }
    }
}
