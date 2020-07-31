using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;

namespace Benchmark
{
    public class LinesPerSecond
    {
        readonly string[] _program = new[] {
            "r = 715237",
            "i = 0 A = 1664525    M = 2^32",
            "s = 0 C = 1013904223 F = 2^16",
            "r=((r*A)+C)%M x=(r%F)/F r=((r*A)+C)%M y=(r%F)/F",
            "s++ i=i+((x*x+y*y)<1) :pi=4*(i/s) goto 4"
        };

        private readonly Func<Memory<Value>, Memory<Value>, int>[] _compiledLines;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        private readonly Dictionary<string, int> _internalsMap;
        private readonly Dictionary<string, int> _externalsMap;

        public LinesPerSecond()
        {
            var ast = Parse(_program);

            _internalsMap = new Dictionary<string, int>();
            _externalsMap = new Dictionary<string, int>();
            _compiledLines = new Func<Memory<Value>, Memory<Value>, int>[ast.Lines.Count];
            for (var i = 0; i < ast.Lines.Count; i++)
                _compiledLines[i] = ast.Lines[i].Compile(i + 1, 20, _internalsMap, _externalsMap);

            _internals = new Value[_internalsMap.Count];
            Array.Fill(_internals, new Value(0));
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, new Value(0));
        }

        public void Run()
        {
            const int iterations = 1000000;
            var pc = 0;

            var samples = new List<double>();
            var timer = new Stopwatch();
            while (true)
            {
                timer.Restart();

                for (var i = 0; i < iterations; i++)
                    pc = _compiledLines[pc](_internals, _externals) - 1;

                timer.Stop();

                var lps = iterations / timer.Elapsed.TotalSeconds;
                samples.Add(lps);

                var avg = samples.Average();
                var sum = samples.Sum(d => Math.Pow(d - avg, 2));
                var stdDev = Math.Sqrt(sum / (samples.Count - 1));

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | {_externals[0]} pi");
            }
        }

        private static Yolol.Grammar.AST.Program Parse([NotNull] params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }
    }
}
