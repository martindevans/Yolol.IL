using Sigil;
using System;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL
{
    public static class ILExtension
    {
        public static Func<int, MachineState, int> Compile(this Line line)
        {
            var emitter = Emit<Func<int, MachineState, int>>.NewDynamicMethod();
            var converter = new ConvertLineVisitor(emitter);

            converter.Visit(line.Statements);

            // temp return, need to properly get goto values somehow
            emitter.LoadConstant(7);
            emitter.Return();

            return emitter.CreateDelegate();
        }
    }

    public class ConvertLineVisitor
        : BaseTreeVisitor
    {
        private readonly Emit<Func<int, MachineState, int>> _emitter;

        public ConvertLineVisitor(Emit<Func<int, MachineState, int>> emitter)
        {
            _emitter = emitter;
        }

        private void SwitchOnTypePair(Local leftType, Local rightType, Label numNum, Label numStr, Label strNum, Label strStr)
        {
            void Test(Execution.Type l, Execution.Type r, Label tgt)
            {
                _emitter.LoadLocal(leftType);
                _emitter.LoadConstant((int)l);
                _emitter.CompareEqual();

                _emitter.LoadLocal(rightType);
                _emitter.LoadConstant((int)r);
                _emitter.CompareEqual();

                _emitter.And();
                _emitter.BranchIfTrue(tgt);
            }

            Test(Execution.Type.Number, Execution.Type.Number, numNum);
            Test(Execution.Type.Number, Execution.Type.String, numStr);
            Test(Execution.Type.String, Execution.Type.Number, strNum);
            Test(Execution.Type.String, Execution.Type.String, strStr);

            // Invalid type pair
            _emitter.NewObject<InvalidOperationException>();
            _emitter.Throw();
        }

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

        protected override BaseStatement Visit(Assignment ass)
        {           
            // Put `MachineState` onto the stack
            var state = _emitter.LoadArgument(1);

            // Put the name of the variable onto the stack
            _emitter.LoadConstant(ass.Left.Name);

            // Put the variable object to assign onto the stack (consume previous 2 items)
            _emitter.Call(typeof(MachineState).GetMethod(nameof(MachineState.GetVariable)));

            // Place the value to put into this variable on the stack
            var l = Visit(ass.Right);

            // Set the value on the stack into the variable
            _emitter.CallVirtual(typeof(IVariable).GetProperty(nameof(IVariable.Value)).SetMethod);

            return ass;
        }

        private void ConvertBinary<T>(T expr, Action<Local, Local> emitNumNum, Action<Local, Local> emitNumStr, Action<Local, Local> emitStrNum, Action<Local, Local> emitStrStr)
            where T : BaseBinaryExpression
        {
            using var lvalue = _emitter.DeclareLocal(typeof(Value));
            using var rvalue = _emitter.DeclareLocal(typeof(Value));
            using var ltype = _emitter.DeclareLocal(typeof(Execution.Type));
            using var rtype = _emitter.DeclareLocal(typeof(Execution.Type));

            _emitter.DefineLabel(out var numNumLabel);
            _emitter.DefineLabel(out var numStrLabel);
            _emitter.DefineLabel(out var strNumlabel);
            _emitter.DefineLabel(out var strStrLabel);
            _emitter.DefineLabel(out var exit);

            // Place the left value into local
            Visit(expr.Left);
            _emitter.StoreLocal(lvalue);

            // Place the right value into local
            Visit(expr.Right);
            _emitter.StoreLocal(rvalue);

            // Put left type into ltype field
            _emitter.LoadLocalAddress(lvalue);
            _emitter.Call(typeof(Value).GetProperty(nameof(Value.Type)).GetMethod);
            _emitter.StoreLocal(ltype);

            // Put right type into rtype field
            _emitter.LoadLocalAddress(rvalue);
            _emitter.Call(typeof(Value).GetProperty(nameof(Value.Type)).GetMethod);
            _emitter.StoreLocal(rtype);

            // Construct tests for the four possible type combinations
            SwitchOnTypePair(ltype, rtype, numNumLabel, numStrLabel, strNumlabel, strStrLabel);

            // Emit the 4 cases, each one jumping to the exit once done
            _emitter.MarkLabel(numNumLabel);
            emitNumNum(lvalue, rvalue);
            _emitter.Branch(exit);

            _emitter.MarkLabel(numStrLabel);
            emitNumStr(lvalue, rvalue);
            _emitter.Branch(exit);

            _emitter.MarkLabel(strNumlabel);
            emitStrNum(lvalue, rvalue);
            _emitter.Branch(exit);

            _emitter.MarkLabel(strStrLabel);
            emitStrStr(lvalue, rvalue);
            _emitter.Branch(exit);

            _emitter.MarkLabel(exit);
        }

        protected override BaseExpression Visit(Add add)
        {
            void NumNum(Local l, Local r)
            {
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.Call(typeof(Number).GetMethod("op_Addition"));
                _emitter.NewObject<Value, Number>();
            }

            void Other(Local l, Local r)
            {
                // `ToString` both sides ready to concat
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToString)));
                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToString)));

                // Concat strings on stack
                _emitter.Call(typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) }));

                // Wrap in a `Value`
                _emitter.NewObject<Value, string>();
            }

            ConvertBinary(add, NumNum, Other, Other, Other);

            return add;
        }

        protected override BaseExpression Visit(Subtract sub)
        {
            void NumNum(Local l, Local r)
            {
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.Call(typeof(Number).GetMethod("op_Subtraction"));
                _emitter.NewObject<Value, Number>();
            }

            void Other(Local l, Local r)
            {
                using var lstr = _emitter.DeclareLocal<string>();
                using var rstr = _emitter.DeclareLocal<string>();
                using var found = _emitter.DeclareLocal<int>();

                var noMatch = _emitter.DefineLabel();
                var exit = _emitter.DefineLabel();

                // `ToString` both sides into locals
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToString)));
                _emitter.StoreLocal(lstr);

                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetMethod(nameof(Value.ToString)));
                _emitter.StoreLocal(rstr);

                // Find right in left
                _emitter.LoadLocal(lstr);
                _emitter.LoadLocal(rstr);
                _emitter.LoadConstant((int)StringComparison.Ordinal);
                _emitter.Call(typeof(string).GetMethod(nameof(string.LastIndexOf), new[] { typeof(string), typeof(StringComparison) }));
                _emitter.StoreLocal(found);

                // Check if a match was found
                _emitter.LoadLocal(found);
                _emitter.LoadConstant(-1);
                _emitter.BranchIfEqual(noMatch);

                // Match was found
                _emitter.LoadLocal(lstr);
                _emitter.LoadLocal(found);
                _emitter.LoadLocal(rstr);
                _emitter.Call(typeof(string).GetProperty(nameof(string.Length)).GetMethod);
                _emitter.Call(typeof(string).GetMethod(nameof(string.Remove), new[] { typeof(int), typeof(int) }));
                _emitter.Branch(exit);

                // No match was found, just return left side
                _emitter.MarkLabel(noMatch);
                _emitter.LoadLocal(lstr);

                // Warp whatever is on the stack as a string value
                _emitter.MarkLabel(exit);
                _emitter.NewObject<Value, string>();
            }

            ConvertBinary(sub, NumNum, Other, Other, Other);

            return sub;
        }

        protected override BaseExpression Visit(Multiply mul)
        {
            void NumNum(Local l, Local r)
            {
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.Call(typeof(Number).GetMethod("op_Multiply"));
                _emitter.NewObject<Value, Number>();
            }

            void Other(Local l, Local r)
            {
                var x = _emitter.DefineLabel();

                _emitter.LoadConstant("Attempted to multiply mixed types");
                _emitter.NewObject<ExecutionException, string>();
                _emitter.Throw();

                // Because this throws the following code is unreachable, Sigil warns about this as invalid code. Adding in this label mades the following code
                // seem reachable to sigil and suppresses that warning.
                _emitter.MarkLabel(x);
            }

            ConvertBinary(mul, NumNum, Other, Other, Other);

            return mul;
        }

        protected override BaseExpression Visit(Divide div)
        {
            void NumNum(Local l, Local r)
            {
                _emitter.LoadLocalAddress(l);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.LoadLocalAddress(r);
                _emitter.Call(typeof(Value).GetProperty(nameof(Value.Number)).GetMethod);
                _emitter.Call(typeof(Number).GetMethod("op_Division"));
                _emitter.NewObject<Value, Number>();
            }

            void Other(Local l, Local r)
            {
                var x = _emitter.DefineLabel();

                _emitter.LoadConstant("Attempted to multiply divide types");
                _emitter.NewObject<ExecutionException, string>();
                _emitter.Throw();

                // Because this throws the following code is unreachable, Sigil warns about this as invalid code. Adding in this label mades the following code
                // seem reachable to sigil and suppresses that warning.
                _emitter.MarkLabel(x);
            }

            ConvertBinary(div, NumNum, Other, Other, Other);

            return div;
        }
    }
}
