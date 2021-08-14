using System.Collections.Generic;
using Yolol.IL.Compiler.Emitter;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler
{
    internal class TypeStack<TEmit>
    {
        private readonly OptimisingEmitter<TEmit> _emitter;
        private readonly Stack<StackType> _types = new Stack<StackType>();

        public StackType Peek => _types.Peek();

        public TypeStack(OptimisingEmitter<TEmit> emitter)
        {
            _emitter = emitter;
        }

        public void Push(StackType type)
        {
            _types.Push(type);
        }

        public void Pop(StackType type)
        {
            var pop = _types.Pop();
            ThrowHelper.Check(pop == type, $"Attempted to pop `{type}` but stack had `{pop}`");
        }

        public void Coerce(StackType target)
        {
            var source = _types.Pop();
            _emitter.EmitCoerce(source, target);
            Push(target);
        }
    }
}
