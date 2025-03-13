namespace Diffables.Core
{
    public abstract class Diffable : IDiffable
    {
        public int RefId { get; set; }
        public int RefCount 
        { 
            get => _refCount;
            set
            { 
                _refCount = value;
                OnRefCountChanged?.Invoke((RefId, _refCount));
            }
        }
        public ChangeTree ChangeTree { get; set; } = new();
        public Action OnSetDirty { get; set; }
        public Action<(int RefId, int RefCount)> OnRefCountChanged { get; set; }
        private static int _nextRefId = 1;
        private int _refCount;

        protected Diffable() 
        { 
            RefId = _nextRefId++;
        }

        public abstract void Encode(SerializationContext context);
        public abstract void Decode(SerializationContext context);

        public void SetDirty(uint bitmaskBitProperty, Operation operation)
        {
            ChangeTree.DirtyPropertiesBitmask |= bitmaskBitProperty;
            if (ChangeTree.Operations.TryGetValue(bitmaskBitProperty, out Operation existingOperation) && existingOperation == Operation.Add)
            {
                // TODO: Not sure what's a good way to do this, but we should not overwrite the Add operation with Update
                // if we haven't yet Added and encoded the instance. Maybe we could track all operations done to a property single using a bitmask,
                // or overwrite the operation on case-by-case basis (since we can add/remove/replace/update etc.)
            }
            else
            {
                ChangeTree.Operations[bitmaskBitProperty] = operation;
            }
            OnSetDirty?.Invoke();
        }
    }
}
