﻿using System;
using Yolol.Execution;

namespace Yolol.IL
{
    public class JitLine
        : IJitLine
    {
        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, int> _compiled;

        internal JitLine(Func<ArraySegment<Value>, ArraySegment<Value>, int> compiled)
        {
            _compiled = compiled;
        }

        public int Run(ArraySegment<Value> internals, ArraySegment<Value> externals)
        {
            return _compiled(internals, externals);
        }
    }

    public interface IJitLine
    {
        int Run(ArraySegment<Value> internals, ArraySegment<Value> externals);
    }
}
