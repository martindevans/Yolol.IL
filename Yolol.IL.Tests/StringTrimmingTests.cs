using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class StringTrimmingTests
    {
        [TestMethod]
        public void TrimConstant()
        {
            var (ms, _) = Test(new[] {
                "a=\"1234567\"",
            }, 3, 5);

            Assert.AreEqual("12345", ms.GetVariable("a").ToString());
        }

        [TestMethod]
        public void TrimAddition()
        {
            var (ms, _) = Test(new[] {
                "a=\"1234567\"",
                "b=\"1234567\"",
                "c=a+b"
            }, 3, 10);

            Assert.AreEqual("1234567123", ms.GetVariable("c").ToString());
        }

        [TestMethod]
        public void TrimIncrement()
        {
            var (ms, _) = Test(new[] {
                "a=\"\" a++ a++ a++ a++ a++ a++ a++ a++ a++ a++ a++",
            }, 1, 3);

            Assert.AreEqual("   ", ms.GetVariable("a").ToString());
        }

        [TestMethod]
        public void TrimCompoundAddition()
        {
            var (ms, _) = Test(new[] {
                "a=\"abcd\" a+=\"efgh\"",
            }, 1, 5);

            Assert.AreEqual("abcde", ms.GetVariable("a").ToString());
        }
    }
}
