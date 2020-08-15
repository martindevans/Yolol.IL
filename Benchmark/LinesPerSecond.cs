using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class LinesPerSecond
    {
        readonly string[] _program = new[] {
            "r = 715237",
            "i = 0 A = 1664524+cos(0)    M = 2^32",
            "s = 0 C = 1013904223 F = 2^16 b = \"str\"" ,
            "r=((r*A)+C)%M x=(r%F)/F r=((r*A)+C)%M y=(r%F)/F",
            "s++ i+=(x*x+y*y)<1 :pi=4*(i/s)" +
            ":pi -= \"t\" goto 1",
        };

        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, int>[] _compiledLines;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        private readonly Dictionary<string, int> _internalsMap;
        private readonly Dictionary<string, int> _externalsMap;

        public LinesPerSecond()
        {
            var ast = Parse(_program);

            var staticTypes = new Dictionary<VariableName, Yolol.Execution.Type> {
                { new VariableName("r"), Yolol.Execution.Type.Number },
                { new VariableName("i"), Yolol.Execution.Type.Number },
                { new VariableName("A"), Yolol.Execution.Type.Number },
                { new VariableName("M"), Yolol.Execution.Type.Number },
                { new VariableName("s"), Yolol.Execution.Type.Number },
                { new VariableName("C"), Yolol.Execution.Type.Number },
                { new VariableName("F"), Yolol.Execution.Type.Number },
                { new VariableName("b"), Yolol.Execution.Type.String },
                { new VariableName("x"), Yolol.Execution.Type.Number },
                { new VariableName("y"), Yolol.Execution.Type.Number },
            };

            _internalsMap = new Dictionary<string, int>();
            _externalsMap = new Dictionary<string, int>();
            _compiledLines = new Func<ArraySegment<Value>, ArraySegment<Value>, int>[ast.Lines.Count];
            for (var i = 0; i < ast.Lines.Count; i++)
            {
                _compiledLines[i] = ast.Lines[i].Compile(i + 1, 20, _internalsMap, _externalsMap, staticTypes);
            }

            _internals = new Value[_internalsMap.Count];
            Array.Fill(_internals, new Value(0));
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, new Value(0));
        }

        private static Yolol.Grammar.AST.Program Parse([NotNull] params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        public void Run()
        {
            const int iterations = 2000000;
            var pc = 0;

            var samples = new List<double>();
            var timer = new Stopwatch();
            while (true)
            {
                timer.Restart();

                RunCompiled(iterations);
                //_externals[0] = RunRewritten(iterations);

                timer.Stop();

                var lps = iterations / timer.Elapsed.TotalSeconds;
                samples.Add(lps);

                var avg = samples.AsEnumerable().Reverse().Take(10).Average();
                var sum = samples.Sum(d => Math.Pow(d - avg, 2));
                var stdDev = Math.Sqrt(sum / (samples.Count - 1));

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | {_externals[0]} pi");
            }
        }

        public void RunCompiled(int iterations)
        {
            var pc = 0;
            for (var i = 0; i < iterations; i++)
                pc = _compiledLines[pc](_internals, _externals) - 1;
        }

        public Value RunRewritten(int iterations)
        {
            Value pi = (Number)0;

            for (var j = 0; j < iterations; j += 6)
            {
                var r = (Number)715237;

                var i = (Number)0;
                var A = 1664524 + ((Number)0).Cos();
                var M = (Number)int.MaxValue;

                var s = (Number)0;
                var C = (Number)1013904223;
                var F = (Number)ushort.MaxValue;
                var b = new YString("str");

                r = ((r * A) + C) % M;
                var x = (r % F) / F;
                r = ((r * A) + C) % M;
                var y = (r % F) / F;

                s += 1;
                i = i + ((x * x + y * y) < 1);
                pi = 4 * (i / s);
                pi -= "t";
            }

            return pi;
        }
    }
}
