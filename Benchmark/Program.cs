using System.IO;

namespace Benchmark
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //var config = DefaultConfig.Instance;
            //var summary = BenchmarkRunner.Run<CompareInterpreter>(config);

            //var lps = new LinesPerSecond();
            //lps.Run();

            var flex = new FlexBench(new DirectoryInfo(Path.Combine(args)));
            flex.Run();
        }
    }
}
