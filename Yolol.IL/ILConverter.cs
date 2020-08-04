﻿using Sigil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Yolol.Analysis.TreeVisitor;
using Yolol.Execution;
using Yolol.Execution.Extensions;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;
using Yolol.Grammar.AST.Statements;

using Yolol.Analysis.TreeVisitor.Reduction;

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
            var emitter = Emit<Func<Memory<Value>, Memory<Value>, int, int, int>>.NewDynamicMethod();
            var gotoLabel = emitter.DefineLabel();
            var converter = new ConvertLineVisitor(emitter, maxLines, internalVariableMap, externalVariableMap, gotoLabel);

            // Convert the entire line into IL
            converter.Visit(line.Statements);

            // If there were no gotos eventually flow will fall through to here
            // goto the next line
            emitter.MarkLabel(emitter.DefineLabel());
            if (lineNumber == maxLines)
                emitter.LoadConstant(1);
            else
                emitter.LoadConstant(lineNumber + 1);
            emitter.Return();

            // Create a block to handle gotos. The destination will already be on the stack, so just return
            emitter.MarkLabel(gotoLabel);
            emitter.Return();

            // Finally convert the IL into a runnable C# method for this line
            var d = emitter.CreateDelegate();

            // Pass in a dummy argument for third argument (it's not used)
            return (a, b) => d(a, b, 0, 0);
        }

        /// <summary>
        /// Compile an entire program into a runnable C# function
        /// </summary>
        /// <param name="program"></param>
        /// <param name="internalMap"></param>
        /// <param name="externalMap"></param>
        /// <returns>A function which runs this program. Accepts two sections of memory, internal variables and external variables. Also accepts a line to start at and a count of the number of lines to run. Returns the line number to go to next</returns>
        public static Func<Memory<Value>, Memory<Value>, int, int, int> Compile(this Program program, Dictionary<string, int> internalMap, Dictionary<string, int> externalMap)
        {
            // Create converter to emit IL code
            var emitter = Emit<Func<Memory<Value>, Memory<Value>, int, int, int>>.NewDynamicMethod(strictBranchVerification: true);
            var lineEnd = emitter.DefineLabel("line_ended");
            var converter = new ConvertLineVisitor(emitter, program.Lines.Count, internalMap, externalMap, lineEnd);

            // Set the count of `linesToExecute` to `arg3`
            var linesToExecute = emitter.DeclareLocal(typeof(int), "linesToExecute");
            emitter.LoadArgument(3);
            emitter.StoreLocal(linesToExecute);

            // Define a label for the start of each line
            var lineLabels = program.Lines.Select((_, i) => emitter.DefineLabel($"line{i + 1}")).ToArray();

            // Work out which line to start executing (`arg2+1`)
            emitter.LoadArgument(2);
            emitter.LoadConstant(1);
            emitter.Add();

            // Lines all jump here when they're done. The next line number to execute should be
            // sitting on the stack.
            emitter.MarkLabel(lineEnd);

            // First, check if we have executed sufficient lines, if so return
            var done = emitter.DefineLabel("done_enough_lines");
            emitter.LoadConstant(0);
            emitter.LoadLocal(linesToExecute);
            emitter.BranchIfGreaterOrEqual(done);

            // More lines need executing, first subtract one from the count to execute
            emitter.LoadLocal(linesToExecute);
            emitter.LoadConstant(-1);
            emitter.Add();
            emitter.StoreLocal(linesToExecute);

            // Clamp index into the correct range and then jump to that index
            emitter.Call(typeof(ILExtension).GetMethod(nameof(FixupGotoIndex), BindingFlags.NonPublic | BindingFlags.Static));
            emitter.Switch(lineLabels.ToArray());

            // If we're here, it's because execution tried to go to a line that doesn't exist
            emitter.LoadConstant("Attempted to `goto` invalid line");
            emitter.NewObject<InvalidOperationException, string>();
            emitter.Throw();

            // Finally, escape the function (next line to go to is on the stack, so that becomes the return value)
            emitter.MarkLabel(done);
            emitter.Return();

            // Convert each line, putting a label at the start of the line
            for (var i = 0; i < program.Lines.Count; i++)
            {
                // Define a label at the start of the line
                emitter.MarkLabel(lineLabels[i]);

                // Convert the entire line
                converter.Visit(program.Lines[i]);

                // If there were no `goto` statements in the line it will fall through to here
                // push the next line number to go to and jump to the `goto` handler
                var lineNumber = i + 1;
                if (lineNumber == program.Lines.Count)
                    emitter.LoadConstant(1);
                else
                    emitter.LoadConstant(lineNumber + 1);
                emitter.Branch(lineEnd);
            }

            // Finally convert the IL into a runnable C# method for this line
            return emitter.CreateDelegate();
        }

        private static int FixupGotoIndex(int oneBasedIndex)
        {
            if (oneBasedIndex <= 0)
                return 0;
            if (oneBasedIndex >= 20)
                return 19;
            return oneBasedIndex - 1;
        }
    }

    public class ConvertLineVisitor
        : BaseTreeVisitor
    {
        private readonly Emit<Func<Memory<Value>, Memory<Value>, int, int, int>> _emitter;

        private readonly int _maxLineNumber;
        private readonly Dictionary<string, int> _internalVariableMap;
        private readonly Dictionary<string, int> _externalVariableMap;
        private readonly Label _gotoLabel;

        public ConvertLineVisitor(Emit<Func<Memory<Value>, Memory<Value>, int, int, int>> emitter, int maxLineNumber, Dictionary<string, int> internalVariableMap, Dictionary<string, int> externalVariableMap, Label gotoLabel)
        {
            _emitter = emitter;
            _maxLineNumber = maxLineNumber;
            _internalVariableMap = internalVariableMap;
            _externalVariableMap = externalVariableMap;
            _gotoLabel = gotoLabel;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int Goto(Value value, int maxLineNumber)
        {
            if (value.Type == Execution.Type.Number)
                return Math.Min(maxLineNumber, Math.Max(1, (int)value.Number));

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

            // Put the max line number on the stack (as the second argument to the `Goto` method we're about to call)
            _emitter.LoadConstant(_maxLineNumber);

            // Call into goto handler
            _emitter.Call(typeof(ConvertLineVisitor).GetMethod(nameof(Goto), BindingFlags.NonPublic | BindingFlags.Static));

            // Jump to the `goto` label
            _emitter.Branch(_gotoLabel);

            return @goto;
        }

        protected override BaseStatement Visit(CompoundAssignment compAss) => Visit(new Assignment(compAss.Left, compAss.Right));


        protected override BaseExpression Visit(ConstantNumber con)
        {
            // Reflect out the raw int64 value
            var rawValueProp = typeof(Number).GetProperty("Value", BindingFlags.NonPublic | BindingFlags.Instance, null, typeof(long), Array.Empty<System.Type>(), null);
            var rawValue = (long)rawValueProp.GetValue(con.Value);

            // Put that raw value onto the stack
            _emitter.LoadConstant(rawValue);

            // Use it to construct a `Number` at runtime by calling the constructor
            _emitter.NewObject<Number, long>();

            // Wrap the number in a `Value`
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
            // Try to statically evaluate the expression
            if (expr.IsConstant)
            {
                var v = expr.TryStaticEvaluate();
                if (v.HasValue)
                {
                    Visit(v.Value.ToConstant());
                    return expr;
                }
            }

            // Failed to statically evaluate the expression, run it normally
            Visit(expr.Left);
            Visit(expr.Right);
            _emitter.Call(typeof(Value).GetMethod(valOp, new[] {typeof(Value), typeof(Value)}));

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

        protected override BaseExpression Visit(Exponent exp) => ConvertBinary(exp, nameof(Value.Exponent));


        private T ConvertUnary<T>(T expr, string valOp)
            where T : BaseUnaryExpression
        {
            if (expr.IsConstant)
            {
                var v = expr.TryStaticEvaluate();
                if (v.HasValue)
                {
                    Visit(v.Value.ToConstant());
                    return expr;
                }
            }

            Visit(expr.Parameter);
            _emitter.Call(typeof(Value).GetMethod(valOp, new[] { typeof(Value) }));

            return expr;
        }

        protected override BaseExpression Visit(Not not) => ConvertUnary(not, "op_LogicalNot");

        protected override BaseExpression Visit(Negate neg) => ConvertUnary(neg, "op_UnaryNegation");

        protected override BaseExpression Visit(Sqrt sqrt) => ConvertUnary(sqrt, "Sqrt");


        protected override BaseExpression Visit(Bracketed brk)
        {
            // Evaluate the inner value and leave it on the stack
            Visit(brk.Parameter);

            return brk;
        }


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

        protected override BaseStatement Visit(ExpressionWrapper expr)
        {
            var r = base.Visit(expr);

            // The wrapped expression left a value on the stack. Pop it off now.
            _emitter.Pop();

            return r;
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
