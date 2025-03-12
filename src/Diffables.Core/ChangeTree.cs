namespace Diffables.Core
{
    public class ChangeTree
    {
        public uint DirtyPropertiesBitmask { get; set; } = 0;
        public Dictionary<uint, Operation> Operations { get; set; } = new Dictionary<uint, Operation>();

        public void Clear()
        {
            DirtyPropertiesBitmask = 0;
            Operations.Clear();
        }
    }
}
