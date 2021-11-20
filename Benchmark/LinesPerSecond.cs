﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly string[] _program = new[] {
            "a=\"_1\" b=\"__1\" c=\"____\" d=c+c c+=1 e=d+d d+=1 f=e+e e+=1 g=f+f f+=1 h=g+g",
            "g+=1 i=h+h h+=1 j=i+i i+=1 j+=1",
            "l=1023-:i m=l>511 l%=512 n=l>255 l%=256 o=l>127 l%=128 p=l>63 l%=64 q=l>31",
            "l%=32 r=l>15 l%=16 s=l>7 l%=8 t=l>3 l%=4 u=l>1 l%=2 k=j-m-j+i-n-i+h-o-h+g-p-g+f",
            "k=k-q-f+e-r-e+d-s-d+c-t-c+b-u-b+a-l-a v=k+:s v-=k :o=v-v-- :done++ goto3"
        };

        private readonly CompiledProgram _compiled;
        private readonly Value[] _externals;
        private readonly Value[] _internals;
        private readonly ExternalsMap _externalsMap;

        public LinesPerSecond()
        {
            // Pin to one core to reduce benchmarking noise.
            var proc = Process.GetCurrentProcess();
            if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
            {
                const int affinity = 1 << 5;
                proc.ProcessorAffinity = (IntPtr)affinity;
            }

            // Make it high priority to reduce benchmarking noise.
            proc.PriorityClass = ProcessPriorityClass.High;

            var ast = Parse(_program);

            var staticTypes = new Dictionary<VariableName, Yolol.Execution.Type> {
                //{ new VariableName("b"), Yolol.Execution.Type.Number },
            };

            _externalsMap = new ExternalsMap();
            var timer = new Stopwatch();
            timer.Start();
            _compiled = ast.Compile(_externalsMap, 20, 1024, staticTypes, true);
            Console.WriteLine($"Compiled in: {timer.Elapsed.TotalMilliseconds}ms");
            
            _externals = new Value[_externalsMap.Count];
            Array.Fill(_externals, Number.Zero);
            _externals[_externalsMap[new VariableName(":i")]] = (Number)126;

            _internals = new Value[_compiled.InternalsMap.Count];
            Array.Fill(_internals, Number.Zero);

            _externals[_externalsMap[new VariableName(":s")]] = new Value("Hello Cylon");
            _externals[_externalsMap[new VariableName(":i")]] = (Number)6;
        }

        private static Yolol.Grammar.AST.Program Parse(params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }

        public void Run()
        {
            var oidx = _externalsMap[new VariableName(":o")];

            const int iterations = 50000;

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

                Console.WriteLine($"{lps:#,##0.00} l/s | {avg:#,##0.00} avg | {stdDev:#,##0.00} dev | o: {_externals[oidx]}");
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

            throw new NotImplementedException();
        }
    }
}
