using Diffables.Core;

namespace Diffables.Tests
{
    [Diffable]
    public partial class Entity
    {
        [DiffableType]
        public partial string Id { get; set; }

        [DiffableType]
        public partial int Health { get; set; }

        [DiffableType]
        public partial int Mana { get; set; }

        [DiffableType]
        public partial Item Item { get; set; }
    }
}
