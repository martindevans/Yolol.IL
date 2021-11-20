using System;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CommandLine;

namespace Fuzzer
{
    public class Options
    {
        [Option('t', "threads", Required = false, HelpText = "Set number of fuzzer threads to use.", Default = 1)]
        public int ThreadCount { get; set; }

        [Option('i', "iters", Required = false, HelpText = "Numbers of iterations to run each generated program for.", Default = 128)]
        public int Iters { get; set; }
    }

    public class Program
    {
        private static async Task Main(string[] args)
        {
            await Parser
                .Default
                .ParseArguments<Options>(args)
                .WithParsedAsync(Run);
        }

        private static async Task Run(Options options)
        {
            // Create a channel for receiving reports from the processing threads.
            var channel = Channel.CreateUnbounded<StatusReport>(new UnboundedChannelOptions {
                SingleReader = true
            });

            // Start of all the fuzzing tasks (don't await this, we just want to task to run)
            #pragma warning disable 4014
            Task.Run(() => {
            #pragma warning restore 4014
                Task.WaitAll(Enumerable
                     .Range(0, options.ThreadCount)
                     .Select(_ => Task.Run(async () => await FuzzForever(options.Iters, channel.Writer)))
                     .ToArray()
                );
                channel.Writer.Complete();
            });

            // Read all reports from the channel
            while (await channel.Reader.WaitToReadAsync()) 
                ProcessReport(await channel.Reader.ReadAsync());
        }

        private static long _totalPrograms = 0;
        private static long _totalFailures = 0;

        private static void ProcessReport(StatusReport report)
        {
            _totalPrograms += report.TotalIters;
            Console.Title = $"Tested:{_totalPrograms} Failed:{_totalFailures}";

            if (report.Failure != null)
            {
                _totalFailures++;
                Console.WriteLine(report.Failure);
            }
        }

        private static async Task FuzzForever(int itersPerRun, ChannelWriter<StatusReport> output)
        {

            var fuzz = new Fuzz();
            var iters = 0;
            var thrd = Thread.CurrentThread.ManagedThreadId;
            while (true)
            {
                iters++;
                try
                {
                    fuzz.Run(itersPerRun);

                    if (iters >= 5)
                    {
                        output.TryWrite(new StatusReport(thrd, iters, null));
                        iters = 0;
                    }
                }
                catch (Exception e)
                {
                    var r = new StatusReport(Thread.CurrentThread.ManagedThreadId, iters, new FailureReport(e));
                    await output.WriteAsync(r);
                    break;
                }
            }
        }
    }
}
