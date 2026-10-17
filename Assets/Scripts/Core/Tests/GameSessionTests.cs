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
            Assert.That(session.Board.MainRouteTileCount, Is.EqualTo(40));
            Assert.That(session.Board.TileCount, Is.EqualTo(47));
            Assert.That(session.Board.GetTileAt(1).Type, Is.EqualTo(BoardTileType.Property));
            Assert.That(session.Board.GetTileAt(1).PurchasePrice, Is.EqualTo(120));
        }

        [Test]
        public void PrototypeMapIdsAndSpecialTileTypesMatchTheMapRoute()
        {
            var board = PrototypeBoardFactory.Create();

            Assert.That(board.MainRouteTileCount, Is.EqualTo(40));
            Assert.That(board.TileCount, Is.EqualTo(47));
            Assert.That(board.GetTileAt(0).Id, Is.EqualTo("start-singapore"));
            Assert.That(board.GetTileAt(3).Type, Is.EqualTo(BoardTileType.Airport));
            Assert.That(board.GetTileAt(30).Id, Is.EqualTo("london-prison"));
            Assert.That(board.GetTileAt(30).Type, Is.EqualTo(BoardTileType.Prison));
            Assert.That(board.GetTileAt(39).NextTileIds[0], Is.EqualTo("start-singapore"));
            Assert.That(board.GetTile("macau-heritage-quarter").RejoinsAtId, Is.EqualTo("hong-kong-victoria-peak"));
            Assert.That(board.GetNextTiles(10).Count, Is.EqualTo(2));
        }

        [Test]
        public void ChoosingTravelBranchConsumesRemainingStepsAndRejoinsTheMainRoute()
        {
            var session = CreateSession(
                GameModeConfig.ClassicBankruptcyFourPlayer(),
                PrototypeBoardFactory.Create().Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));
            session.Execute(new RollDiceCommand(1));
            session.Execute(new EndTurnCommand(1));
            for (var playerId = 2; playerId <= 4; playerId++)
            {
                session.Execute(new RollDiceCommand(playerId));
                session.Execute(new EndTurnCommand(playerId));
            }

            var toJunction = session.Execute(new RollDiceCommand(1));

            Assert.That(toJunction.Succeeded, Is.True);
            Assert.That(toJunction.PathTileIndices, Is.EqualTo(new[] { 9, 10 }));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.ChooseRoute));
            Assert.That(session.State.MovementStepsRemaining, Is.EqualTo(4));
            Assert.That(session.Execute(new ChooseRouteCommand(1, "not-a-route")).ErrorCode, Is.EqualTo(CommandErrorCode.InvalidRouteChoice));

            var branch = session.Execute(new ChooseRouteCommand(1, "macau-ferry-pier"));

            Assert.That(branch.Succeeded, Is.True);
            Assert.That(branch.PathTileIndices, Is.EqualTo(new[] { 40, 41, 11, 12 }));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(12));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.None));
            Assert.That(session.State.MovementStepsRemaining, Is.Zero);
        }

        [Test]
        public void CompletingDistrictPaysCollectionRewardAndRaisesItsRent()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 1000, 0, 50, 25),
                new[]
                {
                    Tile("start", BoardTileType.Start, 0),
                    Tile("rest-1", BoardTileType.Rest, 1),
                    PropertyInDistrict("east-1", "east", 2, 100, 10),
                    Tile("rest-2", BoardTileType.Rest, 3),
                    PropertyInDistrict("east-2", "east", 4, 100, 20),
                    Tile("rest-3", BoardTileType.Rest, 5),
                    Tile("rest-4", BoardTileType.Rest, 6)
                },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(930));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));

            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(960));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(960));
        }

        [Test]
        public void AirportLandingTransfersPlayerToThePairedAirport()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Tile("airport-1", BoardTileType.Airport, 2), Tile("rest-2", BoardTileType.Rest, 3), Tile("airport-2", BoardTileType.Airport, 4) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            Assert.That(roll.ToTileIndex, Is.EqualTo(4));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(4));
            Assert.That(session.State.LastEventMessage, Does.Contain("flew"));
        }

        [Test]
        public void HospitalStaySkipsThePlayersNextTurnWithoutRollingDice()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("hospital", BoardTileType.Hospital, 2, 1), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.GetPlayer(1).DetainedTurnsRemaining, Is.EqualTo(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var skipped = session.Execute(new RollDiceCommand(1));

            Assert.That(skipped.Succeeded, Is.True);
            Assert.That(skipped.DiceResult, Is.Null);
            Assert.That(session.State.GetPlayer(1).DetainedTurnsRemaining, Is.Zero);
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn));
            Assert.That(session.State.LastEventMessage, Does.Contain("skipped"));
        }

        [Test]
        public void PrisonStaySkipsTwoTurnsAndCourtAppliesItsJudgmentFine()
        {
            var prison = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("prison", BoardTileType.Prison, 2, 2), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));
            prison.Execute(new RollDiceCommand(1));
            Assert.That(prison.State.GetPlayer(1).DetainedTurnsRemaining, Is.EqualTo(2));

            var court = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("court", BoardTileType.Court, 2, 150), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));
            court.Execute(new RollDiceCommand(1));

            Assert.That(court.State.GetPlayer(1).Money, Is.EqualTo(350));
            Assert.That(court.State.LastEventMessage, Does.Contain("Court judgment"));
        }

        [Test]
        public void ChanceLandingDrawsCardAndAppliesItsEffect()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Tile("chance-2", BoardTileType.Chance, 2), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));

            Assert.That(session.State.LastDrawnCard.Id, Is.EqualTo("chance-festival-grant"));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(580));
            Assert.That(session.State.LastEventMessage, Does.Contain("Festival Grant"));
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

        private static BoardTileDefinition PropertyInDistrict(string id, string districtId, int positionIndex, int price, int baseRent)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, positionIndex, price, baseRent, 50, new[] { baseRent, baseRent * 2 }, districtId, id);
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
