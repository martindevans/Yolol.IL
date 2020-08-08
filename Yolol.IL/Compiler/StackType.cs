using System;

namespace Yolol.IL.Compiler
{
    [Flags]
    internal enum StackType
    {
        // The three basic Yolol types, represented by the `Number`, `YString` and `Value` types respectively
        YololNumber = 1,
        YololString = 2,
        YololValue = 4,

        // A plain C# bool, representing a number 1 or 0 (true/false)
        Bool = 8,
    }
}
