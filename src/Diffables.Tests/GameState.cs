using Diffables.Core;

namespace Diffables.Tests
{
    [Diffable]
    public partial class GameState
    {
        [DiffableType]
        public partial int Timer { get; set; }

        [DiffableType]
        public partial Entity Player1 { get; set; }

        [DiffableType]
        public partial Entity Player2 { get; set; }

        [DiffableType]
        public partial Entity Player3 { get; set; }
    }
}
