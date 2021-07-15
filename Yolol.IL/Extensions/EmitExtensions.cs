using System;
using System.Reflection;
using Sigil;
using Yolol.Execution;
using Yolol.IL.Compiler;
using Yolol.IL.Compiler.Emitter;
using Type = System.Type;

namespace Yolol.IL.Extensions
{
    internal static class EmitExtensions
    {
        /// <summary>
        /// Call a method, taking a given set of arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="tcallee"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        public static void CallRuntimeN<TEmit>(this OptimisingEmitter<TEmit> emitter, Type tcallee, string methodName, params Type[] args)
        {
            var method = tcallee.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, args, null)!;
            emitter.Call(method);
        }

        /// <summary>
        /// Call a method, taking a given set of arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TCallee"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        public static void CallRuntimeN<TEmit, TCallee>(this OptimisingEmitter<TEmit> emitter, string methodName, params Type[] args)
        {
            CallRuntimeN(emitter, typeof(TCallee), methodName, args);
        }

        /// <summary>
        /// Call a method on the `Runtime` class, taking a given set of arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        public static void CallRuntimeN<TEmit>(this OptimisingEmitter<TEmit> emitter, string methodName, params Type[] args)
        {
            var method = typeof(Runtime).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, args, null)!;
            emitter.Call(method);
        }

        /// <summary>
        /// Call an instance method on a given type, taking zero arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TCallee"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        public static void CallRuntimeThis0<TEmit, TCallee>(this OptimisingEmitter<TEmit> emitter, string methodName)
        {
            using (var local = emitter.DeclareLocal(typeof(TCallee), "CallRuntimeThis0_Callee", false))
            {
                emitter.StoreLocal(local);
                emitter.LoadLocalAddress(local);
                emitter.CallRuntimeN<TEmit, TCallee>(methodName);
            }
        }

        /// <summary>
        /// Call a runtime method with two parameters (types determined by type stack)
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TCallee"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public static void CallRuntime2<TEmit, TCallee>(this OptimisingEmitter<TEmit> emitter, string methodName, TypeStack<TEmit> types)
        {
            // Get the left and right items from the type stack
            var r = types.Peek;
            types.Pop(r);
            var l = types.Peek;
            types.Push(r);

            var method = typeof(TCallee).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { l.ToType(), r.ToType() }, null)!;
            emitter.Call(method);
        }

        /// <summary>
        /// Call a runtime method with one parameter (type determined by type stack)
        /// </summary>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public static Type CallRuntime1<TEmit, TCallee>(this OptimisingEmitter<TEmit> emitter, string methodName, TypeStack<TEmit> types)
        {
            // Get the parameter type
            var p = types.Peek;

            var method = typeof(TCallee).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { p.ToType() }, null)!;
            emitter.Call(method);

            return method!.ReturnType;
        }

        /// <summary>
        /// Get the value of a property on a type
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TType"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="propertyName"></param>
        public static void GetRuntimePropertyValue<TEmit, TType>(this OptimisingEmitter<TEmit> emitter, string propertyName)
        {
            // ReSharper disable once PossibleNullReferenceException
            var method = typeof(TType).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)!.GetMethod;

            using (var local = emitter.DeclareLocal(typeof(TType), "GetRuntimePropertyValue_Callee", false))
            {
                emitter.StoreLocal(local);
                emitter.LoadLocalAddress(local);
                emitter.Call(method);
            }
        }

        /// <summary>
        /// Get the value of a field (even a private one) on a type
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TType"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="fieldName"></param>
        public static void GetRuntimeFieldValue<TEmit, TType>(this OptimisingEmitter<TEmit> emitter, string fieldName)
        {
            var field = typeof(TType).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static)!;
            
            using (var local = emitter.DeclareLocal(typeof(TType), "GetRuntimeFieldValue_Callee", false))
            {
                emitter.StoreLocal(local);
                emitter.LoadLocalAddress(local);
                emitter.LoadField(field);
            }
        }

        /// <summary>
        /// Coerce an object of the given type into another type
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="input"></param>
        /// <param name="output"></param>
        public static void EmitCoerce<TEmit>(this OptimisingEmitter<TEmit> emitter, StackType input, StackType output)
        {
            switch (input, output)
            {
                #region identity conversions
                case (StackType.YololString, StackType.YololString):
                case (StackType.YololNumber, StackType.YololNumber):
                case (StackType.YololValue, StackType.YololValue):
                case (StackType.Bool, StackType.Bool):
                case (StackType.StaticError, StackType.StaticError):
                    break;
                #endregion

                #region error conversion
                case (StackType.StaticError, StackType.YololValue):
                    emitter.CallRuntimeN(nameof(Runtime.ErrorToValue), typeof(StaticError));
                    break;
                #endregion

                #region bool source
                case (StackType.Bool, StackType.YololValue):
                    emitter.NewObject<Value, bool>();
                    break;

                case (StackType.Bool, StackType.YololNumber):
                    emitter.CallRuntimeN(nameof(Runtime.BoolToNumber), typeof(bool));
                    break;
                #endregion

                #region string source
                case (StackType.YololString, StackType.Bool):
                    emitter.Pop();
                    emitter.LoadConstant(false);
                    break;

                case (StackType.YololString, StackType.YololValue):
                    emitter.NewObject<Value, YString>();
                    break;
                #endregion

                #region YololNumber source
                case (StackType.YololNumber, StackType.YololValue):
                    emitter.NewObject<Value, Number>();
                    break;

                case (StackType.YololNumber, StackType.Bool):
                    emitter.CallRuntimeN(nameof(Runtime.NumberToBool), typeof(Number));
                    break;

                case (StackType.YololNumber, StackType.YololString):
                    emitter.CallRuntimeThis0<TEmit, Number>(nameof(Number.ToString));
                    emitter.NewObject<YString, string>();
                    break;
                #endregion

                #region YololValue source
                case (StackType.YololValue, StackType.Bool): {
                    using (var conditional = emitter.DeclareLocal(typeof(Value), "EmitCoerce_ValueToBool_Conditional", false))
                    {
                        emitter.StoreLocal(conditional);
                        emitter.LoadLocalAddress(conditional);
                        emitter.Call(typeof(Value).GetMethod(nameof(Value.ToBool))!);
                    }
                    break;
                }
                #endregion

                default:
                    throw new InvalidOperationException($"Cannot coerce `{input}` -> `{output}`");
            }
        }

        /// <summary>
        /// Define a new label which may be marked later.
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static Label2<TEmit> DefineLabel2<TEmit>(this OptimisingEmitter<TEmit> emitter, string name)
        {
            return new Label2<TEmit>(emitter, name);
        }

        /// <summary>
        /// Mark the destination of a previously defined label
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="_"></param>
        /// <param name="label"></param>
        public static void MarkLabel<TEmit>(this OptimisingEmitter<TEmit> _, Label2<TEmit> label)
        {
            label.Mark();
        }

        /// <summary>
        /// Unconditionally branch to a given label
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="_"></param>
        /// <param name="label"></param>
        public static void Branch<TEmit>(this OptimisingEmitter<TEmit> _, Label2<TEmit> label)
        {
            label.Branch();
        }
    }

    public class Label2<TEmit>
    {
        private readonly OptimisingEmitter<TEmit> _emit;
        private readonly Label _label;

        public bool IsUsed { get; private set; }

        public Label2(OptimisingEmitter<TEmit> emit, string name)
        {
            _emit = emit;
            _label = emit.DefineLabel(name);
        }

        public void Mark()
        {
            _emit.MarkLabel(_label);
        }

        public void Branch()
        {
            _emit.Branch(_label);
            IsUsed = true;
        }
    }

    internal class TypedLocal
    {
        public StackType Type { get; }
        public Local Local { get; }

        public TypedLocal(StackType type, Local local)
        {
            Type = type;
            Local = local;
        }
    }
}
