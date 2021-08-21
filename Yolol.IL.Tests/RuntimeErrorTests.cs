using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class RuntimeErrorTests
    {
        [TestMethod]
        public void StaticRuntimeError()
        {
            var st = Test("a=2 a/=0 a=3");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
        }

        [TestMethod]
        public void DynamicRuntimeError()
        {
            var st = Test("a=2 b=0 a/=b a=3");

            Assert.AreEqual((Value)2, st.GetVariable("a"));
        }
    }
}
