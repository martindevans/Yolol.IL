using System.Collections.Generic;
using Yolol.Grammar;
using Yolol.IL.Compiler;

namespace Yolol.IL.Extensions
{
    internal static class IReadOnlyDictionaryExtensions
    {
        /// <summary>
        /// Get a key for changeset querying for a given VariableName
        /// </summary>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static ChangeSetKey ChangeSetKey(this IReadOnlyDictionary<VariableName, int> dictionary, VariableName key)
        {
            var index = dictionary[key];
            return new ChangeSetKey(1ul << index);
        }
    }
}
