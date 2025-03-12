namespace Diffables.Core
{
    public abstract class DiffableBase : IDiffable
    {
        public int RefId { get; set; }
        public IDiffable? Parent { get; set; }
        public uint BitmaskBitParent { get; set; }
        protected uint _dirtyPropertiesBitmask;
        private static int _nextRefId = 1;

        protected DiffableBase() 
        { 
            RefId = _nextRefId++;
        }

        /// <summary>
        /// Set the reference to the parent of this <see cref="IDiffable"/> and
        /// store the bitmask property bit that represents this instance in the parent.
        /// </summary>
        public void SetParent(IDiffable parent, uint bitmaskBitParent)
        {
            Parent = parent;
            BitmaskBitParent = bitmaskBitParent;
        }

        // Allows deserialization to override the assigned RefId.
        protected void SetRefId(int refId)
        {
            RefId = refId;
            if (refId >= _nextRefId)
            { 
                _nextRefId = refId + 1; 
            }
        }

        /// <summary>
        /// Mark a property of this <see cref="IDiffable"/> as dirty and mark the bit that represents this <see cref="IDiffable"/> 
        /// property in the parent's dirty bitmask as dirty.
        /// </summary>
        /// <param name="propertyBitmaskBit"></param>
        public void SetDirty(uint propertyBitmaskBit) 
        {
            _dirtyPropertiesBitmask |= propertyBitmaskBit;
            // Bubble up the dirty state to the parent.
            Parent?.SetChildDirty(BitmaskBitParent);
        }

        public void SetChildDirty(uint bitmaskBitChild)
        {
            _dirtyPropertiesBitmask |= bitmaskBitChild;
            Parent?.SetChildDirty(BitmaskBitParent);
        }

        public uint GetDirtyPropertiesBitmask() => _dirtyPropertiesBitmask;
        public void ResetDirtyPropertiesBitmask() => _dirtyPropertiesBitmask = 0;

        public abstract void EncodeV2(SerializationContext context);
        public abstract void DecodeV2(SerializationContext context);
    }
}
