using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class TypeExtensionsTests
    {
        [TestMethod]
        public void FromYololType()
        {
            Assert.AreEqual(StackType.YololNumber, Execution.Type.Number.ToStackType());
            Assert.AreEqual(StackType.YololString, Execution.Type.String.ToStackType());
            Assert.AreEqual(StackType.StaticError, Execution.Type.Error.ToStackType());
        }

        [TestMethod]
        public void FromYololTypeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => Execution.Type.Unassigned.ToStackType());
        }

        [TestMethod]
        public void FromRealType()
        {
            Assert.AreEqual(StackType.Bool, typeof(bool).ToStackType());
            Assert.AreEqual(StackType.StaticError, typeof(StaticError).ToStackType());
            Assert.AreEqual(StackType.YololNumber, typeof(Number).ToStackType());
            Assert.AreEqual(StackType.YololString, typeof(YString).ToStackType());
            Assert.AreEqual(StackType.YololValue, typeof(Value).ToStackType());
        }

        [TestMethod]
        public void FromRealTypeThrows()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => typeof(TypeExtensionsTests).ToStackType());
        }
    }
}
