using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class BeginFinallyBlock
        : BaseInstruction
    {
        private readonly ExceptionBlock _exBlock;
        private readonly FinallyBlock _finBlock;

        public BeginFinallyBlock(ExceptionBlock exBlock, FinallyBlock finBlock)
        {
            _exBlock = exBlock;
            _finBlock = finBlock;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            _finBlock.Block = emitter.BeginFinallyBlock(_exBlock.Block);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "BeginFinallyBlock";
        }
    }

    internal class EndFinallyBlock
        : BaseInstruction
    {
        private readonly FinallyBlock _finBlock;

        public EndFinallyBlock(FinallyBlock finBlock)
        {
            _finBlock = finBlock;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            var block = ThrowHelper.CheckNotNull(_finBlock.Block, "Finally block has not been opened yet");
            emitter.EndFinallyBlock(block);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return "EndFinallyBlock";
        }
    }

    public class FinallyBlock
    {
        public Sigil.FinallyBlock? Block { get; set; }
    }
}
