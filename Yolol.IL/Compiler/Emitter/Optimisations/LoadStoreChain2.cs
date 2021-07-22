//using System;
//using System.Collections.Generic;
//using Yolol.IL.Compiler.Emitter.Instructions;
//using System.Linq;

//namespace Yolol.IL.Compiler.Emitter.Optimisations
//{
//    internal class LoadStoreChain2
//        : BaseOptimisation
//    {
//        public LoadStoreChain2()
//            : base(new[] {
//                typeof(LoadLocal),
//                typeof(StoreLocal),
//            })
//        {
//        }

//        protected override bool PreReplace(IReadOnlyList<BaseInstruction> instructions, List<BaseInstruction> slice, int matchStart, int matchLength)
//        {
//            if (matchLength != 2)
//                throw new ArgumentException("incorrect instruction count");

//            var load = (LoadLocal)slice[0];
//            var store = (StoreLocal)slice[1];

//            if (store.Local.Equals(load.Local))
//                return false;
//            if (!store.Local.LocalType.IsValueType)
//                return false;

//            // This optimisation cannot be performed if:
//            // - Stored local is ever stored to again
//            // - A writeable address to the stored local is ever taken
//            // - The loaded local is written before the last load of the stored local

//            var rewriteFound = false;
//            for (var i = matchStart + matchLength; i < instructions.Count; i++)
//            {
//                var op = instructions[i];
//                if (op is StoreLocal sl)
//                {
//                    // first local was stored to, fail if we find a load of the stored local after this
//                    if (sl.Local.Equals(load.Local))
//                        rewriteFound = true;

//                    // Check if stored local has been stored to again
//                    else if (sl.Local.Equals(store.Local))
//                        return false;
//                }
//                else if (op is LoadLocal ll && ll.Local.Equals(store.Local))
//                {
//                    if (rewriteFound)
//                        return false;
//                }
//                else if (op is LoadLocalAddress lla && !lla.IsReadonly && lla.Local.Equals(store.Local))
//                {
//                    return false;
//                }

//            }

//            return true;
//        }

//        protected override bool ReplaceWide(List<BaseInstruction> instructions, int matchStart, int matchLength)
//        {
//            if (matchLength != 2)
//                throw new ArgumentException("incorrect instruction count");

//            var load = (LoadLocal)instructions[matchStart];
//            var store = (StoreLocal)instructions[matchStart + 1];

//            for (var i = 0; i < instructions.Count; i++)
//            {
//                if (!(instructions[i] is LoadLocal ll))
//                    continue;

//                if (!ll.Local.Equals(store.Local))
//                    continue;

//                instructions[i] = new LoadLocal(load.Local);
//            }

//            instructions.RemoveAt(matchStart);
//            instructions.RemoveAt(matchStart);

//            return true;
//        }

//        protected override bool Replace(List<BaseInstruction> instructions)
//        {
//            throw new NotSupportedException();
//        }
//    }
//}
