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
            if (type == typeof(int))
                return StackType.Int32;

            if (type == typeof(Number))
                return StackType.YololNumber;
            if (type == typeof(YString))
                return StackType.YololString;
            if (type == typeof(Value))
                return StackType.YololValue;

            if (type == typeof(StaticError))
                return StackType.StaticError;

            throw new ArgumentOutOfRangeException($"Unknown type `{type.Name}`, cannot convert to `StackType`");
        }

        public static StackType ToStackType(this Execution.Type type)
        {
            return type switch {
                Execution.Type.Number => StackType.YololNumber,
                Execution.Type.String => StackType.YololString,
                Execution.Type.Error => StackType.StaticError,
                _ => throw new ArgumentOutOfRangeException(nameof(type), $"Cannot convert type `{type}` into StackType")
            };
        }
    }
}
