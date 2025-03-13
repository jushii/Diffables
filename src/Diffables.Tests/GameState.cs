using Diffables.Core;

namespace Diffables.Tests
{
    [Diffable]
    public partial class GameState
    {
        [DiffableType]
        public partial int Timer { get; set; }

        [DiffableType]
        public partial Entity Player{ get; set; }

        [DiffableType]
        public partial Entity SamePlayer { get; set; }
    }
}
