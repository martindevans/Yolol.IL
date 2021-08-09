using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class BinaryExpressionTests
    {
        [TestMethod]
        public void Exponent()
        {
            var (st, _) = Test("a = 2 b = 3 c = a ^ b");

            Assert.AreEqual((Value)8, st.GetVariable("c"));
        }

        [TestMethod]
        public void Variables()
        {
            var (st, _) = Test("a = 1 b = a c = b");

            //Assert.AreEqual(1, r);
            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
            Assert.AreEqual((Value)1, st.GetVariable("c"));
        }

        [TestMethod]
        public void AddNumbers()
        {
            var (st, _) = Test("a = 3 + 2");

            Assert.AreEqual((Value)5, st.GetVariable("a"));
        }

        [TestMethod]
        public void AddBools()
        {
            var (st, _) = Test("x=4 a=(x!=x)+(x==x)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void AddStrings()
        {
            var (st, _) = Test("a = \"1\" + \"2\"");

            Assert.AreEqual("12", st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedStrNum()
        {
            var (st, _) = Test("a = \"1\" + 2");

            Assert.AreEqual("12", st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedNumStr()
        {
            var (st, _) = Test("a = 1 + \"2\"");

            Assert.AreEqual("12", st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyBools()
        {
            var (st, _) = Test("a=1*1");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyNumbers()
        {
            var (st, _) = Test("a = 3 * 2");

            Assert.AreEqual((Value)6, st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyStrings()
        {
            var (st, _) = Test("a=1 a=\"3\"*\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void DivideNumbers()
        {
            var (st, _) = Test("a = 6 / 2");

            Assert.AreEqual((Value)3, st.GetVariable("a"));
        }

        [TestMethod]
        public void DivideStrings()
        {
            var (st, _) = Test("a=1 a=\"3\"/\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModNumbers()
        {
            var (st, _) = Test("a = 7 % 2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModNumbers2()
        {
            var (st, _) = Test("a = 1.7 % 1");

            Assert.AreEqual((Value)0.7m, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModStrings()
        {
            var (st, _) = Test("a=1 a=\"3\"%\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void SubNumbers()
        {
            var (st, _) = Test("a = 1 - 2");

            Assert.AreEqual((Value)(-1), st.GetVariable("a"));
        }

        [TestMethod]
        public void SubStrings()
        {
            var (st, _) = Test("a = \"311\" - \"1\"");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("31", st.GetVariable("a"));
        }

        [TestMethod]
        public void EqualityBoolBool()
        {
            var (st, _) = Test("a = 1 == 1 b = 1 == 0");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityBoolNum()
        {
            var (st, _) = Test("a = 1 == (1*7)/7 b = 1 == (3-3)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityBoolStr()
        {
            var (st, _) = Test("a = 1 == \"1\"");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
        }

        [TestMethod]
        public void EqualityNumNum()
        {
            var (st, _) = Test("a = 3 == 3 b = 2 == 3");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityStrStr()
        {
            var (st, _) = Test("a = \"1\" == \"1\" b = \"1\" == \"2\"");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityMixed()
        {
            var (st, _) = Test("a = \"1\" == 2 b = \"2\" == 2");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityNumNum()
        {
            var (st, _) = Test("a = 1 != 1 b = 1 != 2");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityStrStr()
        {
            var (st, _) = Test("a = \"1\" != \"1\" b = \"1\" != \"2\"");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityMixed()
        {
            var (st, _) = Test("a = \"1\" != 1 b = \"1\" != 2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void And()
        {
            var (st, _) = Test("x=1 y=0 a = x and y b = x and x");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void Or()
        {
            var (st, _) = Test("x=1 y=0 a = x or y b = y or y");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }
    }
}
