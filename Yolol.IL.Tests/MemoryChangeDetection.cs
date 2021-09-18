using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Grammar;
using Yolol.IL.Compiler;
using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class MemoryChangeDetection
    {
        [TestMethod]
        public void AssignedVarsAreMarkedWhenDisabled()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1", changeDetection: false);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsTrue(cs.Contains(a));
            Assert.IsTrue(cs.Contains(b));
            Assert.IsTrue(cs.Contains(c));
        }

        [TestMethod]
        public void AssignedVarsAreMarked()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1", changeDetection: true);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsTrue(cs.Contains(a));
            Assert.IsTrue(cs.Contains(b));
            Assert.IsTrue(cs.Contains(c));
        }

        [TestMethod]
        public void CombinedKeysAreMarkedIfAny()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1", changeDetection: true);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));
            var d = st.GetVariableChangeSetKey(new VariableName(":c"));
            var combined = ChangeSetKey.Combine(a, ChangeSetKey.Combine(b, ChangeSetKey.Combine(c, d)));

            var cs = st.ChangeSet;

            Assert.IsTrue(cs.Contains(combined));
        }

        [TestMethod]
        public void CombinedKeysAreNotMarkedIfNone()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1", changeDetection: true);

            var d = st.GetVariableChangeSetKey(new VariableName(":d"));
            var e = st.GetVariableChangeSetKey(new VariableName(":e"));
            var combined = ChangeSetKey.Combine(d, e);

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(combined));
        }

        [TestMethod]
        public void UnassignedVarsAreNotMarked()
        {
            var st = Test(":a=7 :b=8 :c=9 x/=:d :e=1", changeDetection: true);

            var d = st.GetVariableChangeSetKey(new VariableName(":d"));
            var e = st.GetVariableChangeSetKey(new VariableName(":e"));

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(d));
            Assert.IsFalse(cs.Contains(e));
        }

        [TestMethod]
        public void MultiLineChanges()
        {
            var st = Test(new[] { ":a=1", ":b=2", ":c=3" }, 3, changeDetection: true);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));
            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(a));
            Assert.IsFalse(cs.Contains(b));
            Assert.IsTrue(cs.Contains(c));
        }

        [TestMethod]
        public void ChangeAfterErrorIsNotMarked()
        {
            var st = Test(":a=7 x/=0 :b=1", changeDetection: true);

            var a = st.GetVariableChangeSetKey(new VariableName(":a"));
            var b = st.GetVariableChangeSetKey(new VariableName(":b"));

            var cs = st.ChangeSet;

            Assert.IsTrue(cs.Contains(a));
            Assert.IsFalse(cs.Contains(b));
        }

        [TestMethod]
        public void NoChangeForNonExistantVar()
        {
            var st = Test(":a=7 x/=0 :b=1", changeDetection: true);

            var c = st.GetVariableChangeSetKey(new VariableName(":c"));

            var cs = st.ChangeSet;

            Assert.IsFalse(cs.Contains(c));
        }
    }
}
