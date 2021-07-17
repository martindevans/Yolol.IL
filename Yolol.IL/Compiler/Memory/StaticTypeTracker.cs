using System;
using System.Collections.Generic;
using Yolol.Grammar;
using System.Linq;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Compiler.Memory
{
    internal class StaticTypeTracker
    {
        private readonly ITypeContext _root;
        private ITypeContext _current;

        public StaticTypeTracker(IReadOnlyDictionary<VariableName, Type>? staticTypes)
        {
            _root = new RootContext(staticTypes ?? new Dictionary<VariableName, Type>());
            _current = _root;
        }

        public StackType? TypeOf(VariableName name)
        {
            return _current.TypeOf(name);
        }

        public TypeContext EnterContext()
        {
            var ctx = new TypeContext(this, _current);
            _current = ctx;
            return ctx;
        }

        public TypeContext EnterContext(out TypeContext ctx)
        {
            ctx = EnterContext();
            return ctx;
        }

        public void ExitContext(TypeContext context)
        {
            if (!ReferenceEquals(_current, context))
                throw new InvalidOperationException("Cannot exit non-current type context");
            if (ReferenceEquals(_root, context))
                throw new InvalidOperationException("Cannot exit root type context");

            _current = context.Parent;
        }

        public void Unify(params TypeContext[] contexts)
        {
            if (contexts.Contains(_current))
                throw new InvalidOperationException("Cannot unify types with an active context");
            if (contexts.Select(c => c.Parent).Distinct().Count() != 1)
                throw new InvalidOperationException("Cannot unify types with different parents");

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
                    (StackType.YololValue, _) => StackType.YololValue,
                    (_, StackType.YololValue) => StackType.YololValue,

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
        }
    }

    internal interface ITypeContext
    {
        void Store(VariableName name, StackType type);

        public StackType? TypeOf(VariableName varName);
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
