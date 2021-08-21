using Yolol.IL.Compiler;

namespace Yolol.IL
{
    public readonly struct LineResult
    {
        public readonly int ProgramCounter;
        public readonly ChangeSet ChangeSet;

        public LineResult(int programCounter, ChangeSet changed)
        {
            ProgramCounter = programCounter;
            ChangeSet = changed;
        }
    }
}
