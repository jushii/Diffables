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

    [Diffable]
    public partial class Entity
    {
        [DiffableType]
        public partial string Id { get; set; }

        [DiffableType]
        public partial int Health { get; set; }

        [DiffableType]
        public partial int Mana { get; set; }
    }

    [TestFixture]
    public class DiffableTests
    {
        private Repository _repository;
        private Serializer _serializer;
        private Deserializer _deserializer;

        [SetUp]
        public void Setup()
        {
            // Create a new repository for each test.
            _repository = new Repository();
            _serializer = new Serializer(_repository);
            _deserializer = new Deserializer(_repository);
        }

        [Test]
        public void RoundTripTest()
        {
            // Arrange: Create a GameState with a shared Entity instance.
            GameState gameState = new GameState();
            gameState.Timer = 60;

            Entity entity = new Entity();
            entity.Id = "pikachu";
            entity.Health = 100;
            entity.Mana = 20;

            gameState.Player = entity;
            gameState.SamePlayer = entity;

            // Act: Serialize the GameState.
            byte[] bytes = _serializer.Serialize(gameState);

            // For testing deserialization, we can reset the repository
            // so that we simulate a new deserialization cycle.
            _repository = new Repository();
            _deserializer = new Deserializer(_repository);

            // Now, deserialize.
            _deserializer.Deserialize<GameState>(bytes);

            // Retrieve the deserialized GameState from the repository.
            // (Assuming the root object's RefId is used as the key.)
            Assert.IsTrue(_repository.TryGet(gameState.RefId, out IDiffable diffable));
            GameState deserializedGameState = diffable as GameState;
            Assert.NotNull(deserializedGameState, "Deserialized GameState should not be null.");

            // Assert: Check that the values are correctly restored.
            Assert.AreEqual(60, deserializedGameState.Timer, "Timer should be preserved.");
            Assert.NotNull(deserializedGameState.Player, "Player should not be null.");
            Assert.NotNull(deserializedGameState.SamePlayer, "SamePlayer should not be null.");
            Assert.AreEqual("pikachu", deserializedGameState.Player.Id, "Player Id should be 'pikachu'.");
            Assert.AreEqual(100, deserializedGameState.Player.Health, "Player Health should be 100.");
            Assert.AreEqual(20, deserializedGameState.Player.Mana, "Player Mana should be 20.");

            // Verify that both properties point to the same instance.
            Assert.AreSame(deserializedGameState.Player, deserializedGameState.SamePlayer,
                "Player and SamePlayer should reference the same instance.");
        }

        [Test]
        public void UpdateDeltaTest()
        {
            // Arrange: Create and serialize a GameState.
            GameState gameState = new GameState();
            gameState.Timer = 60;

            Entity entity = new Entity();
            entity.Id = "pikachu";
            entity.Health = 100;
            entity.Mana = 20;

            gameState.Player = entity;
            gameState.SamePlayer = entity;

            // Serialize the initial state.
            byte[] initialBytes = _serializer.Serialize(gameState);

            // Simulate the client deserializing the initial state.
            _repository = new Repository();
            _deserializer = new Deserializer(_repository);
            _deserializer.Deserialize<GameState>(initialBytes);

            // Act: Update a nested property (e.g. change Health) and re-serialize.
            // Since the same instance is referenced in both Player and SamePlayer,
            // our encoding should send the delta only once.
            gameState.Player.Health = 80;

            byte[] updateBytes = _serializer.Serialize(gameState);
            // Optionally, you can deserialize the update and assert that the updated Health value is reflected.
            _deserializer.Deserialize<GameState>(updateBytes);
            Assert.IsTrue(_repository.TryGet(gameState.RefId, out IDiffable updatedDiffable));
            GameState updatedGameState = updatedDiffable as GameState;
            Assert.NotNull(updatedGameState);
            Assert.AreEqual(80, updatedGameState.Player.Health, "Player Health should be updated to 80.");

            // Optionally, inspect repository or logs to verify that the delta for the shared instance was sent only once.
        }

        //[Test]
        //public void ReferenceCountingTest()
        //{
        //    // Arrange: Create a GameState with a shared Entity.
        //    GameState gameState = new GameState();
        //    gameState.Timer = 60;

        //    Entity entity = new Entity();
        //    entity.Id = "pikachu";
        //    entity.Health = 100;
        //    entity.Mana = 20;

        //    gameState.Player = entity;
        //    gameState.SamePlayer = entity;

        //    // Act: Serialize the GameState.
        //    byte[] bytes = _serializer.Serialize(gameState);

        //    // Assert: Check that the repository has a single entry for the shared instance,
        //    // and that its reference count is correct.
        //    Assert.IsTrue(_repository.TryGet(entity.RefId, out IDiffable diffable));
        //    // Assuming Repository stores a tuple (IDiffable, int) as in our example,
        //    // you might need to expose the reference count for testing.
        //    // For example, if Repository had a method GetReferenceCount(int refId):
        //    int refCount = _repository.GetReferenceCount(entity.RefId);
        //    // In this case, since both Player and SamePlayer refer to the same instance,
        //    // the reference count should be 2.
        //    Assert.AreEqual(2, refCount, "Reference count should be 2 when two properties reference the same instance.");
        //}
    }
}
