using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class LoadField
        : BaseInstruction
    {
        private readonly FieldInfo _field;
        private readonly bool? _isVolatile;
        private readonly int? _unaligned;

        public LoadField(FieldInfo field, bool? isVolatile, int? unaligned)
        {
            _field = field;
            _isVolatile = isVolatile;
            _unaligned = unaligned;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.LoadField(_field, _isVolatile, _unaligned);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"LoadField({_field}, volatile={_isVolatile}, unaligned={_unaligned})";
        }
    }
}
