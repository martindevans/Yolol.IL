using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Leave
        : BaseInstruction
    {
        private readonly Label _label;

        public Leave(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Leave(_label);
        }

        public override string ToString()
        {
            return $"Leave({_label.Name})";
        }
    }
}
