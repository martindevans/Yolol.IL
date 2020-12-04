using System;
using System.Collections.Generic;
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
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(
            this Line line,
            int lineNumber,
            int maxLines,
            Dictionary<string, int> internalVariableMap,
            Dictionary<string, int> externalVariableMap,
            IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes = null
        )
        {
            var emitter = Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>>.NewDynamicMethod();

            var exBlock = emitter.BeginExceptionBlock();

            // Convert `ArraySegment<Value>` to `Span<Value>` just once and store it in a local field
            using var internals = ConvertSpan(emitter, 0);
            using var externals = ConvertSpan(emitter, 1);

            // Create a local to store the return address from inside the try/catch block
            var retAddr = emitter.DeclareLocal<int>();

            // Create a label which any `goto` statements can use. They drop their destination PC on the stack and then jump to this label
            var gotoLabel = emitter.DefineLabel();

            // Create a label which any runtime errors can use. They jump here after emptying the stack
            var runtimeErrorLabel = emitter.DefineLabel();

            // Create a label which marks the end of the line, code reaching here falls through to the next line
            var eolLabel = emitter.DefineLabel();

            // Define a label that jumps to the end of the try/catch block
            var exitTry = emitter.DefineLabel();

            // Convert the entire line into IL
            var converter = new ConvertLineVisitor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(emitter, maxLines, internalVariableMap, externalVariableMap, gotoLabel, runtimeErrorLabel, staticTypes, internals, externals);
            converter.Visit(line);
            emitter.Branch(eolLabel);

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

            // Load the return address from inside the catch block
            emitter.LoadLocal(retAddr);
            emitter.Return();

            // Sanity check before returning result
            if (!converter.IsTypeStackEmpty)
                throw new InvalidOperationException("Type stack is not empty after conversion");

            // Finally convert the IL into a runnable C# method for this line
            return emitter.CreateDelegate();
        }

        private static Local ConvertSpan<T>(Emit<T> emitter, ushort arg)
        {
            emitter.LoadArgument(arg);
            emitter.Call(typeof(Runtime).GetMethod(nameof(Runtime.GetSpan), BindingFlags.Public | BindingFlags.Static));
            var local = emitter.DeclareLocal(typeof(Span<Value>));
            emitter.StoreLocal(local);

            return local;
        }
    }
}
