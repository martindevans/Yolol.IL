using System;
using Yolol.Execution;
using Type = System.Type;

namespace Yolol.IL.Compiler
{
    internal enum StackType
    {
        // The three basic Yolol types, represented by the `Number`, `YString` and `Value` types respectively
        YololNumber,
        YololString,
        YololValue,

        // A static error, break out now
        StaticError,

        // A plain dotnet bool, representing a number 1 or 0 (true/false)
        Bool,

        // A plain dotnet int32
        Int32
    }

    internal static class StackTypeExtensions
    {
        public static Type ToType(this StackType type)
        {
            return type switch {
                StackType.YololNumber => typeof(Number),
                StackType.YololString => typeof(YString),
                StackType.YololValue => typeof(Value),
                StackType.Bool => typeof(bool),
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
            };
        }
    }
}
