using System;
using System.Linq;
using Yolol.Grammar;
using Yolol.Grammar.AST.Statements;
using MoreLinq;
using Yolol.Grammar.AST.Expressions.Unary;

namespace Fuzzer.Generators
{
    public class StatementGenerator
    {
        private readonly Random _random;

        private readonly ExpressionGenerator _expression;
        private readonly VariableNameGenerator _variable;
        private readonly StatementListGenerator _statements;

        public StatementGenerator(Random random)
        {
            _random = random;
            _expression = new ExpressionGenerator(random);
            _variable = new VariableNameGenerator(random);
            _statements = new StatementListGenerator(random, 5);
        }

        public BaseStatement Generate()
        {
            return (_random.Next(0, 6)) switch {
                0 => GenerateAssignment(),
                1 => GenerateCompoundAssignment(),
                2 => new EmptyStatement(),
                3 => GenerateExpressionWrapper(),
                4 => GenerateGoto(),
                5 => GenerateIf(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private BaseStatement GenerateIf()
        {
            return new If(
                _expression.Generate(),
                _statements.Generate(),
                _statements.Generate()
            );
        }

        private BaseStatement GenerateGoto()
        {
            return new Goto(_expression.Generate());
        }

        private BaseStatement GenerateExpressionWrapper()
        {
            return _random.Next(0, 4) switch {
                0 => new ExpressionWrapper(new PostDecrement(_variable.Generate())),
                1 => new ExpressionWrapper(new PostIncrement(_variable.Generate())),
                2 => new ExpressionWrapper(new PreDecrement(_variable.Generate())),
                3 => new ExpressionWrapper(new PreIncrement(_variable.Generate())),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private CompoundAssignment GenerateCompoundAssignment()
        {
            var op = _random.Next(0, 6) switch {
                0 => YololBinaryOp.Add,
                1 => YololBinaryOp.Divide,
                2 => YololBinaryOp.Exponent,
                3 => YololBinaryOp.Modulo,
                4 => YololBinaryOp.Multiply,
                5 => YololBinaryOp.Subtract,
                _ => throw new ArgumentOutOfRangeException()
            };

            return new CompoundAssignment(
                _variable.Generate(),
                op,
                _expression.Generate()
            );
        }

        private Assignment GenerateAssignment()
        {
            return new Assignment(_variable.Generate(), _expression.Generate());
        }
    }
}
