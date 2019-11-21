using Sigil;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL
{
    public static class ILExtension
    {
        public static Func<MachineState, int> Compile(this Line line, int lineNumber)
        {
            var emitter = Emit<Func<MachineState, int>>.NewDynamicMethod();
            var converter = new ConvertLineVisitor(emitter);

            converter.Visit(line.Statements);

            // Return a dead value (here signified by int.MinValue) to indicate no gotos were encountered
            emitter.MarkLabel(emitter.DefineLabel());
            emitter.LoadConstant(int.MinValue);
            emitter.Return();

            return emitter.CreateDelegate();
        }
    }

    public class ConvertLineVisitor
        : BaseTreeVisitor
    {
        private readonly Emit<Func<MachineState, int>> _emitter;

        public ConvertLineVisitor(Emit<Func< MachineState, int>> emitter)
        {
            _emitter = emitter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Goto(Value value, MachineState state)
        {
            if (value.Type == Execution.Type.Number)
                return Math.Min(state.MaxLineNumber, Math.Max(1, (int)value.Number.Value));

            throw new ExecutionException("Attempted to `goto` a `string`");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write1(MachineState state, string variable, Value value)
        {
            state.GetVariable(variable).Value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Write2(Value value, MachineState state, string variable)
        {
            state.GetVariable(variable).Value = value;
        }

        protected override BaseStatement Visit(Assignment ass)
        {
            // Put `MachineState` onto the stack
            var state = _emitter.LoadArgument(0);

            // Put the name of the variable onto the stack
            _emitter.LoadConstant(ass.Left.Name);

            // Place the value to put into this variable on the stack
            var l = Visit(ass.Right);

            // Call variable assignment method
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(Write1), BindingFlags.NonPublic | BindingFlags.Static));

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

            // Put MachineState on the stack
            _emitter.LoadArgument(0);

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
            // Put `MachineState` onto the stack
            var state = _emitter.LoadArgument(0);

            // Put name of variable we're looking for on the stack
            _emitter.LoadConstant(var.Name.Name);

            // Put `IVariable` instance on the stack
            _emitter.Call(typeof(MachineState).GetMethod(nameof(MachineState.GetVariable), new[] { typeof(string) }));

            // Put value on the stack
            _emitter.CallVirtual(typeof(IVariable).GetProperty(nameof(IVariable.Value)).GetMethod);

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
            _emitter.LoadArgument(0);
            _emitter.LoadConstant(expr.Name.Name);
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(Write2), BindingFlags.NonPublic | BindingFlags.Static));

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
