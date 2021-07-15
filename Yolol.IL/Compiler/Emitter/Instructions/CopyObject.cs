using System;
using System.Linq;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class CopyObject<TObject>
        : BaseInstruction
        where TObject : struct
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.CopyObject<TObject>();
        }

        public override string ToString()
        {
            return $"CopyObject<{typeof(TObject)}>()";
        }
    }

    internal class CopyObject
        : BaseInstruction
    {
        private readonly Type _type;

        public CopyObject(Type type)
        {
            _type = type;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.CopyObject(_type);
        }

        public override string ToString()
        {
            return $"CopyObject<{_type}>()";
        }
    }
}
