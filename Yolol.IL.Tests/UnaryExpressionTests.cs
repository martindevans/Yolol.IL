using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class UnaryExpressionTests
    {
        [TestMethod]
        public void Acos()
        {
            var (st, _) = Test("a=0.3 b=acos(a)");

            Assert.AreEqual("72.542", st.GetVariable("b").ToString());
        }

        [TestMethod]
        public void Asin()
        {
            var (st, _) = Test("a=0.3 b=asin(a)");

            Assert.AreEqual("17.457", st.GetVariable("b").ToString());
        }

        [TestMethod]
        public void Atan()
        {
            var (st, _) = Test("a=0.3 b=atan(a)");

            Assert.AreEqual("16.699", st.GetVariable("b").ToString());
        }

        [TestMethod]
        public void FactorialNum()
        {
            var (st, _) = Test("a=3 b=(1*a)!");

            Assert.AreEqual((Value)6, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotNum()
        {
            var (st, _) = Test("a = not 3 b = not 0");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotStr()
        {
            var (st, _) = Test("a = not \"\" b = not \"0\"");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotBool()
        {
            var (st, _) = Test("a = not 1 b = not 0");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NotVal()
        {
            var (st, _) = Test("aa=1 bb=0 a = not aa b = not bb");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
            Assert.AreEqual((Value)1, st.GetVariable("b"));
        }

        [TestMethod]
        public void NegateNumber()
        {
            var (st, _) = Test("b = -3");

            Assert.AreEqual((Value)(-3), st.GetVariable("b"));
        }

        [TestMethod]
        public void NegateStringConstant()
        {
            var (st, _) = Test("b = -\"a\"");

            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void NegateExternal()
        {
            var (st, _) = Test("b = -:a");

            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void DivideExternals()
        {
            var (st, _) = Test("b = :a/:b");

            Assert.AreEqual((Value)0, st.GetVariable("b"));
        }

        [TestMethod]
        public void NegateString()
        {
            var (st, pc) = Test("a=\"a\" b=-a goto10");

            Assert.AreEqual((Value)0, st.GetVariable("b"));
            Assert.AreEqual(2, pc);
        }

        [TestMethod]
        public void NegateBool()
        {
            var (st, _) = Test("b = -1");

            Assert.AreEqual((Value)(-1), st.GetVariable("b"));
        }

        [TestMethod]
        public void NegateVal()
        {
            var (st, _) = Test("a=2 b = -a");

            Assert.AreEqual((Value)(-2), st.GetVariable("b"));
        }

        [TestMethod]
        public void Bracketed()
        {
            var (st, _) = Test("a = 3 + (2 * 4)");

            Assert.AreEqual((Value)11, st.GetVariable("a"));
        }

        [TestMethod]
        public void SqrtNum()
        {
            var (st, _) = Test("a = sqrt(9)");

            Assert.AreEqual((Value)3, st.GetVariable("a"));
        }

        [TestMethod]
        public void SqrtBool()
        {
            var (st, _) = Test("a = sqrt(1)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void SqrtStr()
        {
            var (st, _) = Test("a=1 a=sqrt(\"9\") a=2");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void SqrtVal()
        {
            var (st, _) = Test("b=9 a=sqrt(b)");

            Assert.AreEqual((Value)3, st.GetVariable("a"));
        }

        [TestMethod]
        public void Sine()
        {
            var (st, _) = Test("a = sin(90)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void Cos90()
        {
            var (st, _) = Test("a = cos(90)");

            Assert.AreEqual((Value)0, st.GetVariable("a"));
        }

        [TestMethod]
        public void Cos0()
        {
            var (st, _) = Test("a = cos(0)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void Tan()
        {
            var (st, _) = Test("a = tan(45)");

            Assert.AreEqual((Value)1, st.GetVariable("a"));
        }

        [TestMethod]
        public void PreInc()
        {
            var (st, _) = Test("a = 7 b = ++a");

            Assert.AreEqual((Value)8, st.GetVariable("a"));
            Assert.AreEqual((Value)8, st.GetVariable("b"));
        }

        //[TestMethod]
        //public void PostInc()
        //{
        //    var (st, _) = Test("a = 7 b = a++");

        //    Assert.AreEqual(8, st.GetVariable("a"));
        //    Assert.AreEqual(7, st.GetVariable("b"));
        //}

        [TestMethod]
        public void PreDec()
        {
            var (st, _) = Test("a = 7 b = --a");

            Assert.AreEqual((Value)6, st.GetVariable("a"));
            Assert.AreEqual((Value)6, st.GetVariable("b"));
        }

        //[TestMethod]
        //public void PostDec()
        //{
        //    var (st, _) = Test("a = 7 b = a--");

        //    Assert.AreEqual(6, st.GetVariable("a"));
        //    Assert.AreEqual(7, st.GetVariable("b"));
        //}
    }
}
