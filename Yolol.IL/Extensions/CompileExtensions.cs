using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.IL.Compiler;

namespace Yolol.IL.Extensions
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
        /// <param name="staticTypes">Statically known types for variables</param>
        /// <returns>A function which runs this line of code. Accepts two sections of memory, internal variables and external variables. Returns the line number to go to next</returns>
        public static Func<Memory<Value>, Memory<Value>, int> Compile(this Line line, int lineNumber, int maxLines, Dictionary<string, int> internalVariableMap, Dictionary<string, int> externalVariableMap, IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes = null)
        {
            var emitter = Emit<Func<Memory<Value>, Memory<Value>, int, int, int>>.NewDynamicMethod();
            var gotoLabel = emitter.DefineLabel();
            var converter = new ConvertLineVisitor(emitter, maxLines, internalVariableMap, externalVariableMap, gotoLabel, staticTypes);

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

            if (!converter.IsTypeStackEmpty)
                throw new InvalidOperationException("Type stack is not empty after conversion");

            // Pass in a dummy argument for third argument (it's not used)
            return (a, b) => d(a, b, 0, 0);
        }

        /// <summary>
        /// Compile an entire program into a runnable C# function
        /// </summary>
        /// <param name="program"></param>
        /// <param name="internalMap"></param>
        /// <param name="externalMap"></param>
        /// <param name="staticTypes"></param>
        /// <returns>A function which runs this program. Accepts two sections of memory, internal variables and external variables. Also accepts a line to start at and a count of the number of lines to run. Returns the line number to go to next</returns>
        public static Func<Memory<Value>, Memory<Value>, int, int, int> Compile(this Program program, Dictionary<string, int> internalMap, Dictionary<string, int> externalMap, IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes = null)
        {
            // Create converter to emit IL code
            var emitter = Emit<Func<Memory<Value>, Memory<Value>, int, int, int>>.NewDynamicMethod(strictBranchVerification: true);
            var lineEnd = emitter.DefineLabel("line_ended");
            var converter = new ConvertLineVisitor(emitter, program.Lines.Count, internalMap, externalMap, lineEnd, staticTypes);

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
            emitter.LoadConstant(program.Lines.Count);
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

        private static int FixupGotoIndex(int oneBasedIndex, int maxLines)
        {
            if (oneBasedIndex <= 0)
                return 0;
            if (oneBasedIndex >= maxLines)
                return maxLines - 1;
            return oneBasedIndex - 1;
        }
    }
}
