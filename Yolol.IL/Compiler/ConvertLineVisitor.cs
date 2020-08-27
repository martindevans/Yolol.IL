using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Sigil;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;
using Yolol.IL.Extensions;
using Type = System.Type;

namespace Yolol.IL.Compiler
{
    internal class ConvertLineVisitor<TEmit>
        : BaseTreeVisitor
    {
        private readonly Emit<TEmit> _emitter;

        private readonly int _maxLineNumber;
        private readonly Dictionary<string, int> _internalVariableMap;
        private readonly Dictionary<string, int> _externalVariableMap;
        private readonly Label _gotoLabel;
        private readonly Local _internalSpanLocal;
        private readonly Local _externalSpanLocal;
        private readonly IReadOnlyDictionary<VariableName, Execution.Type> _staticTypes;
        private readonly Stack<StackType> _types = new Stack<StackType>();

        public ConvertLineVisitor(
            Emit<TEmit> emitter,
            int maxLineNumber, Dictionary<string, int> internalVariableMap,
            Dictionary<string, int> externalVariableMap,
            Label gotoLabel,
            IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes,
            Local internalSpanLocal,
            Local externalSpanLocal
        )
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _internalVariableMap = internalVariableMap;
            _externalVariableMap = externalVariableMap;
            _gotoLabel = gotoLabel;
            _internalSpanLocal = internalSpanLocal;
            _externalSpanLocal = externalSpanLocal;
            _staticTypes = staticTypes ?? new Dictionary<VariableName, Execution.Type>();
        }

        #region runtime
        private void CallRuntimeN<T>(string methodName, params Type[] args)
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, args, null);
            _emitter.Call(method);
        }

        /// <summary>
        /// Call a runtime method with one parameter (type determined by type stack)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private Type CallRuntime1<T>(string methodName)
        {
            // Get the parameter type
            var p = Peek();

            var method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { p.ToType() }, null);
            _emitter.Call(method);

            return method!.ReturnType;
        }

        /// <summary>
        /// Call a runtime method with two parameters (types determined by type stack)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodName"></param>
        /// <returns></returns>
        private void CallRuntime2<T>(string methodName)
        {
            // Get the left and right items from the type stack
            var r = Peek();
            Pop(r);
            var l = Peek();
            Push(r);

            var method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { l.ToType(), r.ToType() }, null);
            _emitter.Call(method);
        }

        private void CallRuntimeThis0<T>(string methodName)
        {
            using (var local = _emitter.DeclareLocal(typeof(T)))
            {
                _emitter.StoreLocal(local);
                _emitter.LoadLocalAddress(local);
                CallRuntimeN<T>(methodName);
            }
        }

#if RELEASE
        private void GetRuntimeFieldValue<T>(string fieldName)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            
            using (var local = _emitter.DeclareLocal(typeof(T)))
            {
                _emitter.StoreLocal(local);
                _emitter.LoadLocalAddress(local);
                _emitter.LoadField(field);
            }
        }
#else
        private void GetRuntimePropertyValue<T>(string propertyName)
        {
            var method = typeof(T).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static).GetMethod;

            using (var local = _emitter.DeclareLocal(typeof(T)))
            {
                _emitter.StoreLocal(local);
                _emitter.LoadLocalAddress(local);
                _emitter.Call(method);
            }
        }
