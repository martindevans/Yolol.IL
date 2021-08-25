using System;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;

namespace Yolol.IL.Runtime.Devices
{
    public class RealtimeClock
        : IDevice
    {
        private readonly VariableName _name;
        private int _index;

        public ChangeSetKey ChangeSet => default;
        public bool AlwaysUpdate { get; private set; }

        public RealtimeClock(VariableName name)
        {
            _name = name;
        }

        public void Initialise(IReadonlyExternalsMap externals)
        {
            AlwaysUpdate = externals.TryGetValue(_name, out _index);
        }

        public void Tick(ArraySegment<Value> externals)
        {
            externals[_index] = (Number)(DateTime.UtcNow.Ticks / 10_000_000.0);
        }
    }
}
