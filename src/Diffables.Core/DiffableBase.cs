namespace Diffables.Core
{
    public abstract class DiffableBase : IDiffable
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

        protected DiffableBase() 
        { 
            RefId = _nextRefId++;
        }

        public abstract void Encode(SerializationContext context);
        public abstract void Decode(SerializationContext context);

        public void SetDirty(uint bitmaskBitProperty, Operation operation)
        {
            ChangeTree.DirtyPropertiesBitmask |= bitmaskBitProperty;
            ChangeTree.Operations[bitmaskBitProperty] = operation;
            OnSetDirty?.Invoke();
        }
    }
}
