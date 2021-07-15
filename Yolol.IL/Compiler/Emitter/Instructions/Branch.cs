using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Branch
        : BaseInstruction
    {
        private readonly Label _label;

        public Branch(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Branch(_label);
        }

        public override string ToString()
        {
            return $"Branch({_label.Name}):";
        }
    }
}
