using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadConstantString
        : BaseInstruction
    {
        private readonly string _value;

        public LoadConstantString(string value)
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
            return $"Load_String(\"{_value}\")";
        }
    }
}
