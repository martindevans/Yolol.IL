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
        //    "z=1 :a=2 ++bc bc++ ++:bc :bc++ ++a c++ goto :a+:b"
        //};

        private readonly string[] _program = new[] {
            ":done++ b=97 c=89",
            ":o++ :done++",
            ":done++ x-- x=\"abc\" x=atan x",
            "i=:i _=(i/3%1==0)*i/3>1+(i/5%1==0)*i/5>1+(i/7%1==0)*i/7>1 a=i/11%1==0 x=atan x",
            "_+=a*i/11>1+(i/13%1==0)*i/13>1+(i/17%1==0)*i/17>1+(i/19%1==0)*i/19>1 x=atan x",
            "_+=(i/23%1==0)*i/23>1+(i/29%1==0)*i/29>1+(i/31%1==0)*i/31>1a=i/37%1==0 x=atan x",
            "_+=a*i/37>1+(i/41%1==0)*i/41>1+(i/43%1==0)*i/43>1+(i/47%1==0)*i/47>1 x=atan x",
            "_+=(i/53%1==0)*i/53>1+(i/59%1==0)*i/59>1+(i/61%1==0)*i/61>1a=i/67%1==0 x=atan x",
            "_+=a*i/67>1+(i/71%1==0)*i/71>1+(i/73%1==0)*i/73>1+(i/79%1==0)*i/79>1 x=atan x",
            "_+=(i/83%1==0)*i/83>1+(i/c%1==0)*i/c>1+(i/b%1==0)*i/b>1:o+=_<1:done++ x=atan x",
            "a=1 if _ then a=2 else a=\"2\" end _/=a",
            "z=:o :done++goto4",
        };

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

        //private readonly string[] _program = {
        //    "z++",
        //};

        private readonly CompiledProgram _compiled;
        private readonly Value[] _externals;
        private readonly Value[] _internals;
        private readonly ExternalsMap _externalsMap;

        public LinesPerSecond()
        {
            // Pin to one core, to reduce noise as it migrates from core to core
            var proc = Process.GetCurrentProcess();
            const int affinity = 1 << 5;
            proc.ProcessorAffinity = (IntPtr)affinity;
            proc.PriorityClass = ProcessPriorityClass.High;

            var ast = Parse(_program);

            var staticTypes = new Dictionary<VariableName, Yolol.Execution.Type> {
                //{ new VariableName("b"), Yolol.Execution.Type.Number },
                //{ new VariableName("c"), Yolol.Execution.Type.Number },
                //{ new VariableName(":o"), Yolol.Execution.Type.Number },
                //{ new VariableName(":done"), Yolol.Execution.Type.Number },
                //{ new VariableName("i"), Yolol.Execution.Type.Number },
                //{ new VariableName("_"), Yolol.Execution.Type.Number },
                //{ new VariableName("z"), Yolol.Execution.Type.Number },

                //{ new VariableName("z"), Yolol.Execution.Type.Number },
                //{ new VariableName(":a"), Yolol.Execution.Type.Number },
                //{ new VariableName("bc"), Yolol.Execution.Type.Number },
                //{ new VariableName(":bc"), Yolol.Execution.Type.Number },
                //{ new VariableName("a"), Yolol.Execution.Type.Number },
                //{ new VariableName("c"), Yolol.Execution.Type.Number },
                //{ new VariableName(":b"), Yolol.Execution.Type.Number },
            };

            _externalsMap = new ExternalsMap();
            var timer = new Stopwatch();
            timer.Start();
            _compiled = ast.Compile(_externalsMap, 20, null, staticTypes);
            Console.WriteLine($"Compiled in: {timer.Elapsed.TotalMilliseconds}ms");
            
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, Number.Zero);
            _externals[_externalsMap[":i"]] = (Number)126;

            _internals = new Value[_compiled.InternalsMap.Count];
            Array.Fill(_internals, Number.Zero);
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
            var zidx = _compiled.InternalsMap["z"];

            const int iterations = 10000000;

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

                const int c = 10;
                var s = samples.AsEnumerable().Reverse().Take(c).ToList();
                var avg = s.Average();
                var sum = s.Sum(d => Math.Pow(d - avg, 2));
                var stdDev = Math.Sqrt(sum / (c - 1));

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | z: {_internals[zidx]}");
            }

            //Console.WriteLine("## Externals");
            //foreach (var (key, value) in _externalsMap)
            //    Console.WriteLine($"{key} {_externals[value]}");

            //Console.WriteLine("## Internals");
            //foreach (var (key, value) in _compiled.InternalsMap)
            //    Console.WriteLine($"{key} {_internals[value]}");
        }

        public void RunCompiled(int iterations)
        {
            for (var i = 0; i < iterations; i++)
                _compiled.Tick(_internals, _externals);
        }

        public void RunRewritten(int iterations)
        {
            // a="" b=1 l=0 z++ a=-""
            // b*=2 c=""+b d=c
            // d-- l++ goto3
            // a+=b if l<25 then goto2 end
            // a-- l-- goto5/(x>0)
            // goto1

            // a  0
            // b  1
            // c  2
            // d  3
            // l  4
            // z  5
            // x  6

            var pc = 1;
            for (var i = 0; i < iterations; i++)
            {
                switch (pc)
                {
                    case 1:
                        // a="" b=1 l=0 z++ a=-""
                        _internals[0] = new Value(new YString(""));
                        _internals[1] = (Number)0;
                        _internals[4] = (Number)0;
                        _internals[5]++;
                        // Static error to next line
                        pc = 2;
                        break;

                    case 2:
                        // b*=2 c=""+b d=c
                        _internals[1] *= (Value)2;
                        _internals[2] = "" + _internals[1];
                        _internals[3] = _internals[2];
                        pc = 3;
                        break;

                    case 3:
                        // d-- l++ goto3
                        var d = _internals[3];
                        if (WillDecThrow(d))
                            pc = 4;
                        else
                        {
                            _internals[3]--;
                            _internals[4]++;
                            pc = 3;
                        }

                        break;

                    case 4:
                        // a+=b if l<25 then goto2 end
                        _internals[0] += _internals[1];
                        if (_internals[4] < (Value)25)
                            pc = 2;
                        else
                            pc = 5;
                        break;

                    case 5:
                        // a-- l-- goto5/(x>0)
                        if (WillDecThrow(_internals[0]))
                        {
                            pc = 6;
                            break;
                        }
                        _internals[0]--;

                        if (WillDecThrow(_internals[4]))
                        {
                            pc = 6;
                            break;
                        }
                        _internals[4]--;

                        pc = _internals[6] <= (Value)0 ? 6 : 5;
                        break;

                    case 6:
                        pc = 1;
                        break;
                }
            }
        }

        private static bool WillDecThrow(Value value)
        {
            return value.Type == Yolol.Execution.Type.String && value.String.Length == 0;
        }
    }
}
