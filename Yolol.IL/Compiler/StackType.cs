using System;

namespace Yolol.IL.Compiler
{
    [Flags]
    internal enum StackType
    {
        YololNumber = 1,
        YololValue = 2,

        String = 4,
        Bool = 8,

        NumericTypes = YololNumber | Bool,
    }
}
