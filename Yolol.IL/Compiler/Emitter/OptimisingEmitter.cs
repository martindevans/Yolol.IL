using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Sigil;
using Yolol.IL.Compiler.Emitter.Instructions;
using Yolol.IL.Compiler.Emitter.Optimisations;

namespace Yolol.IL.Compiler.Emitter
{
    public class OptimisingEmitter<TEmit>
        : IDisposable
    {
        private readonly Emit<TEmit> _emitter;

        private readonly List<BaseInstruction> _ops = new List<BaseInstruction>();

        public OptimisingEmitter(Emit<TEmit> emitter)
        {
            _emitter = emitter;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            foreach (var op in _ops)
                builder.AppendLine(op.ToString());
            return builder.ToString();
        }

        public void Optimise()
        {
            var optimisations = new List<BaseOptimisation> {
                new StoreLoadChain(),
                //new LoadStoreChain(),
            };

            for (var i = 0; i < 128; i++)
            {
                var changed = false;
                foreach (var item in optimisations)
                    changed |= item.Match(_ops);

                if (!changed)
                    break;
            }
        }

        public void Dispose()
        {
            foreach (var op in _ops)
                op.Emit(_emitter);
        }

        #region args
        public void LoadArgument(ushort arg)
        {
            _ops.Add(new LoadArgument(arg));
        }

        public void LoadArgumentAddress(ushort arg)
        {
            _ops.Add(new LoadArgumentAddress(arg));
        }
        #endregion

        #region call
        public void Call(MethodInfo method, Type[]? arglist = null)
        {
            _ops.Add(new Call(method, arglist));
        }
        #endregion

        #region labels
        public Label DefineLabel(string? name = null)
        {
            return _emitter.DefineLabel(name);
        }

        public void MarkLabel(Label label)
        {
            _ops.Add(new MarkLabel(label));
        }

        public void BranchIfFalse(Label label)
        {
            _ops.Add(new BranchIfFalse(label));
        }

        public void BranchIfTrue(Label label)
        {
            _ops.Add(new BranchIfTrue(label));
        }
        
        public void Branch(Label label)
        {
            _ops.Add(new Branch(label));
        }
        #endregion

        #region locals
        public Local DeclareLocal(Type type, string? name = null, bool initializeReused = true)
        {
            return _emitter.DeclareLocal(type, name, initializeReused);
        }

        public Local DeclareLocal<T>(string? name = null, bool initializeReused = true)
        {
            return _emitter.DeclareLocal<T>(name, initializeReused);
        }

        public void StoreLocal(Local local)
        {
            _ops.Add(new StoreLocal(local));
        }

        public void LoadLocal(Local local)
        {
            _ops.Add(new LoadLocal(local));
        }

        public void LoadLocalAddress(Local local)
        {
            _ops.Add(new LoadLocalAddress(local));
        }
        #endregion

        #region fields
        public void LoadField(FieldInfo field, bool? isVolatile = null, int? unaligned = null)
        {
            _ops.Add(new LoadFieldOp(field, isVolatile, unaligned));
        }

        private class LoadFieldOp
            : BaseInstruction
        {
            private readonly FieldInfo _field;
            private readonly bool? _isVolatile;
            private readonly int? _unaligned;

            public LoadFieldOp(FieldInfo field, bool? isVolatile, int? unaligned)
            {
                _field = field;
                _isVolatile = isVolatile;
                _unaligned = unaligned;
            }

            public override void Emit<T>(Emit<T> emitter)
            {
                emitter.LoadField(_field, _isVolatile, _unaligned);
            }
        }
        #endregion

        #region stack
        public void Duplicate()
        {
            _ops.Add(new Duplicate());
        }

        public void Pop()
        {
            _ops.Add(new Pop());
        }

        /// <summary>
        /// Leave an exception or catch block, branching to the given label.
        /// 
        /// This instruction empties the stack.
        /// </summary>
        public void Leave(Label label)
        {
            _ops.Add(new Leave(label));
        }
        #endregion

        #region load constant
        public void LoadConstant(int value)
        {
            _ops.Add(new LoadConstantInt32(value));
        }

        public void LoadConstant(bool value)
        {
            _ops.Add(new LoadConstantBool(value));
        }

        public void LoadConstant(long value)
        {
            _ops.Add(new LoadConstantInt64(value));
        }

        public void LoadConstant(string value)
        {
            _ops.Add(new LoadConstantString(value));
        }
        #endregion

        #region new
        public void NewObject(ConstructorInfo constructor)
        {
            _ops.Add(new NewObjectConstructor(constructor));
        }

        public void NewObject<TA, TB>()
        {
            _ops.Add(new NewObject<TA, TB>());
        }
        #endregion

        #region logical
        public void And()
        {
            _ops.Add(new AndOp());
        }

        private class AndOp
            : BaseInstruction
        {
            public override void Emit<T>(Emit<T> emitter)
            {
                emitter.And();
            }
        }


        public void Or()
        {
            _ops.Add(new OrOp());
        }

        private class OrOp
            : BaseInstruction
        {
            public override void Emit<T>(Emit<T> emitter)
            {
                emitter.Or();
            }
        }
        #endregion

        #region return
        public void Return()
        {
            _ops.Add(new Return());
        }
        #endregion

        #region writeline
        public void WriteLine(string format, params Local[] args)
        {
            _ops.Add(new WriteLine(format, args));
        }
        #endregion

        #region try/catch
        public ExceptionBlock BeginExceptionBlock()
        {
            var ret = new ExceptionBlock();
            _ops.Add(new BeginExceptionBlockOp(ret));
            return ret;
        }

        private class BeginExceptionBlockOp
            : BaseInstruction
        {
            private readonly ExceptionBlock _block;

            public BeginExceptionBlockOp(ExceptionBlock block)
            {
                _block = block;
            }

            public override void Emit<T>(Emit<T> emitter)
            {
                _block.Block = emitter.BeginExceptionBlock();
            }

            public override string ToString()
            {
                return "BeginExceptionBlock";
            }
        }

        public class ExceptionBlock
        {
            public Sigil.ExceptionBlock? Block { get; set; }
        }


        public CatchBlock BeginCatchBlock<Texception>(ExceptionBlock block)
        {
            var ret = new CatchBlock();
            _ops.Add(new BeginCatchBlockOp<Texception>(block, ret));
            return ret;
        }

        private class BeginCatchBlockOp<Texception>
            : BaseInstruction
        {
            private readonly ExceptionBlock _outer;
            private readonly CatchBlock _catch;

            public BeginCatchBlockOp(ExceptionBlock outer, CatchBlock @catch)
            {
                _outer = outer;
                _catch = @catch;
            }

            public override void Emit<T>(Emit<T> emitter)
            {
                _catch.Block = emitter.BeginCatchBlock<Texception>(_outer.Block);
            }

            public override string ToString()
            {
                return $"BeginCatchBlock<{typeof(Texception)}>";
            }
        }

        public class CatchBlock
        {
            public Sigil.CatchBlock? Block { get; set; }
        }


        public void EndCatchBlock(CatchBlock block)
        {
            _ops.Add(new EndCatchBlockOp(block));
        }

        private class EndCatchBlockOp
            : BaseInstruction
        {
            private readonly CatchBlock _block;

            public EndCatchBlockOp(CatchBlock block)
            {
                _block = block;
            }

            public override void Emit<T>(Emit<T> emitter)
            {
                emitter.EndCatchBlock(_block.Block);
            }

            public override string ToString()
            {
                return "EndCatchBlock";
            }
        }


        public void EndExceptionBlock(ExceptionBlock exBlock)
        {
            _ops.Add(new EndExceptionBlockOp(exBlock));
        }

        private class EndExceptionBlockOp
            : BaseInstruction
        {
            private readonly ExceptionBlock _block;

            public EndExceptionBlockOp(ExceptionBlock block)
            {
                _block = block;
            }

            public override void Emit<T>(Emit<T> emitter)
            {
                if (_block.Block == null)
                    throw new InvalidOperationException("Exception block has not been opened yet");

                emitter.EndExceptionBlock(_block.Block);
            }

            public override string ToString()
            {
                return "EndExceptionBlockOp";
            }
        }
        #endregion

        /// <summary>
        /// Takes a destination pointer, a source pointer as arguments.  Pops both off the stack.
        /// 
        /// Copies the given value type from the source to the destination.
        /// </summary>
        public void CopyObject<TObject>()
            where TObject : struct
        {
            _ops.Add(new CopyObject<TObject>());
        }

        /// <summary>
        /// Takes a destination pointer, a source pointer as arguments.  Pops both off the stack.
        /// 
        /// Copies the given value type from the source to the destination.
        /// </summary>
        public void CopyObject(Type type)
        {
            _ops.Add(new CopyObject(type));
        }
    }
}
