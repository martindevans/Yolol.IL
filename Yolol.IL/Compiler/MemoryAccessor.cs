using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.IL.Extensions;
using Yolol.Analysis.TreeVisitor.Inspection;
using Yolol.Analysis.Types;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Compiler
{
    internal class MemoryAccessor<TEmit>
        : IDisposable, ITypeAssignments
    {
        private readonly Emit<TEmit> _emitter;
        private readonly Local _externalArraySegmentLocal;
        private readonly Local _internalArraySegmentLocal;
        private readonly InternalsMap _internals;
        private readonly ExternalsMap _externals;

        private readonly IReadOnlyDictionary<VariableName, Type> _knownTypes;

        private readonly Dictionary<VariableName, TypedLocal> _cache;
        private readonly HashSet<VariableName> _mutated;

        public MemoryAccessor(
            Emit<TEmit> emitter,
            Local externalArraySegmentLocal,
            Local internalArraySegmentLocal,
            InternalsMap internals,
            ExternalsMap externals,
            IReadOnlyDictionary<VariableName, Type>? staticTypes)
        {
            _emitter = emitter;
            _externalArraySegmentLocal = externalArraySegmentLocal;
            _internalArraySegmentLocal = internalArraySegmentLocal;
            _internals = internals;
            _externals = externals;

            _knownTypes = staticTypes ?? new Dictionary<VariableName, Type>();
            _cache = new Dictionary<VariableName, TypedLocal>();
            _mutated = new HashSet<VariableName>();
        }
        
        public void Initialise(Line line)
        {
            // Find every variable that is loaded anywhere in the line
            var loadFinder = new FindReadVariables();
            loadFinder.Visit(line);

            // Find every variable that is written to anywhere in the line
            var storeFinder = new FindAssignedVariables();
            storeFinder.Visit(line);
            var stored = new HashSet<VariableName>(storeFinder.Names);
            _mutated.UnionWith(stored);

            // Find all the variables to cache (in this case, all variables accessed or mutated at all) and cache those variables in a local.
            // This could filter down to a narrower set of variables to cache (all the infra is in place for that to work).
            var accessCounts = loadFinder
                .Counts
                .Concat(storeFinder.Counts)
                .Select(a => a.Key)
                .Distinct()
                .ToList();

            // All stored things will be written out at the end. That means we need to load
            // everything that's loaded _or_ stored so that the write later is valid.
            foreach (var variable in accessCounts)
            {
                var type = _knownTypes.TryGetValue(variable, out var t) ? t.ToStackType() : StackType.YololValue;
                var local = _emitter.DeclareLocal(type.ToType(), $"CacheFor_{variable.Name}", false);

                EmitLoadValue(variable);
                StaticUnbox(type);
                _emitter.StoreLocal(local);
                _cache.Add(variable, new TypedLocal(type, local));
            }
        }

        public void Dispose()
        {
            // Write the cache back out to backing storage
            foreach (var (name, local) in _cache)
            {
                if (_mutated.Contains(name))
                {
                    _emitter.LoadLocal(local.Local);
                    _emitter.EmitCoerce(local.Type, StackType.YololValue);
                    EmitStoreValue(name);
                }

                local.Local.Dispose();
            }
        }

        /// <summary>
        /// Emit code storing the value on the stack to memory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        public void Store(VariableName name, TypeStack<TEmit> types)
        {
            if (_cache.ContainsKey(name))
            {
                var local = _cache[name];

                // Convert to the correct type for this local
                var inType = types.Peek;
                ConvertType(inType, local.Type);
                types.Pop(inType);

                // Store in appropriate local
                _emitter.StoreLocal(local.Local);
            }
            else
            {
                // Immediately convert type to YololValue
                ConvertType(types.Peek, StackType.YololValue);
                types.Pop(types.Peek);
                
                // Write directly to backing store
                EmitStoreValue(name);
            }
        }

        /// <summary>
        /// Emit code to load a value onto the stack
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        public void Load(VariableName name, TypeStack<TEmit> types)
        {
            if (_cache.ContainsKey(name))
            {
                var local = _cache[name];
                _emitter.LoadLocal(local.Local);
                types.Push(local.Type);
            }
            else
            {
                // Load fro backing store
                EmitLoadValue(name);

                // Check if we know what type this variable is
                var type = StackType.YololValue;
                if (_knownTypes.TryGetValue(name, out var kType))
                    type = kType.ToStackType();

                // Unbox it to that type (bypassing dynamic type checking)
                StaticUnbox(type);
                types.Push(type);
            }
        }

        #region conversions
        /// <summary>
        /// Convert a `Value` on the stack into another type (bypassing type checking)
        /// </summary>
        private void StaticUnbox(StackType to)
        {
            switch (to)
            {
                default:
                    throw new ArgumentOutOfRangeException(nameof(to), $"Cannot unbox to {to}");

                case StackType.YololValue:
                    return;

                case StackType.YololNumber:
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the property (which will throw if type annotations are wrong).
#if RELEASE
                    _emitter.GetRuntimeFieldValue<TEmit, Value>("_number");
#else
                    _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.Number));
#endif
                    return;

                case StackType.YololString:
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the property (which will throw if type annotations are wrong).
#if RELEASE
                    _emitter.GetRuntimeFieldValue<TEmit, Value>("_string");
#else
                    _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.String));
