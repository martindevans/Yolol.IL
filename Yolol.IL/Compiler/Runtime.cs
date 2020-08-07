using System;
using System.Runtime.CompilerServices;
using Yolol.Execution;

namespace Yolol.IL.Compiler
{
    internal abstract class Runtime
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Number BoolToNumber(bool b)
        {
            return b ? 1 : 0;
        }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LogicalNot(bool value)
        {
            return !value;
        }
    }
}
