using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class StoreLocal
        : BaseInstruction
    {
        public readonly Local Local;

        public StoreLocal(Local local)
        {
            Local = local;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.StoreLocal(Local);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"StoreLocal({Local.Name})";
        }
    }
}
