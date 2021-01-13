using System;
using System.Reflection;
using Sigil;
using Yolol.IL.Compiler;

namespace Yolol.IL.Extensions
{
    internal static class EmitExtensions
    {
        /// <summary>
        /// Call a method, taking a given set of arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TCallee"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        public static void CallRuntimeN<TEmit, TCallee>(this Emit<TEmit> emitter, string methodName, params Type[] args)
        {
            var method = typeof(TCallee).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, args, null);
            emitter.Call(method);
        }

        /// <summary>
        /// Call a method on the `Runtime` class, taking a given set of arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="args"></param>
        public static void CallRuntimeN<TEmit>(this Emit<TEmit> emitter, string methodName, params Type[] args)
        {
            var method = typeof(Runtime).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, args, null);
            emitter.Call(method);
        }

        /// <summary>
        /// Call an instance method on a given type, taking zero arguments
        /// </summary>
        /// <typeparam name="TEmit"></typeparam>
        /// <typeparam name="TCallee"></typeparam>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        public static void CallRuntimeThis0<TEmit, TCallee>(this Emit<TEmit> emitter, string methodName)
        {
            using (var local = emitter.DeclareLocal(typeof(TCallee)))
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
        public static void CallRuntime2<TEmit, TCallee>(this Emit<TEmit> emitter, string methodName, TypeStack<TEmit> types)
        {
            // Get the left and right items from the type stack
            var r = types.Peek;
            types.Pop(r);
            var l = types.Peek;
            types.Push(r);

            var method = typeof(TCallee).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { l.ToType(), r.ToType() }, null);
            emitter.Call(method);
        }

        /// <summary>
        /// Call a runtime method with one parameter (type determined by type stack)
        /// </summary>
        /// <param name="emitter"></param>
        /// <param name="methodName"></param>
        /// <param name="types"></param>
        /// <returns></returns>
        public static Type CallRuntime1<TEmit, TCallee>(this Emit<TEmit> emitter, string methodName, TypeStack<TEmit> types)
        {
            // Get the parameter type
            var p = types.Peek;

            var method = typeof(TCallee).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static, null, new[] { p.ToType() }, null);
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
        public static void GetRuntimePropertyValue<TEmit, TType>(this Emit<TEmit> emitter, string propertyName)
        {
            // ReSharper disable once PossibleNullReferenceException
            var method = typeof(TType).GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static).GetMethod;

            using (var local = emitter.DeclareLocal(typeof(TType)))
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
        public static void GetRuntimeFieldValue<TEmit, TType>(this Emit<TEmit> emitter, string fieldName)
        {
            var field = typeof(TType).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static);
            
            using (var local = emitter.DeclareLocal(typeof(TType)))
            {
                emitter.StoreLocal(local);
                emitter.LoadLocalAddress(local);
                emitter.LoadField(field);
            }
        }
    }
}
