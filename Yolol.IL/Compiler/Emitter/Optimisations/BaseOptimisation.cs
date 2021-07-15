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

        private bool CheckSegment(IReadOnlyList<BaseInstruction> instructions)
        {
            if (instructions.Count != _opTypes.Length)
                throw new ArgumentException("Incorrect instruction slice length");

            for (var i = 0; i < instructions.Count; i++)
                if (!instructions[i].GetType().IsAssignableFrom(_opTypes[i]))
                    return false;

            return true;
        }

        public bool Match(List<BaseInstruction> ops)
        {
            var status = false;
            var slice = new List<BaseInstruction>(_opTypes.Length);

            for (var i = 0; i < ops.Count; i++)
            {
                slice.Clear();
                slice.AddRange(ops.Skip(i).Take(_opTypes.Length));
                if (slice.Count != _opTypes.Length)
                    return status;

                if (CheckSegment(slice))
                {
                    if (!Replace(slice))
                        continue;

                    ops.RemoveRange(i, _opTypes.Length);
                    ops.InsertRange(i, slice);
                    status = true;
                }
            }

            return status;
        }
    }
}
