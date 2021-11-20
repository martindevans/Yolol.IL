namespace Fuzzer
{
    public readonly struct StatusReport
    {
        public int Thread { get; }
        public int TotalIters { get; }
        public FailureReport? Failure { get; }

        public StatusReport(int thread, int totalIters, FailureReport? failure)
        {
            Thread = thread;
            TotalIters = totalIters;
            Failure = failure;
        }

        public override string ToString()
        {
            if (Failure == null)
                return $"Thread:{Thread} Iters:{TotalIters}";
            else
                return $"Thread:{Thread} Error:{Failure}";
        }
    }
}
