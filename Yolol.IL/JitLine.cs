using System;
using Yolol.Execution;

namespace Yolol.IL
{
    internal class JitLine
        : IJitLine
    {
        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, LineResult> _compiled;

        internal JitLine(Func<ArraySegment<Value>, ArraySegment<Value>, LineResult> compiled)
        {
            _compiled = compiled;
        }

        public LineResult Run(ArraySegment<Value> internals, ArraySegment<Value> externals)
        {
            return _compiled(internals, externals);
        }
    }

    internal interface IJitLine
    {
        LineResult Run(ArraySegment<Value> internals, ArraySegment<Value> externals);
    }
}
