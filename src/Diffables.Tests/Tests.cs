using Diffables.Core;

namespace Diffables.Tests
{
    [TestFixture]
    public class DiffableTests
    {
        private Serializer _serializer;
        private Deserializer _deserializer;

        private static GameState CreateDefaultGameState()
        {
            var gameState = new GameState { Timer = 60 };
            var player = new Entity { Id = "pikachu", Health = 100, Mana = 20 };
            gameState.Player1 = player;
            return gameState;
        }

        [SetUp]
        public void Setup()
        {
            _serializer = new Serializer(new Repository());
            _deserializer = new Deserializer(new Repository());
        }

        // --- Serialize / Deserialize Tests ---

        [Test]
        public void SerializeDeserializeValuePreservationTest()
        {
            // Arrange: Create gameState0.
            GameState gameState0 = CreateDefaultGameState();
            // Set Player2 to the same instance so that deserialization works as expected.
            gameState0.Player2 = gameState0.Player1;

            // Act: Serialize then deserialize.
            byte[] bytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert: Verify that basic values are preserved.
            Assert.NotNull(gameState1, "Deserialized GameState should not be null.");
            Assert.That(gameState1.Timer, Is.EqualTo(gameState0.Timer), "Timer should be preserved.");
            Assert.NotNull(gameState1.Player1, "Player1 should not be null.");
            Assert.That(gameState1.Player1.Id, Is.EqualTo(gameState0.Player1.Id), "Player1 Id should be preserved.");
            Assert.That(gameState1.Player1.Health, Is.EqualTo(gameState0.Player1.Health), "Player1 Health should be preserved.");
            Assert.That(gameState1.Player1.Mana, Is.EqualTo(gameState0.Player1.Mana), "Player1 Mana should be preserved.");
        }

        [Test]
        public void SerializeDeserializeReferenceEqualityTest()
        {
            // Arrange: Create gameState0 and make Player2 point to the same instance as Player1.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;

            // Act: Serialize then deserialize.
            byte[] bytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert: Verify that Player1 and Player2 reference the same instance.
            Assert.NotNull(gameState1.Player2, "Player2 should not be null.");
            Assert.That(gameState1.Player1, Is.EqualTo(gameState1.Player2), "Player1 and Player2 should be equal.");
            Assert.That(gameState1.Player2, Is.SameAs(gameState1.Player1), "Player1 and Player2 should reference the same instance.");
        }

        // --- Update Tests ---

        [Test]
        public void UpdateDeltaTest()
        {
            // Arrange: Create gameState0 and set shared reference.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;

            // Register initial state.
            byte[] initialBytes = _serializer.Serialize(gameState0);
            _deserializer.Deserialize<GameState>(initialBytes);

            // Act: Change Player1's Health.
            gameState0.Player1.Health = 80;
            byte[] updateBytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: Verify that the Health update is reflected.
            Assert.NotNull(gameState1, "Deserialized GameState should not be null.");
            Assert.That(gameState1.Player1.Health, Is.EqualTo(80), "Player Health should be updated to 80.");
        }

        // --- Delete Tests ---

        [Test]
        public void DeleteTest()
        {
            // Arrange: Create gameState0 with shared reference.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;

            // Register the state.
            byte[] bytes = _serializer.Serialize(gameState0);
            _deserializer.Deserialize<GameState>(bytes);

            // Act: Remove Player2 reference.
            gameState0.Player2 = null;
            byte[] updateBytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: Verify that Player1 remains while Player2 is null.
            Assert.NotNull(gameState1, "Deserialized GameState should not be null.");
            Assert.NotNull(gameState1.Player1, "Player1 should remain non-null.");
            Assert.IsNull(gameState1.Player2, "Player2 should be null after deletion.");
        }

        [Test]
        public void RepositoryRegistrationTest()
        {
            // Arrange: Create gameState0.
            GameState gameState0 = CreateDefaultGameState();
            int entityRefId = gameState0.Player1.RefId;

            // Act: Serialize and deserialize to register the entity.
            byte[] initialBytes = _serializer.Serialize(gameState0);
            _deserializer.Deserialize<GameState>(initialBytes);

            // Assert: Verify that the repository contains the entity.
            bool found = _deserializer.Repository.TryGet(entityRefId, out IDiffable cachedEntity);
            Assert.IsTrue(found, "Entity should exist in the repository after initial deserialization.");
            Assert.NotNull(cachedEntity, "Cached Entity should not be null.");
        }

