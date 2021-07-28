using System;
using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadObject
        : BaseInstruction
    {
        public Type Type { get; }
        public bool IsVolatile { get; }
        public int? Unaligned { get; }

        public LoadObject(Type type, bool isVolatile, int? unaligned)
        {
            Type = type;
            IsVolatile = isVolatile;
            Unaligned = unaligned;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadObject(Type, IsVolatile, Unaligned);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"LoadObject<{Type}>(Volatile:{IsVolatile}, Unaligned:{Unaligned})";
        }
    }
}
