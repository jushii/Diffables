namespace Diffables.Core
{
    public enum Operation : byte
    {
        None = 0,
        Update = 1,
        Add = 2,
        AddByRefId = 3,
        Delete = 4,
        Replace = 5
    }
}
