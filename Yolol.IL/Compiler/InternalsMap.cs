using System.Collections.Generic;
using Yolol.Grammar;

namespace Yolol.IL.Compiler
{
    public class InternalsMap
        : Dictionary<VariableName, int>, IReadonlyInternalsMap
    {
    }

    public interface IReadonlyInternalsMap
        : IReadOnlyDictionary<VariableName, int>
    {
    }
}
