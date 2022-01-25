using System.Collections.Generic;
using Yolol.IL.Compiler.Emitter.Instructions;

namespace Yolol.IL.Compiler.Emitter.Optimisations
{
    /// <summary>
    /// Search for the chain: Load(X), Load(X)
    /// Replace with: Load(X), Dup()
    /// </summary>
    internal class LoadLoadChain
        : BaseOptimisation
    {
        public LoadLoadChain()
            : base(new[] {
                typeof(LoadLocal),
                typeof(LoadLocal),
            })
        {
        }

        protected override bool Replace(List<BaseInstruction> instructions)
        {
            ThrowHelper.Check(instructions.Count == 2, "incorrect instruction count");

            var load1 = (LoadLocal)instructions[0];
            var load2 = (LoadLocal)instructions[1];

            if (!load1.Local.Equals(load2.Local))
                return false;
            if (!load1.Local.LocalType.IsValueType)
                return false;

            instructions.Clear();
            instructions.Add(new LoadLocal(load1.Local));
            instructions.Add(new Duplicate());

            return true;

            
        }
    }
}
