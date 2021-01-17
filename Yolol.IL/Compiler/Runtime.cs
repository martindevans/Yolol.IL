using System;
using System.Runtime.CompilerServices;
using Yolol.Execution;

namespace Yolol.IL.Compiler
{
    internal abstract class Runtime
    {
        #region conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NumberToBool(Number num)
        {
            return num != Number.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolToNumber(bool b)
        {
            return (Number)b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value ErrorToValue(StaticError err)
        {
            // Casting a `StaticError` to a `Value` like this throws an ExecutionException 
            return err;
        }
        #endregion

        #region goto
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GotoValue(Value value, int maxLineNumber)
        {
            if (value.Type == Execution.Type.Number)
                return GotoNumber(value.Number, maxLineNumber);
            else
                throw new ExecutionException("Attempted to `goto` a `string`");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GotoNumber(Number value, int maxLineNumber)
        {
            return Math.Min(maxLineNumber, Math.Max(1, (int)value));
        }
        #endregion

        #region bool
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LogicalNot(bool value)
        {
            return !value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolNegate(bool value)
        {
            return -(Number)value;
        }
        #endregion
    }
}
