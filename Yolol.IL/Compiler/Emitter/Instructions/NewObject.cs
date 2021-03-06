﻿using System.Reflection;
using Sigil;

namespace Yolol.IL.Compiler.Emitter.Instructions
{
    internal class NewObjectConstructor
        : BaseInstruction
    {
        private readonly ConstructorInfo _cons;

        public NewObjectConstructor(ConstructorInfo cons)
        {
            _cons = cons;
        }

        public override void Emit<T>(Emit<T> emitter)
        {
            emitter.NewObject(_cons);
        }

        public override string ToString()
        {
            return $"NewObject({_cons})";
        }
    }
}
