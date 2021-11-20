using System;

namespace Fuzzer
{
    public class FailureReport
    {
        private readonly Exception _e;

        public FailureReport(Exception e)
        {
            _e = e;
        }

        public override string ToString()
        {
            return _e.ToString();
        }
    }
}
