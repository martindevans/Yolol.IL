using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class BranchIfFalse
        : BaseInstruction
    {
        private readonly Label _label;

        public BranchIfFalse(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.BranchIfFalse(_label);
        }

        public override string ToString()
        {
            return $"BranchIfFalse({_label.Name}):";
        }
    }
}