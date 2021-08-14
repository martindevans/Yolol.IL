using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Grammar.AST.Statements;
using Yolol.IL.Compiler.Analysis;

using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class AnalysisTests
    {
        [TestMethod]
        public void NoMutationFound()
        {
            var prog = Parse("a = not abs sqrt sin cos tan acos asin atan 1+2*(3/-b)%5!^6-\"7\" == 7 != 7 and 7 or 7 >= 7 <= 7 < 7 > 7");
            var expr = ((Assignment)prog.Lines[0].Statements.Statements[0]).Right;

            Assert.IsFalse(expr.DoesExpressionContainMutation());
        }

        [TestMethod]
        public void FindMutation()
        {
            void T(string program)
            {
                var prog = Parse(program);
                var expr = ((Assignment)prog.Lines[0].Statements.Statements[0]).Right;
                Assert.IsTrue(expr.DoesExpressionContainMutation());
            }

            T("a = b++");
            T("a = ++b");
            T("a = b--");
            T("a = --b");
        }
    }
}
