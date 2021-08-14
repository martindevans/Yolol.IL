using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.IL.Compiler;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class StackTypeTests
    {
        [TestMethod]
        public void ValidConversions()
        {
            Assert.AreEqual(typeof(Number), StackType.YololNumber.ToType());
            Assert.AreEqual(typeof(YString), StackType.YololString.ToType());
            Assert.AreEqual(typeof(Value), StackType.YololValue.ToType());
            Assert.AreEqual(typeof(bool), StackType.Bool.ToType());
        }

        [TestMethod]
        public void InvalidConversions()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => StackType.StaticError.ToType());
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ((StackType)(-10)).ToType());
        }
    }
}
