using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class LinesPerSecond
    {
        // a=1 b=2 c=3 d=4 e=5 f=6 g=7 h=8
        // z=a*b*c*d y=e*f*g*h
        // a+=1 b++ c+=1 d++
        // goto 2
        private readonly string[] _program = {
            "a=1 b=2 c=3 d=4 e=5 f=6 g=7 h=8",
            "x=a*c-b*d y=e*f-g*h z=x+y",
            "a++ b++ c++ d++ e++ f++ g++ h++ a++ b++ c++ d++ e++ f++ g++ h++ a++ b++ c++ d++ e++ f++ g++ h++ a++ b++ c++ d++ e++ f++ g++ h++",
            "goto2"
        };

        private readonly Func<ArraySegment<Value>, ArraySegment<Value>, int>[] _compiledLines;
        private readonly Value[] _internals;
        private readonly Value[] _externals;

        private readonly InternalsMap _internalsMap;
        private readonly ExternalsMap _externalsMap;

        public LinesPerSecond()
        {
            var ast = Parse(_program);

            var staticTypes = new Dictionary<VariableName, Yolol.Execution.Type> {
                { new VariableName("a"), Yolol.Execution.Type.Number },
                { new VariableName("b"), Yolol.Execution.Type.Number },
                { new VariableName("c"), Yolol.Execution.Type.Number },
                { new VariableName("d"), Yolol.Execution.Type.Number },
                { new VariableName("e"), Yolol.Execution.Type.Number },
                { new VariableName("f"), Yolol.Execution.Type.Number },
                { new VariableName("g"), Yolol.Execution.Type.Number },
                { new VariableName("h"), Yolol.Execution.Type.Number },
                { new VariableName("z"), Yolol.Execution.Type.Number },
            };

            _internalsMap = new InternalsMap();
            _externalsMap = new ExternalsMap();
            _compiledLines = ast.Compile(_internalsMap, _externalsMap, staticTypes);

            _internals = new Value[_internalsMap.Count];
            Array.Fill(_internals, new Value((Number)0));
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, new Value((Number)0));
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
            const int iterations = 5000000;

            var samples = new List<double>();
            var timer = new Stopwatch();
            while (true)
            {
                timer.Restart();

                RunCompiled(iterations);
                //RunRewritten(iterations);

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

        //public void RunRewritten(int iterations)
        //{
        //    // a="" b=1 l=0 z++ a=-""
        //    // b*=2 c=""+b d=c
        //    // d-- l++ goto3
        //    // a+=b if l<25 then goto2 end
        //    // a-- l-- goto5/(x>0)
        //    // goto1

        //    // a  0
        //    // b  1
        //    // c  2
        //    // d  3
        //    // l  4
        //    // z  5
        //    // x  6

        //    var pc = 1;
        //    for (var i = 0; i < iterations; i++)
        //    {
        //        switch (pc)
        //        {
        //            case 1:
        //                // a="" b=1 l=0 z++ a=-""
        //                _internals[0] = new Value(new YString(""));
        //                _internals[1] = (Number)0;
        //                _internals[4] = (Number)0;
        //                _internals[5]++;
        //                // Static error to next line
        //                pc = 2;
        //                break;

        //            case 2:
        //                // b*=2 c=""+b d=c
        //                _internals[1] *= 2;
        //                _internals[2] = "" + _internals[1];
        //                _internals[3] = _internals[2];
        //                pc = 3;
        //                break;

        //            case 3:
        //                // d-- l++ goto3
        //                var d = _internals[3];
        //                if (WillDecThrow(d))
        //                    pc = 4;
        //                else
        //                {
        //                    _internals[3]--;
        //                    _internals[4]++;
        //                    pc = 3;
        //                }

        //                break;

        //            case 4:
        //                // a+=b if l<25 then goto2 end
        //                _internals[0] += _internals[1];
        //                if (_internals[4] < 25)
        //                    pc = 2;
        //                else
        //                    pc = 5;
        //                break;

        //            case 5:
        //                // a-- l-- goto5/(x>0)
        //                if (WillDecThrow(_internals[0]))
        //                {
        //                    pc = 6;
        //                    break;
        //                }
        //                _internals[0]--;

        //                if (WillDecThrow(_internals[4]))
        //                {
        //                    pc = 6;
        //                    break;
        //                }
        //                _internals[4]--;

        //                pc = _internals[6] <= 0 ? 6 : 5;
        //                break;

        //            case 6:
        //                pc = 1;
        //                break;
        //        }
        //    }
        //}

        //private static bool WillDecThrow(Value value)
        //{
        //    return value.Type == Yolol.Execution.Type.String && value.String.Length == 0;
        //}
    }
}
