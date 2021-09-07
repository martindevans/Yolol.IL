using System.Collections.Generic;
using Yolol.Grammar;

namespace Yolol.IL.Compiler.Memory
{
    internal class NullStaticTypeTracker
        : IStaticTypeTracker
    {
        public StackType? TypeOf(VariableName variable)
        {
            return null;
        }

        public void Store(VariableName name, StackType type)
        {
        }

        public ITypeContext EnterContext()
        {
            return new Context();
        }

        public void Unify(params ITypeContext[] contexts)
        {
        }

        private class Context
            : ITypeContext
        {
            public ITypeContext Parent => new Context();

            public IReadOnlyDictionary<VariableName, StackType> Types { get; }

            public Context()
            {
                Types = new Dictionary<VariableName, StackType>();
            }

            public void Store(VariableName name, StackType type)
            {
            }

            public StackType? TypeOf(VariableName varName)
            {
                return null;
            }

            public void Dispose()
            {
            }
        }
    }
}
