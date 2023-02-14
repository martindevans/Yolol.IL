using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sigil;
using Yolol.Analysis.TreeVisitor.Inspection;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.IL.Compiler.Emitter;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler.Memory
{
    internal class ArraySegmentMemoryAccessor<TEmit>
        : IMemoryAccessor<TEmit>
    {
        private readonly OptimisingEmitter<TEmit> _emitter;
        private readonly ushort _externalArraySegmentArg;
        private readonly ushort _internalArraySegmentArg;
        private readonly IReadonlyInternalsMap _internals;
        private readonly IReadonlyExternalsMap _externals;

        private readonly IStaticTypeTracker _types;

        private readonly Dictionary<VariableName, TypedLocal> _cache;
        private readonly Dictionary<VariableName, Local> _cacheDirty;
        private readonly HashSet<VariableName> _mutated;
        private readonly Local? _changeSet;

        public ArraySegmentMemoryAccessor(
            OptimisingEmitter<TEmit> emitter,
            ushort externalArraySegmentArg,
            ushort internalArraySegmentArg,
            IReadonlyInternalsMap internals,
            IReadonlyExternalsMap externals,
            IStaticTypeTracker types,
            Local? changeSet)
        {
            _emitter = emitter;
            _externalArraySegmentArg = externalArraySegmentArg;
            _internalArraySegmentArg = internalArraySegmentArg;
            _internals = internals;
            _externals = externals;
            _types = types;
            _changeSet = changeSet;

            _cache = new Dictionary<VariableName, TypedLocal>();
            _cacheDirty = new Dictionary<VariableName, Local>();
            _mutated = new HashSet<VariableName>();
        }
        
        public void EmitLoad(Line line)
        {
            // Find every variable that is loaded anywhere in the line
            var loadFinder = new FindReadVariables();
            loadFinder.Visit(line);

            // Find every variable that is written to anywhere in the line
            var storeFinder = new FindAssignedVariables();
            storeFinder.Visit(line);
            _mutated.UnionWith(storeFinder.Names);

            // Only cache things which read or written more than once in the line
            var toCache = new HashSet<VariableName>();
            foreach (var (name, count) in loadFinder.Counts)
                if (count > 1)
                    toCache.Add(name);
            foreach (var (name, count) in storeFinder.Counts)
                if (count > 1)
                    toCache.Add(name);

            // Load up everything that will be read later in the line
            // The `IsDirty` flags protect against writing back uninitialised variables at the end
            foreach (var variable in toCache)
            {
                var type = _types.TypeOf(variable) ?? StackType.YololValue;
                var local = _emitter.DeclareLocal(type.ToType(), $"CacheFor_{variable.Name}", false);

                EmitLoadValue(variable);
                StaticUnbox(type);
                _emitter.StoreLocal(local);
                _cache.Add(variable, new TypedLocal(type, local));

                var dirty = _emitter.DeclareLocal(typeof(bool), $"CacheFor_{variable.Name}_IsDirty");
                _emitter.LoadConstant(false);
                _emitter.StoreLocal(dirty);
                _cacheDirty.Add(variable, dirty);
            }
        }

        public void Dispose()
        {
            // Write the cache back out to backing storage
            foreach (var (name, local) in _cache.OrderByDescending(a => SegmentIndex(a.Key)))
            {
                if (_mutated.Contains(name))
                {
                    var skip = _emitter.DefineLabel();

                    var dirty = _cacheDirty[name];
                    _emitter.LoadLocal(dirty);
                    _emitter.BranchIfFalse(skip);

                    _emitter.LoadLocal(local.Local);
                    _emitter.EmitCoerce(local.Type, StackType.YololValue);
                    EmitStoreValue(name);

                    _emitter.MarkLabel(skip);
                }

                local.Local.Dispose();
            }

            // Dispose the locals
            foreach (var local in _cacheDirty)
                local.Value.Dispose();
        }

        /// <summary>
        /// Emit code storing the value on the stack to memory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        public void Store(VariableName name, TypeStack<TEmit> types)
        {
            // Update the type tracker with the newly discovered type
            _types.Store(name, types.Peek);

            if (_cache.ContainsKey(name))
            {
                var local = _cache[name];

                // Convert to the correct type for this local
                var inType = types.Peek;
                ConvertType(inType, local.Type);
                types.Pop(inType);

                // Store in appropriate local
                _emitter.StoreLocal(local.Local);

                // Mark cache dirty
                _emitter.LoadConstant(true);
                _emitter.StoreLocal(_cacheDirty[name]);
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
            // Check if we know what type this variable is
            var type = _types.TypeOf(name) ?? StackType.YololValue;

            if (_cache.ContainsKey(name))
            {
                var local = _cache[name];
                _emitter.LoadLocal(local.Local);

                ConvertType(local.Type, type);
                types.Push(type);
            }
            else
            {
                // Load from backing store
                EmitLoadValue(name);

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

                case StackType.Bool:
                    _emitter.EmitCoerce(StackType.YololValue, StackType.Bool);
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
        private ulong SegmentBitFlag(VariableName name)
        {
            var map = name.IsExternal ? (IReadOnlyDictionary<VariableName, int>)_externals : _internals;
            var flag = map.ChangeSetKey(name).Flag;
            return flag;
        }

        private int SegmentIndex(VariableName name)
        {
            var map = name.IsExternal ? (IReadOnlyDictionary<VariableName, int>)_externals : _internals;
            var idx = map[name];
            return idx;
        }

        /// <summary>
        /// Load the array segment for this type of variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void EmitLoadArraySegmentAddr(VariableName name)
        {
            // Load the correct array segment for whichever type of variable we're accessing
            _emitter.LoadArgumentAddress(name.IsExternal ? _externalArraySegmentArg : _internalArraySegmentArg);
        }

        /// <summary>
        /// Load the index in the array for this variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void EmitLoadIndex(VariableName name)
        {
            var idx = SegmentIndex(name);
            _emitter.LoadConstant(idx);
        }

        private void EmitStoreValue(VariableName name)
        {
            EmitLoadArraySegmentAddr(name);
            EmitLoadIndex(name);
            _emitter.CallRuntimeN(nameof(Runtime.Store), typeof(Value), typeof(ArraySegment<Value>).MakeByRefType(), typeof(int));

            if (_changeSet != null)
            {
                var flag = SegmentBitFlag(name);
                _emitter.LoadConstant(flag);
                _emitter.LoadLocalAddress(_changeSet, false);
                _emitter.CallRuntimeN(nameof(Runtime.BitSet), typeof(ulong), typeof(ulong).MakeByRefType());
            }
        }

        private void EmitLoadValue(VariableName name)
        {
            // Put the array segment and index of this variable onto the stack
            EmitLoadArraySegmentAddr(name);
            EmitLoadIndex(name);

            // Get the value from the array segment
            var get = typeof(ArraySegment<Value>).GetProperty("Item", BindingFlags.Instance | BindingFlags.Public)!.GetGetMethod(false)!;
            _emitter.Call(get);
        }
        #endregion
    }
}
