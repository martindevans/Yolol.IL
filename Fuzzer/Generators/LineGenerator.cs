using System;
using Yolol.Grammar.AST;

namespace Fuzzer.Generators
{
    public class LineGenerator
    {
        private readonly StatementListGenerator _statements;

        public LineGenerator(Random random)
        {
            _statements = new StatementListGenerator(random, 10);
        }

        public Line Generate()
        {
            return new Line(_statements.Generate());
        }
    }
}
