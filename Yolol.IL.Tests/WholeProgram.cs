using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sigil;
using Yolol.Execution;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class WholeProgram
    {
        [TestMethod]
        public void CompileProgram()
        {
            var ast = Parse(
                "a = :a b = :b c = a * b",
                "d = c * c e = sqrt(c + c + d)",
                "e++",
                "f = e / 2",
                ":g = f",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                ""
            );

            //todo: rewrite this to be oriented more towards running an entire "program" rather than a chunk of lines
            // - Always start at line X (make this a parameter to the compile method)
            // - Always terminate when execution reaches line Y (make this a parameter to the compile method)
            // - Internally fuse together lines that are never jumped to (e.g. this test program would become one line)
            //   - This is confounded by computed gotos

            var internals = new Dictionary<string, int>();
            var externals = new Dictionary<string, int>();
            var result = ast.Compile(internals, externals);

            var a = new Value[10];
            Array.Fill(a, new Value(0));
            var b = new Value[10];
            Array.Fill(b, new Value(0));
            var pc = result(a, b, 0, 5);

            Assert.AreEqual(6, pc);

            foreach (var (k, i) in internals)
                Console.WriteLine($"{k} = {a[i]}");
            foreach (var (k, i) in externals)
                Console.WriteLine($"{k} = {b[i]}");
        }

        [TestMethod]
        public void MethodName()
        {
            var emitter = Emit<Func<uint, int>>.NewDynamicMethod(strictBranchVerification: true);

            var a = emitter.DefineLabel();
            var b = emitter.DefineLabel();
            var c = emitter.DefineLabel();
            var d = emitter.DefineLabel();

            emitter.LoadArgument(0);
            emitter.Switch(a, b, c, d);
            emitter.LoadConstant(5);
            emitter.Return();

            emitter.MarkLabel(a);
            emitter.LoadConstant(1);
            emitter.Return();

            emitter.MarkLabel(b);
            emitter.LoadConstant(2);
            emitter.Return();

            emitter.MarkLabel(c);
            emitter.LoadConstant(3);
            emitter.Return();

            emitter.MarkLabel(d);
            emitter.LoadConstant(4);
            emitter.Return();

            var m = emitter.CreateDelegate();
            var r = m(1);

            Assert.AreEqual(2, r);
        }
    }
}
