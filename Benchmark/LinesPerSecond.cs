using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class LinesPerSecond
    {
        //private readonly string[] _program = {
        //    "n=1 x=sqrt 24 y=4.899 if x!=y then goto19 end n++ ",
        //    "x=(sqrt 2) y=1.414 if x!=y then goto19 end n++ ",
        //    "x=(sqrt 7) y=2.645 if x!=y then goto19 end n++ ",
        //    "x=(sqrt 32199) y=179.440 if x!=y then goto19 end n++ ",
        //    "x=(sqrt 1000001) y=1000 if x!=y then goto19 end n++ ",
        //    "x=(sqrt 1000002) y=1000.001 if x!=y then goto19 end n++ ",
        //    "x=sqrt 9223372036854775.807 y=-9223372036854775.808 n++ goto19/(x!=y)",
        //    "x=(sqrt -3) y=-9223372036854775.808 if x!=y then goto19 end n++ ",
        //    "x=sqrt 9223372036854775 y=-9223372036854775.808 n++ goto19/(x!=y) ",
        //    "x=sqrt 9223372036854774.999 y=96038388.349 n++ goto19/(x!=y)",
        //    "",
        //    "",
        //    "",
        //    "",
        //    "",
        //    "",
        //    "if n != 11 then OUTPUT=\"Skipped: \"+(11-n)+\" tests\" goto 20 end",
        //    "OUTPUT=\"ok\" goto20",
        //    "OUTPUT=\"Failed test #\"+n+\" got: \"+x+\" but wanted: \"+y",
        //    "z=OUTPUT"
        //};

        private readonly string[] _program = {
            "n++",
        };

        private readonly CompiledProgram _compiled;
        private readonly Value[] _externals;

        public LinesPerSecond()
        {
            var ast = Parse(_program);

            var staticTypes = new Dictionary<VariableName, Yolol.Execution.Type> {
                //{ new VariableName("n"), Yolol.Execution.Type.Number },
                //{ new VariableName("x"), Yolol.Execution.Type.Number },
                //{ new VariableName("y"), Yolol.Execution.Type.Number },
                //{ new VariableName("OUTPUT"), Yolol.Execution.Type.String },
            };

            var externals = new ExternalsMap();
            _compiled = ast.Compile(externals, 20, staticTypes);

            _externals = new Value[externals.Count];
            Array.Fill(_externals, Number.Zero);
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

                RunCompiled(iterations, _externals);
                //RunRewritten(iterations);

                timer.Stop();

                var lps = iterations / timer.Elapsed.TotalSeconds;
                samples.Add(lps);

                const int c = 10;
                var s = samples.AsEnumerable().Reverse().Take(c).ToList();
                var avg = s.Average();
                var sum = s.Sum(d => Math.Pow(d - avg, 2));
                var stdDev = Math.Sqrt(sum / (c - 1));

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | z: {_compiled["z"]}");
            }
        }

        public void RunCompiled(int iterations, Value[] externals)
        {
            for (var i = 0; i < iterations; i++)
                _compiled.Tick(externals);
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
