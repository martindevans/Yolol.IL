using System;
using System.Collections.Generic;
using System.Linq;
using Sigil;
using Yolol.Analysis.TreeVisitor.Inspection;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.Grammar.AST;
using Yolol.Grammar.AST.Statements;
using Yolol.IL.Compiler;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Extensions
{
    public static class ILExtension
    {
        /// <summary>
        /// Store the given `ArraySegment[Value]` argument index into a local
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="arg"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        private static Local StoreMemorySegments<T>(Emit<T> emitter, ushort arg, string name)
        {
            emitter.LoadArgument(arg);
            var local = emitter.DeclareLocal<ArraySegment<Value>>(name);
            emitter.StoreLocal(local);

            return local;
        }

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
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(
            this Line line,
            int lineNumber,
            int maxLines,
            InternalsMap internalVariableMap,
            ExternalsMap externalVariableMap,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            // Locate all accessed variables and load them into the maps
            var stored = new FindAssignedVariables();
            stored.Visit(line);
            var loaded = new FindReadVariables();
            loaded.Visit(line);
            foreach (var name in stored.Names.Concat(loaded.Names).Distinct())
            {
                var dict = name.IsExternal ? (Dictionary<string, int>)externalVariableMap : internalVariableMap;
                if (!dict.TryGetValue(name.Name, out _))
                    dict[name.Name] = dict.Count;
            }

            return line.Compile(lineNumber, maxLines, (IReadonlyInternalsMap)internalVariableMap, externalVariableMap, staticTypes);
        }

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
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(
            this Line line,
            int lineNumber,
            int maxLines,
            IReadonlyInternalsMap internalVariableMap,
            IReadonlyExternalsMap externalVariableMap,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            // Create an emitter for a dynamic method
            var emitter = Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>>.NewDynamicMethod();

            // Compile code into the emitter
            line.Compile(emitter, lineNumber, maxLines, internalVariableMap, externalVariableMap, staticTypes);

            //Console.WriteLine(emitter.Instructions());
            //Console.WriteLine("-----------------------------");

            // Finally convert the IL into a runnable C# method for this line
            return emitter.CreateDelegate();
        }

        /// <summary>
        /// Compile all the lines of a Yolol program into runnable C# functions (one per line)
        /// </summary>
        /// <param name="ast"></param>
        /// <param name="externals"></param>
        /// <param name="maxLines"></param>
        /// <param name="staticTypes"></param>
        /// <returns></returns>
        public static CompiledProgram Compile(
            this Program ast,
            ExternalsMap externals,
            int maxLines = 20,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            if (maxLines < ast.Lines.Count)
                throw new ArgumentOutOfRangeException(nameof(maxLines), "ast has more than `maxLines` lines");

            var internals = new InternalsMap();

            var compiledLines = new JitLine[maxLines];
            for (var i = 0; i < maxLines; i++)
            {
                var lineNum = i + 1;
                var line = ast.Lines.ElementAtOrDefault(i) ?? new Line(new StatementList());
                compiledLines[i] = new JitLine(line.Compile(lineNum, maxLines, internals, externals, staticTypes));
            }

            return new CompiledProgram(internals, compiledLines);
        }

        /// <summary>
        /// Compile a line of Yolol into the given IL emitter
        /// </summary>
        /// <param name="line"></param>
        /// <param name="emitter"></param>
        /// <param name="lineNumber"></param>
        /// <param name="maxLines"></param>
        /// <param name="internalVariableMap"></param>
        /// <param name="externalVariableMap"></param>
        /// <param name="staticTypes"></param>
        public static void Compile(
            this Line line,
            Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>> emitter,
            int lineNumber,
            int maxLines,
            IReadonlyInternalsMap internalVariableMap,
            IReadonlyExternalsMap externalVariableMap,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            // Special case for totally empty lines
            if (line.Statements.Statements.Count == 0)
            {
                if (lineNumber == maxLines)
                    emitter.LoadConstant(1);
                else
                    emitter.LoadConstant(lineNumber + 1);
                emitter.Return();

                return;
            }

            // Begin an exception block to catch Yolol runtime errors
            var exBlock = emitter.BeginExceptionBlock();

            using var internals = StoreMemorySegments(emitter, 0, "Internals_Memory");
            using var externals = StoreMemorySegments(emitter, 1, "Externals_Memory");

            // Create a local to store the return address from inside the try/catch block
            var retAddr = emitter.DeclareLocal<int>("ret_addr");

            // Create a label which any `goto` statements can use. They drop their destination PC on the stack and then jump to this label
            var gotoLabel = emitter.DefineLabel("encountered_goto");

            // Create a label which any runtime errors can use. They jump here after emptying the stack
            var runtimeErrorLabel = emitter.DefineLabel("encountered_runtime_error");

            // Create a label which marks the end of the line, code reaching here falls through to the next line
            var eolLabel = emitter.DefineLabel("encountered_eol");

            // Define a label that jumps to the end of the try/catch block
            var exitTry = emitter.DefineLabel("exit_try_catch");

            // Create a memory accessor which manages reading and writing the memory arrays
            using (var accessor = new MemoryAccessor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(
                emitter,
                externals,
                internals,
                internalVariableMap,
                externalVariableMap,
                staticTypes
            ))
            {
                accessor.Initialise(line);

                // Convert the entire line into IL
                var converter = new ConvertLineVisitor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(emitter, maxLines, accessor, gotoLabel, runtimeErrorLabel);
                converter.Visit(line);
                emitter.Branch(eolLabel);

                // Sanity check before returning result
                if (!converter.IsTypeStackEmpty)
                    throw new InvalidOperationException("Type stack is not empty after conversion");

                // If an error occurs control flow will jump to here. Just act as if control flow fell off the end of the line
                emitter.MarkLabel(runtimeErrorLabel);
                emitter.Branch(eolLabel);

                // When a line finishes (with no gotos in the line) call flow eventually reaches here.
                // go to the next line.
                emitter.MarkLabel(eolLabel);
                if (lineNumber == maxLines)
                    emitter.LoadConstant(1);
                else
                    emitter.LoadConstant(lineNumber + 1);
                emitter.StoreLocal(retAddr);
                emitter.Branch(exitTry);

                // Create a block to handle gotos. The destination will already be on the stack, so just return
                emitter.MarkLabel(gotoLabel);
                emitter.StoreLocal(retAddr);
                emitter.Branch(exitTry);

                // Mark the end of the block for things that want to leave
                emitter.MarkLabel(exitTry);

                // Catch all execution exceptions and return the appropriate next line number to fall through to
                var catchBlock = emitter.BeginCatchBlock<ExecutionException>(exBlock);
                emitter.Pop();
                if (lineNumber == maxLines)
                    emitter.LoadConstant(1);
                else
                    emitter.LoadConstant(lineNumber + 1);
                emitter.StoreLocal(retAddr);

                // Close the exception block which was wrapping the entire method
                emitter.EndCatchBlock(catchBlock);
                emitter.EndExceptionBlock(exBlock);
            }

            // Load the return address from inside the catch block
            emitter.LoadLocal(retAddr);
            emitter.Return();
        }
    }
}
