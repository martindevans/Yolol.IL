using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class NewObject<TConstruct, TParameter>
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.NewObject<TConstruct, TParameter>();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"NewObject<{typeof(TConstruct)},{typeof(TParameter)}>()";
        }
    }

    internal class NewObject<TConstruct, TParameter1, TParameter2>
        : BaseInstruction
    {
        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.NewObject<TConstruct, TParameter1, TParameter2>();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"NewObject<{typeof(TConstruct)},{typeof(TParameter1)},{typeof(TParameter2)}>()";
        }
    }
}
