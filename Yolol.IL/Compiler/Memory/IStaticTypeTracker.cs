using System;
using System.Collections.Generic;
using Yolol.Grammar;

namespace Yolol.IL.Compiler.Memory
{
    internal interface IStaticTypeTracker
    {
        StackType? TypeOf(VariableName variable);

        void Store(VariableName name, StackType type);

        ITypeContext EnterContext();

        void Unify(params ITypeContext[] contexts);
    }

    internal interface ITypeContext
        : IDisposable
    {
        ITypeContext Parent { get; }

        IReadOnlyDictionary<VariableName, StackType> Types { get; }

        void Store(VariableName name, StackType type);

        public StackType? TypeOf(VariableName varName);
    }
}
