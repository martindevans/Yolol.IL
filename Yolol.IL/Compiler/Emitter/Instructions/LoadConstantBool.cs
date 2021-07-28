using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadConstantBool
        : BaseInstruction
    {
        private readonly bool _value;

        public LoadConstantBool(bool value)
        {
            _value = value;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadConstant(_value);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"Load_Bool({_value})";
        }
    }
}
