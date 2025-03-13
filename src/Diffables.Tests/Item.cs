using Diffables.Core;

namespace Diffables.Tests
{
    [Diffable]
    public partial class Item
    {
        [DiffableType]
        public partial string Id { get; set; }

        [DiffableType]
        public partial int Cost { get; set; }
    }
}
