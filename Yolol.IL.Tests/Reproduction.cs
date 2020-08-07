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
        public void MethodName()
        {
            var ast = TestHelpers.Parse(
                "a = :a b = :b c = a * b"
                //"d = c * c e = sqrt(c + c)",
                //"e++",
                //"f = e / 2",
                //":g = f"
            );

            var internalsMap = new Dictionary<string, int>();
            var externalsMap = new Dictionary<string, int>();
            var compiledLines = new Func<Memory<Value>, Memory<Value>, int>[ast.Lines.Count];
            for (var i = 0; i < ast.Lines.Count; i++)
                compiledLines[i] = ast.Lines[i].Compile(i + 1, 20, internalsMap, externalsMap);

            var internals = new Value[internalsMap.Count];
            Array.Fill(internals, new Value(0));
            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, new Value(0));

            for (var i = 0; i < ast.Lines.Count; i++)
                compiledLines[i](internals, externals);
        }
    }
}
