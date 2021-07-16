using System;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class BeginExceptionBlock
        : BaseInstruction
    {
        private readonly ExceptionBlock _block;

        public BeginExceptionBlock(ExceptionBlock block)
        {
            _block = block;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            _block.Block = emitter.BeginExceptionBlock();
        }

        public override string ToString()
        {
            return "BeginExceptionBlock";
        }
    }

    internal class EndExceptionBlock
        : BaseInstruction
    {
        private readonly ExceptionBlock _block;

        public EndExceptionBlock(ExceptionBlock block)
        {
            _block = block;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            if (_block.Block == null)
                throw new InvalidOperationException("Exception block has not been opened yet");

            emitter.EndExceptionBlock(_block.Block);
        }

        public override string ToString()
        {
            return "EndExceptionBlock";
        }
    }

    public class ExceptionBlock
    {
        public Sigil.ExceptionBlock? Block { get; set; }
    }
}
