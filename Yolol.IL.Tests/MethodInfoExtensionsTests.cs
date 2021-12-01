using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yolol.Execution;
using Yolol.Execution.Attributes;
using Yolol.IL.Extensions;
using Type = System.Type;

namespace Yolol.IL.Tests
{
    [TestClass]
    public class MethodInfoExtensionsTests
    {
        public static bool SimpleWillThrow(Number a, Number b)
        {
            return a < b;
        }

        [ErrorMetadata(nameof(SimpleWillThrow), null)]
        public static int Simple(Number _1, Number _2)
        {
            return 0;
        }

        public static int Simple([TypeImplication(Execution.Type.Number)] Value _1, [TypeImplication(Execution.Type.String)] Value _2)
        {
            return 0;
        }

        private static MethodInfo Get(params Type[] parameters)
        {
            var method = typeof(MethodInfoExtensionsTests).GetMethod(nameof(Simple), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, parameters, null);
            Assert.IsNotNull(method);
            return method;
        }

        [TestMethod]
        public void GetSimpleMethod()
        {
            var method = Get(typeof(Number), typeof(Number));

            var err = method.TryGetErrorMetadata(typeof(Number), typeof(Number));
            Assert.IsNotNull(err);

            Assert.AreEqual(method, err.Value.OriginalMethod);
            Assert.IsNull(err.Value.UnsafeAlternative);
            Assert.AreEqual(nameof(SimpleWillThrow), err.Value.WillThrow?.Name);
        }

        [TestMethod]
        public void StaticWillThrow()
        {
            var method = Get(typeof(Number), typeof(Number));

            var err = method.TryGetErrorMetadata(typeof(Number), typeof(Number));
            Assert.IsNotNull(err);

            Assert.IsTrue(err.Value.StaticWillThrow(new[] {(Value?)1, (Value?)2}));
            Assert.IsFalse(err.Value.StaticWillThrow(new[] {(Value?)2, (Value?)1}));
            Assert.IsNull(err.Value.StaticWillThrow(new[] {null, (Value?)1}));
            Assert.IsNull(err.Value.StaticWillThrow(new[] {(Value?)1, null}));
        }

        [TestMethod]
        public void GetSimpleMethodTypeImplications()
        {
            var method = Get(typeof(Value), typeof(Value));

            var impls = method.GetTypeImplications();

            Assert.IsNotNull(impls);
            Assert.AreEqual(2, impls.Count);
            Assert.AreEqual(Execution.Type.Number.ToStackType(), impls[0]);
            Assert.AreEqual(Execution.Type.String.ToStackType(), impls[1]);
        }


        [TestMethod]
        public void GetLastCharacterMetadata()
        {
            // Get the "will throw" method to check if `LastCharacter` throws
            var dec = typeof(YString).GetMethod(nameof(YString.LastCharacter), BindingFlags.NonPublic | BindingFlags.Static)!;
            var err = dec.TryGetErrorMetadata(typeof(YString));

            Assert.IsNotNull(err);
        }
    }
}
