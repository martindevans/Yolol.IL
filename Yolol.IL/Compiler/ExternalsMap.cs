﻿using System.Collections.Generic;

namespace Yolol.IL.Compiler
{
    public class ExternalsMap
        : Dictionary<string, int>, IReadonlyExternalsMap
    {
    }

    public interface IReadonlyExternalsMap
        : IReadOnlyDictionary<string, int>
    {
    }
}
