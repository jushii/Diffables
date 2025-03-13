using Diffables.Core;

namespace Diffables.Tests
{

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
            GameState deserializedGameState = (GameState)diffable;
            Assert.NotNull(deserializedGameState, "Deserialized GameState should not be null.");

            // Assert: Check that the values are correctly restored.
            Assert.That(deserializedGameState.Timer, Is.EqualTo(gameState.Timer), "Timer should be preserved.");
            Assert.NotNull(deserializedGameState.Player, "Player should not be null.");
            Assert.NotNull(deserializedGameState.SamePlayer, "SamePlayer should not be null.");
            Assert.That(deserializedGameState.Player.Id, Is.EqualTo(entity.Id), $"Player Id should be {entity.Id}.");
            Assert.That(deserializedGameState.Player.Health, Is.EqualTo(entity.Health), $"Player Health should be {entity.Health}.");
            Assert.That(deserializedGameState.Player.Mana, Is.EqualTo(entity.Mana), $"Player Mana should be {entity.Mana}.");

            // Verify that both properties point to the same instance.
            Assert.That(deserializedGameState.SamePlayer, Is.SameAs(deserializedGameState.Player), "Player and SamePlayer should reference the same instance.");
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
            GameState updatedGameState = (GameState)updatedDiffable;
            Assert.NotNull(updatedGameState);
            Assert.That(updatedGameState.Player.Health, Is.EqualTo(80), "Player Health should be updated to 80.");

            // Optionally, inspect repository or logs to verify that the delta for the shared instance was sent only once.
        }

        [Test]
        public void DeleteTest()
        {
            // Arrange: create a GameState with a shared Entity.
            GameState gameState = new GameState();
            gameState.Timer = 60;
            Entity entity = new Entity { Id = "pikachu", Health = 100, Mana = 20 };
            gameState.Player = entity;
            gameState.SamePlayer = entity;

            // First, serialize and deserialize to establish shared instance.
            byte[] bytes = _serializer.Serialize(gameState);
            _repository = new Repository();
            _deserializer = new Deserializer(_repository);
            _deserializer.Deserialize<GameState>(bytes);

            // Act: set the shared instance to null for one property.
            gameState.SamePlayer = null;
            byte[] updateBytes = _serializer.Serialize(gameState);
            _deserializer.Deserialize<GameState>(updateBytes);

            // Retrieve the updated GameState.
            Assert.IsTrue(_repository.TryGet(gameState.RefId, out IDiffable diffable));
            GameState updatedGameState = (GameState)diffable;
            Assert.NotNull(updatedGameState);

            // Assert: Player should still be non-null, and SamePlayer should be null.
            Assert.NotNull(updatedGameState.Player, "Player should remain non-null.");
            Assert.IsNull(updatedGameState.SamePlayer, "SamePlayer should be null after deletion.");
        }

        [Test]
        public void DeleteAllReferencesTest()
        {
            // Arrange: Create a GameState with a shared Entity instance.
            GameState gameState = new GameState();
            gameState.Timer = 60;

            Entity entity = new Entity
            {
                Id = "pikachu",
                Health = 100,
                Mana = 20
            };

            gameState.Player = entity;
            gameState.SamePlayer = entity;

            // Act (initial cycle): Serialize the GameState so that the entity is added.
            byte[] initialBytes = _serializer.Serialize(gameState);

            // Simulate a new deserialization cycle.
            _deserializer.Deserialize<GameState>(initialBytes);

            // Verify that the repository contains the shared Entity.
            Assert.IsTrue(_repository.TryGet(entity.RefId, out IDiffable storedEntity),
                "Entity should exist in the repository after initial deserialization.");
            Assert.NotNull(storedEntity, "Stored Entity should not be null.");

            // Act (update cycle): Set both Player and SamePlayer to null.
            gameState.Player = null;
            gameState.SamePlayer = null;

            byte[] updateBytes = _serializer.Serialize(gameState);
            _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: The repository should no longer contain the Entity.
            bool existsAfterDeletion = _repository.TryGet(entity.RefId, out _);
            Assert.IsFalse(existsAfterDeletion, "Entity should be removed from the repository after all references are deleted.");
        }

        [Test]
        public void AddItemTest()
        {
            // Step 1: Create a GameState with a Player assigned.
            GameState gameState = new GameState();
            gameState.Timer = 60;

            Entity entity = new Entity
            {
                Id = "pikachu",
                Health = 100,
                Mana = 20
            };
            gameState.Player = entity;
            // For this test, we leave SamePlayer unassigned (or you can set it to null)
            // to focus on the nested Item case.

            // Step 2: Serialize and deserialize the initial state.
            byte[] initialBytes = _serializer.Serialize(gameState);

            // Simulate a new deserialization cycle.
            _repository = new Repository();
            _deserializer = new Deserializer(_repository);
            _deserializer.Deserialize<GameState>(initialBytes);

            // Retrieve the deserialized GameState.
            Assert.IsTrue(_repository.TryGet(gameState.RefId, out IDiffable diffable));
            GameState deserializedGameState = (GameState)diffable;
            Assert.NotNull(deserializedGameState);
            Assert.NotNull(deserializedGameState.Player, "Player should not be null in the initial state.");

            // Step 3: Create a new Item instance and assign it to Player.Item.
            Item newItem = new Item
            {
                Id = "sword",
                Cost = 100
            };
            gameState.Player.Item = newItem;

            // Step 4: Serialize the updated GameState.
            byte[] updateBytes = _serializer.Serialize(gameState);

            // Simulate a new deserialization cycle for the update.
            _deserializer.Deserialize<GameState>(updateBytes);

            // Retrieve the updated GameState.
            Assert.NotNull(deserializedGameState.Player, "Player should not be null in the updated state.");

            // Assert that the primitive properties are identical.
            Assert.That(deserializedGameState.Timer, Is.EqualTo(gameState.Timer), "Timer values should be identical.");
            Assert.That(deserializedGameState.Player.Id, Is.EqualTo(gameState.Player.Id), "Player Id should be identical.");
            Assert.That(deserializedGameState.Player.Health, Is.EqualTo(gameState.Player.Health), "Player Health should be identical.");
            Assert.That(deserializedGameState.Player.Mana, Is.EqualTo(gameState.Player.Mana), "Player Mana should be identical.");

            // Assert that the nested Item is correctly added and its values are identical.
            Assert.NotNull(deserializedGameState.Player.Item, "Player.Item should not be null in the updated state.");
            Assert.That(deserializedGameState.Player.Item.Id, Is.EqualTo(gameState.Player.Item.Id), "Item Id should be 'sword'.");
            Assert.That(deserializedGameState.Player.Item.Cost, Is.EqualTo(gameState.Player.Item.Cost), "Item Cost should be 100.");
        }
    }
}
