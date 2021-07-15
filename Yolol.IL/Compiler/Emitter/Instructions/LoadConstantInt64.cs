using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadConstantInt64
        : BaseInstruction
    {
        private readonly long _value;

        public LoadConstantInt64(long value)
        {
            _value = value;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadConstant(_value);
        }

        public override string ToString()
        {
            return $"Load_i64({_value})";
        }
    }
}
