using System;
using System.Runtime.CompilerServices;
using Yolol.Execution;

namespace Yolol.IL.Compiler
{
    internal abstract class Runtime
    {
        #region conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolToNumber(bool b)
        {
            return b;
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

        #region memory access
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetSpanIndex(Value v, Span<Value> span, int index)
        {
            span[index] = v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value GetSpanIndex(Span<Value> span, int index)
        {
            return span[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<Value> GetSpan(ArraySegment<Value> seg)
        {
            return seg;
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
