namespace Diffables.Core
{
    public interface IDiffable
    {
        int RefId { get; set; }
        ChangeTree ChangeTree { get; set; }
        Action OnSetDirty { get; set; }
        Action<(int RefId, int RefCount)> OnRefCountChanged { get; set; }
        void SetDirty(uint bitmaskBitProperty, Operation operation);
        void Encode(SerializationContext context);
        void Decode(SerializationContext context);
    }
}
