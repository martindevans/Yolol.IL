using System;
using Yolol.Grammar;

namespace Fuzzer.Generators
{
    public class VariableNameGenerator
    {
        private readonly Random _random;

        public VariableNameGenerator(Random random)
        {
            _random = random;
        }

        public VariableName Generate()
        {
            const string names = "abcdefgh";//ijklmnopqrstuvwxyz";
            var name = names[_random.Next(names.Length)];

            if (_random.NextDouble() < 0.25)
                return new VariableName($":{name}");
            else
                return new VariableName($"{name}");
        }
    }
}
