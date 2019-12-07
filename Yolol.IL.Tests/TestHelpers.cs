using Sigil;
using System;
using System.Collections.Generic;
using Yolol.Execution;
using Yolol.Grammar;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static (EasyMachineState, int) Test(string line, int lineNumber = 1)
        {
            var internals = new Dictionary<string, int>();
            var externals = new Dictionary<string, int>();

            try
            {
                var tokens = Grammar.Tokenizer.TryTokenize(line).Value;
                var ast = Grammar.Parser.TryParseLine(tokens).Value;
                var compiled = ast.Compile(lineNumber, 20, internals, externals);

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
