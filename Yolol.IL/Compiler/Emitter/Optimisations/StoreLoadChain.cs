using System;
using System.Collections.Generic;
using Sigil;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    internal class StoreLoadChain
        : BaseOptimisation
    {
        public StoreLoadChain()
            : base(new[] {
                typeof(StoreLocal),
                typeof(LoadLocal),
            })
        {
        }

        protected override bool Replace(List<BaseInstruction> instructions)
        {
            if (instructions.Count != 2)
                throw new ArgumentException("incorrect instruction count");

            var store = (StoreLocal)instructions[0];
            var load = (LoadLocal)instructions[1];

            if (!store.Local.Equals(load.Local))
                return false;

            instructions.Clear();
            instructions.Add(new Duplicate());
            instructions.Add(new StoreLocal(store.Local));

            return true;
        }
    }
}
