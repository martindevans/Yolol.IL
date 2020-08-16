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
        public static Func<ArraySegment<Value>, ArraySegment<Value>, int> Compile(this Line line, int lineNumber, int maxLines, Dictionary<string, int> internalVariableMap, Dictionary<string, int> externalVariableMap, IReadOnlyDictionary<VariableName, Execution.Type>? staticTypes = null)
        {
            var emitter = Emit<Func<ArraySegment<Value>, ArraySegment<Value>, int>>.NewDynamicMethod();

            // Convert `ArraySegment<Value>` to `Span<Value>` just once and store it in a local field
            using var internals = ConvertSpan(emitter, 0);
            using var externals = ConvertSpan(emitter, 1);
            
            // Convert the entire line into IL
            var gotoLabel = emitter.DefineLabel();
            var converter = new ConvertLineVisitor<Func<ArraySegment<Value>, ArraySegment<Value>, int>>(emitter, maxLines, internalVariableMap, externalVariableMap, gotoLabel, staticTypes, internals, externals);
            converter.Visit(line);

            // If there were no gotos eventually flow will fall through to here
            // goto the next line. Drop a label here to work around this code being unreachable on lines that do have a goto.
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
            return d;
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
