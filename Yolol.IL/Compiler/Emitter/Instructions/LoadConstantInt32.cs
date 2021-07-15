using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadConstantInt32
        : BaseInstruction
    {
        private readonly int _value;

        public LoadConstantInt32(int value)
        {
            _value = value;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadConstant(_value);
        }

        public override string ToString()
        {
            return $"Load_i32({_value})";
        }
    }
}
