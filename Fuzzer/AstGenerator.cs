using System;
using Fuzzer.Generators;

namespace Fuzzer
{
    public class AstGenerator
    {
        private readonly Random _random;

        public AstGenerator(Random random)
        {
            _random = random;
        }

        public Yolol.Grammar.AST.Program Generate()
        {
            var ast = new ProgramGenerator(_random).Generate();
            Console.WriteLine($"\n=====\n{ast}");
            return ast;
        }
    }
}