        [Test]
        public void RepositoryCleanupAfterAllReferencesDeletionTest()
        {
            // Arrange: Create gameState0.
            GameState gameState0 = CreateDefaultGameState();
            int entityRefId = gameState0.Player1.RefId;

            // Act: Register the entity.
            byte[] initialBytes = _serializer.Serialize(gameState0);
            _deserializer.Deserialize<GameState>(initialBytes);

            // Remove all references.
            gameState0.Player1 = null;
            gameState0.Player2 = null;
            byte[] updateBytes = _serializer.Serialize(gameState0);
            _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: Verify that the entity is removed from the repository.
            bool exists = _deserializer.Repository.TryGet(entityRefId, out _);
            Assert.IsFalse(exists, "Entity should be removed from the repository after all references are deleted.");
        }

        // --- AddItem Tests ---

        [Test]
        public void AddItem_VerifyItemAddedTest()
        {
            // Arrange: Create gameState0 and register initial state.
            GameState gameState0 = CreateDefaultGameState();
            byte[] initialBytes = _serializer.Serialize(gameState0);
            GameState initialState = _deserializer.Deserialize<GameState>(initialBytes);
            Assert.NotNull(initialState, "Initial deserialized GameState should not be null.");
            Assert.NotNull(initialState.Player1, "Player1 should be present in the initial state.");

            // Act: Add an item to Player1.
            Item newItem = new Item { Id = "sword", Cost = 100 };
            gameState0.Player1.Item = newItem;
            byte[] updateBytes = _serializer.Serialize(gameState0);
            GameState updatedState = _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: Verify that the item is correctly added.
            Assert.NotNull(updatedState.Player1.Item, "Player1.Item should not be null after being added.");
            Assert.That(updatedState.Player1.Item.Id, Is.EqualTo("sword"), "Item Id should be 'sword'.");
            Assert.That(updatedState.Player1.Item.Cost, Is.EqualTo(100), "Item Cost should be 100.");
        }

        [Test]
        public void AddItem_GameStateIntegrityTest()
        {
            // Arrange: Create gameState0 and record original values.
            GameState gameState0 = CreateDefaultGameState();
            int originalTimer = gameState0.Timer;
            string originalPlayerId = gameState0.Player1.Id;
            int originalHealth = gameState0.Player1.Health;
            int originalMana = gameState0.Player1.Mana;

            // Act: Add an item (different from the previous test).
            Item newItem = new Item { Id = "shield", Cost = 150 };
            gameState0.Player1.Item = newItem;
            byte[] updateBytes = _serializer.Serialize(gameState0);
            GameState updatedState = _deserializer.Deserialize<GameState>(updateBytes);

            // Assert: Verify that other parts of the GameState remain unchanged.
            Assert.That(updatedState.Timer, Is.EqualTo(originalTimer), "Timer value should remain unchanged.");
            Assert.That(updatedState.Player1.Id, Is.EqualTo(originalPlayerId), "Player1 Id should remain unchanged.");
            Assert.That(updatedState.Player1.Health, Is.EqualTo(originalHealth), "Player1 Health should remain unchanged.");
            Assert.That(updatedState.Player1.Mana, Is.EqualTo(originalMana), "Player1 Mana should remain unchanged.");
        }

        #region Replace tests

        [Test]
        public void ReplaceExistingInstanceWithAnotherExistingInstanceTest()
        {
            // Arrange: Create gameState0.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;
            gameState0.Player3 = new Entity { Id = "zapdos", Health = 200, Mana = 40 };

            // Act:
            // Encode the complete GameState.
            byte[] bytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Replace Player2 with Player3.
            gameState0.Player2 = gameState0.Player3;
            // Encode again.
            bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert on gameState1:
            Assert.That(gameState1.Player2, Is.EqualTo(gameState1.Player3), "Player2 and Player3 should be equal in gameState1.");
            Assert.That(gameState1.Player2, Is.SameAs(gameState1.Player3), "Player2 and Player3 should reference the same instance in gameState1.");
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(1), "Player1 RefCount should be 1 in gameState1.");
            Assert.That(gameState1.Player2.RefCount, Is.EqualTo(2), "Player2 RefCount should be 2 in gameState1.");

