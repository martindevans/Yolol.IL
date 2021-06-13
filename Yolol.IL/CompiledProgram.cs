using System;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL
{
    public class CompiledProgram
    {
        public int ProgramCounter { get; private set; }
        private readonly JitLine[] _lines;

        public IReadonlyInternalsMap InternalsMap { get; }

        internal CompiledProgram(IReadonlyInternalsMap internals, JitLine[] lines)
        {
            if (lines.Length < 1)
                throw new ArgumentException("Cannot create a program with no lines", nameof(lines));

            InternalsMap = internals;

            _lines = lines;
            ProgramCounter = 1;
        }

        public void Tick(ArraySegment<Value> internals, ArraySegment<Value> externals)
        {
            ProgramCounter = _lines[ProgramCounter - 1].Run(internals, externals);
        }
    }
}
