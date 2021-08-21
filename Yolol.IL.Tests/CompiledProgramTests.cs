using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class CompiledProgramTests
    {
        [TestMethod]
        public void CreateEmptyProgramThrows()
        {
            Assert.ThrowsException<ArgumentException>(() => new CompiledProgram(new InternalsMap(), Array.Empty<JitLine>()));
        }

        [TestMethod]
        public void RunUntilChanged()
        {
            var ast = TestHelpers.Parse(":a=1", ":b=2", ":c=3", ":d=1");
            var ext = new ExternalsMap();
            var prog = ast.Compile(ext, 4, null, null, true);

            var i = new Value[prog.InternalsMap.Count];
            Array.Fill(i, new Value((Number)0));
            var e = new Value[ext.Count];
            Array.Fill(e, new Value((Number)0));

            var count = prog.Run(i, e, 4, ext.ChangeSetKey(new VariableName(":c")));

            Assert.AreEqual(3, count);
            Assert.AreEqual((Number)1, e[ext[new VariableName(":a")]]);
            Assert.AreEqual((Number)2, e[ext[new VariableName(":b")]]);
            Assert.AreEqual((Number)3, e[ext[new VariableName(":c")]]);
            Assert.AreEqual((Number)0, e[ext[new VariableName(":d")]]);
        }
    }
}
