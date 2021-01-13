using System;
using System.Collections.Generic;
using Sigil;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Compiler
{
    internal class MemoryAccessor<TEmit>
        : IDisposable
    {
        private readonly Emit<TEmit> _emitter;
        private readonly Local _externalArraySegmentLocal;
        private readonly Local _internalArraySegmentLocal;
        private readonly InternalsMap _internals;
        private readonly ExternalsMap _externals;
        private readonly IReadOnlyDictionary<VariableName, Type>? _staticTypes;

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
            _staticTypes = staticTypes;
        }
        
        /// <summary>
        /// Load the array segment for this type of variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void LoadArraySegment(VariableName name)
        {
            // Load the correct array segment for whichever type of variable we're accessing
            if (name.IsExternal)
                _emitter.LoadLocal(_externalArraySegmentLocal);
            else
                _emitter.LoadLocal(_internalArraySegmentLocal);
        }

        /// <summary>
        /// Load the index in the array for this variable name onto the stack
        /// </summary>
        /// <param name="name"></param>
        private void LoadIndex(VariableName name)
        {
            var map = (name.IsExternal ? (Dictionary<string, int>)_externals : _internals);
            if (!map.TryGetValue(name.Name, out var idx))
            {
                idx = map.Count;
                map[name.Name] = idx;
            }
            _emitter.LoadConstant(idx);
        }

        /// <summary>
        /// Emit code storing the value on the stack to memory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        public void EmitStore(VariableName name, TypeStack<TEmit> types)
        {
            // Coerce whatever is on the stack into a `Value`
            types.Coerce(StackType.YololValue);
            types.Pop(StackType.YololValue);

            // Put the array segment and index of this variable onto the stack
            LoadArraySegment(name);
            LoadIndex(name);

            // Put the value on the stack into the array segment
            _emitter.CallRuntimeN(nameof(Runtime.SetArraySegmentIndex), typeof(Value), typeof(ArraySegment<Value>), typeof(int));
        }

        /// <summary>
        /// Emit code to load a value onto the stack
        /// </summary>
        /// <param name="name"></param>
        /// <param name="types"></param>
        public void EmitLoad(VariableName name, TypeStack<TEmit> types)
        {
            // Put the array segment and index of this variable onto the stack
            LoadArraySegment(name);
            LoadIndex(name);

            // Get the value from the array segment
            _emitter.CallRuntimeN(nameof(Runtime.GetArraySegmentIndex), typeof(ArraySegment<Value>), typeof(int));

            // Check if we statically know the type
            if (_staticTypes != null && _staticTypes.TryGetValue(name, out var type))
            {
                if (type == Type.Number)
                {
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the property (which will throw if type annotations are wrong).
                    #if RELEASE
                        _emitter.GetRuntimeFieldValue<TEmit, Value>("_number");
                    #else
                        _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.Number));
                    #endif

                    types.Push(StackType.YololNumber);
                }

                if (type == Type.String)
                {
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the property (which will throw if type annotations are wrong).
                    #if RELEASE
                        _emitter.GetRuntimeFieldValue<TEmit, Value>("_string");
                    #else
                        _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.String));
                    #endif

                    types.Push(StackType.YololString);
                }
            }
            else
            {
                // Didn't statically know the type, just emit a `Value`
                types.Push(StackType.YololValue);
            }
        }

        public void Dispose()
        {
        }
    }
}
