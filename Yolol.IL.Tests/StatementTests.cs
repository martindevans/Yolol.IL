using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class StatementTests
    {
        [TestMethod]
        public void IfStatementTrue()
        {
            var (st, _) = Test("if 1 then a = 1 else a = 2 end");

            Assert.AreEqual(1, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void IfStatementFalse()
        {
            var (st, _) = Test("if 0 then a = 1 else a = 2 end");

            Assert.AreEqual(2, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void GotoNumber()
        {
            var (_, l) = Test("goto 10");

            Assert.AreEqual(10, l);
        }

        [TestMethod]
        public void GotoNegativeNumber()
        {
            var (_, l) = Test("goto -10");

            Assert.AreEqual(1, l);
        }

        [TestMethod]
        public void GotoOverNumber()
        {
            var (_, l) = Test("goto 21");

            Assert.AreEqual(20, l);
        }
    }
}
