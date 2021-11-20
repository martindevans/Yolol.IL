using System;
using System.Linq;
using Yolol.Grammar.AST.Statements;

namespace Fuzzer.Generators
{
    public class StatementListGenerator
    {
        private readonly Random _random;
        private readonly int _count;

        public StatementListGenerator(Random random, int count)
        {
            _random = random;
            _count = count;
        }

        public StatementList Generate()
        {
            var statements = new StatementGenerator(_random);

            if (_random.NextDouble() < 0.01)
            {
                var v = new VariableNameGenerator(_random).Generate();
                var e = new ExpressionGenerator(_random);

                return new StatementList(
                    Enumerable.Range(0, _random.Next(1, _count)).Select(_ => new Assignment(v, e.Generate()))
                );
            }
            else
            {
                return new StatementList(
                    Enumerable.Range(0, _random.Next(1, _count)).Select(_ => statements.Generate())
                );
            }
        }
    }
}
