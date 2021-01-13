using System.Collections.Generic;
using Sigil;
using Yolol.Grammar.AST.Statements;

namespace Yolol.IL.Compiler.Vectorisation
{
    internal abstract class BaseVectorisationStrategy<TEmit>
    {
        public abstract bool Try(List<BaseStatement> statements, Emit<TEmit> emitter);
    }
}
