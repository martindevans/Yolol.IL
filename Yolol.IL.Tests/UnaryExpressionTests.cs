using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class UnaryExpressionTests
    {
        [TestMethod]
        public void NotNumbers()
        {
            var (st, _) = Test("a = not 1 b = not 0");

            Assert.AreEqual(0, st.GetVariable("a").Value);
            Assert.AreEqual(1, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void NotStrings()
        {
            var (st, _) = Test("a = not \"\" b = not \"0\"");

            Assert.AreEqual(0, st.GetVariable("a").Value);
            Assert.AreEqual(0, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void NegateNumber()
        {
            var (st, _) = Test("a = 3 b = -a");

            Assert.AreEqual(-3, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void Bracketed()
        {
            var (st, _) = Test("a = 3 + (2 * 4)");

            Assert.AreEqual(11, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void Sqrt()
        {
            var (st, _) = Test("a = sqrt(9)");

            Assert.AreEqual(3, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void Sine()
        {
            var (st, _) = Test("a = sin(90)");

            Assert.AreEqual(1, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void Cos()
        {
            var (st, _) = Test("a = cos(90)");

            Assert.AreEqual(0, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void Tan()
        {
            var (st, _) = Test("a = tan(45)");

            Assert.AreEqual(1, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void PreInc()
        {
            var (st, _) = Test("a = 7 b = ++a");

            Assert.AreEqual(8, st.GetVariable("a").Value);
            Assert.AreEqual(8, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void PostInc()
        {
            var (st, _) = Test("a = 7 b = a++");

            Assert.AreEqual(8, st.GetVariable("a").Value);
            Assert.AreEqual(7, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void PreDec()
        {
            var (st, _) = Test("a = 7 b = --a");

            Assert.AreEqual(6, st.GetVariable("a").Value);
            Assert.AreEqual(6, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void PostDec()
        {
            var (st, _) = Test("a = 7 b = a--");

            Assert.AreEqual(6, st.GetVariable("a").Value);
            Assert.AreEqual(7, st.GetVariable("b").Value);
        }
    }
}
