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
using Yolol.IL.Compiler.Emitter;
using Yolol.IL.Compiler.Memory;
using Type = Yolol.Execution.Type;

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
        /// <param name="maxStringLength"></param>
        /// <param name="internalVariableMap">A dictionary used for mapping variables to integers in all lines in this script</param>
        /// <param name="externalVariableMap">A dictionary used for mapping externals to integers in all lines in this script</param>
        /// <param name="staticTypes">Statically known types for variables</param>
        /// <returns>A function which runs this line of code. Accepts two sections of memory, internal variables and external variables. Returns the line number to go to next</returns>
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(
            this Line line,
            int lineNumber,
            int maxLines,
            int? maxStringLength,
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

            return line.Compile(lineNumber, maxLines, maxStringLength, (IReadonlyInternalsMap)internalVariableMap, externalVariableMap, staticTypes);
        }

        /// <summary>
        /// Compile a line of Yolol into a runnable C# function
        /// </summary>
        /// <param name="line">The line of code to convert</param>
        /// <param name="lineNumber">The number of this line</param>
        /// <param name="maxLines">The max number of lines in a valid program (20, in standard Yolol)</param>
        /// <param name="maxStringLength"></param>
        /// <param name="internalVariableMap">A dictionary used for mapping variables to integers in all lines in this script</param>
        /// <param name="externalVariableMap">A dictionary used for mapping externals to integers in all lines in this script</param>
        /// <param name="staticTypes">Statically known types for variables</param>
        /// <returns>A function which runs this line of code. Accepts two sections of memory, internal variables and external variables. Returns the line number to go to next</returns>
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(
            this Line line,
            int lineNumber,
            int maxLines,
            int? maxStringLength,
            IReadonlyInternalsMap internalVariableMap,
            IReadonlyExternalsMap externalVariableMap,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            // Create an emitter for a dynamic method
            var emitter = Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>>.NewDynamicMethod(strictBranchVerification: true);

            // Compile code into the emitter
            line.Compile(emitter, lineNumber, maxLines, maxStringLength, internalVariableMap, externalVariableMap, staticTypes);

            // Finally convert the IL into a runnable C# method for this line
            var del = emitter.CreateDelegate();
            return del;
        }

        /// <summary>
        /// Compile all the lines of a Yolol program into a `CompiledProgram` object
        /// </summary>
        /// <param name="ast"></param>
        /// <param name="externals"></param>
        /// <param name="maxLines"></param>
        /// <param name="maxStringLength"></param>
        /// <param name="staticTypes"></param>
        /// <returns></returns>
        public static CompiledProgram Compile(
            this Program ast,
            ExternalsMap externals,
            int maxLines = 20,
            int? maxStringLength = null,
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
                compiledLines[i] = new JitLine(line.Compile(lineNum, maxLines, maxStringLength, internals, externals, staticTypes));
            }

            return new CompiledProgram(internals, compiledLines);
        }

        /// <summary>
        /// Compile a line of Yolol into the given IL emitter
        /// </summary>
        /// <param name="line"></param>
        /// <param name="emit"></param>
        /// <param name="lineNumber"></param>
        /// <param name="maxLines"></param>
        /// <param name="maxStringLength"></param>
        /// <param name="internalVariableMap"></param>
        /// <param name="externalVariableMap"></param>
        /// <param name="staticTypes"></param>
        public static void Compile(
            this Line line,
            Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>> emit,
            int lineNumber,
            int maxLines,
            int? maxStringLength,
            IReadonlyInternalsMap internalVariableMap,
            IReadonlyExternalsMap externalVariableMap,
            IReadOnlyDictionary<VariableName, Type>? staticTypes = null
        )
        {
            using (var emitter = new OptimisingEmitter<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(emit))
            {
                void EmitFallthroughCalc()
                {
                    if (lineNumber == maxLines)
                        emitter.LoadConstant(1);
                    else
                        emitter.LoadConstant(lineNumber + 1);
                }

                // Special case for totally empty lines
                if (line.Statements.Statements.Count == 0)
                {
                    EmitFallthroughCalc();
                    emitter.Return();
                    return;
                }

                // Begin an exception block to catch Yolol runtime errors
                var exBlock = emitter.BeginExceptionBlock();

                // Create a local to store the return address from inside the try/catch block
                var retAddr = emitter.DeclareLocal<int>("ret_addr", initializeReused: false);

                // Store the default return address to go to
                EmitFallthroughCalc();
                emitter.StoreLocal(retAddr);

                // Create a label which any `goto` statements can use. They drop their destination PC on the stack and then jump to this label
                var gotoLabel = emitter.DefineLabel2("encountered_goto");

                var types = new StaticTypeTracker(staticTypes);

                // Create a memory accessor which manages reading and writing the memory arrays
                using (var accessor = new ArraySegmentMemoryAccessor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(
                    emitter,
                    1,
                    0,
                    internalVariableMap,
                    externalVariableMap,
                    types
                ))
                {
                    accessor.EmitLoad(line);

                    // Convert the entire line into IL
                    var converter = new ConvertLineVisitor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(emitter, maxLines, accessor, exBlock, gotoLabel, types, maxStringLength);
                    converter.Visit(line);

                    // When a line finishes (with no gotos in the line) call flow eventually reaches here. Go to the next line.
                    emitter.Leave(exBlock);

                    // Create a block to handle gotos. The destination will already be on the stack, so just return
                    if (gotoLabel.IsUsed)
                    {
                        emitter.MarkLabel(gotoLabel);
                        emitter.StoreLocal(retAddr);
                        emitter.Leave(exBlock);
                    }

                    // Catch all execution exceptions and return the appropriate next line number to fall through to
                    var catchBlock = emitter.BeginCatchBlock<ExecutionException>(exBlock);
#if DEBUG
                    using (var ex = emitter.DeclareLocal(typeof(ExecutionException), initializeReused: false))
                    {
                        emitter.StoreLocal(ex);
                        emitter.WriteLine("execution exception: {0}", ex);
                    }
#else
                    emitter.Pop();
#endif

                    // Close the exception block which was wrapping the entire method
                    emitter.EndCatchBlock(catchBlock);
                    emitter.EndExceptionBlock(exBlock);
                }

                // Load the return address from inside the catch block
                emitter.LoadLocal(retAddr);
                emitter.Return();

                emitter.Optimise();

#if DEBUG
                Console.WriteLine($"// {line}");
                Console.WriteLine(emitter.ToString());
                Console.WriteLine($"------------------------------");
#endif
            }
        }
    }
}
