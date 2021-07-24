using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Grammar.AST;
using Yolol.IL.Compiler;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class CompileExtensionTests
    {
        [TestMethod]
        public void CreateEmptyProgramThrows()
        {
            var p = new Program(new[] {
                new Line(new Grammar.AST.Statements.StatementList()),
                new Line(new Grammar.AST.Statements.StatementList()),
                new Line(new Grammar.AST.Statements.StatementList()),
                new Line(new Grammar.AST.Statements.StatementList()),
                new Line(new Grammar.AST.Statements.StatementList()),
            });

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => p.Compile(new ExternalsMap(), 4));
        }
    }
}
