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
        public static int Simple(Number a, Number b)
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
    }
}
