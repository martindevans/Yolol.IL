using System;
using Yolol.Analysis.Types;
using Yolol.Grammar;

namespace Yolol.IL.Compiler.Memory
{
    internal interface IMemoryAccessor<TEmit>
        : IDisposable, ITypeAssignments
    {
        /// <summary>
        /// Store a value from the stack into the given variable.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        void Store(VariableName name, TypeStack<TEmit> types);

        /// <summary>
        /// Load a value onto the stack from the given variable
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        void Load(VariableName name, TypeStack<TEmit> types);
    }
}
