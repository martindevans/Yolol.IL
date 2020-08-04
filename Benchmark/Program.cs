using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = DefaultConfig.Instance.With(ConfigOptions.DisableOptimizationsValidator);
            var summary = BenchmarkRunner.Run<CompareInterpreter>(config);

            //var lps = new LinesPerSecond();
            //lps.Run();
        }
    }
}
