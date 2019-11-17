using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void AddNumbers()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = 1 + 2").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual(3, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddStrings()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = \"1\" + \"2\"").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddMixedStrNum()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = \"1\" + 2").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void AddMixedNumStr()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = 1 + \"2\"").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual("12", st.GetVariable("a").Value);
        }

        [TestMethod]
        public void MultiplyNumbers()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = 3 * 2").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual(6, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void MultiplyStrings()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = \"3\" * \"2\"").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());

            Assert.ThrowsException<ExecutionException>(() =>
            {
                var r = c(0, st);
            });
        }

        [TestMethod]
        public void DivideNumbers()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = 6 / 2").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual(3, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void DivideStrings()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = \"3\" / \"2\"").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());

            Assert.ThrowsException<ExecutionException>(() =>
            {
                var r = c(0, st);
            });
        }

        [TestMethod]
        public void SubNumbers()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = 1 - 2").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual(-1, st.GetVariable("a").Value);
        }

        [TestMethod]
        public void SubStrings()
        {
            var t = Grammar.Tokenizer.TryTokenize("a = \"311\" - \"1\"").Value;
            var a = Grammar.Parser.TryParseLine(t).Value;
            var c = a.Compile();

            var st = new MachineState(new NullDeviceNetwork());
            var r = c(0, st);

            //Assert.AreEqual(1, r);
            Assert.AreEqual("31", st.GetVariable("a").Value);
        }
    }
}
