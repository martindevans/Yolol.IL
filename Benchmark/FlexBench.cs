using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Benchmark
{
    public class FlexBench
    {
        // How long to run batches for. Set according to patience.
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(2);

        private readonly DirectoryInfo _dir;

        public FlexBench(DirectoryInfo dir)
        {
            _dir = dir;
        }

        public void Run()
        {
            // Pin to one core to reduce benchmarking noise.
            var proc = Process.GetCurrentProcess();
            const int affinity = 1 << 5;
            proc.ProcessorAffinity = (IntPtr)affinity;

            // Make it high priority to reduce benchmarking noise.
            proc.PriorityClass = ProcessPriorityClass.High;

            // Run all files, ordered by their name
            foreach (var file in _dir.EnumerateFiles("*.yolol", SearchOption.AllDirectories).OrderBy(a => a.Name))
            {
                Console.WriteLine($"## {file.Name}");
                try
                {
                    RunTestFile(file);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"FAILED!\n{e}");
                }

                Console.WriteLine();
            }
        }

        private static void RunTestFile(FileInfo file)
        {
            var outerTimer = new Stopwatch();

            // Parse file
            var txt = File.ReadAllText(file.FullName);
            outerTimer.Start();
            var ast = Parse(txt);
            outerTimer.Stop();
            Console.WriteLine($" - Parsed:\t{outerTimer.Elapsed.TotalMilliseconds} ms");

            // Set up the compiler
            var externalsMap = new ExternalsMap();

            // Compile - program has 20 line, or however many are in the file. Whichever is more.
            outerTimer.Start();
            var compiled = ast.Compile(externalsMap, Math.Max(20, ast.Lines.Count));
            outerTimer.Stop();

            // Write out time to compile program
            Console.WriteLine($" - Compiled:\t{outerTimer.Elapsed.TotalMilliseconds} ms");

            // Set up runtime
            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, Number.Zero);
            var internals = new Value[compiled.InternalsMap.Count];
            Array.Fill(internals, Number.Zero);
            var outputIdx = externalsMap[new VariableName(":output")];

            var iterations = 500000;

            // Run the program for some time
            var totalLineCount = 0ul;
            var samples = new List<double>();
            outerTimer.Restart();
            while (outerTimer.Elapsed < Duration)
            {
                var innerTimer = new Stopwatch();
                innerTimer.Start();
                for (var i = 0; i < iterations; i++)
                    compiled.Tick(internals, externals);
                innerTimer.Stop();
                totalLineCount += (ulong)iterations;

                // Calculate how many lines-per-second that was and store in lps buffer
                var lps = iterations / innerTimer.Elapsed.TotalSeconds;
                samples.Add(lps);

                // Adjust iteration count so next batch takes about 0.5 seconds
                iterations = (int)(lps * 0.5);
            }

            // Calculate average LPS over the last 10 samples (this should cut out any weirdness in the first few samples as things are getting going)
            var avg = samples.Take(10).Average();

            // Calculate standard deviation over entire set
            var s = samples.AsEnumerable().ToList();
            var sum = s.Sum(d => Math.Pow(d - avg, 2));
            var stdDev = Math.Sqrt(sum / (samples.Count - 1));

            // Print out some stats
            Console.WriteLine($" - Total:\t{totalLineCount:#,#} lines");
            Console.WriteLine($" - Average:\t{avg:#,##0} l/s");
            Console.WriteLine($" - StdDev:\t{stdDev:#,##0}");

            // Verify final result
            var output = externals[outputIdx];
            if (output != "ok")
                Console.WriteLine($" - FAILED! Expected `:OUTPUT==\"ok\"`, got ``:OUTPUT==\"{output}\"``");
        }

        private static Yolol.Grammar.AST.Program Parse(params string[] lines)
        {
            var result = Parser.ParseProgram(string.Join("\n", lines));
            if (!result.IsOk)
                throw new ArgumentException($"Cannot parse program: {result.Err}");

            return result.Ok;
        }
    }
}