#endif
        #endregion

        #region type tracking
        public bool IsTypeStackEmpty => _types.Count == 0;

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

        private void Coerce(StackType target)
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
                    CallRuntimeN<Runtime>(nameof(Runtime.ErrorToValue), typeof(StaticError));
                    break;
                #endregion

                #region bool source
                case (StackType.Bool, StackType.YololValue):
                    _emitter.NewObject<Value, bool>();
                    break;

                case (StackType.Bool, StackType.YololNumber):
                    CallRuntimeN<Runtime>(nameof(Runtime.BoolToNumber), typeof(bool));
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
                    CallRuntimeN<Runtime>(nameof(Runtime.NumberToBool), typeof(Number));
                    break;

                case (StackType.YololNumber, StackType.YololString):
                    CallRuntimeThis0<Number>(nameof(Number.ToString));
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
        #endregion

        #region statements
        private void EmitAssign(VariableName name)
        {
            // Coerce whatever is on the stack into a `Value`
            Coerce(StackType.YololValue);
            Pop(StackType.YololValue);

            // Load the correct memory span for whichever type of variable we're accessing
            if (name.IsExternal)
                _emitter.LoadLocal(_externalSpanLocal);
            else
                _emitter.LoadLocal(_internalSpanLocal);

            // Lookup the index for the given name
            var map = (name.IsExternal ? _externalVariableMap : _internalVariableMap);
            if (!map.TryGetValue(name.Name, out var idx))
            {
                idx = map.Count;
                map[name.Name] = idx;
            }
            _emitter.LoadConstant(idx);

            // Put the value on the stack into the span
            CallRuntimeN<Runtime>(nameof(Runtime.SetSpanIndex), typeof(Value), typeof(Span<Value>), typeof(int));
        }

        protected override BaseStatement Visit(Assignment ass)
        {
            // Place the value to put into this variable on the stack
            Visit(ass.Right);

            // Emit code to assign the value on the stack to the variable
            EmitAssign(ass.Left);

            return ass;
        }

        protected override BaseStatement Visit(If @if)
        {
            // Create labels for control flow like:
            //
            //     entry point
            //     branch_to trueLabel or falseLabel
            //     trueLabel:
            //         true branch code
            //         jmp exitLabel
            //     falseLabel:
            //         false branch code
            //         jmp exitlabel
            //     exitlabel:
            //
            var trueLabel = _emitter.DefineLabel();
            var falseLabel = _emitter.DefineLabel();
            var exitLabel = _emitter.DefineLabel();

            // Visit conditional which places a value on the stack
            Visit(@if.Condition);

            // Convert it to a bool we can branch on
            Coerce(StackType.Bool);
            Pop(StackType.Bool);

            // jump to false branch if the condition is false. Fall through to true branch
            _emitter.BranchIfFalse(falseLabel);

            // Emit true branch
            _emitter.MarkLabel(trueLabel);
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
            // Put destination on the stack
            Visit(@goto.Destination);

            switch (Peek())
            {
                case StackType.Bool:
                case StackType.YololNumber:
                    Coerce(StackType.YololNumber);
                    Pop(StackType.YololNumber);
                    _emitter.LoadConstant(_maxLineNumber);
                    CallRuntimeN<Runtime>(nameof(Runtime.GotoNumber), typeof(Number), typeof(int));
                    break;

                case StackType.YololValue:
                case StackType.YololString:
                    Coerce(StackType.YololValue);
                    Pop(StackType.YololValue);
                    _emitter.LoadConstant(_maxLineNumber);
                    CallRuntimeN<Runtime>(nameof(Runtime.GotoValue), typeof(Value), typeof(int));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
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

            // Coercing it to a value will throw the execution exception
            if (Peek() == StackType.StaticError)
                Coerce(StackType.YololValue);

            return result;
        }

        protected override BaseExpression Visit(ConstantNumber con)
        {
            // Reflect out the raw int64 value
            var rawValueField = typeof(Number).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
            var rawValue = (long)rawValueField!.GetValue(con.Value);

            // if the raw value represents one of the two boolean values, emit a bool
            if (rawValue == 0 || rawValue == 1000)
            {
                _emitter.LoadConstant(rawValue == 1000);
                Push(StackType.Bool);
            }
            else
            {
                // Convert raw value into a number
                _emitter.LoadConstant(rawValue);
                _emitter.NewObject<Number, long>();
                Push(StackType.YololNumber);
            }


            return con;
        }

        protected override BaseExpression Visit(ConstantString str)
        {
            // Put a string on the stack
            _emitter.LoadConstant(str.Value.ToString());
            _emitter.NewObject<YString, string>();
            Push(StackType.YololString);

            return str;
        }

        protected override BaseExpression Visit(Grammar.AST.Expressions.Variable var)
        {
            // Load the correct memory span for whichever type of variable we're accessing
            if (var.Name.IsExternal)
                _emitter.LoadLocal(_externalSpanLocal);
            else
                _emitter.LoadLocal(_internalSpanLocal);

            // Lookup the index for the given name
            var map = (var.Name.IsExternal ? _externalVariableMap : _internalVariableMap);
            if (!map.TryGetValue(var.Name.Name, out var idx))
            {
                idx = map.Count;
                map[var.Name.Name] = idx;
            }
            _emitter.LoadConstant(idx);

            // Get the value from the span
            CallRuntimeN<Runtime>(nameof(Runtime.GetSpanIndex), typeof(Span<Value>), typeof(int));

            // Check if we statically know the type
            if (_staticTypes.TryGetValue(var.Name, out var type))
            {
                if (type == Execution.Type.Number)
                {
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the field (which will throw if your type annotations are wrong)
                    #if RELEASE
                        GetRuntimeFieldValue<Value>("_number");
                    #else
                        GetRuntimePropertyValue<Value>(nameof(Value.Number));
                    #endif

                    Push(StackType.YololNumber);
                    return var;
                }

                if (type == Execution.Type.String)
                {
                    // If this is a release build directly access the underlying field value, avoiding the dynamic type check. If it's
                    // a debug build load it through the field (which will throw if your type annotations are wrong)
                    #if RELEASE
                        GetRuntimeFieldValue<Value>("_string");
                    #else
                        GetRuntimePropertyValue<Value>(nameof(Value.String));
                    #endif

                    Push(StackType.YololString);
                    return var;
                }
            }

            // Didn't statically know the type, just emit a `Value`
            Push(StackType.YololValue);
            return var;
        }

        private bool TryStaticEvaluate<T>(T expr)
            where T : BaseExpression
        {
#if !DEBUG
            if (expr.IsConstant)
            {
                var v = Execution.Extensions.BaseExpressionExtensions.TryStaticEvaluate(expr);
                if (v.HasValue)
                {
                    Visit(v.Value.ToConstant());
                    return true;
                }
            }
#endif

            return false;
        }
        #endregion

        #region binary expressions
        private T ConvertBinaryCoerce<T>(T expr, StackType input, StackType output, Action emit)
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr))
                return expr;

            // Visit the inner expression of the unary and coerce it to the correct type
            // Visit left and right, coercing them into `Value`s
            Visit(expr.Left);
            Coerce(input);
            Visit(expr.Right);
            Coerce(input);

            // Emit IL
            emit();

            // Update types
            Pop(input);
            Pop(input);
            Push(output);

            return expr;
        }

        // ReSharper disable InconsistentNaming
        private T ConvertBinaryExpr<T, TBB, TBN, TBS, TBV, TNB, TNN, TNS, TNV, TSB, TSN, TSS, TSV, TVB, TVN, TVS, TVV>(T expr,
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
            if (TryStaticEvaluate(expr))
                return expr;

            // Convert the two sides of the expression
            Visit(expr.Left);
            var left = Peek();
            Visit(expr.Right);
            var right = Peek();

            // Emit code
            var emitType = (left, right) switch {

                (StackType.Bool, StackType.Bool) => emitBoolBool.ConvertBinary(_emitter),
                (StackType.Bool, StackType.YololNumber) => emitBoolNum.ConvertBinary(_emitter),
                (StackType.Bool, StackType.YololString) => emitBoolStr.ConvertBinary(_emitter),
                (StackType.Bool, StackType.YololValue) => emitBoolVal.ConvertBinary(_emitter),

                (StackType.YololNumber, StackType.Bool) => emitNumBool.ConvertBinary(_emitter),
                (StackType.YololNumber, StackType.YololNumber) => emitNumNum.ConvertBinary(_emitter),
                (StackType.YololNumber, StackType.YololString) => emitNumStr.ConvertBinary(_emitter),
                (StackType.YololNumber, StackType.YololValue) => emitNumVal.ConvertBinary(_emitter),

                (StackType.YololString, StackType.Bool) => emitStrBool.ConvertBinary(_emitter),
                (StackType.YololString, StackType.YololNumber) => emitStNum.ConvertBinary(_emitter),
                (StackType.YololString, StackType.YololString) => emitStStr.ConvertBinary(_emitter),
                (StackType.YololString, StackType.YololValue) => emitStVal.ConvertBinary(_emitter),

                (StackType.YololValue, StackType.Bool) => emitValBool.ConvertBinary(_emitter),
                (StackType.YololValue, StackType.YololNumber) => emitValNum.ConvertBinary(_emitter),
                (StackType.YololValue, StackType.YololString) => emitValStr.ConvertBinary(_emitter),
                (StackType.YololValue, StackType.YololValue) => emitValVal.ConvertBinary(_emitter),

                _ => throw new InvalidOperationException($"{expr.GetType().Name}({left},{right})")
            };
            var result = emitType!.ToStackType();

            Pop(right);
            Pop(left);
            Push(result);

            return expr;
        }

        protected override BaseExpression Visit(Add add) => ConvertBinaryExpr(add,
            (a, b) => (Number)a + b,
            (a, b) => a + b,
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

        protected override BaseExpression Visit(Subtract sub) => ConvertBinaryExpr(sub,
            (a, b) => (Number)a - b,
            (a, b) => a - b,
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

        protected override BaseExpression Visit(Multiply mul) => ConvertBinaryExpr(mul,
            (a, b) => (Number)a * b,
            (a, b) => a * b,
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
            (a, b) => a / b,
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
            (a, b) => a == b,
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
            (a, b) => a != b,
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
            (a, b) => a > b,
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
            (a, b) => a >= b,
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
            (a, b) => a < b,
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
            (a, b) => a <= b,
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
            (a, b) => a % b,
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
        private T ConvertUnaryExpr<T, TB, TN, TS, TV>(T expr, Expression<Func<bool, TB>> emitBool, Expression<Func<Number, TN>> emitNum, Expression<Func<YString, TS>> emitStr, Expression<Func<Value, TV>> emitVal)
            where T : BaseUnaryExpression
        {
            if (TryStaticEvaluate(expr))
                return expr;

            // Visit the inner expression of the unary
            Visit(expr.Parameter);

            // Emit code
            var p = Peek();
            var emitType = p switch {
                StackType.Bool => emitBool.ConvertUnary(_emitter),
                StackType.YololNumber => emitNum.ConvertUnary(_emitter),
                StackType.YololString => emitStr.ConvertUnary(_emitter),
                StackType.YololValue => emitVal.ConvertUnary(_emitter),
                _ => throw new InvalidOperationException($"{expr.GetType().Name}({p})")
            };
            var result = emitType!.ToStackType();

            // Update types
            Pop(Peek());
            Push(result);

            return expr;
        }

        protected override BaseExpression Visit(Not not) => ConvertUnaryExpr(not,
            b => !b,
            n => n == 0,
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
        private T Modify<T>(T expr, bool preOp, Func<StackType>? boolEmit, Func<StackType>? numEmit, Func<StackType>? strEmit, Func<StackType>? valEmit)
            where T : BaseModifyInPlace
        {
            // Put the current value of the variable onto the stack
            Visit(new Grammar.AST.Expressions.Variable(expr.Name));

            // If we need the old value save it now
            if (!preOp)
            {
                _emitter.Duplicate();
                Push(Peek());
            }

            // emit the IL for the operation
            // Replace all null emitters by falling back to a more general case
            if (valEmit == null)
                throw new InvalidOperationException("Must specify `YololValue` handler");
            if (numEmit == null)
            {
                numEmit = () => {
                    Coerce(StackType.YololValue);
                    return valEmit();
                };
            }
            if (strEmit == null)
            {
                strEmit = () => {
                    Coerce(StackType.YololValue);
                    return valEmit();
                };
            }
            if (boolEmit == null)
            {
                boolEmit = () => {
                    Coerce(StackType.YololNumber);
                    return numEmit();
                };
            }

            // Invoke the emitters for the type on the stack
            var peek = Peek();
            var result = peek switch {
                StackType.YololNumber => numEmit(),
                StackType.YololValue => valEmit(),
                StackType.YololString => strEmit(),
                StackType.Bool => boolEmit(),
                _ => throw new ArgumentOutOfRangeException($"Unknown StackType.{peek}")
            };
            Pop(peek);
            Push(result);

            // If we need to return the new value, save it now by duplicating it
            if (preOp)
            {
                _emitter.Duplicate();
                Push(Peek());
            }

            // Write value to variable
            EmitAssign(expr.Name);

            return expr;
        }

        protected override BaseExpression Visit(PreIncrement inc) => Modify(inc, true,
            null,
            () => {
                _emitter.LoadConstant(1000L);
                _emitter.NewObject<Number, long>();
                CallRuntime2<Number>("op_Addition");
                return StackType.YololNumber;
            },
            () => {
                CallRuntime1<YString>("op_Increment");
                return StackType.YololString;
            },
            () => CallRuntime1<Value>("op_Increment").ToStackType()
        );

        protected override BaseExpression Visit(PreDecrement inc) => Modify(inc, true,
            null,
            () => {
                _emitter.LoadConstant(-1000L);
                _emitter.NewObject<Number, long>();
                CallRuntime2<Number>("op_Addition");
                return StackType.YololNumber;
            },
            () => {
                CallRuntime1<YString>("op_Decrement");
                return StackType.YololString;
            },
            () => CallRuntime1<Value>("op_Decrement").ToStackType()
        );

        protected override BaseExpression Visit(PostIncrement inc) => Modify(inc, false,
            null,
            () => {
                _emitter.LoadConstant(1000L);
                _emitter.NewObject<Number, long>();
                CallRuntime2<Number>("op_Addition");
                return StackType.YololNumber;
            },
            () => {
                CallRuntime1<YString>("op_Increment");
                return StackType.YololString;
            },
            () => CallRuntime1<Value>("op_Increment").ToStackType()
        );

        protected override BaseExpression Visit(PostDecrement inc) => Modify(inc, false,
            null,
            () => {
                _emitter.LoadConstant(-1000L);
                _emitter.NewObject<Number, long>();
                CallRuntime2<Number>("op_Addition");
                return StackType.YololNumber;
            },
            () => {
                CallRuntime1<YString>("op_Decrement");
                return StackType.YololString;
            },
            () => CallRuntime1<Value>("op_Decrement").ToStackType()
        );
        #endregion

        protected override BaseStatement Visit(ExpressionWrapper expr)
        {
            var r = base.Visit(expr);

            // The wrapped expression left a value on the stack. Pop it off now.
            _emitter.Pop();
            _types.Pop();

            return r;
        }

        protected override BaseExpression Visit(Bracketed brk)
            => Visit(brk.Parameter);
    }
}
