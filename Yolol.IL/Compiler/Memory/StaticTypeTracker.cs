#define ENABLE_STACK_TYPE_TRACKING

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yolol.Grammar;
using System.Linq;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Compiler.Memory
{
    internal class StaticTypeTracker
        : IStaticTypeTracker
    {
        private readonly ITypeContext _root;
        private ITypeContext _current;

        public int UsefulTypeQueriesCount { get; private set; }
        public int TotalTypeQueriesCount { get; private set; }

        public StaticTypeTracker(IReadOnlyDictionary<VariableName, Type>? staticTypes)
        {
            _root = new RootContext(staticTypes ?? new Dictionary<VariableName, Type>());
            _current = _root;
        }

        public StackType? TypeOf(VariableName name)
        {
            var type = _current.TypeOf(name);

            TotalTypeQueriesCount++;
            if (type.HasValue && type.Value != StackType.YololValue)
                UsefulTypeQueriesCount++;

            return type;
        }

        public ITypeContext EnterContext()
        {
            var ctx = new TypeContext(this, _current);
            _current = ctx;
            return ctx;
        }

        public void ExitContext(ITypeContext context)
        {
            ThrowHelper.Check(ReferenceEquals(_current, context), "Cannot exit non-current type context");
            ThrowHelper.Check(!ReferenceEquals(_root, context), "Cannot exit root type context");

            _current = ((TypeContext)context).Parent;
        }

        public void Unify(params ITypeContext[] contexts)
        {
            ThrowHelper.Check(!contexts.Contains(_current), "Cannot unify types with an active context");
            ThrowHelper.Check(contexts.Select(c => c.Parent).Distinct().Count() == 1, "Cannot unify types with different parents");

            var groups = contexts.SelectMany(ctx => ctx.Types)
                    .GroupBy(a => a.Key)
                    .Select(a => (a.Key, a.Select(b => b.Value).Distinct().ToList()))
                    .ToList();

            foreach (var (key, types) in groups)
            {
                // It's possible that some branches did not store to a variable, and thus has no opinion about the type.
                // In this case, use the type from the parent context.
                if (types.Count != contexts.Length)
                    types.Add(contexts[0].Parent.TypeOf(key) ?? StackType.YololValue);

                var t = types.Aggregate(UnifyTypes);
                Store(key, t);
            }

            static StackType UnifyTypes(StackType a, StackType b)
            {
                if (a == b)
                    return a;

                return (a, b) switch {
                    (StackType.Bool, StackType.YololNumber) => StackType.YololNumber,
                    (StackType.YololNumber, StackType.Bool) => StackType.YololNumber,

                    _ => StackType.YololValue
                };
            }
        }

        public void Store(VariableName name, StackType type)
        {
            _current.Store(name, type);
        }

        private class RootContext
            : ITypeContext
        {
            private readonly IDictionary<VariableName, StackType> _types;

            [ExcludeFromCodeCoverage]
            public ITypeContext Parent => throw ThrowHelper.Invalid("Root type context has no parent");

            public IReadOnlyDictionary<VariableName, StackType> Types => (IReadOnlyDictionary<VariableName, StackType>)_types;

            public RootContext(IReadOnlyDictionary<VariableName, Type> types)
            {
                var t = new Dictionary<VariableName, StackType>();
                foreach (var (key, val) in types)
                    t.Add(key, val.ToStackType());
                _types = t;
            }

            public void Store(VariableName name, StackType type)
            {
                _types[name] = type;
            }

            public StackType? TypeOf(VariableName name)
            {
                if (_types.TryGetValue(name, out var type))
                    return type;
                return null;
            }

            [ExcludeFromCodeCoverage]
            public void Dispose()
            {
                throw ThrowHelper.Invalid("Cannot dispose root type context");
            }
        }
    }

    internal class TypeContext
        : IDisposable, ITypeContext
    {
        private readonly StaticTypeTracker _tracker;
        private readonly IDictionary<VariableName, StackType> _types;

        public ITypeContext Parent { get; }
        public IReadOnlyDictionary<VariableName, StackType> Types => (IReadOnlyDictionary<VariableName, StackType>)_types;

        public TypeContext(StaticTypeTracker tracker, ITypeContext parent)
        {
            _tracker = tracker;
            Parent = parent;

            _types = new Dictionary<VariableName, StackType>();
        }

        public void Dispose()
        {
            _tracker.ExitContext(this);
        }

        public void Store(VariableName name, StackType type)
        {
            _types[name] = type;
        }

        public StackType? TypeOf(VariableName name)
        {
            if (_types.TryGetValue(name, out var type))
                return type;

            return Parent.TypeOf(name);
        }
    }
}
