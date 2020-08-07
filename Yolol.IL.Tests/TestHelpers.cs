using Sigil;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Extensions;

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

        public static (EasyMachineState, int) Test(string line, int lineNumber = 1)
        {
            var internals = new Dictionary<string, int>();
            var externals = new Dictionary<string, int>();

            try
            {
                var ast = Parse(line);
                var compiled = ast.Lines[0].Compile(lineNumber, 20, internals, externals);

                var i = new Value[internals.Count];
                var e = new Value[externals.Count];

                var r = compiled(i, e);

                return (new EasyMachineState(i, e, internals, externals), r);
            }
            catch (SigilVerificationException e)
            {
                Console.WriteLine(e.GetDebugInfo());
                throw;
            }
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
