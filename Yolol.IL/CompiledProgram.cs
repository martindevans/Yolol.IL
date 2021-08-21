using System;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL
{
    public class CompiledProgram
    {
        /// <summary>
        /// The next line to execute (one based)
        /// </summary>
        public int ProgramCounter { get; private set; }

        /// <summary>
        /// The set of external variables which were changed last tick. The bit will be set for variables
        /// which were changed (see `IReadOnlyDictionaryExtensions.BitFlag`).
        /// False positives are possible, false negatives are not possible.
        /// </summary>
        public ChangeSet ChangeSet { get; private set; }

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
            var result = _lines[ProgramCounter - 1].Run(internals, externals);
            ProgramCounter = result.ProgramCounter;
            ChangeSet = result.ChangeSet;
        }

        /// <summary>
        /// Run the program for multiple ticks
        /// </summary>
        /// <param name="internals"></param>
        /// <param name="externals"></param>
        /// <param name="maxTicks">Maximum number of ticks to run</param>
        /// <param name="changed">Execution will end if any of the variables represented by this key are changed</param>
        /// <returns>Total ticks executed</returns>
        public int Run(ArraySegment<Value> internals, ArraySegment<Value> externals, int maxTicks, ChangeSetKey changed)
        {
            var i = 0;
            for (; i < maxTicks; i++)
            {
                Tick(internals, externals);

                if (ChangeSet.Contains(changed))
                    break;
            }
            return i + 1;
        }
    }
}
