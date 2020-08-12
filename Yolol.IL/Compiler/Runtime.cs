using System;
using System.Runtime.CompilerServices;
using Yolol.Execution;

namespace Yolol.IL.Compiler
{
    internal abstract class Runtime
    {
        #region comparison
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThan(bool l, bool r)
        {
            return Convert.ToInt32(l) > Convert.ToInt32(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThan(bool l, Value r)
        {
            return (Number)l > r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThan(bool l, Number r)
        {
            return (Number)l > r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThan(bool l, YString r)
        {
            return (Number)l > r;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThanEqualTo(bool l, bool r)
        {
            return Convert.ToInt32(l) >= Convert.ToInt32(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThanEqualTo(bool l, Value r)
        {
            return (Number)l >= r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThanEqualTo(bool l, Number r)
        {
            return (Number)l >= r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolGreaterThanEqualTo(bool l, YString r)
        {
            return (Number)l >= r;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThan(bool l, bool r)
        {
            return Convert.ToInt32(l) < Convert.ToInt32(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThan(bool l, Value r)
        {
            return (Number)l < r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThan(bool l, Number r)
        {
            return (Number)l < r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThan(bool l, YString r)
        {
            return (Number)l < r;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThanEqualTo(bool l, bool r)
        {
            return Convert.ToInt32(l) <= Convert.ToInt32(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThanEqualTo(bool l, Value r)
        {
            return (Number)l <= r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThanEqualTo(bool l, Number r)
        {
            return (Number)l <= r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolLessThanEqualTo(bool l, YString r)
        {
            return (Number)l <= r;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoolEquals(bool l, Value r)
        {
            return BoolToNumber(l) == r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoolEquals(bool l, YString r)
        {
            return BoolToNumber(l) == r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoolEquals(bool l, Number r)
        {
            return l == r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool BoolEquals(bool l, bool r)
        {
            return l == r;
        }
        #endregion

        #region maths
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Modulus(bool l, Value r)
        {
            return BoolToNumber(l) % r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Modulus(bool l, YString r)
        {
            return BoolToNumber(l) % new Value(r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Modulus(bool l, Number r)
        {
            return BoolToNumber(l) % r;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value Modulus(bool l, bool r)
        {
            return BoolToNumber(l) % BoolToNumber(r);
        }
        #endregion

        #region conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolToNumber(bool b)
        {
            return b ? Number.One : Number.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value ErrorToValue(StaticError err)
        {
            return err;
        }
        #endregion

        #region goto
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GotoValue(Value value, int maxLineNumber)
        {
            if (value.Type == Execution.Type.Number)
                GotoNumber(value.Number, maxLineNumber);

            throw new ExecutionException("Attempted to `goto` a `string`");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GotoNumber(Number value, int maxLineNumber)
        {
            return Math.Min(maxLineNumber, Math.Max(1, (int)value));
        }
        #endregion

        #region memory access
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpanIndex(Value v, Memory<Value> mem, int index)
        {
            mem.Span[index] = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value GetSpanIndex(Memory<Value> mem, int index)
        {
            return mem.Span[index];
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
