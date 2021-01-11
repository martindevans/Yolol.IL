using System.Collections.Generic;

namespace Yolol.IL.Extensions
{
    internal static class IReadOnlyDictionaryExtensions
    {
        public static TV? GetOrNull<TK, TV>(this IReadOnlyDictionary<TK, TV> dict, TK key)
            where TV : struct
        {
            if (dict.TryGetValue(key, out var v))
                return v;
            return null;
        }
    }
}
