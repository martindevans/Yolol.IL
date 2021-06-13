using System;
using System.Collections.Generic;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL
{
    public class CompiledProgram
    {
        private readonly InternalsMap _internalsMap;
        private readonly ExternalsMap _externalsMap;
        private readonly JitLine[] _lines;

        private readonly Value[] _internals;
        private readonly Value[] _externals;

        public int ProgramCounter { get; private set; }

        public Value this[string identifier]
        {
            get
            {
                if (_internalsMap.TryGetValue(identifier, out var idxi))
                    return _internals[idxi];
                if (_externalsMap.TryGetValue(identifier, out var idxe))
                    return _externals[idxe];

                return Number.Zero;
            }
            set
            {
                if (_internalsMap.TryGetValue(identifier, out var idxi))
                    _internals[idxi] = value;
                else if (_externalsMap.TryGetValue(identifier, out var idxe))
                    _externals[idxe] = value;
            }
        }

        internal CompiledProgram(InternalsMap internalsMap, ExternalsMap externalsMap, JitLine[] lines)
        {
            _lines = lines;
            ProgramCounter = 0;

            _internalsMap = internalsMap;
            _internals = new Value[_internalsMap.Count];
            Array.Fill(_internals, new Value(Number.Zero));

            _externalsMap = externalsMap;
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, new Value(Number.Zero));
        }

        public void Tick()
        {
            ProgramCounter = _lines[ProgramCounter].Run(_internals, _externals) - 1;
        }
    }
}
