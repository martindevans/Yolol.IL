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
}