            // Additional assertions on gameState0:
            Assert.That(gameState0.Player2, Is.SameAs(gameState0.Player3), "gameState0: Player2 and Player3 should reference the same instance.");
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(1), "gameState0: Player1 RefCount should be 1.");
            Assert.That(gameState0.Player2.RefCount, Is.EqualTo(2), "gameState0: Player2 RefCount should be 2.");
        }

        [Test]
        public void ReplaceExistingInstanceWithNewInstanceTest()
        {
            // Arrange: Create gameState0.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;

            // Act:
            // Encode the complete GameState.
            byte[] bytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(bytes);
            // Replace Player2 with a new instance.
            gameState0.Player2 = new Entity { Id = "zapdos", Health = 200, Mana = 40 };
            // Encode again.
            bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert on gameState1:
            Assert.That(gameState1.Player1, Is.Not.SameAs(gameState1.Player2), "Player1 and Player2 should not reference the same instance in gameState1.");
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(1), "Player1 RefCount should be 1 in gameState1.");
            Assert.That(gameState1.Player2.RefCount, Is.EqualTo(1), "Player2 RefCount should be 1 in gameState1.");
            Assert.That(gameState1.Player2.Id, Is.EqualTo("zapdos"), "Player2 Id should be 'zapdos' in gameState1.");

            // Additional assertions on gameState0:
            Assert.That(gameState0.Player1, Is.Not.SameAs(gameState0.Player2), "gameState0: Player1 and Player2 should not reference the same instance.");
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(1), "gameState0: Player1 RefCount should be 1.");
            Assert.That(gameState0.Player2.RefCount, Is.EqualTo(1), "gameState0: Player2 RefCount should be 1.");
            Assert.That(gameState0.Player2.Id, Is.EqualTo("zapdos"), "gameState0: Player2 Id should be 'zapdos'.");
        }

        [Test]
        public void ReplaceExistingInstanceWithNewInstance_MultipleReplacementsTest()
        {
            // Arrange: Create a game state where Player1 and Player2 reference the same entity.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;

            // Register initial state.
            byte[] initialBytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(initialBytes);

            // Act 1: Replace Player2 with a new instance ("zapdos").
            var newEntity1 = new Entity { Id = "zapdos", Health = 200, Mana = 40 };
            gameState0.Player2 = newEntity1;
            byte[] bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert 1: Verify that Player2 is a new instance.
            Assert.That(gameState1.Player1, Is.Not.SameAs(gameState1.Player2), "After first replacement, Player1 and Player2 should not be the same in gameState1.");
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(1), "Player1's RefCount should be 1 in gameState1 after first replacement.");
            Assert.That(gameState1.Player2.RefCount, Is.EqualTo(1), "New entity's RefCount should be 1 in gameState1 after first replacement.");
            var firstReplacement = gameState1.Player2;

            // Additional assertions on gameState0 after first replacement:
            Assert.That(gameState0.Player2, Is.EqualTo(newEntity1), "gameState0: Player2 should equal newEntity1 after first replacement.");
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(1), "gameState0: Player1's RefCount should be 1 after first replacement.");
            Assert.That(gameState0.Player2.RefCount, Is.EqualTo(1), "gameState0: newEntity1's RefCount should be 1 after first replacement.");

            // Act 2: Replace Player2 with another new instance ("moltres").
            var newEntity2 = new Entity { Id = "moltres", Health = 180, Mana = 30 };
            gameState0.Player2 = newEntity2;
            bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert 2: Verify that the second replacement is distinct.
            Assert.That(gameState1.Player1, Is.Not.SameAs(gameState1.Player2), "After second replacement, Player1 and Player2 should not be the same in gameState1.");
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(1), "Player1's RefCount should remain 1 in gameState1 after second replacement.");
            Assert.That(gameState1.Player2.RefCount, Is.EqualTo(1), "Second new entity's RefCount should be 1 in gameState1 after second replacement.");
            Assert.That(gameState1.Player2, Is.Not.SameAs(firstReplacement), "The second replacement should create a different instance than the first in gameState1.");

            // Additional assertions on gameState0 after second replacement:
            Assert.That(gameState0.Player2, Is.EqualTo(newEntity2), "gameState0: Player2 should equal newEntity2 after second replacement.");
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(1), "gameState0: Player1's RefCount should remain 1 after second replacement.");
            Assert.That(gameState0.Player2.RefCount, Is.EqualTo(1), "gameState0: newEntity2's RefCount should be 1 after second replacement.");

            // Act 3: Replace Player2 with Player1 (an existing instance).
            gameState0.Player2 = gameState0.Player1;
            bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert 3: Now both Player1 and Player2 reference the same instance.
            Assert.That(gameState1.Player1, Is.SameAs(gameState1.Player2), "After third replacement, Player2 should reference the same instance as Player1 in gameState1.");
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(2), "Player1's RefCount should be 2 in gameState1 as it is now referenced twice.");

            // Additional assertions on gameState0 after third replacement:
            Assert.That(gameState0.Player2, Is.SameAs(gameState0.Player1), "gameState0: Player2 should reference the same instance as Player1 after third replacement.");
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(2), "gameState0: Player1's RefCount should be 2 after third replacement.");
        }

        [Test]
        public void ReplaceMultipleInstancesWithNewInstancesTest()
        {
            // Arrange: Create a game state where Player1, Player2, and Player3 initially reference the same entity.
            GameState gameState0 = CreateDefaultGameState();
            gameState0.Player2 = gameState0.Player1;
            gameState0.Player3 = gameState0.Player1;

            // Register the initial state.
            byte[] initialBytes = _serializer.Serialize(gameState0);
            GameState gameState1 = _deserializer.Deserialize<GameState>(initialBytes);

            // Act: Replace Player2 and Player3 with new instances.
            var newPlayer2 = new Entity { Id = "zapdos", Health = 200, Mana = 40 };
            var newPlayer3 = new Entity { Id = "moltres", Health = 180, Mana = 35 };
            gameState0.Player2 = newPlayer2;
            gameState0.Player3 = newPlayer3;
            byte[] bytes = _serializer.Serialize(gameState0);
            gameState1 = _deserializer.Deserialize<GameState>(bytes);

            // Assert on gameState1:
            Assert.That(gameState1.Player1.RefCount, Is.EqualTo(1), "Player1's RefCount should be 1 in gameState1 after replacements on other properties.");
            Assert.That(gameState1.Player2.RefCount, Is.EqualTo(1), "Player2's new instance RefCount should be 1 in gameState1.");
            Assert.That(gameState1.Player3.RefCount, Is.EqualTo(1), "Player3's new instance RefCount should be 1 in gameState1.");
            Assert.That(gameState1.Player1, Is.Not.SameAs(gameState1.Player2), "Player1 and Player2 should be distinct in gameState1 after replacement.");
            Assert.That(gameState1.Player1, Is.Not.SameAs(gameState1.Player3), "Player1 and Player3 should be distinct in gameState1 after replacement.");
            Assert.That(gameState1.Player2, Is.Not.SameAs(gameState1.Player3), "Player2 and Player3 should be distinct instances in gameState1.");

            // Additional assertions on gameState0:
            Assert.That(gameState0.Player1.RefCount, Is.EqualTo(1), "gameState0: Player1's RefCount should be 1 after replacements on other properties.");
            Assert.That(gameState0.Player2.RefCount, Is.EqualTo(1), "gameState0: Player2's new instance RefCount should be 1.");
            Assert.That(gameState0.Player3.RefCount, Is.EqualTo(1), "gameState0: Player3's new instance RefCount should be 1.");
            Assert.That(gameState0.Player1, Is.Not.SameAs(gameState0.Player2), "gameState0: Player1 and Player2 should be distinct after replacement.");
            Assert.That(gameState0.Player1, Is.Not.SameAs(gameState0.Player3), "gameState0: Player1 and Player3 should be distinct after replacement.");
            Assert.That(gameState0.Player2, Is.Not.SameAs(gameState0.Player3), "gameState0: Player2 and Player3 should be distinct instances.");
        }

        #endregion Replace tests
    }
}
