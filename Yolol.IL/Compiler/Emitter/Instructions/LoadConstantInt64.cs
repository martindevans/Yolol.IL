using System.Diagnostics.CodeAnalysis;
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

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"Load_i64({_value})";
        }
    }

    internal class LoadConstantUInt64
        : BaseInstruction
    {
        private readonly ulong _value;

        public LoadConstantUInt64(ulong value)
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
            return $"Load_u64({_value})";
        }
    }
}
