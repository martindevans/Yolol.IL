using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;
using Type = Yolol.Execution.Type;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static Grammar.AST.Program Parse([NotNull] params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        public static (EasyMachineState, int) Test(string line, int lineNumber = 1, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var internals = new InternalsMap();
            var externals = new ExternalsMap();

            //try
            //{
                var ast = Parse(line);
                var compiled = ast.Lines[0].Compile(lineNumber, 20, internals, externals, staticTypes);

                var i = new Value[internals.Count];
                Array.Fill(i, new Value((Number)0));
                var e = new Value[externals.Count];
                Array.Fill(e, new Value((Number)0));

                var r = compiled(i, e);

                return (new EasyMachineState(i, e, internals, externals), r);
            //}
            //catch (SigilVerificationException e)
            //{
            //    Console.WriteLine(e.GetDebugInfo());
            //    throw;
            //}
        }

        public static (EasyMachineState, int) Test(string[] lines, int iterations, IReadOnlyDictionary<VariableName, Type>? staticTypes = null)
        {
            var internals = new InternalsMap();
            var externals = new ExternalsMap();

            var i = new Value[internals.Count];
            Array.Fill(i, new Value((Number)0));
            var e = new Value[externals.Count];
            Array.Fill(e, new Value((Number)0));

            var prog = Parser.ParseProgram(string.Join("\n", lines)).Ok;
            var compiled = prog.Compile(internals, externals);

            var pc = 0;
            for (var j = 0; j < iterations; j++)
            {
                pc = compiled[pc](i, e) - 1;

                if (externals.TryGetValue("done", out var doneIndex))
                    if (e[doneIndex].ToBool())
                        break;
            }

            return (new EasyMachineState(i, e, internals, externals), pc);
        }
    }

    public class EasyMachineState
    {
        public Value[] Internals;
        public Value[] Externals;

        public Dictionary<string, int> InternalMap;
        public Dictionary<string, int> ExternalMap;

        public EasyMachineState(Value[] i, Value[] e, Dictionary<string, int> internals, Dictionary<string, int> externals)
        {
            Internals = i;
            Externals = e;
            InternalMap = internals;
            ExternalMap = externals;
        }

        internal Value GetVariable(string v)
        {
            var n = new VariableName(v);
            var (a, m) = n.IsExternal ? (Externals, ExternalMap) : (Internals, InternalMap);
            return a[m[v]];
        }
    }
}
