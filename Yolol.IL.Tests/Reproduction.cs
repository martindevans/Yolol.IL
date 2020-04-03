using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class Reproduction
    {
        [TestMethod]
        public void PRNG()
        {
            var ast = TestHelpers.Parse(
                ":seed=123456787654321",
                "m=2^28-57 a=31792125 c=12345 k=2^16 d=2^11 r=:seed%m",
                "r=(a*r+c)%m :out=r/d%k*k r=(a*r+c)%m :out+=r/d%k :out-=:out%1",
                "high+=:out>2^31 count++ goto 3"
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

            var line = 1;
            for (var i = 0; i < 10000000; i++)
                line = compiledLines[line - 1](internals, externals);

            Console.WriteLine("Count: " + internals[internalsMap["count"]]);
            Console.WriteLine("High: " + internals[internalsMap["high"]]);
        }

        [TestMethod]
        public void MethodName()
        {
            var ast = TestHelpers.Parse(
                "a = :a b = :b c = a * b",
                "d = c * c e = sqrt(c + c)",
                "e++",
                "f = e / 2",
                ":g = f"
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
