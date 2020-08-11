using System;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Type = System.Type;

namespace Yolol.IL.Extensions
{
    internal static class TypeExtensions
    {
        public static StackType ToStackType(this Type type)
        {
            if (type == typeof(bool))
                return StackType.Bool;
            if (type == typeof(Number))
                return StackType.YololNumber;
            if (type == typeof(YString))
                return StackType.YololString;
            if (type == typeof(Value))
                return StackType.YololValue;

            if (type == typeof(StaticError))
                return StackType.StaticError;

            throw new ArgumentException($"Unknown type `{type.Name}`, cannot convert to `StackType`");
        }
    }
}
