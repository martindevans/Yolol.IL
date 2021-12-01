using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class BranchIfEqual
        : BaseInstruction
    {
        private readonly Label _label;

        public BranchIfEqual(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.BranchIfEqual(_label);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"BranchIfEqual({_label.Name}):";
        }
    }
}