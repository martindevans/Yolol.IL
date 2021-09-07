﻿using System.Collections.Generic;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    /// <summary>
    /// Search for the chain: Dup(), Store(X), Pop()
    /// Remove the Dup/Pop operations.
    /// </summary>
    internal class DupStorePopChain
        : BaseOptimisation
    {
        public DupStorePopChain()
            : base(new[] {
                typeof(Duplicate),
                typeof(StoreLocal),
                typeof(Pop),
            })
        {
        }

        protected override bool Replace(List<BaseInstruction> instructions)
        {
            ThrowHelper.Check(instructions.Count == 3, "incorrect instruction count");

            var dup = (Duplicate)instructions[0];
            var store = (StoreLocal)instructions[1];
            var pop = (Pop)instructions[2];

            instructions.Clear();
            instructions.Add(store);

            return true;

            
        }
    }
}
