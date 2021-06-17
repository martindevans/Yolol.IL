using System;
using System.Collections.Generic;
using System.Linq;
using Sigil;
using Yolol.Execution;

namespace Yolol.IL.Compiler
{
    /// <summary>
    /// Supplies label to unwind a given number of arguments from the stack.
    /// Marks the labels with appropriate unwinding depth when disposed.
    /// </summary>
    /// <typeparam name="TEmit"></typeparam>
    internal class StackUnwinder<TEmit>
        : IDisposable
    {
        private readonly Emit<TEmit> _emitter;
        private readonly List<(int, Label)> _labels = new List<(int, Label)>();

        public StackUnwinder(Emit<TEmit> emitter)
        {
            _emitter = emitter;
        }

        public Label GetUnwinder(int depth)
        {
            var l = _emitter.DefineLabel();
            _labels.Add((depth, l));
            return l;
        }

        public void ReturnUnwinder(Label label)
        {
            _labels.RemoveAll(a => a.Item2 == label);
        }

        public void Dispose()
        {
            if (_labels.Count == 0)
                return;

            _emitter.MarkLabel(_emitter.DefineLabel("begin_unwinder"));
            var end = _emitter.DefineLabel("end_unwinder");

            foreach (var (depth, label) in _labels)
            {
                _emitter.MarkLabel(label);
                for (var i = 0; i < depth; i++)
                    _emitter.Pop();
                _emitter.Branch(end);
            }

            _emitter.MarkLabel(end);
        }

        
    }
}
