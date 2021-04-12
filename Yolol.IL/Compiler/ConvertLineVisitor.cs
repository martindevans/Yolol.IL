using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Sigil;
using Yolol.Analysis.ControlFlowGraph.AST;
using Yolol.Analysis.TreeVisitor;
using Yolol.Analysis.Types;
using Yolol.Execution;
using Yolol.Execution.Attributes;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;
using Yolol.IL.Compiler.Vectorisation;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler
{
    internal class ConvertLineVisitor<TEmit>
        : BaseTreeVisitor
    {
        private readonly Emit<TEmit> _emitter;

        private readonly int _maxLineNumber;
        private readonly MemoryAccessor<TEmit> _memory;
        private readonly Label _gotoLabel;
        private readonly Label _runtimeErrorLabel;

        private readonly TypeStack<TEmit> _types;

        public bool IsTypeStackEmpty => _types.IsEmpty;

        public ConvertLineVisitor(
            Emit<TEmit> emitter,
            int maxLineNumber,
            MemoryAccessor<TEmit> memory,
            Label gotoLabel,
            Label runtimeErrorLabel
        )
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _memory = memory;
            _gotoLabel = gotoLabel;
            _runtimeErrorLabel = runtimeErrorLabel;

            _types = new TypeStack<TEmit>(_emitter);
        }

        #region statements
        protected override StatementList Visit(StatementList list)
        {
            var strategies = new BaseVectorisationStrategy<TEmit>[] {
                new NoStrategy<TEmit>()
            };

            var q = new List<BaseStatement>(list.Statements);
            while (q.Count > 0)
            {
                var success = false;
                foreach (var strat in strategies)
                {
                    if (strat.Try(q, _emitter))
                    {
                        success = true;
                        break;
                    }
                }

                // if no vectorisation strategies succeeded remove one item from the queue and try again
                if (!success)
                {
                    Visit(q[0]);
                    q.RemoveAt(0);
                }
            }

            return list;
        }

        protected override BaseStatement Visit(Assignment ass)
        {
            // Place the value to put into this variable on the stack
            if (Visit(ass.Right) is ErrorExpression)
                return ass;

            // Emit code to assign the value on the stack to the variable
            _memory.Store(ass.Left, _types);

            return ass;
        }

        protected override BaseStatement Visit(If @if)
        {
            // Create labels for control flow like:
            //
            //     entry point
            //     branch_if_false falseLabel
            //     true branch code
            //     jmp exitLabel
            //     falseLabel:
            //         false branch code
            //         jmp exitlabel
            //     exitlabel:
            //
            var falseLabel = _emitter.DefineLabel();
            var exitLabel = _emitter.DefineLabel();

            // Visit conditional which places a value on the stack
            if (Visit(@if.Condition) is ErrorExpression)
                return @if;

            // Convert it to a bool we can branch on
            _types.Coerce(StackType.Bool);
            _types.Pop(StackType.Bool);

            // jump to false branch if the condition is false. Fall through to true branch
            _emitter.BranchIfFalse(falseLabel);

            // Emit true branch
            Visit(@if.TrueBranch);
            _emitter.Branch(exitLabel);

            // Emit false branch
            _emitter.MarkLabel(falseLabel);
            Visit(@if.FalseBranch);
            _emitter.Branch(exitLabel);

            // Exit point for both branches
            _emitter.MarkLabel(exitLabel);

            return @if;
        }

        protected override BaseStatement Visit(Goto @goto)
        {
            if (@goto.Destination is ConstantNumber constNum)
            {
                var dest = Runtime.GotoNumber(constNum.Value, _maxLineNumber);
                _emitter.LoadConstant(dest);
            }
            else
            {
                // Put destination value on the stack
                if (Visit(@goto.Destination) is ErrorExpression)
                    return @goto;

                switch (_types.Peek)
                {
                    case StackType.Bool:
                        // `Goto1` and `Goto0` both go to line 1
                        _types.Pop(StackType.Bool);
                        _emitter.Pop();
                        _emitter.LoadConstant(1);
                        break;

                    case StackType.YololNumber:
                        _types.Coerce(StackType.YololNumber);
                        _types.Pop(StackType.YololNumber);
                        _emitter.LoadConstant(_maxLineNumber);
                        _emitter.CallRuntimeN(nameof(Runtime.GotoNumber), typeof(Number), typeof(int));
                        break;

                    case StackType.YololValue:
                    case StackType.YololString:
                        _types.Coerce(StackType.YololValue);
                        _types.Pop(StackType.YololValue);
                        _emitter.LoadConstant(_maxLineNumber);
                        _emitter.CallRuntimeN(nameof(Runtime.GotoValue), typeof(Value), typeof(int));
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Jump to the `goto` label
            _emitter.Branch(_gotoLabel);

            // Drop a label here so any code following the goto is still "reachable" and thus valid.
            _emitter.MarkLabel(_emitter.DefineLabel());

            return @goto;
        }

        protected override BaseStatement Visit(CompoundAssignment compAss) => Visit(new Assignment(compAss.Left, compAss.Right));
        #endregion

        #region basic expressions
        public override BaseExpression Visit(BaseExpression expression)
        {
            var result = base.Visit(expression);

            // Check if the last expression was guaranteed to generate an error
            // If so jump to the runtime error handler
            if (_types.Peek == StackType.StaticError || result is ErrorExpression)
            {
                // Empty out all items on the stack
                while (!_types.IsEmpty)
                {
                    _types.Pop(_types.Peek);
                    _emitter.Pop();
                }

                // Jump away to the error handler
                _emitter.Branch(_runtimeErrorLabel);

                // Some dead code will be emitted after this point. Drop down a label to satisfy sigil that this is ok
                _emitter.MarkLabel(_emitter.DefineLabel());

                // Return an error expression, indicating that an error is guaranteed to have happened in this evaluation
                return new ErrorExpression();
            }

            return result;
        }

        protected override BaseExpression Visit(ConstantNumber con)
        {
            // Reflect out the raw int64 value
            var rawValueField = typeof(Number).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
            var rawValue = (long)rawValueField!.GetValue(con.Value)!;

            // if the raw value represents one of the two boolean values, emit a bool
            if (rawValue == 0 || rawValue == 1000)
            {
                _emitter.LoadConstant(rawValue == 1000);
                _types.Push(StackType.Bool);
            }
            else
            {
                // Convert raw value into a number
                _emitter.LoadConstant(rawValue);
                _emitter.NewObject<Number, long>();
                _types.Push(StackType.YololNumber);
            }

            return con;
        }

        protected override BaseExpression Visit(ConstantString str)
        {
            // Put a string on the stack
            _emitter.LoadConstant(str.Value.ToString());
            _emitter.NewObject<YString, string>();
            _types.Push(StackType.YololString);

            return str;
        }

        protected override BaseExpression Visit(Grammar.AST.Expressions.Variable var)
        {
            _memory.Load(var.Name, _types);
            return var;
        }

        private bool TryStaticEvaluate<T>(T expr, out bool runtimeError)
            where T : BaseExpression
        {
#if !DEBUG
            if (expr.IsConstant)
            {
                var v = Execution.Extensions.BaseExpressionExtensions.TryStaticEvaluate(expr, out runtimeError);
                if (v.HasValue)
                {
                    Visit(v.Value.ToConstant());
                    return true;
                }
            }
#endif
            runtimeError = false;
            return false;
        }
        #endregion

        #region binary expressions
        private BaseExpression ConvertBinaryCoerce<T>(T expr, StackType input, StackType output, Action emit)
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : (BaseExpression)expr;

            // Visit the inner expression of the unary and coerce it to the correct type
            // Visit left and right, coercing them into `Value`s
            if (Visit(expr.Left) is ErrorExpression)
                return new ErrorExpression();
            _types.Coerce(input);
            if (Visit(expr.Right) is ErrorExpression)
                return new ErrorExpression();
            _types.Coerce(input);

            // Emit IL
            emit();

            // Update types
            _types.Pop(input);
            _types.Pop(input);
            _types.Push(output);

            return expr;
        }

        // ReSharper disable InconsistentNaming
        private BaseExpression ConvertBinaryExpr<T, TBB, TBN, TBS, TBV, TNB, TNN, TNS, TNV, TSB, TSN, TSS, TSV, TVB, TVN, TVS, TVV>(T expr,
        // ReSharper restore InconsistentNaming
            Expression<Func<bool, bool, TBB>> emitBoolBool,
            Expression<Func<bool, Number, TBN>> emitBoolNum,
            Expression<Func<bool, YString, TBS>> emitBoolStr,
            Expression<Func<bool, Value, TBV>> emitBoolVal,
            Expression<Func<Number, bool, TNB>> emitNumBool,
            Expression<Func<Number, Number, TNN>> emitNumNum,
            Expression<Func<Number, YString, TNS>> emitNumStr,
            Expression<Func<Number, Value, TNV>> emitNumVal,
            Expression<Func<YString, bool, TSB>> emitStrBool,
            Expression<Func<YString, Number, TSN>> emitStNum,
            Expression<Func<YString, YString, TSS>> emitStStr,
            Expression<Func<YString, Value, TSV>> emitStVal,
            Expression<Func<Value, bool, TVB>> emitValBool,
            Expression<Func<Value, Number, TVN>> emitValNum,
            Expression<Func<Value, YString, TVS>> emitValStr,
            Expression<Func<Value, Value, TVV>> emitValVal
        )
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : (BaseExpression)expr;

            // Convert the two sides of the expression
            if (Visit(expr.Left) is ErrorExpression)
                return new ErrorExpression();
            var left = _types.Peek;
            if (Visit(expr.Right) is ErrorExpression)
                return new ErrorExpression();
            var right = _types.Peek;

            // Create some labels for error handling stuff
            var errorLabel = _emitter.DefineLabel();
            var noErrorLabel = _emitter.DefineLabel();
            
            // Emit code
            _types.Pop(right);
            _types.Pop(left);
            var (emitType, fallible) = (left, right) switch {

                (StackType.Bool, StackType.Bool) => emitBoolBool.ConvertBinary(_emitter, errorLabel),
                (StackType.Bool, StackType.YololNumber) => emitBoolNum.ConvertBinary(_emitter, errorLabel),
                (StackType.Bool, StackType.YololString) => emitBoolStr.ConvertBinary(_emitter, errorLabel),
                (StackType.Bool, StackType.YololValue) => emitBoolVal.ConvertBinary(_emitter, errorLabel),

                (StackType.YololNumber, StackType.Bool) => emitNumBool.ConvertBinary(_emitter, errorLabel),
                (StackType.YololNumber, StackType.YololNumber) => emitNumNum.ConvertBinary(_emitter, errorLabel),
                (StackType.YololNumber, StackType.YololString) => emitNumStr.ConvertBinary(_emitter, errorLabel),
                (StackType.YololNumber, StackType.YololValue) => emitNumVal.ConvertBinary(_emitter, errorLabel),

                (StackType.YololString, StackType.Bool) => emitStrBool.ConvertBinary(_emitter, errorLabel),
                (StackType.YololString, StackType.YololNumber) => emitStNum.ConvertBinary(_emitter, errorLabel),
                (StackType.YololString, StackType.YololString) => emitStStr.ConvertBinary(_emitter, errorLabel),
                (StackType.YololString, StackType.YololValue) => emitStVal.ConvertBinary(_emitter, errorLabel),

                (StackType.YololValue, StackType.Bool) => emitValBool.ConvertBinary(_emitter, errorLabel),
                (StackType.YololValue, StackType.YololNumber) => emitValNum.ConvertBinary(_emitter, errorLabel),
                (StackType.YololValue, StackType.YololString) => emitValStr.ConvertBinary(_emitter, errorLabel),
                (StackType.YololValue, StackType.YololValue) => emitValVal.ConvertBinary(_emitter, errorLabel),

                _ => throw new InvalidOperationException($"{expr.GetType().Name}({left},{right})")
            };
            _types.Push(emitType!.ToStackType());

            if (fallible)
            {
                // Jump past the error handler
                _emitter.Branch(noErrorLabel);

                // Create the error handler which empties the stack
                _emitter.MarkLabel(errorLabel);

                // If execution arrives here a runtime error has occured. Empty the stack and jump to the global error handler.
                // There is one less item on the stack than you'd think, because the result of this operation was pushed onto the
                // type tracking stack (but isn't really there in the error case)
                for (var i = 0; i < _types.Count - 1; i++)
                    _emitter.Pop();
                _emitter.Branch(_runtimeErrorLabel);

                // Mark label to jump past error handler
                _emitter.MarkLabel(noErrorLabel);
            }

            return expr;
        }

        protected override BaseExpression Visit(Add add) => ConvertBinaryExpr(add,
            (a, b) => (Number)a + b,
            (a, b) => (Number)a + b,
            (a, b) => (Number)a + b,
            (a, b) => (Number)a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b,
            (a, b) => a + b
        );

        protected override BaseExpression Visit(Subtract sub)
        {
            if (sub.Left is PostDecrement pd && sub.Right is Grammar.AST.Expressions.Variable var && pd.Name.Equals(var.Name))
                return UnaryTripleSubtract(var);

            return ConvertBinaryExpr(sub,
                (a, b) => (Number)a - b,
                (a, b) => (Number)a - b,
                (a, b) => (Number)a - b,
                (a, b) => (Number)a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b,
                (a, b) => a - b
            );
        }

        /// <summary>
        /// Special case handling for `a---a`
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        private BaseExpression UnaryTripleSubtract(Grammar.AST.Expressions.Variable variable) => EmitUnaryExpr(new Bracketed(variable), errorLabel =>
        {
            void EmitNum()
            {
                // Pop value off stack, it'll be reloaded later
                // This isn't ideal - we've already done a runtime type check to get here and this forces the `add` method to do it again!
                _emitter.Pop();

                // Rewrite `a-- - a` into `a-- + -a` to avoid this optimisation being detected again and causing an infinite loop
                Visit(new Add(new PostDecrement(variable.Name), new Negate(variable)));
            }

            void EmitStr()
            {
                // Get the "will throw" method to check if the decrement throws
                var dec = typeof(YString).GetMethod("op_Decrement", BindingFlags.Public | BindingFlags.Static)!;
                var willThrow = dec.TryGetWillThrowMethod(typeof(YString))!;

                // Jump to error handling if necessary
                using (var willThrowLocal = _emitter.DeclareLocal(typeof(YString), "will_throw_tmp"))
                {
                    _emitter.StoreLocal(willThrowLocal);
                    _emitter.LoadLocal(willThrowLocal);
                    _emitter.Call(willThrow);
                    _emitter.BranchIfTrue(errorLabel);

                    // Decrement string and save changed value
                    _emitter.LoadLocal(willThrowLocal);
                    _emitter.CallRuntimeN<TEmit, YString>("op_Decrement", typeof(YString));
                    _types.Push(StackType.YololString);
                    _memory.Store(variable.Name, _types);

                    // Get last character from string
                    _emitter.LoadLocal(willThrowLocal);
                    _emitter.CallRuntimeThis0<TEmit, YString>(nameof(YString.LastCharacter));
                    _types.Push(StackType.YololString);
                }
            }

            void EmitVal()
            {
                var numLabel = _emitter.DefineLabel();
                var endLabel = _emitter.DefineLabel();

                using (var valueLocal = _emitter.DeclareLocal(typeof(Value), "TripleSubtractTemp"))
                {
                    _emitter.StoreLocal(valueLocal);

                    // Check type and jump to branch for string
                    _emitter.LoadLocal(valueLocal);
                    _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.Type));
                    _emitter.LoadConstant((int)Execution.Type.Number);
                    _emitter.BranchIfEqual(numLabel);

                    // Fallthrough to here means it's a string
                    _emitter.LoadLocal(valueLocal);
                    _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.String));
                    EmitStr();
                    _types.Pop(StackType.YololString);
                    _emitter.EmitCoerce(StackType.YololString, StackType.YololValue);
                    _emitter.Branch(endLabel);

                    // Jump here means it's a number
                    _emitter.MarkLabel(numLabel);
                    _emitter.LoadLocal(valueLocal);
                    _emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.Number));
                    EmitNum();
                    _types.Pop(StackType.YololValue);
                }

                // However we got here, there's a value left on the stack
                _emitter.MarkLabel(endLabel);
                _types.Push(StackType.YololValue);
            }

            // Discover what type we're working with
            var p = _types.Peek;
            _types.Pop(p);

            bool fallible;
            switch (p)
            {
                case StackType.Bool:
                    fallible = false;
                    _emitter.EmitCoerce(StackType.Bool, StackType.YololNumber);
                    EmitNum();
                    break;

                case StackType.YololNumber:
                    fallible = false;
                    EmitNum();
                    break;

                case StackType.YololString:
                    fallible = true;
                    EmitStr();
                    break;

                case StackType.YololValue:
                    fallible = true;
                    EmitVal();
                    break;

                default:
                    throw new InvalidProgramException($"Cannot triple subtract `StackType.{p}`");
            }

            return fallible;
        });

        protected override BaseExpression Visit(Multiply mul) => ConvertBinaryExpr(mul,
            (a, b) => (Number)a * b,
            (a, b) => (Number)a * b,
            (a, b) => (Number)a * b,
            (a, b) => (Number)a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b,
            (a, b) => a * b
        );

        protected override BaseExpression Visit(Divide div) => ConvertBinaryExpr(div,
            (a, b) => (Number)a / b,
            (a, b) => (Number)a / b,
            (a, b) => (Number)a / b,
            (a, b) => (Number)a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b,
            (a, b) => a / b
        );

        protected override BaseExpression Visit(EqualTo eq) => ConvertBinaryExpr(eq,
            (a, b) => (Number)a == b,
            (a, b) => (Number)a == b,
            (a, b) => (Number)a == b,
            (a, b) => (Number)a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b,
            (a, b) => a == b
        );

        protected override BaseExpression Visit(NotEqualTo neq) => ConvertBinaryExpr(neq,
            (a, b) => (Number)a != b,
            (a, b) => (Number)a != b,
            (a, b) => (Number)a != b,
            (a, b) => (Number)a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b,
            (a, b) => a != b
        );

        protected override BaseExpression Visit(GreaterThan gt) => ConvertBinaryExpr(gt,
            (a, b) => (Number)a > b,
            (a, b) => (Number)a > b,
            (a, b) => (Number)a > b,
            (a, b) => (Number)a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b,
            (a, b) => a > b
        );

        protected override BaseExpression Visit(GreaterThanEqualTo gteq) => ConvertBinaryExpr(gteq,
            (a, b) => (Number)a >= b,
            (a, b) => (Number)a >= b,
            (a, b) => (Number)a >= b,
            (a, b) => (Number)a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b,
            (a, b) => a >= b
        );

        protected override BaseExpression Visit(LessThan lt) => ConvertBinaryExpr(lt,
            (a, b) => (Number)a < b,
            (a, b) => (Number)a < b,
            (a, b) => (Number)a < b,
            (a, b) => (Number)a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b,
            (a, b) => a < b
        );

        protected override BaseExpression Visit(LessThanEqualTo lteq) => ConvertBinaryExpr(lteq,
            (a, b) => (Number)a <= b,
            (a, b) => (Number)a <= b,
            (a, b) => (Number)a <= b,
            (a, b) => (Number)a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b,
            (a, b) => a <= b
        );

        protected override BaseExpression Visit(Modulo mod) => ConvertBinaryExpr(mod,
            (a, b) => (Number)a % b,
            (a, b) => (Number)a % b,
            (a, b) => (Number)a % b,
            (a, b) => (Number)a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % (Number)b,
            (a, b) => a % b,
            (a, b) => a % b,
            (a, b) => a % b
        );

        protected override BaseExpression Visit(And and) => ConvertBinaryCoerce(and, StackType.Bool, StackType.Bool, () => {
            _emitter.And();
        });

        protected override BaseExpression Visit(Or or) => ConvertBinaryCoerce(or, StackType.Bool, StackType.Bool, () => {
            _emitter.Or();
        });

        protected override BaseExpression Visit(Exponent exp) => ConvertBinaryExpr(exp,
            (a, b) => ((Number)a).Exponent(b),
            (a, b) => ((Number)a).Exponent(b),
            (a, b) => ((Number)a).Exponent(b),
            (a, b) => ((Number)a).Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => a.Exponent(b),
            (a, b) => Value.Exponent(a, b),
            (a, b) => Value.Exponent(a, b),
            (a, b) => Value.Exponent(a, b),
            (a, b) => Value.Exponent(a, b)
        );
        #endregion

        #region unary expressions
        private BaseExpression EmitUnaryExpr<T>(T expr, Func<Label, bool> emit)
            where T : BaseUnaryExpression
        {
             if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : (BaseExpression)expr;

             // Visit the inner expression of the unary
             if (Visit(expr.Parameter) is ErrorExpression)
                 return new ErrorExpression();

             // Create some labels for error handling stuff
             var errorLabel = _emitter.DefineLabel();
             var noErrorLabel = _emitter.DefineLabel();

             // Emit code
             var fallible = emit(errorLabel);

             if (fallible)
             {
                 // Jump past the error handler
                 _emitter.Branch(noErrorLabel);

                 // Create the error handler which empties the stack
                 _emitter.MarkLabel(errorLabel);

                 // If execution arrives here a runtime error has occured. Empty the stack and jump to the global error handler.
                 // There is one less item on the stack than you'd think, because the result of this operation was pushed onto the
                 // type tracking stack (but isn't really there in the error case)
                 for (var i = 0; i < _types.Count - 1; i++)
                     _emitter.Pop();
                 _emitter.Branch(_runtimeErrorLabel);

                 // Mark label to jump past error handler
                 _emitter.MarkLabel(noErrorLabel);
             }

             return expr;
        }

        private BaseExpression ConvertUnaryExpr<T, TB, TN, TS, TV>(T expr, Expression<Func<bool, TB>> emitBool, Expression<Func<Number, TN>> emitNum, Expression<Func<YString, TS>> emitStr, Expression<Func<Value, TV>> emitVal)
            where T : BaseUnaryExpression
        {
            return EmitUnaryExpr(expr, errorLabel => {
                // Emit code
                var p = _types.Peek;
                _types.Pop(p);
                var (emitType, fallible) = p switch {
                    StackType.Bool => emitBool.ConvertUnary(_emitter, errorLabel),
                    StackType.YololNumber => emitNum.ConvertUnary(_emitter, errorLabel),
                    StackType.YololString => emitStr.ConvertUnary(_emitter, errorLabel),
                    StackType.YololValue => emitVal.ConvertUnary(_emitter, errorLabel),
                    _ => throw new InvalidOperationException($"{expr.GetType().Name}({p})")
                };
                _types.Push(emitType!.ToStackType());

                return fallible;
            });
        }

        protected override BaseExpression Visit(Factorial fac) => ConvertUnaryExpr(fac,
            b => (Number)1,
            n => n.Factorial(),
            s => new StaticError("Attempted to Factorial a string value"),
            v => v.Factorial()
        );

        protected override BaseExpression Visit(Not not) => ConvertUnaryExpr(not,
            b => !b,
            n => n == (Number)0,
            s => false,
            v => !v
        );

        protected override BaseExpression Visit(Negate neg) => ConvertUnaryExpr(neg,
            b => -(Number)b,
            n => -n,
            s => new StaticError("Attempted to negate a string value"),
            v => -v
        );

        protected override BaseExpression Visit(Sqrt sqrt) => ConvertUnaryExpr(sqrt,
            b => b,
            n => n.Sqrt(),
            s => new StaticError("Attempted to `SQRT` a string value"),
            v => Value.Sqrt(v)
        );

        protected override BaseExpression Visit(ArcCos acos) => ConvertUnaryExpr(acos,
            b => ((Number)b).ArcCos(),
            n => n.ArcCos(),
            s => new StaticError("Attempted to `ACOS` a string value"),
            v => Value.ArcCos(v)
        );

        protected override BaseExpression Visit(ArcSine asin) => ConvertUnaryExpr(asin,
            b => ((Number)b).ArcSin(),
            n => n.ArcSin(),
            s => new StaticError("Attempted to `ASIN` a string value"),
            v => Value.ArcSin(v)
        );

        protected override BaseExpression Visit(ArcTan atan) => ConvertUnaryExpr(atan,
            b => ((Number)b).ArcTan(),
            n => n.ArcTan(),
            s => new StaticError("Attempted to `ATAN` a string value"),
            v => Value.ArcTan(v)
        );

        protected override BaseExpression Visit(Cosine cos) => ConvertUnaryExpr(cos,
            b => ((Number)b).Cos(),
            n => n.Cos(),
            s => new StaticError("Attempted to `COS` a string value"),
            v => Value.Cos(v)
        );

        protected override BaseExpression Visit(Sine sin) => ConvertUnaryExpr(sin,
            b => ((Number)b).Sin(),
            n => n.Sin(),
            s => new StaticError("Attempted to `SIN` a string value"),
            v => Value.Sin(v)
        );

        protected override BaseExpression Visit(Tangent tan) => ConvertUnaryExpr(tan,
            b => ((Number)b).Tan(),
            n => n.Tan(),
            s => new StaticError("Attempted to `TAN` a string value"),
            v => Value.Tan(v)
        );

        protected override BaseExpression Visit(Abs abs) => ConvertUnaryExpr(abs,
            b => b,
            n => n.Abs(),
            s => new StaticError("Attempted to `ABS` a string value"),
            v => Value.Abs(v)
        );
        #endregion

        #region modify expressions
        private T Decrement<T>(T expr, bool preOp)
            where T : BaseModifyInPlace
        {
            // Put the current value of the variable onto the stack
            Visit(new Grammar.AST.Expressions.Variable(expr.Name));

            // If we need the old value save it now
            if (!preOp)
            {
                _emitter.Duplicate();
                _types.Push(_types.Peek);
            }

            // If there is a bool on the stack coerce it to a number (bool won't have inc/dec operators defined on it)
            if (StackType.Bool == _types.Peek)
                _types.Coerce(StackType.YololNumber);

            // Find the operator method
            var peek = _types.Peek.ToType();
            var method = peek.GetMethod("op_Decrement", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { peek }, null);
            if (method == null)
                throw new InvalidOperationException($"Cannot find method `op_Decrement` on type {peek}");

            // Find the `will throw` method
            var willThrow = method.TryGetWillThrowMethod(peek);

            // Emit code to check if the decrement will throw
            var noErrorLabel = _emitter.DefineLabel();
            if (willThrow != null)
            {
                // Duplicate the item on the top of the stack
                using (var dup = _emitter.DeclareLocal(peek, "StackPeek", false))
                {
                    _emitter.StoreLocal(dup);
                    _emitter.LoadLocal(dup);
                    _emitter.LoadLocal(dup);
                }

                // Call the "will throw" method
                _emitter.Call(willThrow);

                // Jump past the error handler if "will throw" returned false
                _emitter.BranchIfFalse(noErrorLabel);

                // If execution arrives here a runtime error has occured. Empty the stack and jump to the global error handler.
                for (var i = 0; i < _types.Count; i++)
                    _emitter.Pop();
                _emitter.Branch(_runtimeErrorLabel);
            }

            // Mark label to jump past error handler
            _emitter.MarkLabel(noErrorLabel);

            // Do the actual work
            _emitter.Call(method);
            _types.Pop(_types.Peek);
            _types.Push(method.ReturnType.ToStackType());
            

            // If we need to return the new value, save it now by duplicating it
            if (preOp)
            {
                _emitter.Duplicate();
                _types.Push(_types.Peek);
            }

            // Write value to variable
            _memory.Store(expr.Name, _types);

            return expr;
        }

        private T Increment<T>(T expr, bool preOp)
            where T : BaseModifyInPlace
        {
            // Put the current value of the variable onto the stack
            Visit(new Grammar.AST.Expressions.Variable(expr.Name));

            // If we need the old value save it now
            if (!preOp)
            {
                _emitter.Duplicate();
                _types.Push(_types.Peek);
            }

            // If there is a bool on the stack coerce it to a number (bool won't have inc/dec operators defined on it)
            if (StackType.Bool == _types.Peek)
                _types.Coerce(StackType.YololNumber);

            // Find the operator method
            var peek = _types.Peek.ToType();
            var method = peek.GetMethod("op_Increment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { peek }, null);
            if (method == null)
                throw new InvalidOperationException($"Cannot find method `op_Increment` on type {peek}");

            // Do the actual work
            _emitter.Call(method);
            _types.Pop(_types.Peek);
            _types.Push(method.ReturnType.ToStackType());
            
            // If we need to return the new value, save it now by duplicating it
            if (preOp)
            {
                _emitter.Duplicate();
                _types.Push(_types.Peek);
            }

            // Write value to variable
            _memory.Store(expr.Name, _types);

            return expr;
        }

        protected override BaseExpression Visit(PreIncrement inc) => Increment(inc, true);

        protected override BaseExpression Visit(PreDecrement dec) => Decrement(dec, true);

        protected override BaseExpression Visit(PostIncrement inc) => Increment(inc, false);

        protected override BaseExpression Visit(PostDecrement dec) => Decrement(dec, false);
        #endregion

        protected override BaseExpression Visit(Bracketed brk)
            => Visit(brk.Parameter);

        protected override BaseStatement Visit(ExpressionWrapper expr)
        {
            bool Inc(BaseIncrement inc)
            {
                // Put the value to increment on the stack
                Visit(new Grammar.AST.Expressions.Variable(inc.Name));

                // Find the increment method on whatever type it is
                var peek = _types.Peek.ToType();
                var method = peek.GetMethod("op_Increment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { peek }, null);
                if (method == null)
                    return false;

                // Do the work
                _emitter.Call(method);
                _types.Pop(_types.Peek);
                _types.Push(method!.ReturnType.ToStackType());

                // Save the result
                _memory.Store(inc.Name, _types);

                return true;
            }

            // Special case increment here. Since we know the return value isn't going to be used it doesn't matter if it's a pre/post inc
            // and all of the work duplicating values and shuffling the stack to ensure the right value is returned doesn't matter. Decrement
            // could also be special cased like this in principle, but it's quite a lot more complex due to error handling.
            switch (expr.Expression)
            {
                case BaseIncrement inc:
                    // If special case failed, fall back to default handling
                    if (!Inc(inc))
                        goto default;
                    return expr;

                default:
                    var r = base.Visit(expr);

                    // The wrapped expression left a value on the stack. Pop it off now.
                    _emitter.Pop();
                    _types.Pop(_types.Peek);

                    return r;
            }
        }
    }
}
