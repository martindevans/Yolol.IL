using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class RuntimeTests
    {
        [TestMethod]
        public void ErrorToValueThrows()
        {
            Assert.ThrowsException<ExecutionException>(() => Runtime.ErrorToValue(new Execution.StaticError("Hello")));
        }

        [TestMethod]
        public void BoolNegate()
        {
            Assert.AreEqual(0, (int)Runtime.BoolNegate(false));
            Assert.AreEqual(-1, (int)Runtime.BoolNegate(true));
        }

        [TestMethod]
        public void BoolAddString()
        {
            Assert.AreEqual("0Hello", Runtime.BoolAdd(false, new YString("Hello")).ToString());
            Assert.AreEqual("1World", Runtime.BoolAdd(true, new YString("World")).ToString());
        }

        [TestMethod]
        public void BoolAddNumber()
        {
            Assert.AreEqual(8, (int)Runtime.BoolAdd(false, (Number)8));
            Assert.AreEqual(9, (int)Runtime.BoolAdd(true, (Number)8));
        }

        [TestMethod]
        public void BoolAddValueNumber()
        {
            Assert.AreEqual((Value)8, (Value)Runtime.BoolAdd(false, (Value)(Number)8));
            Assert.AreEqual((Value)9, (Value)Runtime.BoolAdd(true, (Value)(Number)8));
        }

        [TestMethod]
        public void BoolAddValueString()
        {
            Assert.AreEqual("0Hello", Runtime.BoolAdd(false, new Value(new YString("Hello"))).ToString());
            Assert.AreEqual("1World", Runtime.BoolAdd(true, new Value(new YString("World"))).ToString());
        }
        
        [TestMethod]
        public void GotoValueNumber()
        {
            Assert.AreEqual(7, Runtime.GotoValue((Number)7, 20));
            Assert.AreEqual(20, Runtime.GotoValue((Number)30, 20));
        }

        [TestMethod]
        public void GotoValueString()
        {
            Assert.ThrowsException<ExecutionException>(() => Runtime.GotoValue(new Value(new YString("Hello")), 20));
        }

        [TestMethod]
        public void BoolAnd()
        {
            Assert.AreEqual(false, Runtime.And(false, false));
            Assert.AreEqual(false, Runtime.And(false, true));
            Assert.AreEqual(false, Runtime.And(true, false));
            Assert.AreEqual(false, Runtime.And(true, true));
        }
    }
}
