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
            "a=\"\" b=1 l=0 z++ a=-\"\"",
            "b*=2 c=\"\"+b d=c",
            "d-- l++ goto3",
            "a+=b if l<25 then goto2 end",
            "a-- l-- goto5",
            "goto1"
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
                //{ new VariableName("l"), Yolol.Execution.Type.Number },
                //{ new VariableName("z"), Yolol.Execution.Type.Number },
                //{ new VariableName("b"), Yolol.Execution.Type.Number },
                //{ new VariableName("a"), Yolol.Execution.Type.String },
                //{ new VariableName("c"), Yolol.Execution.Type.String },
                //{ new VariableName("d"), Yolol.Execution.Type.String },
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
            const int iterations = 10000;
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

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | z: {_internals[_internalsMap["z"]]}");
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

            // Yolol code is 6 lines, this code is all 6 lines. Increase the iters counter by 6 for every run to compensate.
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
