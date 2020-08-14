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
            var compiledLines = new Func<Memory<Value>, Memory<Value>, int>[ast.Lines.Count];
            for (var i = 0; i < ast.Lines.Count; i++)
                compiledLines[i] = ast.Lines[i].Compile(i + 1, 20, internalsMap, externalsMap);

            var pc = compiledLines[0](new Value[internalsMap.Count], new Value[externalsMap.Count]);
            Assert.AreEqual(1, pc);
        }
    }
}
