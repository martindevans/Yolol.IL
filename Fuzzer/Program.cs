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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await FuzzForever(options.Iters, channel.Writer);
                channel.Writer.Complete();
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

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
            var thrd = Environment.CurrentManagedThreadId;
            while (true)
            {
                iters++;
                try
                {
                    fuzz.Run(itersPerRun);
                    output.TryWrite(new StatusReport(thrd, 1, null));
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
