using System;
using System.Collections.Generic;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    internal class LoadStoreChain
        : BaseOptimisation
    {
        public LoadStoreChain()
            : base(new[] {
                typeof(LoadLocal),
                typeof(StoreLocal),
            })
        {
        }

        protected override bool Replace(List<BaseInstruction> instructions)
        {
            if (instructions.Count != 2)
                throw new ArgumentException("incorrect instruction count");

            var store = (LoadLocal)instructions[0];
            var load = (StoreLocal)instructions[1];

            if (store.Local.Equals(load.Local))
                return false;
            if (!store.Local.LocalType.IsValueType)
                return false;

            instructions.Clear();
            instructions.Add(new LoadLocalAddress(load.Local, true));
            instructions.Add(new LoadLocalAddress(store.Local, false));
            instructions.Add(new CopyObject(store.Local.LocalType));

            return true;

            
        }
    }
}
