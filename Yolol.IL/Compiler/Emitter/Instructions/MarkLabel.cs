using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class MarkLabel
        : BaseInstruction
    {
        private readonly Label _label;

        public MarkLabel(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.MarkLabel(_label);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"MarkLabel({_label.Name}):";
        }
    }
}
