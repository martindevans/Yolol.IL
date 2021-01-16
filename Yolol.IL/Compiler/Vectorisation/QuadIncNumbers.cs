//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using System.Reflection;
//using System.Runtime.Intrinsics;
//using System.Runtime.Intrinsics.X86;
//using Sigil;
//using Yolol.Execution;
//using Yolol.Grammar;
//using Yolol.Grammar.AST.Expressions.Unary;
//using Yolol.Grammar.AST.Statements;
//using Yolol.IL.Extensions;
//using Type = Yolol.Execution.Type;

//namespace Yolol.IL.Compiler.Vectorisation
//{
//    internal class QuadIncNumbers<TEmit>
//        : BaseVectorisationStrategy<TEmit>
//    {
//        private readonly InternalsMap _internalsMap;
//        private readonly ExternalsMap _externalsMap;
//        private readonly Local _internalLocal;
//        private readonly Local _externalLocal;
//        private readonly IReadOnlyDictionary<VariableName, Type>? _types;

//        public QuadIncNumbers(InternalsMap internalsMap, ExternalsMap externalsMap, Local internalLocal, Local externalLocal, IReadOnlyDictionary<VariableName, Type>? types)
//        {
//            _internalsMap = internalsMap;
//            _externalsMap = externalsMap;
//            _internalLocal = internalLocal;
//            _externalLocal = externalLocal;
//            _types = types;
//        }

//        public override bool Try(List<BaseStatement> statements, Emit<TEmit> emitter)
//        {
//            // Currently not active:
//            // - Not actually faster (no surprise, this was just a proof of concept)
//            // - no runtime check for SIMD availability
//            return false;

//            if (statements.Count < 4)
//                return false;
//            if (_types == null)
//                return false;

//            // If they are not all expression wrappers they can't be simple increments
//            if (!statements.Take(4).All(c => c is ExpressionWrapper))
//                return false;
//            var wrappers = statements.Take(4).Cast<ExpressionWrapper>();

//            // Check that all wrappers contain a pre or post inc (it doesn't matter which, return value is unused)
//            if (!wrappers.All(w => w.Expression is PostIncrement || w.Expression is PreIncrement))
//                return false;
//            var incs = wrappers.Select(a => (BaseIncrement)a.Expression);

//            // Check that all the variables are different
//            if (incs.GroupBy(a => a.Name.Name).Count() != 4)
//                return false;

//            // Check that all the variables are known to be numbers
//            if (incs.Any(a => _types.GetValueOrDefault(a.Name, Type.Unassigned) != Type.Number))
//                return false;

//            // Get the indices
//            var indices = incs.Select(a => GetVarIndex(a.Name)).OrderBy(a => a.Item2).ToArray();

//            // Check that they all touch the same array (all locals or all externals)
//            if (indices.GroupBy(a => a.Item1).Count() != 1)
//                return false;

//            // Load the necessary array onto the stack
//            var arrayValues = indices[0].Item1 ? _externalLocal : _internalLocal;

//            // Load the 4 values as longs onto the stack
//            for (var i = 0; i < indices.Length; i++)
//                EmitLoadElement(emitter, arrayValues, indices[i].Item2);

//            // Create the first vector of values to increment
//            emitter.Call(typeof(Vector256).GetMethod(
//                nameof(Vector256.Create),
//                BindingFlags.Public | BindingFlags.Static,
//                null,
//                new[] { typeof(long), typeof(long), typeof(long), typeof(long) },
//                null
//            ));

//            // Create the second vector of 4x1000
//            emitter.LoadConstant(1000L);
//            emitter.Duplicate();
//            emitter.Duplicate();
//            emitter.Duplicate();
//            emitter.Call(typeof(Vector256).GetMethod(
//                nameof(Vector256.Create),
//                BindingFlags.Public | BindingFlags.Static,
//                null,
//                new[] { typeof(long), typeof(long), typeof(long), typeof(long) },
//                null
//            ));

//            // Add them
//            emitter.Call(typeof(Avx2).GetMethod(
//                nameof(Avx2.Add),
//                BindingFlags.Public | BindingFlags.Static,
//                null,
//                new[] { typeof(Vector256<long>), typeof(Vector256<long>) },
//                null
//            ));

//            // Store back where they came from
//            EmitStoreElements(emitter, arrayValues, indices.Select(a => a.Item2).ToArray());

//            // Remove the statements we managed to compile
//            statements.RemoveAt(0);
//            statements.RemoveAt(0);
//            statements.RemoveAt(0);
//            statements.RemoveAt(0);

//            //todo: add runtime check for vector support
//            //throw new NotImplementedException("Add runtime check");
//            return true;
//        }

//        private static void EmitLoadElement(Emit<TEmit> emitter, Local array, int index)
//        {
//            // Load `Value` from array
//            emitter.LoadLocal(array);
//            emitter.LoadConstant(index);
//            emitter.CallRuntimeN(nameof(Runtime.GetArraySegmentIndex), typeof(ArraySegment<Value>), typeof(int));

//            // Get `Number` from `Value`
//            emitter.GetRuntimePropertyValue<TEmit, Value>(nameof(Value.Number));

//            // Get raw long value from `Number`
//            emitter.GetRuntimeFieldValue<TEmit, Number>("_value");
//        }

//        private static void EmitStoreElements(Emit<TEmit> emitter, Local array, IReadOnlyList<int> arrayIndices)
//        {
//            emitter.LoadLocal(array);
//            emitter.LoadConstant(arrayIndices[0]);
//            emitter.LoadConstant(arrayIndices[1]);
//            emitter.LoadConstant(arrayIndices[2]);
//            emitter.LoadConstant(arrayIndices[3]);
//            emitter.CallRuntimeN(
//                nameof(Runtime.ExtractVectorNumbers),
//                typeof(Vector256<long>),
//                typeof(ArraySegment<Value>),
//                typeof(int),
//                typeof(int),
//                typeof(int),
//                typeof(int)
//            );
//        }

//        private (bool, int) GetVarIndex(VariableName name)
//        {
//            if (name.IsExternal)
//                return (true, _externalsMap.GetValueOrDefault(name.Name, -1));
//            else
//                return (false, _internalsMap.GetValueOrDefault(name.Name, -1));
//        }
//    }
//}
