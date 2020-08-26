using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class Reproduction
    {
        [TestMethod]
        public void GotoStringBug()
        {
            var ast = TestHelpers.Parse("b=\"tt\" :pi += (b-\"t\")==b goto 1");

            var internalsMap = new Dictionary<string, int>();
            var externalsMap = new Dictionary<string, int>();
            var compiledLines = new Func<ArraySegment<Value>, ArraySegment<Value>, int>[ast.Lines.Count];
            for (var i = 0; i < ast.Lines.Count; i++)
                compiledLines[i] = ast.Lines[i].Compile(i + 1, 20, internalsMap, externalsMap);

            var pc = compiledLines[0](new Value[internalsMap.Count], new Value[externalsMap.Count]);
            Assert.AreEqual(1, pc);
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
            var (ms, _) = TestHelpers.Test("if 7 then end");
        }
    }
}
