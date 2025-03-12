namespace Diffables.Core
{
    public interface IDiffable
    {
        int RefId { get; set; }
        ChangeTree ChangeTree { get; set; }
        Action OnSetDirty { get; set; }
        //void SetDirty(uint bitmaskBitProperty);
        void SetDirty(uint bitmaskBitProperty, Operation operation);
        void EncodeV2(SerializationContext context);
        void DecodeV2(SerializationContext context);
    }
}
