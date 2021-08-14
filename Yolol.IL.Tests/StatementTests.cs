using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class StatementTests
    {
        [TestMethod]
        public void Increment()
        {
            var (st1, _) = Test(new[] { "a = 2", "b = ++a" }, 2, 10);
            Assert.AreEqual(3, (int)st1.GetVariable("a").Number);

            var (st2, _) = Test(new[] { "a = \"2\"", "b = ++a" }, 2, 10);
            Assert.AreEqual("2 ", st2.GetVariable("a").ToString());
        }

        [TestMethod]
        public void Assignment()
        {
            var (st, _) = Test("a = 2");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
        }

        [TestMethod]
        public void IfStatementError()
        {
            var (st, _) = Test("if \"1\"! then a = 1 else a = 2 end");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
        }

        [TestMethod]
        public void IfStatementTrue()
        {
            var (st, _) = Test("if 1 then a = 1 else a = 2 end");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void IfStatementFalse()
        {
            var (st, _) = Test("if 0 then a = 1 else a = 2 end");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
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

        //[TestMethod]
        //public void IncrementString()
        //{
        //    var (st, _) = Test("a = \"1\" b = a++");

        //    Assert.AreEqual("1 ", st.GetVariable("a"));
        //    Assert.AreEqual("1", st.GetVariable("b"));
        //}

        [TestMethod]
        public void PreIncrementString()
        {
            var (st, _) = Test("a = \"1\" b = ++a");

            Assert.AreEqual("1 ", st.GetVariable("a"));
            Assert.AreEqual("1 ", st.GetVariable("b"));
        }

        //[TestMethod]
        //public void IncrementNumber()
        //{
        //    var (st, _) = Test("a = 1 b = a++");

        //    Assert.AreEqual(2, st.GetVariable("a"));
        //    Assert.AreEqual(1, st.GetVariable("b"));
        //}

        [TestMethod]
        public void PreIncrementNumber()
        {
            var (st, _) = Test("a = 1 b = ++a");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
            Assert.AreEqual((Value)2, st.GetVariable("b"));
        }

        [TestMethod]
        public void StandaloneIncrement()
        {
            var (st, _) = Test("a=1 ++a");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
        }

        [TestMethod]
        public void Factorial()
        {
            var (st, _) = Test("a=7 b=a!");

            Assert.AreEqual((Number)5040, st.GetVariable("b"));
        }

        [TestMethod]
        public void LoadInIf()
        {
            var (st, _) = Test("a=1 if false then a=2 b=a else c=a end");

            Assert.AreEqual((Number)1, st.GetVariable("c"));
        }
    }
}
