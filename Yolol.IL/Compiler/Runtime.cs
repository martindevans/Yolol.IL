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

        #region value
        public static Value TrimValue(Value value, int maxStringLength)
        {
            if (value.Type != Execution.Type.String)
                return value;

            return new Value(YString.Trim(value.String, maxStringLength));
        }
        #endregion

        #region number
        public static Number Abs(Number value)
        {
            return value.Abs();
        }
        #endregion

        #region goto
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValueANumber(Value value)
        {
            return value.Type == Execution.Type.Number;
        }

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
            return Math.Clamp((int)value, 1, maxLineNumber);
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
            return value ? -Number.One : Number.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolAdd(bool a, bool b)
        {
            return (Number)(Convert.ToInt32(a) + Convert.ToInt32(b));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static YString BoolAdd(bool a, YString b)
        {
            return (Number)a + b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolAdd(bool a, Number b)
        {
            return (Number)a + b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value BoolAdd(bool a, Value b)
        {
            return (Number)a + b;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolMul(bool a, Number b)
        {
            return a ? b : Number.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool And(bool a, bool b)
        {
            return a & b;
        }
        #endregion

        #region memory access
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Value value, ref ArraySegment<Value> segment, int index)
        {
            segment[index] = value;
        }
        #endregion
    }
}
