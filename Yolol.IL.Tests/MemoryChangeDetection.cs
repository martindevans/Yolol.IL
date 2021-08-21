using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Grammar;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class MemoryChangeDetection
    {
        [TestMethod]
        public void AssignedVarsAreMarked()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1");

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsTrue(cs.Contains(a));
            Assert.IsTrue(cs.Contains(b));
            Assert.IsTrue(cs.Contains(c));
        }

        [TestMethod]
        public void UnassignedVarsAreNotMarked()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1");

            var d = st.GetVariableChangeSetKey(new VariableName(":d"));
            var e = st.GetVariableChangeSetKey(new VariableName(":e"));

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(d));
            Assert.IsFalse(cs.Contains(e));
        }

        [TestMethod]
        public void MultiLineChanges()
        {
            var st = Test(new[] { ":a=1", ":b=2", ":c=3" }, 3);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(a));
            Assert.IsFalse(cs.Contains(b));
            Assert.IsTrue(cs.Contains(c));
        }
    }
}
