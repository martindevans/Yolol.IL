using System.Collections.Generic;
using Sigil;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL.Compiler.Vectorisation
{
    internal class NoStrategy<TEmit>
        : BaseVectorisationStrategy<TEmit>
    {
        public override bool Try(List<BaseStatement> _, Emit<TEmit> __)
        {
            return false;
        }
    }
}
