using Diffables.Core;

namespace Diffables.Tests
{
    [Diffable]
    public partial class PrimitiveTypes
    {
        [DiffableType]
        public partial sbyte SByteProp { get; set; }

        [DiffableType]
        public partial byte ByteProp { get; set; }

        [DiffableType]
        public partial short ShortProp { get; set; }

        [DiffableType]
        public partial ushort UShortProp { get; set; }

        [DiffableType]
        public partial int IntProp { get; set; }

        [DiffableType]
        public partial uint UIntProp { get; set; }

        [DiffableType]
        public partial long LongProp { get; set; }

        [DiffableType]
        public partial ulong ULongProp { get; set; }

        [DiffableType]
        public partial bool BoolProp { get; set; }

        [DiffableType]
        public partial float FloatProp { get; set; }
    }
}
