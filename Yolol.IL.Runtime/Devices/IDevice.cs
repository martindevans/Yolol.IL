using System;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL.Runtime.Devices
{
    public interface IDevice
    {
        /// <summary>
        /// If any of the fields in this set change this device should be run
        /// </summary>
        ChangeSetKey ChangeSet { get; }

        /// <summary>
        /// If true, this device will update every tick
        /// </summary>
        bool AlwaysUpdate { get; }

        /// <summary>
        /// Initialise the device.
        /// </summary>
        /// <param name="externals"></param>
        void Initialise(IReadonlyExternalsMap externals);

        /// <summary>
        /// Run this device
        /// </summary>
        /// <param name="externals"></param>
        void Tick(ArraySegment<Value> externals);
    }
}
