using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
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

        #region vectors
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExtractVectorNumbers(Vector256<long> values, ArraySegment<Value> destination, int index0, int index1, int index2, int index3)
        {
            destination[index0] = Number.FromRaw(values.GetElement(0));
            destination[index1] = Number.FromRaw(values.GetElement(1));
            destination[index2] = Number.FromRaw(values.GetElement(2));
            destination[index3] = Number.FromRaw(values.GetElement(3));
        }
        #endregion

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
    }
}
