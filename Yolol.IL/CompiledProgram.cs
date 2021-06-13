using System;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL
{
    public class CompiledProgram
    {
        private readonly InternalsMap _internalsMap;
        private readonly JitLine[] _lines;

        private readonly Value[] _internals;

        public int ProgramCounter { get; private set; }

        public Value this[string identifier]
        {
            get
            {
                if (_internalsMap.TryGetValue(identifier, out var idxi))
                    return _internals[idxi];

                return Number.Zero;
            }
            set
            {
                if (_internalsMap.TryGetValue(identifier, out var idxi))
                    _internals[idxi] = value;
            }
        }

        internal CompiledProgram(InternalsMap internalsMap, JitLine[] lines)
        {
            if (lines.Length < 1)
                throw new ArgumentException("Cannot create a program with no lines", nameof(lines));

            _lines = lines;
            ProgramCounter = 0;

            _internalsMap = internalsMap;
            _internals = new Value[_internalsMap.Count];
            Array.Fill(_internals, new Value(Number.Zero));
        }

        public void Tick(Value[] externals)
        {
            ProgramCounter = _lines[ProgramCounter].Run(_internals, externals) - 1;
        }
    }
}
