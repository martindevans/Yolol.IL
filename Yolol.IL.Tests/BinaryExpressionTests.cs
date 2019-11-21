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

            Assert.AreEqual(8, st.GetVariable("c").Value);
        }

        [TestMethod]
        public void Variables()
        {
            var (st, _) = Test("a = 1 b = a c = b");

            //Assert.AreEqual(1, r);
            Assert.AreEqual(1, st.GetVariable("a").Value);
            Assert.AreEqual(1, st.GetVariable("b").Value);
            Assert.AreEqual(1, st.GetVariable("c").Value);
        }

        [TestMethod]
        public void AddNumbers()
        {
            var (st, _) = Test("a = 1 + 2");

            //Assert.AreEqual(1, r);
            Assert.AreEqual(3, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddStrings()
        {
            var (st, _) = Test("a = \"1\" + \"2\"");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddMixedStrNum()
        {
            var (st, _) = Test("a = \"1\" + 2");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddMixedNumStr()
        {
            var (st, _) = Test("a = 1 + \"2\"");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void MultiplyNumbers()
        {
            var (st, _) = Test("a = 3 * 2");

            //Assert.AreEqual(1, r);
            Assert.AreEqual(6, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void MultiplyStrings()
        {
            Assert.ThrowsException<ExecutionException>(() =>
            {
                Test("a = \"3\" * \"2\"");
            });
        }

        [TestMethod]
        public void DivideNumbers()
        {
            var (st, _) = Test("a = 6 / 2");

            //Assert.AreEqual(1, r);
            Assert.AreEqual(3, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void DivideStrings()
        {
            Assert.ThrowsException<ExecutionException>(() =>
            {
                Test("a = \"3\" / \"2\"");
            });
        }

        [TestMethod]
        public void SubNumbers()
        {
            var (st, _) = Test("a = 1 - 2");

            //Assert.AreEqual(1, r);
            Assert.AreEqual(-1, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void SubStrings()
        {
            var (st, _) = Test("a = \"311\" - \"1\"");

            //Assert.AreEqual(1, r);
            Assert.AreEqual("31", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void EqualityNumNum()
        {
            var (st, _) = Test("a = 1 == 1 b = 1 == 2");

            Assert.AreEqual(1, st.GetVariable("a").Value);
            Assert.AreEqual(0, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void EqualityStrStr()
        {
            var (st, _) = Test("a = \"1\" == \"1\" b = \"1\" == \"2\"");

            Assert.AreEqual(1, st.GetVariable("a").Value);
            Assert.AreEqual(0, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void EqualityMixed()
        {
            var (st, _) = Test("a = \"1\" == 1 b = \"1\" == 2");

            Assert.AreEqual(0, st.GetVariable("a").Value);
            Assert.AreEqual(0, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void NotEqualityNumNum()
        {
            var (st, _) = Test("a = 1 != 1 b = 1 != 2");

            Assert.AreEqual(0, st.GetVariable("a").Value);
            Assert.AreEqual(1, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void NotEqualityStrStr()
        {
            var (st, _) = Test("a = \"1\" != \"1\" b = \"1\" != \"2\"");

            Assert.AreEqual(0, st.GetVariable("a").Value);
            Assert.AreEqual(1, st.GetVariable("b").Value);
        }

        [TestMethod]
        public void NotEqualityMixed()
        {
            var (st, _) = Test("a = \"1\" != 1 b = \"1\" != 2");

            Assert.AreEqual(1, st.GetVariable("a").Value);
            Assert.AreEqual(1, st.GetVariable("b").Value);
        }
    }
}
