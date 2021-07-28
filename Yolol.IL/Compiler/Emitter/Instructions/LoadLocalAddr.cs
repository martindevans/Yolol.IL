using System.Diagnostics.CodeAnalysis;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadLocalAddress
        : BaseInstruction
    {
        public bool IsReadonly { get; }
        public Local Local { get; }

        public LoadLocalAddress(Local local, bool isReadonly)
        {
            IsReadonly = isReadonly;
            Local = local;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadLocalAddress(Local);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"LoadLocalAddr({Local.Name}):";
        }
    }
}