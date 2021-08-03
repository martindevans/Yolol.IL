using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sigil;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Compiler.Emitter;
using Yolol.IL.Extensions;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class EmitExtensionsTests
    {
        private void IdentityConversionTest<T>()
        {
            var type = typeof(T).ToStackType();

            var emitter = Emit<Func<T, T>>.NewDynamicMethod(strictBranchVerification: true);
            var optim = new OptimisingEmitter<Func<T, T>>(emitter);

            optim.EmitCoerce(type, type);

            Assert.AreEqual(0, optim.InstructionCount);
        }

        private Func<TA, TB> CreateFunc<TA, TB>(Action<OptimisingEmitter<Func<TA, TB>>> act)
        {
            var emitter = Emit<Func<TA, TB>>.NewDynamicMethod(strictBranchVerification: true);
            using (var optim = new OptimisingEmitter<Func<TA, TB>>(emitter))
            {
                optim.LoadArgument(0);
                act(optim);
                optim.Return();
            }

            return emitter.CreateDelegate();
        }

        [TestMethod]
        public void IdentityCoercions()
        {
            IdentityConversionTest<YString>();
            IdentityConversionTest<Number>();
            IdentityConversionTest<Value>();
            IdentityConversionTest<bool>();
        }

        [TestMethod]
        public void ErrorToValue()
        {
            var func = CreateFunc<StaticError, Value>(optim => {
                optim.EmitCoerce(StackType.StaticError, StackType.YololValue);
            });

            Assert.ThrowsException<ExecutionException>(() => func(new StaticError("Hello")));
        }

        [TestMethod]
        public void NumberToString()
        {
            var func = CreateFunc<Number, YString>(optim => {
                optim.EmitCoerce(StackType.YololNumber, StackType.YololString);
            });

            Assert.AreEqual("17", func((Number)17).ToString());
        }

        [TestMethod]
        public void StringToNumber()
        {
            Assert.ThrowsException<InvalidOperationException>(() => {
                CreateFunc<YString, Number>(optim =>{
                    optim.EmitCoerce(StackType.YololString, StackType.YololNumber);
                });
            });
        }

        private struct Foo
        {
            public int A;
            public int B => A;

            public Foo(int a)
            {
                A = a;
            }
        }

        [TestMethod]
        public void GetRuntimePropertyValue()
        {
            var func = CreateFunc<Foo, int>(optim => {
                optim.GetRuntimePropertyValue<Func<Foo, int>, Foo>(nameof(Foo.B));
            });

            Assert.AreEqual(17, func(new Foo(17)));
        }

        [TestMethod]
        public void GetRuntimeFieldValue()
        {
            var func = CreateFunc<Foo, int>(optim => {
                optim.GetRuntimeFieldValue<Func<Foo, int>, Foo>(nameof(Foo.A));
            });

            Assert.AreEqual(17, func(new Foo(17)));
        }
    }
}
