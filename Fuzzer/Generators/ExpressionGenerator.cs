﻿using System;
using System.Linq;
using System.Text;
using MoreLinq.Extensions;
using Yolol.Execution;
using Yolol.Grammar.AST.Expressions;
using Yolol.Grammar.AST.Expressions.Binary;
using Yolol.Grammar.AST.Expressions.Unary;

namespace Fuzzer.Generators
{
    public class ExpressionGenerator
    {
        private readonly Random _random;

        private readonly VariableNameGenerator _variable;

        public ExpressionGenerator(Random random)
        {
            _random = random;
            _variable = new VariableNameGenerator(random);
        }

        public BaseExpression Generate()
        {
            return _random.Next(0, 100) switch {
                0 => new ConstantNumber(RandomNumber()),
                1 => new ConstantString(RandomString()),

                2 => new Abs(Generate()),
                3 => new ArcCos(Generate()),
                4 => new ArcSine(Generate()),
                5 => new ArcTan(Generate()),
                6 => new PreDecrement(_variable.Generate()),
                7 => new PreIncrement(_variable.Generate()),
                8 => new PostDecrement(_variable.Generate()),
                9 => new PostIncrement(_variable.Generate()),
                10 => new Bracketed(Generate()),
                11 => new Cosine(Generate()),
                12 => new Factorial(Generate()),
                13 => new Negate(Generate()),
                14 => new Not(Generate()),
                15 => new Sqrt(Generate()),
                16 => new Sine(Generate()),
                17 => new Cosine(Generate()),
                18 => new Tangent(Generate()),

                19 => new Add(Generate(), Generate()),
                20 => new And(Generate(), Generate()),
                21 => new Divide(Generate(), Generate()),
                22 => new EqualTo(Generate(), Generate()),
                23 => new Exponent(Generate(), Generate()),
                24 => new GreaterThan(Generate(), Generate()),
                25 => new GreaterThanEqualTo(Generate(), Generate()),
                26 => new LessThan(Generate(), Generate()),
                27 => new LessThanEqualTo(Generate(), Generate()),
                28 => new Modulo(Generate(), Generate()),
                29 => new Multiply(Generate(), Generate()),
                30 => new NotEqualTo(Generate(), Generate()),
                31 => new Or(Generate(), Generate()),
                32 => new Subtract(Generate(), Generate()),

                _ => new Yolol.Grammar.AST.Expressions.Variable(_variable.Generate()),
            };

            return new ConstantNumber(Number.One);
        }

        private Number RandomNumber()
        {
            return Number.FromRaw(_random.Next());
        }

        private string RandomString()
        {
            const string characters = "abcdefghijklmnopqrstuvwxyz";

            var str = new StringBuilder();
            Enumerable.Range(0, _random.Next(20))
                      .Select(i => characters[_random.Next(characters.Length)])
                      .ForEach(c => str.Append(c));

            return str.ToString();
        }
    }
}
