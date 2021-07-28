using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class Call
        : BaseInstruction
    {
        private readonly MethodInfo _method;
        private readonly Type[]? _arglist;

        public Call(MethodInfo method, Type[]? arglist)
        {
            _method = method;
            _arglist = arglist;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.Call(_method, _arglist);
        }

        [ExcludeFromCodeCoverage]
        public override string ToString()
        {
            return $"Call({_method})";
        }
    }
}
