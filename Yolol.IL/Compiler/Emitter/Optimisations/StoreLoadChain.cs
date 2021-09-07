using System.Collections.Generic;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    /// <summary>
    /// Search for the chain: Store(X), Load(X)
    /// Replace with: Dup(), Store(X)
    /// </summary>
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
            ThrowHelper.Check(instructions.Count == 2, "incorrect instruction count");

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