#endif
                    return;
            }
        }

        private void ConvertType(StackType from, StackType to)
        {
            if (from == to)
                return;

            switch (from, to)
            {
                case (StackType.YololValue, _):
                    StaticUnbox(to);
                    break;

                default:
                    _emitter.EmitCoerce(from, to);
                    break;
            }
        }
        #endregion

        #region direct memory access
        /// <summary>
        /// Load the array segment for this type of variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void EmitLoadArraySegmentAddr(VariableName name)
        {
            // Load the correct array segment for whichever type of variable we're accessing
            if (name.IsExternal)
                _emitter.LoadLocalAddress(_externalArraySegmentLocal);
            else
                _emitter.LoadLocalAddress(_internalArraySegmentLocal);
        }

        /// <summary>
        /// Load the index in the array for this variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void EmitLoadIndex(VariableName name)
        {
            var map = (name.IsExternal ? (Dictionary<string, int>)_externals : _internals);
            if (!map.TryGetValue(name.Name, out var idx))
            {
                idx = map.Count;
                map[name.Name] = idx;
            }
            _emitter.LoadConstant(idx);
        }

        private void EmitStoreValue(VariableName name)
        {
            using (var l = _emitter.DeclareLocal(typeof(Value), "EmitStoreValueTmp", false))
            {
                _emitter.StoreLocal(l);

                // Put the array segment and index of this variable onto the stack
                EmitLoadArraySegmentAddr(name);
                EmitLoadIndex(name);

                // Put the value back on the stack
                _emitter.LoadLocal(l);
            }

            // Find the indexer for array segments
            var set = typeof(ArraySegment<Value>).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public)!.GetSetMethod(false);
            _emitter.Call(set);
        }

        private void EmitLoadValue(VariableName name)
        {
            // Put the array segment and index of this variable onto the stack
            EmitLoadArraySegmentAddr(name);
            EmitLoadIndex(name);

            // Get the value from the array segment
            var get = typeof(ArraySegment<Value>).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public)!.GetGetMethod(false);
            _emitter.Call(get);
        }
        #endregion

        private class TypedLocal
        {
            public StackType Type { get; }
            public Local Local { get; }

            public TypedLocal(StackType type, Local local)
            {
                Type = type;
                Local = local;
            }
        }

        Type? ITypeAssignments.TypeOf(VariableName name)
        {
            if (_knownTypes.TryGetValue(name, out var type))
                return type;
            return default;
        }
    }
}
