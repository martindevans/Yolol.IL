using Sigil;
using System;
using Yolol.Execution;

namespace Yolol.IL.Tests
{
    public static class TestHelpers
    {
        public static (MachineState, int) Test(string line, int lineNumer = 1)
        {
            try
            {
                var tokens = Grammar.Tokenizer.TryTokenize(line).Value;
                var ast = Grammar.Parser.TryParseLine(tokens).Value;
                var compiled = ast.Compile(lineNumer);

                var state = new MachineState(new NullDeviceNetwork());
                var r = compiled(state);

                return (state, r);
            }
            catch (SigilVerificationException e)
            {
                Console.WriteLine(e.GetDebugInfo());
                throw;
            }
        }
    }
}
