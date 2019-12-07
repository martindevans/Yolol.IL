using Sigil;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL
{
    public static class ILExtension
    {
        /// <summary>
        /// Compile a line of Yolol into a runnable C# function
        /// </summary>
        /// <param name="line">The line of code to convert</param>
        /// <param name="lineNumber">The number of this line</param>
        /// <param name="maxLines">The max number of lines in a valid program (20, in standard Yolol)</param>
        /// <param name="internalVariableMap">A dictionary used for mapping variables to integers in all lines in this script</param>
        /// <param name="externalVariableMap">A dictionary used for mapping externals to integers in all lines in this script</param>
        /// <returns>A function which runs this line of code. Accepts two sections of memory, internal variables and external variables. Returns the line number to go to next</returns>
        public static Func<Memory<Value>, Memory<Value>, int> Compile(this Line line, int lineNumber, int maxLines, Dictionary<string, int> internalVariableMap, Dictionary<string, int> externalVariableMap)
        {
            var emitter = Emit<Func<Memory<Value>, Memory<Value>, int>>.NewDynamicMethod();
            var converter = new ConvertLineVisitor(emitter, maxLines, internalVariableMap, externalVariableMap);

            // Convert the entire line into IL
            converter.Visit(line.Statements);

            // If no other gotos were encountered, goto the next line
            emitter.MarkLabel(emitter.DefineLabel());
            if (lineNumber == maxLines)
                emitter.LoadConstant(1);
            else
                emitter.LoadConstant(lineNumber + 1);
            emitter.Return();

            // Finally convert the IL into a runnable C# method for this line
            return emitter.CreateDelegate();
        }
    }

    public class ConvertLineVisitor
        : BaseTreeVisitor
    {
        private readonly Emit<Func<Memory<Value>, Memory<Value>, int>> _emitter;

        private readonly int _maxLineNumber;
        private readonly Dictionary<string, int> _internalVariableMap;
        private readonly Dictionary<string, int> _externalVariableMap;

        public ConvertLineVisitor(Emit<Func<Memory<Value>, Memory<Value>, int>> emitter, int maxLineNumber, Dictionary<string, int> internalVariableMap, Dictionary<string, int> externalVariableMap)
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _internalVariableMap = internalVariableMap;
            _externalVariableMap = externalVariableMap;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Goto(Value value, int maxLineNumber)
        {
            if (value.Type == Execution.Type.Number)
                return Math.Min(maxLineNumber, Math.Max(1, (int)value.Number.Value));

            throw new ExecutionException("Attempted to `goto` a `string`");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetSpanIndex(Value v, Memory<Value> mem, int index)
        {
            mem.Span[index] = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Value GetSpanIndex(Memory<Value> mem, int index)
        {
            return mem.Span[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write2(Value value, MachineState state, string variable)
        {
            state.GetVariable(variable).Value = value;
        }

        private void EmitAssign(VariableName name)
        {
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

            // Value was already on stack before this
            // Put the value into the span
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(SetSpanIndex), BindingFlags.NonPublic | BindingFlags.Static));
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
            using (var conditional = _emitter.DeclareLocal(typeof(Value)))
            {
                _emitter.StoreLocal(conditional);
                _emitter.LoadLocalAddress(conditional);
                _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToBool)));
            }

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

            // Put the max line number on the stack
            _emitter.LoadConstant(_maxLineNumber);

            // Call into goto handler
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(Goto), BindingFlags.NonPublic | BindingFlags.Static));

            // Return this value as the new program counter
            _emitter.Return();

            return @goto;
        }

        protected override BaseStatement Visit(CompoundAssignment compAss) => throw new NotImplementedException();


        protected override BaseExpression Visit(ConstantNumber con)
        {
            // Split up decimal and load it onto the stack
            var bits = decimal.GetBits(con.Value.Value);
            bool sign = (bits[3] & 0x80000000) != 0;
            int scale = (byte)((bits[3] >> 16) & 0x7f);

            _emitter.LoadConstant(bits[0]);
            _emitter.LoadConstant(bits[1]);
            _emitter.LoadConstant(bits[2]);
            _emitter.LoadConstant(sign ? 1 : 0);
            _emitter.LoadConstant(scale);

            _emitter.NewObject<decimal, int, int, int, bool, byte>();

            // Wrap `decimal` in `Number`
            _emitter.NewObject<Number, decimal>();

            // Wrap `Number` in `Value`
            _emitter.NewObject<Value, Number>();

            return con;
        }

        protected override BaseExpression Visit(ConstantString str)
        {
            // Get string value
            _emitter.LoadConstant(str.Value);

            // Wrap `string` in `Value`
            _emitter.NewObject<Value, string>();

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
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(GetSpanIndex), BindingFlags.NonPublic | BindingFlags.Static));

            return var;
        }


        private T ConvertBinary<T>(T expr, string valOp)
            where T : BaseBinaryExpression
        {
            Visit(expr.Left);
            Visit(expr.Right);

            _emitter.Call(typeof(Value).GetMethod(valOp, new[] { typeof(Value), typeof(Value) }));

            return expr;
        }

        protected override BaseExpression Visit(Add add) => ConvertBinary(add, "op_Addition");

        protected override BaseExpression Visit(Subtract sub) => ConvertBinary(sub, "op_Subtraction");

        protected override BaseExpression Visit(Multiply mul) => ConvertBinary(mul, "op_Multiply");

        protected override BaseExpression Visit(Divide div) => ConvertBinary(div, "op_Division");

        protected override BaseExpression Visit(EqualTo eq) => ConvertBinary(eq, "op_Equality");

        protected override BaseExpression Visit(NotEqualTo neq) => ConvertBinary(neq, "op_Inequality");

        protected override BaseExpression Visit(GreaterThan eq) => ConvertBinary(eq, "op_GreaterThan");

        protected override BaseExpression Visit(GreaterThanEqualTo eq) => ConvertBinary(eq, "op_GreaterThanOrEqual");

        protected override BaseExpression Visit(LessThan eq) => ConvertBinary(eq, "op_LessThan");

        protected override BaseExpression Visit(LessThanEqualTo eq) => ConvertBinary(eq, "op_LessThanOrEqual");

        protected override BaseExpression Visit(Modulo mod) => ConvertBinary(mod, "op_Modulus");

        protected override BaseExpression Visit(And and) => ConvertBinary(and, "op_BitwiseAnd");

        protected override BaseExpression Visit(Or or) => ConvertBinary(or, "op_BitwiseOr");

        protected override BaseExpression Visit(Exponent expr)
        {
            Visit(expr.Left);
            Visit(expr.Right);

            _emitter.Call(typeof(Value).GetMethod(nameof(Value.Exponent), new[] { typeof(Value), typeof(Value) }));

            return expr;
        }


        private T ConvertUnary<T>(T expr, string valOp)
            where T : BaseUnaryExpression
        {
            Visit(expr.Parameter);

            _emitter.Call(typeof(Value).GetMethod(valOp, new[] { typeof(Value) }));

            return expr;
        }

        protected override BaseExpression Visit(Not not) => ConvertUnary(not, "op_LogicalNot");

        protected override BaseExpression Visit(Negate neg) => ConvertUnary(neg, "op_UnaryNegation");

        protected override BaseExpression Visit(Bracketed brk)
        {
            // Evaluate the inner value and leave it on the stack
            Visit(brk.Parameter);

            return brk;
        }

        protected override BaseExpression Visit(Sqrt sqrt) => ConvertUnary(sqrt, "Sqrt");

        private T Modify<T>(T expr, string op, bool preOp)
            where T : BaseModifyInPlace
        {
            // Put the current value of the variable onto the stack
            Visit(new Grammar.AST.Expressions.Variable(expr.Name));

            // If we need the old value save it now
            if (!preOp)
                _emitter.Duplicate();

            // Run the inc/dec operation
            _emitter.Call(typeof(Value).GetMethod(op, new[] { typeof(Value) }));

            // If we need to return the new value, save it now by duplicating it
            if (preOp)
                _emitter.Duplicate();

            // Write value to variable
            EmitAssign(expr.Name);

            return expr;
        }

        protected override BaseExpression Visit(PreIncrement inc) => Modify(inc, "op_Increment", true);

        protected override BaseExpression Visit(PreDecrement inc) => Modify(inc, "op_Decrement", true);

        protected override BaseExpression Visit(PostIncrement inc) => Modify(inc, "op_Increment", false);

        protected override BaseExpression Visit(PostDecrement inc) => Modify(inc, "op_Decrement", false);

        protected override BaseExpression Visit(ArcCos acos) => ConvertUnary(acos, "ArcCos");

        protected override BaseExpression Visit(ArcSine acos) => ConvertUnary(acos, "ArcSin");

        protected override BaseExpression Visit(ArcTan acos) => ConvertUnary(acos, "ArcTan");

        protected override BaseExpression Visit(Cosine acos) => ConvertUnary(acos, "Cos");

        protected override BaseExpression Visit(Sine acos) => ConvertUnary(acos, "Sin");

        protected override BaseExpression Visit(Tangent acos) => ConvertUnary(acos, "Tan");
    }
}
