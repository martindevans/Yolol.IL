using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.IL.Compiler;

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
    }
}
