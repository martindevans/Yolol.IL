using System;
using System.Collections.Generic;
using System.Reflection;
using Sigil;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Execution.Extensions;
using Yolol.Grammar;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL.Compiler
{
    internal class ConvertLineVisitor
        : BaseTreeVisitor
    {
        private readonly Emit<Func<Memory<Value>, Memory<Value>, int, int, int>> _emitter;

        private readonly int _maxLineNumber;
        private readonly Dictionary<string, int> _internalVariableMap;
        private readonly Dictionary<string, int> _externalVariableMap;
        private readonly Label _gotoLabel;
        private readonly IReadOnlyDictionary<VariableName, Execution.Type> _staticTypes;
        private readonly Stack<StackType> _types = new Stack<StackType>();

        public ConvertLineVisitor(
            Emit<Func<Memory<Value>, Memory<Value>, int, int, int>> emitter,
            int maxLineNumber, Dictionary<string, int> internalVariableMap,
            Dictionary<string, int> externalVariableMap,
            Label gotoLabel,
            IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes
        )
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _internalVariableMap = internalVariableMap;
            _externalVariableMap = externalVariableMap;
            _gotoLabel = gotoLabel;
            _staticTypes = staticTypes ?? new Dictionary<VariableName, Execution.Type>();
        }

        #region runtime
        private void CallRuntime<T>(string methodName)
        {
            var method = typeof(T).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            _emitter.Call(method);
        }

        private void CallRuntimeThis<T>(string methodName)
        {
            using (var local = _emitter.DeclareLocal(typeof(T)))
            {
                _emitter.StoreLocal(local);
                _emitter.LoadLocalAddress(local);
                CallRuntime<T>(methodName);
            }
        }

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
                    break;
                #endregion

                #region bool source
                case (StackType.Bool, StackType.YololValue):
                    _emitter.NewObject<Value, bool>();
                    break;

                case (StackType.Bool, StackType.YololNumber):
                    CallRuntime<Runtime>(nameof(Runtime.BoolToNumber));
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
                    _emitter.LoadConstant(0L);
                    _emitter.NewObject<Number, long>();
                    _emitter.Call(typeof(Number).GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static));
                    break;

                case (StackType.YololNumber, StackType.YololString):
                    CallRuntimeThis<Number>(nameof(Number.ToString));
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
                    throw new NotImplementedException($"Cannot coerce `{source}` -> `{target}`");
            }

            // If we get here type coercion succeeded
            Push(target);
        }

        private Local Stash(StackType type)
        {
            Pop(type);

            var local = type switch {
                StackType.YololNumber => _emitter.DeclareLocal(typeof(Number)),
                StackType.YololValue => _emitter.DeclareLocal(typeof(Value)),
                StackType.YololString => _emitter.DeclareLocal(typeof(YString)),
                StackType.Bool => _emitter.DeclareLocal(typeof(bool)),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
            _emitter.StoreLocal(local);

            return local;
        }

        private void Unstash(Local local, StackType type)
        {
            _emitter.LoadLocal(local);
            Push(type);
        }

        void CoerceOneDown(StackType output)
        {
            var top = Peek();
            using (var stash = Stash(top))
            {
                Coerce(output);
                Unstash(stash, top);
            }
        }
        #endregion

        #region type emitters
        private class Emitters1
        {
            public Func<StackType>? Number;
            public Func<StackType>? Value;
            public Func<StackType>? String;
            public Func<StackType>? Bool;

            public void Emit(ConvertLineVisitor converter)
            {
                // Replace all null emitters by falling back to a more general case
                if (Value == null)
                    throw new InvalidOperationException("Must specify `YololValue` handler");
                if (Number == null)
                {
                    Number = () => {
                        converter.Coerce(StackType.YololValue);
                        return Value();
                    };
                }
                if (String == null)
                {
                    String = () => {
                        converter.Coerce(StackType.YololValue);
                        return Value();
                    };
                }
                if (Bool == null)
                {
                    Bool = () => {
                        converter.Coerce(StackType.YololNumber);
                        return Number();
                    };
                }

                // Invoke the emitters for the type on the stack
                var peek = converter.Peek();
                var result = peek switch {
                    StackType.YololNumber => Number(),
                    StackType.YololValue => Value(),
                    StackType.YololString => String(),
                    StackType.Bool => Bool(),
                    _ => throw new ArgumentOutOfRangeException($"Unknown StackType.{peek}")
                };

                // Update the stack to reflect the values
                converter.Pop(converter.Peek());
                converter.Push(result);
            }
        }

        private class Emitters2
        {
            public Func<StackType>? Number;
            public Func<StackType>? Value;
            public Func<StackType>? String;
            public Func<StackType>? Bool;

            public void Emit(ConvertLineVisitor converter, BaseExpression expr)
            {
                // Replace all null emitters by falling back to a more general case
                if (Value == null)
                    throw new InvalidOperationException("Must specify `YololValue` handler");
                if (Number == null)
                {
                    Number = () => {
                        converter.CoerceOneDown(StackType.YololValue);
                        converter.Coerce(StackType.YololValue);
                        return Value();
                    };
                }
                if (String == null)
                {
                    String = () => {
                        converter.CoerceOneDown(StackType.YololValue);
                        converter.Coerce(StackType.YololValue);
                        return Value();
                    };
                }
                if (Bool == null)
                {
                    Bool = () => {
                        converter.CoerceOneDown(StackType.YololNumber);
                        converter.Coerce(StackType.YololNumber);
                        return Number();
                    };
                }

                // Get the types of the left and right
                var right = converter.Peek();
                converter.Pop(right);
                var left = converter.Peek();
                converter.Push(right);

                // Coerce both sides to the same type
                CoerceToPair(converter, ref left, ref right);

                // Invoke the emitters for the type on the stack
                var result = left switch {
                    StackType.YololNumber => Number(),
                    StackType.YololValue => Value(),
                    StackType.YololString => String(),
                    StackType.Bool => Bool(),
                    _ => throw new ArgumentOutOfRangeException($"Unknown StackType.{left}")
                };
                

                // Update the stack to reflect the values
                converter.Pop(right);
                converter.Pop(left);
                converter.Push(result);
            }

            private static void CoerceToPair(ConvertLineVisitor converter, ref StackType left, ref StackType right)
            {
                void CoerceLeft(StackType output, ref StackType left)
                {
                    converter.CoerceOneDown(output);
                    left = output;
                }

                void CoerceRight(StackType output, ref StackType right)
                {
                    converter.Coerce(output);
                    right = output;
                }

                // They might already be the same
                if (left == right)
                    return;

                // If either side is a value we can't statically determine anything useful, just cast the other side
                if (left == StackType.YololValue || right == StackType.YololValue)
                {
                    CoerceRight(StackType.YololValue, ref right);
                    CoerceLeft(StackType.YololValue, ref left);
                    return;
                }

                // If one side is a bool, try to convert it into a number
                if (left == StackType.Bool)
                    CoerceLeft(StackType.YololNumber, ref left);
                if (right == StackType.Bool)
                    CoerceRight(StackType.YololNumber, ref right);

                // Maybe that worked (num<=>num)
                if (left == right)
                    return;

                // Out of ideas, just use a pair of values
                CoerceRight(StackType.YololValue, ref right);
                CoerceLeft(StackType.YololValue, ref left);
            }
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
                _emitter.LoadArgument(1);
            else
                _emitter.LoadArgument(0);

            // Lookup the index for the given name
            var map = (name.IsExternal ? _externalVariableMap : _internalVariableMap);
            if (!map.TryGetValue(name.Name, out var idx))
            {
                idx = map.Count;
                map[name.Name] = idx;
            }
            _emitter.LoadConstant(idx);

            // Put the value on the stack into the span
            CallRuntime<Runtime>(nameof(Runtime.SetSpanIndex));
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

            if (Peek() == StackType.YololNumber)
            {
                Pop(StackType.YololNumber);
                _emitter.LoadConstant(_maxLineNumber);
                CallRuntime<Runtime>(nameof(Runtime.GotoNumber));
            }
            else
            {
                Coerce(StackType.YololValue);
                Pop(StackType.YololValue);
                _emitter.LoadConstant(_maxLineNumber);
                CallRuntime<Runtime>(nameof(Runtime.GotoValue));
            }

            // Jump to the `goto` label
            _emitter.Branch(_gotoLabel);

            return @goto;
        }

        protected override BaseStatement Visit(CompoundAssignment compAss) => Visit(new Assignment(compAss.Left, compAss.Right));
        #endregion

        #region base expressions
        protected override BaseExpression Visit(ConstantNumber con)
        {
            // Reflect out the raw int64 value
            var rawValueField = typeof(Number).GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance);
            var rawValue = (long)rawValueField.GetValue(con.Value);

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
                _emitter.LoadArgument(1);
            else
                _emitter.LoadArgument(0);

            // Lookup the index for the given name
            var map = (var.Name.IsExternal ? _externalVariableMap : _internalVariableMap);
            if (!map.TryGetValue(var.Name.Name, out var idx))
            {
                idx = map.Count;
                map[var.Name.Name] = idx;
            }
            _emitter.LoadConstant(idx);

            // Get the value from the span
            CallRuntime<Runtime>(nameof(Runtime.GetSpanIndex));

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
                var v = expr.TryStaticEvaluate();
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

        private T ConvertBinary<T>(T expr, Emitters2 emitters)
            where T : BaseBinaryExpression
        {
            if (TryStaticEvaluate(expr))
                return expr;

            // Evaluate the two sides
            Visit(expr.Left);
            Visit(expr.Right);

            // left and right will be the same type now, emit code to handle them
            emitters.Emit(this, expr);

            return expr;
        }

        protected override BaseExpression Visit(Add add) => ConvertBinary(add, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Addition");
                return StackType.YololValue;
            },
            Number = () => {
                CallRuntime<Number>("op_Addition");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Addition");
                return StackType.YololString;
            }
        });

        protected override BaseExpression Visit(Subtract sub) => ConvertBinary(sub, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Subtraction");
                return StackType.YololValue;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_Subtraction");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Subtraction");
                return StackType.YololString;
            }
        });

        protected override BaseExpression Visit(Multiply mul) => ConvertBinary(mul, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Multiply");
                return StackType.YololNumber;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_Multiply");
                return StackType.YololNumber;
            },
            Bool = () =>
            {
                _emitter.And();
                return StackType.Bool;
            }
        });

        protected override BaseExpression Visit(Divide div) => ConvertBinary(div, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Division");
                return StackType.YololNumber;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_Division");
                return StackType.YololNumber;
            }
        });

        protected override BaseExpression Visit(EqualTo eq) => ConvertBinary(eq, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Equality");
                return StackType.Bool;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_Equality");
                return StackType.Bool;
            },
            Bool = () =>
            {
                CallRuntime<Runtime>(nameof(Runtime.BoolEquals));
                return StackType.Bool;
            },
            String = () =>
            {
                CallRuntime<YString>("op_Equality");
                return StackType.Bool;
            },
        });

        protected override BaseExpression Visit(NotEqualTo neq) => Visit(new Not(new EqualTo(neq.Left, neq.Right)));

        protected override BaseExpression Visit(GreaterThan gt) => ConvertBinary(gt, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_GreaterThan");
                return StackType.Bool;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_GreaterThan");
                return StackType.Bool;
            },
            String = () =>
            {
                CallRuntime<YString>("op_GreaterThan");
                return StackType.Bool;
            },
        });

        protected override BaseExpression Visit(GreaterThanEqualTo gteq) => ConvertBinary(gteq, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_GreaterThanOrEqual");
                return StackType.Bool;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_GreaterThanOrEqual");
                return StackType.Bool;
            },
            String = () =>
            {
                CallRuntime<YString>("op_GreaterThanOrEqual");
                return StackType.Bool;
            },
        });

        protected override BaseExpression Visit(LessThan lt) => ConvertBinary(lt, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_LessThan");
                return StackType.Bool;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_LessThan");
                return StackType.Bool;
            },
            String = () =>
            {
                CallRuntime<YString>("op_LessThan");
                return StackType.Bool;
            },
        });

        protected override BaseExpression Visit(LessThanEqualTo lteq) => ConvertBinary(lteq, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_LessThanOrEqual");
                return StackType.Bool;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_LessThanOrEqual");
                return StackType.Bool;
            },
            String = () =>
            {
                CallRuntime<YString>("op_LessThanOrEqual");
                return StackType.Bool;
            },
        });

        protected override BaseExpression Visit(Modulo mod) => ConvertBinary(mod, new Emitters2 {
            Value = () => {
                CallRuntime<Value>("op_Modulus");
                return StackType.YololNumber;
            },
            Number = () =>
            {
                CallRuntime<Number>("op_Modulus");
                return StackType.YololNumber;
            }
        });

        protected override BaseExpression Visit(And and) => ConvertBinaryCoerce(and, StackType.Bool, StackType.Bool, () => {
            _emitter.And();
        });

        protected override BaseExpression Visit(Or or) => ConvertBinaryCoerce(or, StackType.Bool, StackType.Bool, () => {
            _emitter.Or();
        });

        protected override BaseExpression Visit(Exponent exp) => ConvertBinary(exp, new Emitters2 {
            Value = () => {
                CallRuntime<Value>(nameof(Value.Exponent));
                return StackType.YololNumber;
            },
            Number = () => {
                CallRuntime<Runtime>(nameof(Runtime.Exponent));
                return StackType.YololNumber;
            }
        });
        #endregion

        #region unary expressions
        private T ConvertUnaryCoerce<T>(T expr, StackType input, StackType output, Action emit)
            where T : BaseUnaryExpression
        {
            if (TryStaticEvaluate(expr))
                return expr;

            // Visit the inner expression of the unary and coerce it to the correct type
            Visit(expr.Parameter);
            Coerce(input);

            // Emit IL
            emit();

            // Update types
            Pop(input);
            Push(output);

            return expr;
        }

        private T ConvertUnary<T>(T expr, Emitters1 emitters)
            where T : BaseUnaryExpression
        {
            if (TryStaticEvaluate(expr))
                return expr;

            // Visit the inner expression of the unary
            Visit(expr.Parameter);

            // Emit the correct IL code for the type on the stack
            emitters.Emit(this);

            return expr;
        }

        protected override BaseExpression Visit(Not not) => ConvertUnaryCoerce(not, StackType.Bool, StackType.Bool, () => CallRuntime<Runtime>(nameof(Runtime.LogicalNot)));

        protected override BaseExpression Visit(Negate neg) => ConvertUnary(neg, new Emitters1
        {
            Number = () => {
                CallRuntime<Number>("op_UnaryNegation");
                return StackType.YololNumber;
            },
            Value = () => {
                CallRuntime<Value>("op_UnaryNegation");
                return StackType.YololNumber;
            }
        });

        protected override BaseExpression Visit(Sqrt sqrt) => ConvertUnary(sqrt, new Emitters1
        {
            Number = () => {
                CallRuntimeThis<Number>(nameof(Number.Sqrt));
                return StackType.YololNumber;
            },
            Value = () => {
                CallRuntime<Value>(nameof(Value.Sqrt));
                return StackType.YololNumber;
            }
        });
        #endregion

        #region trig expressions
        private T ConvertUnaryTrig<T>(T trig, string name)
            where T : BaseTrigonometry
        {
            return ConvertUnary(trig, new Emitters1
            {
                Number = () => {
                    CallRuntimeThis<Number>(name);
                    return StackType.YololNumber;
                },
                Value = () => {
                    CallRuntime<Value>(name);
                    return StackType.YololNumber;
                }
            });
        }

        protected override BaseExpression Visit(ArcCos acos) => ConvertUnaryTrig(acos, "ArcCos");

        protected override BaseExpression Visit(ArcSine acos) => ConvertUnaryTrig(acos, "ArcSin");

        protected override BaseExpression Visit(ArcTan acos) => ConvertUnaryTrig(acos, "ArcTan");

        protected override BaseExpression Visit(Cosine acos) => ConvertUnaryTrig(acos, "Cos");

        protected override BaseExpression Visit(Sine acos) => ConvertUnaryTrig(acos, "Sin");

        protected override BaseExpression Visit(Tangent acos) => ConvertUnaryTrig(acos, "Tan");
        #endregion

        #region modify expressions
        private T Modify<T>(T expr, bool preOp, Emitters1 emitters)
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
            emitters.Emit(this);

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

        protected override BaseExpression Visit(PreIncrement inc) => Modify(inc, true, new Emitters1 {
            Value = () => {
                CallRuntime<Value>("op_Increment");
                return StackType.YololValue;
            },
            Number = () => {
                _emitter.LoadConstant(1L);
                _emitter.NewObject<Number, long>();
                CallRuntime<Number>("op_Addition");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Increment");
                return StackType.YololString;
            }
        });

        protected override BaseExpression Visit(PreDecrement inc) => Modify(inc, true, new Emitters1 {
            Value = () => {
                CallRuntime<Value>("op_Decrement");
                return StackType.YololValue;
            },
            Number = () => {
                _emitter.LoadConstant(-1L);
                _emitter.NewObject<Number, long>();
                CallRuntime<Number>("op_Addition");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Decrement");
                return StackType.YololString;
            }
        });

        protected override BaseExpression Visit(PostIncrement inc) => Modify(inc, false, new Emitters1 {
            Value = () => {
                CallRuntime<Value>("op_Increment");
                return StackType.YololValue;
            },
            Number = () => {
                _emitter.LoadConstant(1L);
                _emitter.NewObject<Number, long>();
                CallRuntime<Number>("op_Addition");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Increment");
                return StackType.YololString;
            }
        });

        protected override BaseExpression Visit(PostDecrement inc) => Modify(inc, false, new Emitters1 {
            Value = () => {
                CallRuntime<Value>("op_Decrement");
                return StackType.YololValue;
            },
            Number = () => {
                _emitter.LoadConstant(-1L);
                _emitter.NewObject<Number, long>();
                CallRuntime<Number>("op_Addition");
                return StackType.YololNumber;
            },
            String = () => {
                CallRuntime<YString>("op_Decrement");
                return StackType.YololString;
            }
        });
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
        {
            // Evaluate the inner value and leave it on the stack
            Visit(brk.Parameter);

            return brk;
        }
    }
}
