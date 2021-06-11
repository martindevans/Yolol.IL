using System;
using Yolol.Execution;

namespace Yolol.IL
{
    public class CompiledLine
    {
        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, int> _func;

        internal CompiledLine(Func<ArraySegment<Value>, ArraySegment<Value>, int> func)
        {
            _func = func;
        }

        public int Run(Value[] internals, Value[] externals)
        {
            return _func(internals, externals);
        }
    }
}
