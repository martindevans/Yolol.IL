using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Compiler.Exceptions;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class ValueExtensionsTests
    {
        //[TestMethod]
        //public void NumberToValue()
        //{
        //    var v = (Value)7;
        //    var c = v.Coerce(StackType.YololValue);

        //    Assert.IsInstanceOfType(c, typeof(Value));
        //    Assert.AreEqual((Number)7, ((Value)c).Number);
        //}

        //[TestMethod]
        //public void StringToValue()
        //{
        //    var v = (Value)7;
        //    var c = v.Coerce(StackType.YololValue);

        //    Assert.IsInstanceOfType(c, typeof(Value));
        //    Assert.AreEqual((Number)7, ((Value)c).Number);
        //}

        [TestMethod]
        public void NumberToBoolTrue()
        {
            var v = (Value)1;
            var c = v.Coerce(StackType.Bool);

            Assert.IsInstanceOfType(c, typeof(bool));
            Assert.AreEqual(true, (bool)c);
        }

        [TestMethod]
        public void NumberToBoolFalse()
        {
            var v = (Value)0;
            var c = v.Coerce(StackType.Bool);

            Assert.IsInstanceOfType(c, typeof(bool));
            Assert.AreEqual(false, (bool)c);
        }

        [TestMethod]
        public void NumberToBoolInvalid()
        {
            var v = (Value)7;
            Assert.ThrowsException<InternalCompilerException>(() => {
                v.Coerce(StackType.Bool);
            });
        }

        [TestMethod]
        public void ValueToString()
        {
            var v = (Value)"Hello";
            var c = v.Coerce(StackType.YololString);

            Assert.IsInstanceOfType(c, typeof(YString));
            Assert.AreEqual("Hello", ((YString)c).ToString());
        }

        [TestMethod]
        public void ValueToNumber()
        {
            var v = (Value)7;
            var c = v.Coerce(StackType.YololNumber);

            Assert.IsInstanceOfType(c, typeof(Number));
            Assert.AreEqual((Number)7, (Number)c);
        }

        [TestMethod]
        public void NumberToStringThrows()
        {
            var v = (Value)7;

            Assert.ThrowsException<InternalCompilerException>(() => {
                v.Coerce(StackType.YololString);
            });
        }
    }
}
