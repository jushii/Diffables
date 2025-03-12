﻿namespace Diffables.Core
{
    public abstract class DiffableBase : IDiffable
    {
        public int RefId { get; set; }
        public int RefCount { get; set; }
        public ChangeTree ChangeTree { get; set; } = new();
        public Action OnSetDirty { get; set; }
        protected uint _dirtyPropertiesBitmask;
        private static int _nextRefId = 1;

        protected DiffableBase() 
        { 
            RefId = _nextRefId++;
        }

        public uint GetDirtyPropertiesBitmask() => _dirtyPropertiesBitmask;
        public void ResetDirtyPropertiesBitmask() => _dirtyPropertiesBitmask = 0;

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
