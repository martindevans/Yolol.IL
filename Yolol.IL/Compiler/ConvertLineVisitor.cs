using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Yolol.Analysis.ControlFlowGraph.AST;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;
using Yolol.IL.Compiler.Emitter;
using Yolol.IL.Compiler.Emitter.Instructions;
using Yolol.IL.Compiler.Memory;
using Yolol.IL.Extensions;

namespace Yolol.IL.Compiler
{
    internal class ConvertLineVisitor<TEmit>
        : BaseTreeVisitor
    {
        private readonly OptimisingEmitter<TEmit> _emitter;

        private readonly int _maxLineNumber;
        private readonly IMemoryAccessor<TEmit> _memory;
        private readonly ExceptionBlock _unwinder;
        private readonly Label2<TEmit> _gotoLabel;

        private readonly TypeStack<TEmit> _typesStack;
        private readonly IStaticTypeTracker _staticTypes;

        private readonly int? _maybeMaxStringLength;
        private readonly int _maxStringLength;

        public ConvertLineVisitor(
            OptimisingEmitter<TEmit> emitter,
            int maxLineNumber,
            IMemoryAccessor<TEmit> memory,
            ExceptionBlock unwinder,
            Label2<TEmit> gotoLabel,
            IStaticTypeTracker staticTypes,
            int? maxStringLength
        )
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _memory = memory;
            _unwinder = unwinder;
            _gotoLabel = gotoLabel;
            _staticTypes = staticTypes;

            _maybeMaxStringLength = maxStringLength;
            _maxStringLength = maxStringLength ?? int.MaxValue;

            _typesStack = new TypeStack<TEmit>(_emitter);
        }

