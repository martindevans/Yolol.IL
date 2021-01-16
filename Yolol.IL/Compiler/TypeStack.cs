using System;
using System.Collections.Generic;
using Sigil;
using Yolol.Execution;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler
{
    internal class TypeStack<TEmit>
    {
        private readonly Emit<TEmit> _emitter;
        private readonly Stack<StackType> _types = new Stack<StackType>();

        public bool IsEmpty => _types.Count == 0;

        public int Count => _types.Count;

        public StackType Peek => _types.Peek();

        public TypeStack(Emit<TEmit> emitter)
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
            if (pop != type)
                throw new InvalidOperationException($"Attempted to pop `{type}` but stack had `{pop}`");
        }

        public void Coerce(StackType target)
        {
            var source = _types.Pop();
            _emitter.EmitCoerce(source, target);
            Push(target);
        }
    }
}
