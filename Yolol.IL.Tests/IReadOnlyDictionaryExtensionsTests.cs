using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Grammar;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class IReadOnlyDictionaryExtensionsTests
    {
        [TestMethod]
        public void LowIndexBitFlag()
        {
            var d = new Dictionary<VariableName, int> {
                { new VariableName("A"), 0 }
            };
            var flag = d.ChangeSetKey(new VariableName("A")).Flag;

            Assert.AreEqual(1ul, flag);
        }

        [TestMethod]
        public void OverflowBitFlag()
        {
            var di = new Dictionary<VariableName, int> {
                { new VariableName("A"), 62 },
                { new VariableName("B"), 63 },
                { new VariableName("C"), 64 },
                { new VariableName("D"), 65 },
            };

            var a = di.ChangeSetKey(new VariableName("A")).Flag;
            var b = di.ChangeSetKey(new VariableName("B")).Flag;
            var c = di.ChangeSetKey(new VariableName("C")).Flag;
            var d = di.ChangeSetKey(new VariableName("D")).Flag;

            Assert.AreEqual(1ul << 62, a);
            Assert.AreEqual(1ul << 63, b);
            // ReSharper disable once ShiftExpressionRealShiftCountIsZero
            Assert.AreEqual(1ul << 64, c);
            // ReSharper disable once ShiftExpressionRightOperandNotEqualRealCount
            Assert.AreEqual(1ul << 65, d);
        }
    }
}
