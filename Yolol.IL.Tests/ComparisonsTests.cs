using Microsoft.VisualStudio.TestTools.UnitTesting;

using static Yolol.IL.Tests.TestHelpers;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class ComparisonsTests
    {
        private static void Cmp(string op, string a, string b, bool aa, bool ab, bool ba)
        {
            var (st, _) = Test($"a={a}{op}{a} b={a}{op}{b} c={b}{op}{a}");

            Assert.AreEqual(aa, st.GetVariable("a").ToBool(), $"{a}{op}{a}");
            Assert.AreEqual(ab, st.GetVariable("b").ToBool(), $"{a}{op}{b}");
            Assert.AreEqual(ba, st.GetVariable("c").ToBool(), $"{b}{op}{a}");
        }

        private static void CmpVal(string op, string a, string b, bool aa, bool ab, bool ba)
        {
            var (st, _) = Test($"l={a} r={b} a={a}{op}l b={a}{op}r  c={b}{op}l");

            Assert.AreEqual(aa, st.GetVariable("a").ToBool(), $"L={a} Result={a}{op}L");
            Assert.AreEqual(ab, st.GetVariable("b").ToBool(), $"R={b} Result={a}{op}R");
            Assert.AreEqual(ba, st.GetVariable("c").ToBool(), $"L={a} Result={b}{op}L");
        }

        #region bools
        [TestMethod]
        public void GreaterThanBoolBool() => Cmp(">", "1", "0", false, true, false);

        [TestMethod]
        public void GreaterThanEqualToBoolBool() => Cmp(">=", "1", "0", true, true, false);

        [TestMethod]
        public void LessThanBoolBool() => Cmp("<", "1", "0", false, false, true);

        [TestMethod]
        public void LessThanEqualToBoolBool() => Cmp("<=", "1", "0", true, false, true);

        [TestMethod]
        public void EqualToBoolBool() => Cmp("==", "1", "0", true, false, false);

        [TestMethod]
        public void NotEqualToBoolBool() => Cmp("!=", "1", "0", false, true, true);
        #endregion

        #region bool string
        [TestMethod]
        public void GreaterThanBoolStr() => Cmp(">", "1", "\"0\"", false, true, false);

        [TestMethod]
        public void GreaterThanEqualToBoolStr() => Cmp(">=", "1", "\"0\"", true, true, false);

        [TestMethod]
        public void LessThanBoolStr() => Cmp("<", "1", "\"0\"", false, false, true);

        [TestMethod]
        public void LessThanEqualToBoolStr() => Cmp("<=", "1", "\"0\"", true, false, true);

        [TestMethod]
        public void EqualToBoolStr() => Cmp("==", "1", "\"0\"", true, false, false);

        [TestMethod]
        public void NotEqualToBoolStr() => Cmp("!=", "1", "\"0\"", false, true, true);
        #endregion

        #region bool num
        [TestMethod]
        public void GreaterThanBoolNum() => Cmp(">", "1", "7", false, false, true);

        [TestMethod]
        public void GreaterThanEqualToBoolNum() => Cmp(">=", "1", "7", true, false, true);

        [TestMethod]
        public void LessThanBoolNum() => Cmp("<", "1", "7", false, true, false);

        [TestMethod]
        public void LessThanEqualToBoolNum() => Cmp("<=", "1", "7", true, true, false);

        [TestMethod]
        public void EqualToBoolNum() => Cmp("==", "1", "7", true, false, false);

        [TestMethod]
        public void NotEqualToBoolNum() => Cmp("!=", "1", "7", false, true, true);
        #endregion

        #region bool val
        [TestMethod]
        public void Playground()
        {
            var (st, _) = Test($"L=1 R=7 a=1==L b=1==R c=7==L");

            Assert.AreEqual(true, st.GetVariable("a").ToBool());
            Assert.AreEqual(false, st.GetVariable("b").ToBool());
            Assert.AreEqual(false, st.GetVariable("c").ToBool());
        }

        [TestMethod]
        public void GreaterThanBoolVal() => CmpVal(">", "1", "7", false, false, true);

        [TestMethod]
        public void GreaterThanEqualToBoolVal() => CmpVal(">=", "1", "7", true, false, true);

        [TestMethod]
        public void LessThanBoolVal() => CmpVal("<", "1", "7", false, true, false);

        [TestMethod]
        public void LessThanEqualToBoolVal() => CmpVal("<=", "1", "7", true, true, false);

        [TestMethod]
        public void EqualToBoolVal() => CmpVal("==", "1", "7", true, false, false);

        [TestMethod]
        public void NotEqualToBoolVal() => CmpVal("!=", "1", "7", false, true, true);
        #endregion
    }
}
