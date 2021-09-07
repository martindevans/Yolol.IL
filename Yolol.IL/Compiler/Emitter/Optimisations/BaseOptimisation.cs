using System;
using System.Collections.Generic;
using System.Linq;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    internal abstract class BaseOptimisation
    {
        private readonly Type[] _opTypes;

        protected BaseOptimisation(Type[] opTypes)
        {
            _opTypes = opTypes;
        }

        protected abstract bool Replace(List<BaseInstruction> instructions);

        private bool MatchWindow(IReadOnlyList<BaseInstruction> allOps, int start, int length)
        {
            ThrowHelper.Check(length == _opTypes.Length, "Incorrect instruction slice length");
            ThrowHelper.Check(start + length <= allOps.Count, "Slice is out of bounds");

            for (var i = 0; i < length; i++)
                if (!allOps[start + i].GetType().IsAssignableFrom(_opTypes[i]))
                    return false;

            return true;
        }

        public bool Match(List<BaseInstruction> allOps)
        {
            var status = false;
            var slice = new List<BaseInstruction>(_opTypes.Length);

            // Run a sliding window over the instruction stream, checking for matches
            for (var i = 0; i < allOps.Count - _opTypes.Length; i++)
            {
                if (MatchWindow(allOps, i, _opTypes.Length))
                {
                    // Copy out the relevant instructions
                    slice.Clear();
                    slice.AddRange(allOps.Skip(i).Take(_opTypes.Length));
                    ThrowHelper.Check(slice.Count == _opTypes.Length, "Incorrect slice length");

                    // Try to mutate that list with the optimisation
                    if (!Replace(slice))
                        continue;

                    // It succeeded! Replace the instructions in the original
                    allOps.RemoveRange(i, _opTypes.Length);
                    allOps.InsertRange(i, slice);
                    status = true;
                }
            }

            return status;
        }
    }
}
