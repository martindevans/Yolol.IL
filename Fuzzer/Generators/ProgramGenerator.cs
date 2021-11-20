using System;
using System.Linq;

namespace Fuzzer.Generators
{
    public class ProgramGenerator
    {
        private readonly Random _random;

        public ProgramGenerator(Random random)
        {
            _random = random;
        }

        public Yolol.Grammar.AST.Program Generate()
        {
            var l = new LineGenerator(_random);

            return new Yolol.Grammar.AST.Program(
                Enumerable.Range(0, _random.Next(1, 21)).Select(_ => l.Generate())
            );
        }
    }
}
