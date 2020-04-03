using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var config = DefaultConfig.Instance.With(ConfigOptions.DisableOptimizationsValidator);
            //var summary = BenchmarkRunner.Run<CompareInterpreter>(config);

            var lps = new LinesPerSecond();
            lps.Run();
        }
    }
}
