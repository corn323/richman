using System.Collections.Generic;
using NUnit.Framework;
using Richman.Core;

namespace Richman.Core.Tests
{
    public sealed class GameSessionTests
    {
        [Test]
        public void ClassicModeStartsWithFourActivePlayersAndDataDrivenBoard()
        {
            var session = PrototypeGameFactory.CreateFourPlayerSession(928371);

            Assert.That(session.State.Players.Count, Is.EqualTo(session.Mode.MaxPlayers));
            Assert.That(session.State.Players, Has.All.Property("Status").EqualTo(PlayerStatus.Active));
            Assert.That(session.Board.TileCount, Is.EqualTo(36));
            Assert.That(session.Board.GetTileAt(1).Type, Is.EqualTo(BoardTileType.Property));
            Assert.That(session.Board.GetTileAt(1).PurchasePrice, Is.EqualTo(120));
        }

        [Test]
        public void RollMovesPlayerAndBuyCommandChangesMoneyAndOwnership()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[]
                {
                    Tile("start", BoardTileType.Start, 0),
                    Tile("rest", BoardTileType.Rest, 1),
                    Property("p1", 100, 20, 50, 2, 20, 40)
                },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));
            Assert.That(roll.Succeeded, Is.True);
            Assert.That(roll.FromTileIndex, Is.EqualTo(0));
            Assert.That(roll.ToTileIndex, Is.EqualTo(2));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.BuyProperty));

            var buy = session.Execute(new BuyPropertyCommand(1));
            Assert.That(buy.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(400));
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.EqualTo(1));
            Assert.That(session.Execute(new EndTurnCommand(1)).Succeeded, Is.True);
        }

        [Test]
        public void PassingStartAwardsConfiguredBonusAndWrapsByTileIndex()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 100, 50, 25),
                new[]
                {
                    Tile("rest-0", BoardTileType.Rest, 0),
                    Tile("start", BoardTileType.Start, 1),
                    Tile("rest-2", BoardTileType.Rest, 2),
                    Tile("rest-3", BoardTileType.Rest, 3)
                },
                new ScriptedDiceRoller(new DiceResult(6, 6)));

            var result = session.Execute(new RollDiceCommand(1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(600));
        }

        [Test]
        public void CannotBuyPropertyWhenFundsAreInsufficient()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 50, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            var buy = session.Execute(new BuyPropertyCommand(1));

            Assert.That(buy.Succeeded, Is.False);
            Assert.That(buy.ErrorCode, Is.EqualTo(CommandErrorCode.InsufficientFunds));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(50));
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.Null);
        }

        [Test]
        public void OwnedPropertyCanBeUpgradedAndChargesTheHigherRent()
        {
            var roller = new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3));
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) },
                roller);

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));

            session.Execute(new RollDiceCommand(2));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(480));
            session.Execute(new EndTurnCommand(2));

            session.Execute(new RollDiceCommand(1));
            var upgrade = session.Execute(new UpgradePropertyCommand(1));
            Assert.That(upgrade.Succeeded, Is.True);
            Assert.That(session.State.GetProperty("p1").UpgradeLevel, Is.EqualTo(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(370));
        }

        [Test]
        public void RentShortfallTransfersRemainingCashAndPropertiesToCreditor()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 100, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 40, 120, 50, 2, 120) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));

            var rent = session.Execute(new RollDiceCommand(2));

            Assert.That(rent.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(2).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(0));
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.EqualTo(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(160));
            Assert.That(session.State.Outcome.WinnerId, Is.EqualTo(1));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished));
        }

        [Test]
        public void TaxShortfallEliminatesPlayersAndDeclaresLastActiveWinner()
        {
            var session = CreateSession(
                new GameModeConfig("test", 4, 4, 100, 0, 101, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("tax", BoardTileType.Tax, 2, -101) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));

            Assert.That(session.Execute(new RollDiceCommand(1)).Succeeded, Is.True);
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(2));
            Assert.That(session.Execute(new RollDiceCommand(2)).Succeeded, Is.True);
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(3));
            Assert.That(session.Execute(new RollDiceCommand(3)).Succeeded, Is.True);

            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(2).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(3).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(4).Status, Is.EqualTo(PlayerStatus.Winner));
            Assert.That(session.State.Outcome.WinnerId, Is.EqualTo(4));
        }

        [Test]
        public void InvalidTurnAndPhaseCommandsAreRejectedWithoutChangingState()
        {
            var session = PrototypeGameFactory.CreateFourPlayerSession(7);

            var wrongTurn = session.Execute(new RollDiceCommand(2));
            var invalidPhase = session.Execute(new BuyPropertyCommand(1));

            Assert.That(wrongTurn.Succeeded, Is.False);
            Assert.That(wrongTurn.ErrorCode, Is.EqualTo(CommandErrorCode.WrongTurn));
            Assert.That(invalidPhase.Succeeded, Is.False);
            Assert.That(invalidPhase.ErrorCode, Is.EqualTo(CommandErrorCode.InvalidPhase));
            Assert.That(session.State.TurnNumber, Is.EqualTo(1));
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(1));
        }

        private static GameSession CreateSession(GameModeConfig mode, IEnumerable<BoardTileDefinition> tiles, IDiceRoller diceRoller)
        {
            var players = new List<PlayerSetup>();
            for (var i = 1; i <= mode.MaxPlayers; i++) players.Add(new PlayerSetup(i, "Player " + i));
            return new GameSession(mode, new BoardDefinition(tiles), players, diceRoller);
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Effect(string id, BoardTileType type, int positionIndex, int amount)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, amount);
        }

        private static BoardTileDefinition Property(string id, int price, int baseRent, int upgradeCost, int positionIndex, params int[] rents)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, positionIndex, price, baseRent, upgradeCost, rents, "test", id);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly Queue<DiceResult> _results;

            public ScriptedDiceRoller(params DiceResult[] results)
            {
                _results = new Queue<DiceResult>(results);
            }

            public DiceResult Roll()
            {
                if (_results.Count == 0) throw new System.InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
