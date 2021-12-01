using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class Reproduction
    {
        [TestMethod]
        public void NumberPop()
        {
            var result = TestHelpers.Test(":a=11 :b=:a-:a--");

            Assert.AreEqual(10, (int)result.GetVariable(":a").Number);
            Assert.AreEqual(1, (int)result.GetVariable(":b").Number);
        }

        [TestMethod]
        public void NumberValPop()
        {
            var result = TestHelpers.Test(new[] { ":a=11", ":b=:a-:a--" }, 2);

            Assert.AreEqual(10, (int)result.GetVariable(":a").Number);
            Assert.AreEqual(1, (int)result.GetVariable(":b").Number);
        }

        [TestMethod]
        public void StringPop()
        {
            var result = TestHelpers.Test(":a=\"abc\" :b=:a-:a--");

            Assert.AreEqual("ab", result.GetVariable(":a").String.ToString());
            Assert.AreEqual("c", result.GetVariable(":b").String.ToString());
        }

        [TestMethod]
        public void StringPopErr()
        {
            var result = TestHelpers.Test(":a=\"\" :b=:a-:a-- :c=1");

            Assert.AreEqual("", result.GetVariable(":a").String.ToString());
            Assert.AreEqual(0, (int)result.GetVariable(":b").Number);
            Assert.AreEqual(0, (int)result.GetVariable(":c").Number);
        }

        [TestMethod]
        public void StringValPop()
        {
            var result = TestHelpers.Test(new[] { ":a=\"abc\"", ":b=:a-:a--" }, 2);

            Assert.AreEqual("ab", result.GetVariable(":a").String.ToString());
            Assert.AreEqual("c", result.GetVariable(":b").String.ToString());
        }

        [TestMethod]
        public void StringValPopErr()
        {
            var result = TestHelpers.Test(new[] { ":a=\"\"", ":b=:a-:a-- :c=1" }, 2);

            Assert.AreEqual("", result.GetVariable(":a").String.ToString());
            Assert.AreEqual(0, (int)result.GetVariable(":b").Number);
            Assert.AreEqual(0, (int)result.GetVariable(":c").Number);
        }

        [TestMethod]
        public void FuzzOverflow()
        {
            var ast = TestHelpers.Parse(
                "b=-9223372036854775.808 b%=-0.001"
            );

            var lines = Math.Max(20, ast.Lines.Count);
            var externalsMap = new ExternalsMap();
            var compiled = ast.Compile(externalsMap, lines, 1024, null, false);

            var externals = new Value[externalsMap.Count];
            Array.Fill(externals, Number.Zero);
            var internals = new Value[compiled.InternalsMap.Count];
            Array.Fill(internals, Number.Zero);

            for (var i = 0; i < 1502; i++)
                compiled.Tick(internals, externals);
        }

        [TestMethod]
        public void GotoStringBug()
        {
            var ms = TestHelpers.Test("b=\"tt\" :pi += (b-\"t\")==b goto 1");
            Assert.AreEqual(1, ms.ProgramCounter);
        }

        [TestMethod]
        public void NullRefString()
        {
            var ms = TestHelpers.Test("c += \"t\"");
            Assert.AreEqual("0t", ms.GetVariable("c").String.ToString());
        }

        [TestMethod]
        public void NullRefString2()
        {
            var ms = TestHelpers.Test("c = c + \"t\"");
            Assert.AreEqual("0t", ms.GetVariable("c").String.ToString());
        }

        [TestMethod]
        public void ConstantEvaluation()
        {
            var result = TestHelpers.Test("a = \"2\" + 2 + 2");

            Assert.AreEqual("222", result.GetVariable("a").String.ToString());
        }

        [TestMethod]
        public void GotoDonePlusPlus()
        {
            var ms = TestHelpers.Test("goto:done++");

            Assert.AreEqual(1, ms.ProgramCounter);
        }

        [TestMethod]
        public void UnreachableCode()
        {
            TestHelpers.Test("if :i>8191 then :done=1 goto 1 end");
        }

        [TestMethod]
        public void NonStringSubtraction()
        {
            var ms = TestHelpers.Test("a=70 b=\"\"+a-0");
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
            var ms = TestHelpers.Test("a = abs -7");
            Assert.AreEqual((Number)7, ms.GetVariable("a").Number);
        }

        [TestMethod]
        public void NotModulo()
        {
            TestHelpers.Test(":o=not (:i%100)");
        }

        [TestMethod]
        public void Spaceship()
        {
            TestHelpers.Test("if :q then :x=0 goto l end if :q then :x++ end");
        }

        [TestMethod]
        public void ZijkhalBoolDivision()
        {
            const string code = "a=-9223372036854775.808 c=a/1";
            var ast = TestHelpers.Parse(code);

            var st = new MachineState(new NullDeviceNetwork(), 20);
            ast.Lines[0].Evaluate(1, st);

            var ms = TestHelpers.Test(code);

            Assert.AreEqual(st.GetVariable("c").Value, ms.GetVariable("c"));
        }

        [TestMethod]
        public void CraterIncrement()
        {
            var ms = TestHelpers.Test(":o=0 if v then :o++ end");
            Assert.AreEqual((Number)0, ms.GetVariable(":o").Number);
        }
    }
}
