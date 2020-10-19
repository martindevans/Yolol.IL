using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class Reproduction
    {
        [TestMethod]
        public void GotoStringBug()
        {
            var (_, pc) = TestHelpers.Test("b=\"tt\" :pi += (b-\"t\")==b goto 1");
            Assert.AreEqual(1, pc);
        }

        [TestMethod]
        public void NullRefString()
        {
            var (ms, _) = TestHelpers.Test("c += \"t\"");
            Assert.AreEqual("0t", ms.GetVariable("c").String.ToString());
        }

        [TestMethod]
        public void NullRefString2()
        {
            var (ms, _) = TestHelpers.Test("c = c + \"t\"");
            Assert.AreEqual("0t", ms.GetVariable("c").String.ToString());
        }

        [TestMethod]
        public void ConstantEvaluation()
        {
            var result = TestHelpers.Test("a = \"2\" + 2 + 2");

            Assert.AreEqual("222", result.Item1.GetVariable("a").String.ToString());
        }

        [TestMethod]
        public void GotoDonePlusPlus()
        {
            var (_, pc) = TestHelpers.Test("goto:done++");

            Assert.AreEqual(1, pc);
        }

        [TestMethod]
        public void UnreachableCode()
        {
            TestHelpers.Test("if :i>8191 then :done=1 goto 1 end");
        }

        [TestMethod]
        public void NonStringSubtraction()
        {
            var (ms, _) = TestHelpers.Test("a=70 b=\"\"+a-0");
            Assert.AreEqual(new YString("7"), ms.GetVariable("b").String);
        }

        [TestMethod]
        public void SomeoneLucasIf()
        {
            TestHelpers.Test("if 7 then end");
        }

        [TestMethod]
        public void Abs()
        {
            var (ms, _) = TestHelpers.Test("a = abs -7");
            Assert.AreEqual(7, ms.GetVariable("a").Number);
        }
    }
}
