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
        public void FuzzDifference()
        {
            var ast = TestHelpers.Parse(
                "f=:f c=b>=sin :h!=a if b then :a=h b=:h goto ABS c else g=sin :h++^:b a+=\"cvxpy\" if :c then e=ABS e==h :e-=575284.008 else :a+=a d-=d==:e if ++e then :h^=h if a and :c! then goto f or e else goto b goto :b end else if :a then if a then h/=a else d++ end else :e*=not a-- end ++e end end end goto c --b goto :d --a g%=e --e",
                "if a then g=b else e=:c end",
                "d-- goto ABS a if f then :g/=:f if :f then :c=d! else goto d end d=f---h else if ABS :c then if b then d/=h else d=tan f or e*e end c=d else c=:a :e=f d=f end ++d end goto g if b then g=ABS b else if g then --a :c=b-:d>=--e-f>g%e else g=:a!=h%e g=b end end c++",
                "h=c f++ :c++ if d then if :c then :a-- c=a b=b if h then ++h else e++ if g then if f and e then --h f+=b :a++ goto d else if --c then a-- else :g=g if e then g*=tan a else d-- a=:e end end end c+=--e else goto --c end end else c=:h e=h a++ d-- end goto a goto f or d else :h^=tan e>=:e++ end a=b if b then e-=e and c if :h then h^=b g+=f goto h/e goto c else c-- end else e^=a g=g :f=asin d if c then d*=:a goto :g else goto :c if :h then :d=cos a and b if :a then goto :h goto d<d goto asin ++a==g+:g else if 1631903.144 then d/=c e=b a+=e if :h then e=d else --:d goto --:c*-c end else :b=h h^=c>=g>d end end else b*=a d/=h%c end end end",
                "goto c :g-=e d/=:h if g then goto :b else d=e d-- end :f=:d goto e if \"xdcwvqdwmzpeypb\" then goto acos h a=c h*=:f else :g++ end",
                "b+=g and h if :a then e-=acos :b --g c/=g-- goto b else d*=c goto e b++ a=asin \"mtidofadichdwvbujei\" end",
                "goto :h --f"
            );

            var compiled = TestHelpers.Test(ast, 128);
            var interpreted = TestHelpers.Interpret(ast, 128);

            Assert.AreEqual(compiled.ProgramCounter, interpreted.ProgramCounter);

            // duh, it's a string length thing
            Assert.Fail("compare values");
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
            var ms = TestHelpers.Test(":o=not (:i%100)");
        }

        [TestMethod]
        public void Spaceship()
        {
            var ms = TestHelpers.Test("if :q then :x=0 goto l end if :q then :x++ end");
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
