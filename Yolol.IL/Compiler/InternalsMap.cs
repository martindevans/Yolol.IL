using System.Collections.Generic;

namespace Yolol.IL.Compiler
{
    public class InternalsMap
        : Dictionary<string, int>, IReadonlyInternalsMap
    {
    }

    public interface IReadonlyInternalsMap
        : IReadOnlyDictionary<string, int>
    {
    }
}
