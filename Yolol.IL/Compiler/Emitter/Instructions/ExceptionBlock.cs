using System.Diagnostics.CodeAnalysis;
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

        [ExcludeFromCodeCoverage]
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
            var block = ThrowHelper.CheckNotNull(_block.Block, "Exception block has not been opened yet");
            emitter.EndExceptionBlock(block);
        }

        [ExcludeFromCodeCoverage]
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
