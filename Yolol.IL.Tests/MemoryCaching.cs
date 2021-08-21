using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.Grammar;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class MemoryCaching
    {
        [TestMethod]
        public void SingleAssignment()
        {
            var st = Test("a=7");

            Assert.AreEqual(7, (int)st.GetVariable("a").Number);
        }

        [TestMethod]
        public void SingleAssignmentWithAnnotations()
        {
            var st = Test("a=7", staticTypes: new Dictionary<VariableName, Type> {
                {  new VariableName("a"), Type.Number },
            });

            Assert.AreEqual(7, (int)st.GetVariable("a").Number);
        }

        [TestMethod]
        public void OverwriteAssignment()
        {
            var st = Test("a=7 a=8");

            Assert.AreEqual(8, (int)st.GetVariable("a").Number);
        }

        [TestMethod]
        public void OverwriteTypeAssignment()
        {
            var st = Test("a=7 a=\"8\"");

            Assert.AreEqual("8", st.GetVariable("a").String.ToString());
        }
    }
}
