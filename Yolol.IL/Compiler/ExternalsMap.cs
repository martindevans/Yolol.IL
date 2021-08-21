using System.Collections.Generic;
using Yolol.Grammar;

namespace Yolol.IL.Compiler
{
    public class ExternalsMap
        : Dictionary<VariableName, int>, IReadonlyExternalsMap
    {
    }

    public interface IReadonlyExternalsMap
        : IReadOnlyDictionary<VariableName, int>
    {
    }
}
