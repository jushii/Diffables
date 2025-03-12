namespace Diffables.Core
{
    public interface IDiffable
    {
        int RefId { get; set; }
        IDiffable? Parent { get; set; }
        void SetDirty(uint bitmaskBitProperty);
        void SetChildDirty(uint bitmaskBitChild);
        void EncodeV2(SerializationContext context);
        void DecodeV2(SerializationContext context);
    }
}
