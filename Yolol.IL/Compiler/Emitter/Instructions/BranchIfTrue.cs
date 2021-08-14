using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class BranchIfTrue
        : BaseInstruction
    {
        private readonly Label _label;

        public BranchIfTrue(Label label)
        {
            _label = label;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.BranchIfTrue(_label);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"BranchIfTrue({_label.Name}):";
        }
    }
}