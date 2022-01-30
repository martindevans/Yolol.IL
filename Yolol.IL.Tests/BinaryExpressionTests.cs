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
            var st = Test("a = 2 b = 3 c = a ^ b");

            Assert.AreEqual((Value)8, st.GetVariable("c"));
        }

        [TestMethod]
        public void Variables()
        {
            var st = Test("a = 1 b = a c = b");

            //Assert.AreEqual(1, r);
            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
            Assert.AreEqual((Value)1, st.GetVariable("c"));
        }

        [TestMethod]
        public void AddNumbers()
        {
            var st = Test("a = 3 + 2");

            Assert.AreEqual((Value)5, st.GetVariable("a"));
        }

        [TestMethod]
        public void AddBools()
        {
            var st = Test("x=4 a=(x!=x)+(x==x)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void AddStrings()
        {
            var st = Test("a = \"1\" + \"2\"");

            Assert.AreEqual("12", st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedStrNum()
        {
            var st = Test("a = \"1\" + 2");

            Assert.AreEqual("12", st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedBoolVal()
        {
            var st = Test("a = 1 + :c");

            Assert.AreEqual(Number.One, st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedNumStr()
        {
            var st = Test("a = 7 + (:c+\"2\")");

            Assert.AreEqual("702", st.GetVariable("a"));
        }

        [TestMethod]
        public void AddMixedBoolStr()
        {
            var st = Test("a = 1 + (:c+\"2\")");

            Assert.AreEqual("102", st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyBools()
        {
            var st = Test("a=1*1");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyNumbers()
        {
            var st = Test("a = 3 * 2");

            Assert.AreEqual((Value)6, st.GetVariable("a"));
        }

        [TestMethod]
        public void MultiplyStrings()
        {
            var st = Test("a=1 a=\"3\"*\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void DivideNumbers()
        {
            var st = Test("a = 6 / 2");

            Assert.AreEqual((Value)3, st.GetVariable("a"));
        }

        [TestMethod]
        public void DivideStrings()
        {
            var st = Test("a=1 a=\"3\"/\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModNumbers()
        {
            var st = Test("a = 7 % 2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModNumbers2()
        {
            var st = Test("a = 1.7 % 1");

            Assert.AreEqual((Value)0.7m, st.GetVariable("a"));
        }

        [TestMethod]
        public void ModStrings()
        {
            var st = Test("a=1 a=\"3\"%\"2\" a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void SubNumbers()
        {
            var st = Test("a = 1 - 2");

            Assert.AreEqual((Value)(-1), st.GetVariable("a"));
        }

        [TestMethod]
        public void SubStrings()
        {
            var st = Test("a = \"311\" - \"1\"");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("31", st.GetVariable("a"));
        }

        [TestMethod]
        public void EqualityBoolBool()
        {
            var st = Test("a = 1 == 1 b = 1 == 0");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityBoolNum()
        {
            var st = Test("a = 1 == (1*7)/7 b = 1 == (3-3)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityBoolStr()
        {
            var st = Test("a = 1 == \"1\"");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
        }

        [TestMethod]
        public void EqualityNumNum()
        {
            var st = Test("a = 3 == 3 b = 2 == 3");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityStrStr()
        {
            var st = Test("a = \"1\" == \"1\" b = \"1\" == \"2\"");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void EqualityMixed()
        {
            var st = Test("a = \"1\" == 2 b = \"2\" == 2");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityNumNum()
        {
            var st = Test("a = 1 != 1 b = 1 != 2");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityStrStr()
        {
            var st = Test("a = \"1\" != \"1\" b = \"1\" != \"2\"");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotEqualityMixed()
        {
            var st = Test("a = \"1\" != 1 b = \"1\" != 2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void And()
        {
            var st = Test("x=1 y=0 a = x and y b = x and x");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void Or()
        {
            var st = Test("x=1 y=0 a = x or y b = y or y");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }
    }
}
