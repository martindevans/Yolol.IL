using System;
using System.Collections.Generic;
using Sigil;
using Yolol.Execution;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler
{
    internal class TypeStack<TEmit>
    {
        private readonly Emit<TEmit> _emitter;
        private readonly Stack<StackType> _types = new Stack<StackType>();

        public bool IsEmpty => _types.Count == 0;

        public TypeStack(Emit<TEmit> emitter)
        {
            _emitter = emitter;
        }

        public void Push(StackType type)
        {
            _types.Push(type);
        }

        public void Pop(StackType type)
        {
            var pop = _types.Pop();
            if (pop != type)
                throw new InvalidOperationException($"Attempted to pop `{type}` but stack had `{pop}`");
        }

        public StackType Peek()
        {
            return _types.Peek();
        }

        public void Coerce(StackType target)
        {
            var source = _types.Pop();

            switch (source, target)
            {
                #region identity conversions
                case (StackType.YololString, StackType.YololString):
                case (StackType.YololNumber, StackType.YololNumber):
                case (StackType.YololValue, StackType.YololValue):
                case (StackType.Bool, StackType.Bool):
                case (StackType.StaticError, StackType.StaticError):
                    break;
                #endregion

                #region error conversion
                case (StackType.StaticError, StackType.YololValue):
                    _emitter.CallRuntimeN(nameof(Runtime.ErrorToValue), typeof(StaticError));
                    break;
                #endregion

                #region bool source
                case (StackType.Bool, StackType.YololValue):
                    _emitter.NewObject<Value, bool>();
                    break;

                case (StackType.Bool, StackType.YololNumber):
                    _emitter.CallRuntimeN(nameof(Runtime.BoolToNumber), typeof(bool));
                    break;
                #endregion

                #region string source
                case (StackType.YololString, StackType.Bool):
                    _emitter.Pop();
                    _emitter.LoadConstant(true);
                    break;

                case (StackType.YololString, StackType.YololValue):
                    _emitter.NewObject<Value, YString>();
                    break;
                #endregion

                #region YololNumber source
                case (StackType.YololNumber, StackType.YololValue):
                    _emitter.NewObject<Value, Number>();
                    break;

                case (StackType.YololNumber, StackType.Bool):
                    _emitter.CallRuntimeN(nameof(Runtime.NumberToBool), typeof(Number));
                    break;

                case (StackType.YololNumber, StackType.YololString):
                    _emitter.CallRuntimeThis0<TEmit, Number>(nameof(Number.ToString));
                    _emitter.NewObject<YString, string>();
                    break;
                #endregion

                #region YololValue source
                case (StackType.YololValue, StackType.Bool): {
                    using (var conditional = _emitter.DeclareLocal(typeof(Value)))
                    {
                        _emitter.StoreLocal(conditional);
                        _emitter.LoadLocalAddress(conditional);
                        _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToBool)));
                    }
                    break;
                }
                #endregion

                default:
                    throw new InvalidOperationException($"Cannot coerce `{source}` -> `{target}`");
            }

            // If we get here type coercion succeeded
            Push(target);
        }
    }
}
