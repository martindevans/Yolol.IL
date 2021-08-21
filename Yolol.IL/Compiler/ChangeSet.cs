namespace Yolol.IL.Compiler
{
    public readonly struct ChangeSet
    {
        private readonly ulong _bits;

        internal ChangeSet(ulong bits)
        {
            _bits = bits;
        }

        /// <summary>
        /// Test if this set contains the given key. May returns false positives. Will not return false negatives.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Contains(ChangeSetKey key)
        {
            return (_bits & key.Flag) != 0;
        }
    }

    public readonly struct ChangeSetKey
    {
        internal readonly ulong Flag;

        internal ChangeSetKey(ulong flag)
        {
            Flag = flag;
        }
    }
}