        #region statements
        protected override BaseStatement Visit(Assignment ass)
        {
            // Place the value to put into this variable on the stack
            if (Visit(ass.Right) is ErrorExpression)
                return ass;

            // Emit code to assign the value on the stack to the variable
            _memory.Store(ass.Left, _typesStack);

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
            _typesStack.Coerce(StackType.Bool);
            _typesStack.Pop(StackType.Bool);

            // jump to false branch if the condition is false. Fall through to true branch
            _emitter.BranchIfFalse(falseLabel);

            // Emit true branch
            ITypeContext trueCtx;
            using (trueCtx = _staticTypes.EnterContext())
            {
                Visit(@if.TrueBranch);
                _emitter.Branch(exitLabel);
            }

            // Emit false branch
            ITypeContext falseCtx;
            using (falseCtx = _staticTypes.EnterContext())
            {
                _emitter.MarkLabel(falseLabel);
                Visit(@if.FalseBranch);
                _emitter.Branch(exitLabel);
            }

            // Update types to the common types of both branches
            _staticTypes.Unify(trueCtx, falseCtx);

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

                switch (_typesStack.Peek)
                {
                    case StackType.Bool:
                        // `Goto1` and `Goto0` both go to line 1
                        _typesStack.Pop(StackType.Bool);
                        _emitter.Pop();
                        _emitter.LoadConstant(1);
                        break;

                    case StackType.YololNumber:
                        _typesStack.Coerce(StackType.YololNumber);
                        _typesStack.Pop(StackType.YololNumber);
                        _emitter.LoadConstant(_maxLineNumber);
                        _emitter.CallRuntimeN(nameof(Runtime.GotoNumber), typeof(Number), typeof(int));
                        break;

                    case StackType.YololString:
                        _emitter.Leave(_unwinder);
                        _emitter.MarkLabel(_emitter.DefineLabel());
                        break;

                    case StackType.YololValue:
                        _typesStack.Coerce(StackType.YololValue);
                        _emitter.Duplicate();
                        _emitter.CallRuntimeN(nameof(Runtime.IsValueANumber), typeof(Value));
                        _emitter.LeaveIfFalse(_unwinder);
                        _emitter.LoadConstant(_maxLineNumber);
                        _emitter.CallRuntimeN(nameof(Runtime.GotoValue), typeof(Value), typeof(int));
                        break;

                    default:
                        throw new InvalidOperationException($"Unknown type on stack: `{_typesStack.Peek}`");
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
            if (_typesStack.Peek == StackType.StaticError || result is ErrorExpression)
            {
                _emitter.Leave(_unwinder);

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
                _typesStack.Push(StackType.Bool);
            }
            else
            {
                // Convert raw value into a number
                _emitter.LoadConstant(rawValue);
                _emitter.NewObject<Number, long>();
                _typesStack.Push(StackType.YololNumber);
            }

            return con;
        }

        protected override BaseExpression Visit(ConstantString str)
        {
            // Ensure the constant is not over the max length
            var trimmed = str.Value;
            if (_maybeMaxStringLength.HasValue)
                trimmed = YString.Trim(trimmed, _maybeMaxStringLength.Value);

            // Put a string on the stack
            _emitter.LoadConstant(trimmed.ToString());
            _emitter.NewObject<YString, string>();
            _typesStack.Push(StackType.YololString);

            return str;
        }

        protected override BaseExpression Visit(Grammar.AST.Expressions.Variable var)
        {
            _memory.Load(var.Name, _typesStack);
            return var;
        }

        private bool TryStaticEvaluate<T>(T expr, out bool runtimeError)
            where T : BaseExpression
        {
            runtimeError = false;
            if (expr.IsConstant)
            {
                var v = Execution.Extensions.BaseExpressionExtensions.TryStaticEvaluate(expr, out runtimeError);
                if (v.HasValue)
                {
                    Visit(v.Value.ToConstant());
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region binary expressions
        private BaseExpression ConvertBinaryCoerce<T>(T expr, StackType input, StackType output, Action emit)
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : expr;

            // Visit the inner expression of the unary and coerce it to the correct type
            // Visit left and right, coercing them into `Value`s
            if (Visit(expr.Left) is ErrorExpression)
                return new ErrorExpression();
            _typesStack.Coerce(input);
            if (Visit(expr.Right) is ErrorExpression)
                return new ErrorExpression();
            _typesStack.Coerce(input);

            // Emit IL
            emit();

            // Update types
            _typesStack.Pop(input);
            _typesStack.Pop(input);
            _typesStack.Push(output);

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
            Expression<Func<Value, Value, TVV>> emitValVal,
            bool trimString = false
        )
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : (BaseExpression)expr;

            // Convert the two sides of the expression
            if (Visit(expr.Left) is ErrorExpression)
                return new ErrorExpression();
            var leftType = _typesStack.Peek;
            var leftDepth = _typesStack.Count;

            if (Visit(expr.Right) is ErrorExpression)
                return new ErrorExpression();
            var rightType = _typesStack.Peek;
            var rightDepth = _typesStack.Count;

            ThrowHelper.Check(rightDepth == leftDepth + 1, "Unbalanced type stack");

            // Try to calculate static values for the two sides
            var constLeft = expr.Left.IsConstant ? Execution.Extensions.BaseExpressionExtensions.TryStaticEvaluate(expr.Left, out _) : null;
            var constRight = expr.Right.IsConstant ? Execution.Extensions.BaseExpressionExtensions.TryStaticEvaluate(expr.Right, out _) : null;

            // Emit code
            _typesStack.Pop(rightType);
            _typesStack.Pop(leftType);
            var convert = (left: leftType, right: rightType) switch {

                (StackType.Bool, StackType.Bool) => emitBoolBool.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.Bool, StackType.YololNumber) => emitBoolNum.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.Bool, StackType.YololString) => emitBoolStr.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.Bool, StackType.YololValue) => emitBoolVal.ConvertBinary(_emitter, _unwinder, constLeft, constRight),

                (StackType.YololNumber, StackType.Bool) => emitNumBool.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololNumber, StackType.YololNumber) => emitNumNum.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololNumber, StackType.YololString) => emitNumStr.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololNumber, StackType.YololValue) => emitNumVal.ConvertBinary(_emitter, _unwinder, constLeft, constRight),

                (StackType.YololString, StackType.Bool) => emitStrBool.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololString, StackType.YololNumber) => emitStNum.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololString, StackType.YololString) => emitStStr.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololString, StackType.YololValue) => emitStVal.ConvertBinary(_emitter, _unwinder, constLeft, constRight),

                (StackType.YololValue, StackType.Bool) => emitValBool.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololValue, StackType.YololNumber) => emitValNum.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololValue, StackType.YololString) => emitValStr.ConvertBinary(_emitter, _unwinder, constLeft, constRight),
                (StackType.YololValue, StackType.YololValue) => emitValVal.ConvertBinary(_emitter, _unwinder, constLeft, constRight),

                _ => throw new InvalidOperationException($"{expr.GetType().Name}({leftType},{rightType})")
            };
            _typesStack.Push(convert.OnStack.ToStackType());

            // Store discovered types for any parameters passed into the expression
            if (convert.Implications != null)
            {
                if (expr.Left is Grammar.AST.Expressions.Variable vl)
                    _staticTypes.Store(vl.Name, convert.Implications[0]);

                if (expr.Right is Grammar.AST.Expressions.Variable vr)
                    _staticTypes.Store(vr.Name, convert.Implications[1]);
            }

            // Ensure max string length is not exceeded
            if (trimString)
                CheckStringLength();

            return expr;
        }

        private void CheckStringLength()
        {
            if (_maybeMaxStringLength.HasValue)
            {
                if (_typesStack.Peek == StackType.YololString)
                {
                    _emitter.LoadConstant(_maybeMaxStringLength.Value);
                    _emitter.CallRuntimeN<TEmit, YString>(nameof(YString.Trim), typeof(YString), typeof(int));
                }
                else if (_typesStack.Peek == StackType.YololValue)
                {
                    _emitter.LoadConstant(_maybeMaxStringLength.Value);
                    _emitter.CallRuntimeN(nameof(Runtime.TrimValue), typeof(Value), typeof(int));
                }
            }
        }

        protected override BaseExpression Visit(Add add)
        {
            return ConvertBinaryExpr(add,
                (a, b) => Runtime.BoolAdd(a, b),
                (a, b) => Runtime.BoolAdd(a, b),
                (a, b) => Runtime.BoolAdd(a, b),
                (a, b) => Runtime.BoolAdd(a, b),
                (a, b) => a + b,
                (a, b) => a + b,
                (a, b) => YString.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => Value.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => YString.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => YString.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => YString.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => YString.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => Value.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => Value.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => Value.Add(a, b, new ConstantInt32(_maxStringLength)),
                (a, b) => Value.Add(a, b, new ConstantInt32(_maxStringLength)),
                trimString: true
            );
        }

        protected override BaseExpression Visit(Subtract sub)
        {
            // Special case for `a-a--`
            if (sub.Left is Grammar.AST.Expressions.Variable var && sub.Right is PostDecrement pd && pd.Name.Equals(var.Name))
                return PopCharacter(var);

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
        /// Special case handling for `a-a--` as popping a character from a string
        /// </summary>
        /// <param name="variable"></param>
        /// <returns></returns>
        private BaseExpression PopCharacter(Grammar.AST.Expressions.Variable variable) => EmitUnaryExpr(new Bracketed(variable), errorLabel =>
        {
            // Value of variable is on stack, it's a number
            // Given: `a=11 b=a-a--`
            // Evaluate to:
            //   a--
            //   result=1
            void EmitNum()
            {
                _emitter.Pop();

                // a--
                Visit(new PostDecrement(variable.Name));
                _typesStack.Pop(_typesStack.Peek);
                _emitter.Pop();

                // result=1 (left on stack)
                Visit(new ConstantNumber(Number.One));
            }

            // Value of variable is on stack, it's a string
            // Given: `a="abc" b=a-a--`
            // Evaluate to:
            //   a = "ab"
            //   b = "c"
            void EmitStr()
            {
                Expression<Func<YString, YString>> expr = str => str.LastCharacter();
                var result = expr.ConvertUnary(_emitter, errorLabel);
                _typesStack.Push(result.OnStack.ToStackType());

                Visit(new PostDecrement(variable.Name));
                _emitter.Pop();
            }

            void EmitVal()
            {
                // Set up locals to store results
                // we're calculating:
                //   a=value_on_stack
                //   b=last_char(a)
                //   a--
                using (var valueA = _emitter.DeclareLocal<Value>(initializeReused: false))
                using (var valueB = _emitter.DeclareLocal<Value>(initializeReused: false))
                {
                    // Store current value in A
                    _emitter.StoreLocal(valueA);

                    // Do the calculation
                    _emitter.LoadLocalAddress(valueA, false);
                    _emitter.LoadLocalAddress(valueB, false);
                    _emitter.CallRuntimeN(nameof(Runtime.TryPopValue), new[] { typeof(Value).MakeByRefType(), typeof(Value).MakeByRefType() });

                    // Exit line if error
                    _emitter.LeaveIfTrue(errorLabel);

                    // Store the modified value
                    _emitter.LoadLocal(valueA);
                    _typesStack.Push(StackType.YololValue);
                    _memory.Store(variable.Name, _typesStack);

                    // Leave the result on the stack
                    _emitter.LoadLocal(valueB);
                    _typesStack.Push(StackType.YololValue);
                }
            }

            // Discover what type we're working with
            var p = _typesStack.Peek;
            _typesStack.Pop(p);
            switch (p)
            {
                case StackType.Bool:
                    _emitter.EmitCoerce(StackType.Bool, StackType.YololNumber);
                    EmitNum();
                    break;

                case StackType.YololNumber:
                    EmitNum();
                    break;

                case StackType.YololString:
                    EmitStr();
                    break;

                case StackType.YololValue:
                    EmitVal();
                    break;

                default:
                    throw new InvalidProgramException($"Cannot triple subtract `StackType.{p}`");
            }
        });

        protected override BaseExpression Visit(Multiply mul) => ConvertBinaryExpr(mul,
            (a, b) => Runtime.And(a, b),
            (a, b) => Runtime.BoolMul(a, b),
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
            (a, b) => a & !b,
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
            (a, b) => a % b,
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
        private BaseExpression EmitUnaryExpr<T>(T expr, Action<ExceptionBlock> emit)
            where T : BaseUnaryExpression
        {
             if (TryStaticEvaluate(expr, out var runtimeError))
                return runtimeError ? new ErrorExpression() : (BaseExpression)expr;

             // Visit the inner expression of the unary
             if (Visit(expr.Parameter) is ErrorExpression)
                 return new ErrorExpression();

             // Emit code
             emit(_unwinder);

             return expr;
        }

        private BaseExpression ConvertUnaryExpr<T, TB, TN, TS, TV>(T expr, Expression<Func<bool, TB>> emitBool, Expression<Func<Number, TN>> emitNum, Expression<Func<YString, TS>> emitStr, Expression<Func<Value, TV>> emitVal)
            where T : BaseUnaryExpression
        {
            return EmitUnaryExpr(expr, errorLabel => {
                var p = _typesStack.Peek;
                _typesStack.Pop(p);
                var convert = p switch {
                    StackType.Bool => emitBool.ConvertUnary(_emitter, errorLabel),
                    StackType.YololNumber => emitNum.ConvertUnary(_emitter, errorLabel),
                    StackType.YololString => emitStr.ConvertUnary(_emitter, errorLabel),
                    StackType.YololValue => emitVal.ConvertUnary(_emitter, errorLabel),
                    _ => throw new InvalidOperationException($"{expr.GetType().Name}({p})")
                };
                _typesStack.Push(convert.OnStack.ToStackType());

                // Store discovered types for parameter passed into the expression
                if (convert.Implications != null)
                {
                    if (expr.Parameter is Grammar.AST.Expressions.Variable vl)
                        _staticTypes.Store(vl.Name, convert.Implications[0]);
                }
            });
        }

        protected override BaseExpression Visit(Factorial fac) => ConvertUnaryExpr(fac,
            b => (Number)1,
            n => n.Factorial(),
            s => new StaticError("Attempted to Factorial a string value"),
            v => Value.Factorial(v)
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
            n => Runtime.Abs(n),
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
                _typesStack.Push(_typesStack.Peek);
            }

            // If there is a bool on the stack coerce it to a number (bool won't have inc/dec operators defined on it)
            if (StackType.Bool == _typesStack.Peek)
                _typesStack.Coerce(StackType.YololNumber);

            // Find the operator method
            var peek = _typesStack.Peek.ToType();
            var method = peek.GetMethod("op_Decrement", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { peek }, null);
            if (method == null)
                throw new InvalidOperationException($"Cannot find method `op_Decrement` on type {peek}");

            // Find the `will throw` method
            var errorMetadata = method.TryGetErrorMetadata(peek);

            // Emit code to check if the decrement will throw
            if (errorMetadata != null)
            {
                if (errorMetadata.Value.WillThrow == null)
                    throw new InvalidOperationException("null `WillThrow` method for decrement op");

                // Call the "will throw" method (consuming the duplicated value)
                _emitter.Duplicate();
                _emitter.Call(errorMetadata.Value.WillThrow);

                // Jump away to unwinder if this would throw
                _emitter.LeaveIfTrue(_unwinder);
            }

            // Do the actual work
            _emitter.Call(method);
            _typesStack.Pop(_typesStack.Peek);
            _typesStack.Push(method.ReturnType.ToStackType());
            

            // If we need to return the new value, save it now by duplicating it
            if (preOp)
            {
                _emitter.Duplicate();
                _typesStack.Push(_typesStack.Peek);
            }

            // Write value to variable
            _memory.Store(expr.Name, _typesStack);

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
                _typesStack.Push(_typesStack.Peek);
            }

            // If there is a bool on the stack coerce it to a number (bool won't have inc/dec operators defined on it)
            if (StackType.Bool == _typesStack.Peek)
                _typesStack.Coerce(StackType.YololNumber);

            // Find the operator method
            var peek = _typesStack.Peek.ToType();
            var method = peek.GetMethod("op_Increment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { peek }, null);
            if (method == null)
                throw new InvalidOperationException($"Cannot find method `op_Increment` on type {peek}");

            // Do the actual work
            _emitter.Call(method);
            _typesStack.Pop(_typesStack.Peek);
            _typesStack.Push(method.ReturnType.ToStackType());
            CheckStringLength();

            // If we need to return the new value, save it now by duplicating it
            if (preOp)
            {
                _emitter.Duplicate();
                _typesStack.Push(_typesStack.Peek);
            }

            // Write value to variable
            _memory.Store(expr.Name, _typesStack);

            return expr;
        }

        protected override BaseExpression Visit(PreIncrement inc) => Increment(inc, true);

        protected override BaseExpression Visit(PreDecrement dec) => Decrement(dec, true);

        protected override BaseExpression Visit(PostIncrement inc) => Increment(inc, true); // When pre/post inc is fixed, change this to false

        protected override BaseExpression Visit(PostDecrement dec) => Decrement(dec, true); // When pre/post dec is fixed, change this to false
        #endregion

        protected override BaseExpression Visit(Bracketed brk)
            => Visit(brk.Parameter);

        protected override BaseStatement Visit(ExpressionWrapper expr)
        {
            var r = base.Visit(expr);

            // The wrapped expression left a value on the stack. Pop it off now.
            _emitter.Pop();
            _typesStack.Pop(_typesStack.Peek);

            return r;
        }
    }
}
