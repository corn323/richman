using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Richman.Core;
using Richman.Core.Events;

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
            Assert.That(session.Board.MainRouteTileCount, Is.EqualTo(41));
            Assert.That(session.Board.TileCount, Is.EqualTo(48));
            Assert.That(session.Board.GetTileAt(1).Type, Is.EqualTo(BoardTileType.Property));
            Assert.That(session.Board.GetTileAt(1).PurchasePrice, Is.EqualTo(140));
        }

        [Test]
        public void TradeExchangesMoneyAndPropertiesAtomicallyBetweenActivePlayers()
        {
            var session = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), PrototypeBoardFactory.Create().Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 1), new DiceResult(1, 1)));
            Assert.That(session.Execute(new RollDiceCommand(1)).Succeeded, Is.True);
            Assert.That(session.Execute(new BuyPropertyCommand(1)).Succeeded, Is.True);
            Assert.That(session.Execute(new EndTurnCommand(1)).Succeeded, Is.True);
            Assert.That(session.Execute(new RollDiceCommand(2)).Succeeded, Is.True);
            Assert.That(session.Execute(new BuyPropertyCommand(2)).Succeeded, Is.True);
            Assert.That(session.Execute(new EndTurnCommand(2)).Succeeded, Is.True);
            Assert.That(session.Execute(new RollDiceCommand(3)).Succeeded, Is.True);
            Assert.That(session.Execute(new EndTurnCommand(3)).Succeeded, Is.True);
            Assert.That(session.Execute(new RollDiceCommand(4)).Succeeded, Is.True);
            Assert.That(session.Execute(new EndTurnCommand(4)).Succeeded, Is.True);

            var playerOne = session.State.GetPlayer(1);
            var playerTwo = session.State.GetPlayer(2);
            var originalOneMoney = playerOne.Money;
            var originalTwoMoney = playerTwo.Money;
            var rejected = session.Execute(new TradeCommand(1, 2, 5000, 0, "singapore-marina"));
            Assert.That(rejected.ErrorCode, Is.EqualTo(CommandErrorCode.InsufficientFunds));
            Assert.That(playerOne.Money, Is.EqualTo(originalOneMoney));
            Assert.That(playerTwo.Money, Is.EqualTo(originalTwoMoney));
            Assert.That(session.State.GetProperty("singapore-marina").OwnerId, Is.EqualTo(1));

            var trade = session.Execute(new TradeCommand(1, 2, 20, 10, "singapore-marina", "kyoto-gion"));

            Assert.That(trade.Succeeded, Is.True);
            Assert.That(session.State.GetProperty("singapore-marina").OwnerId, Is.EqualTo(2));
            Assert.That(session.State.GetProperty("kyoto-gion").OwnerId, Is.EqualTo(1));
            Assert.That(playerOne.Money, Is.EqualTo(originalOneMoney - 10));
            Assert.That(playerTwo.Money, Is.EqualTo(originalTwoMoney + 10));
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void PrototypeMapIdsAndSpecialTileTypesMatchTheMapRoute()
        {
            var board = PrototypeBoardFactory.Create();

            Assert.That(board.MainRouteTileCount, Is.EqualTo(41));
            Assert.That(board.TileCount, Is.EqualTo(48));
            Assert.That(board.GetTileAt(0).Id, Is.EqualTo("start-singapore"));
            Assert.That(board.GetTileAt(3).Type, Is.EqualTo(BoardTileType.Airport));
            Assert.That(board.GetTileAt(30).Id, Is.EqualTo("london-prison"));
            Assert.That(board.GetTileAt(30).Type, Is.EqualTo(BoardTileType.Prison));
            Assert.That(board.GetTileAt(27).Type, Is.EqualTo(BoardTileType.Property));
            Assert.That(board.GetTileAt(27).Id, Is.EqualTo("paris-montmartre"));
            Assert.That(board.GetTileAt(40).Type, Is.EqualTo(BoardTileType.Court));
            Assert.That(board.GetTileAt(40).NextTileIds[0], Is.EqualTo("start-singapore"));
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
            Assert.That(branch.PathTileIndices, Is.EqualTo(new[] { 41, 42, 11, 12 }));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(12));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.None));
            Assert.That(session.State.MovementStepsRemaining, Is.Zero);
            Assert.That(session.State.LastEventMessage, Is.Empty);
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
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Airport("airport-1", 2, "airport-2"), Tile("rest-2", BoardTileType.Rest, 3), Airport("airport-2", 4, "airport-1") },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            Assert.That(roll.ToTileIndex, Is.EqualTo(4));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(4));
            // T3（去字串化）：改以結構化狀態斷言，確認降落後直接進入待結束回合、無殘留待決動作，
            // 不再依賴 LastEventMessage 的「機場航線」中文字串。
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.None));
        }

        [TestCase("airport-changi", "airport-incheon")]
        [TestCase("airport-incheon", "airport-changi")]
        [TestCase("airport-dubai", "airport-jfk")]
        [TestCase("airport-jfk", "airport-dubai")]
        public void WorldTourAirportsUseTheirDeclaredRoutes(string originId, string destinationId)
        {
            var board = PrototypeBoardFactory.Create();
            var origin = board.GetTile(originId);
            Assert.That(origin, Is.Not.Null);
            Assert.That(origin.AirportDestinationId, Is.EqualTo(destinationId));
            Assert.That(board.GetTile(origin.AirportDestinationId).Type, Is.EqualTo(BoardTileType.Airport));

            var transferBoard = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("approach", BoardTileType.Rest, 1),
                Airport(originId, 2, destinationId), Tile("runway", BoardTileType.Rest, 3),
                Airport(destinationId, 4, originId), Tile("exit", BoardTileType.Rest, 5)
            });
            var transferSession = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), transferBoard.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1)));
            var transfer = transferSession.Execute(new RollDiceCommand(1));
            Assert.That(transfer.Succeeded, Is.True);
            Assert.That(transfer.PathTileIndices, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(transferSession.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(4));
        }

        [Test]
        public void HospitalStaySkipsThePlayersNextTurnWithoutRollingDice()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("hospital", BoardTileType.Hospital, 2, 2), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.GetPlayer(1).HospitalTurnsRemaining, Is.EqualTo(2));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var skipped = session.Execute(new RollDiceCommand(1));

            Assert.That(skipped.Succeeded, Is.True);
            Assert.That(skipped.DiceResult, Is.Null);
            Assert.That(session.State.GetPlayer(1).HospitalTurnsRemaining, Is.EqualTo(1));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn));
            // T3（去字串化）：改以結構化狀態斷言 PendingAction，不再依賴 LastEventMessage 的 "skipped" 字串。
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.None));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var secondSkipped = session.Execute(new RollDiceCommand(1));
            Assert.That(secondSkipped.DiceResult, Is.Null);
            Assert.That(session.State.GetPlayer(1).HospitalTurnsRemaining, Is.Zero);
        }

        [Test]
        public void PrisonRequiresDoublesAndAfterThreeFailedAttemptsChargesBail()
        {
            var prison = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("prison", BoardTileType.Prison, 2, 1), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 2), new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 1), new DiceResult(3, 4)));
            prison.Execute(new RollDiceCommand(1));
            Assert.That(prison.State.GetPlayer(1).PrisonAttemptsRemaining, Is.EqualTo(3));
            prison.Execute(new EndTurnCommand(1)); prison.Execute(new RollDiceCommand(2)); prison.Execute(new EndTurnCommand(2));
            Assert.That(prison.Execute(new RollDiceCommand(1)).PathTileIndices, Is.Empty);
            prison.Execute(new EndTurnCommand(1)); prison.Execute(new RollDiceCommand(2)); prison.Execute(new EndTurnCommand(2));
            Assert.That(prison.Execute(new RollDiceCommand(1)).PathTileIndices, Is.Empty);
            prison.Execute(new EndTurnCommand(1)); prison.Execute(new RollDiceCommand(2)); prison.Execute(new EndTurnCommand(2));
            var bail = prison.Execute(new RollDiceCommand(1));
            Assert.That(bail.PathTileIndices, Is.Empty);
            Assert.That(prison.State.GetPlayer(1).PrisonAttemptsRemaining, Is.Zero);
            Assert.That(prison.State.GetPlayer(1).Money, Is.EqualTo(400));
            // T3（去字串化）：改以結構化狀態斷言（玩家仍為 Active 且扣款金額已在上方驗證），
            // 不再依賴 LastEventMessage 的 "$100" 字串。
            Assert.That(prison.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Active));
        }

        [Test]
        public void ThirdPrisonFailureWithInsufficientBailAutomaticallyAdvancesTurn()
        {
            var mode = new GameModeConfig("poor-prison-test", 4, 4, 50, 0, 50, 25);
            var board = PrisonRoute();
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), new DiceResult(2, 2), new DiceResult(3, 3), new DiceResult(2, 3),
                new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1),
                new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 2));
            var session = CreateSession(mode, board.Tiles, dice);

            session.Execute(new RollDiceCommand(1)); session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(3)); session.Execute(new EndTurnCommand(3));
            session.Execute(new RollDiceCommand(4)); session.Execute(new EndTurnCommand(4));
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var bail = session.Execute(new RollDiceCommand(1));
                Assert.That(bail.Succeeded, Is.True);
                if (attempt < 2) session.Execute(new EndTurnCommand(1));
                if (attempt < 2)
                {
                    for (var other = 2; other <= 4; other++)
                    {
                        session.Execute(new RollDiceCommand(other));
                        session.Execute(new EndTurnCommand(other));
                    }
                }
            }

            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(1).Money, Is.Zero);
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(2));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingRoll));
            Assert.That(session.State.Outcome, Is.Null);
            // T3（去字串化）：改以結構化狀態斷言（破產時嘗試付保釋金必然耗盡逃獄次數），
            // 不再依賴 LastEventMessage 的 "unable to pay $100" 字串。
            Assert.That(session.State.GetPlayer(1).PrisonAttemptsRemaining, Is.Zero);
            Assert.That(session.Execute(new EndTurnCommand(1)).ErrorCode, Is.EqualTo(CommandErrorCode.PlayerEliminated));
        }

        [Test]
        public void ThirdPrisonFailureThatEndsGameLeavesPhaseFinished()
        {
            var mode = new GameModeConfig("last-player-bail-test", 2, 2, 50, 0, 50, 25);
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 2),
                new DiceResult(1, 1), new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 2));
            var session = CreateSession(mode, PrisonRoute().Tiles, dice);

            session.Execute(new RollDiceCommand(1)); session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1)); session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1)); session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            var lastAttempt = session.Execute(new RollDiceCommand(1));

            Assert.That(lastAttempt.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(2).Status, Is.EqualTo(PlayerStatus.Winner));
            Assert.That(session.State.Outcome.WinnerId, Is.EqualTo(2));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished));
            Assert.That(session.Execute(new EndTurnCommand(1)).ErrorCode, Is.EqualTo(CommandErrorCode.GameFinished));
        }

        [Test]
        public void StartUpgradePermissionAppearsForStopButNotForPassingStart()
        {
            var property = Property("owned", 100, 20, 50, 2, 20, 40);
            var stopSession = CreateSession(new GameModeConfig("start-upgrade", 2, 2, 500, 100, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1), property, Tile("rest-3", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            stopSession.Execute(new RollDiceCommand(1)); stopSession.Execute(new BuyPropertyCommand(1)); stopSession.Execute(new EndTurnCommand(1));
            stopSession.Execute(new RollDiceCommand(2)); stopSession.Execute(new EndTurnCommand(2));
            stopSession.Execute(new RollDiceCommand(1));
            Assert.That(stopSession.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(0));
            Assert.That(stopSession.State.CanUpgradeOwnedProperty, Is.True);
            Assert.That(stopSession.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn));
            Assert.That(stopSession.Execute(new UpgradePropertyCommand(1, "owned")).Succeeded, Is.True);

            var passSession = CreateSession(new GameModeConfig("start-pass", 2, 2, 500, 100, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1), Tile("rest-2", BoardTileType.Rest, 2), Tile("rest-3", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 1)));
            passSession.Execute(new RollDiceCommand(1)); passSession.Execute(new EndTurnCommand(1));
            passSession.Execute(new RollDiceCommand(2)); passSession.Execute(new EndTurnCommand(2));
            passSession.Execute(new RollDiceCommand(1));
            Assert.That(passSession.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(1));
            Assert.That(passSession.State.CanUpgradeOwnedProperty, Is.False);
        }

        [Test]
        public void StartUpgradeRejectsAPropertyOwnedByAnotherPlayer()
        {
            var session = CreateStartUpgradeSession(500, false);

            Assert.That(session.State.CanUpgradeOwnedProperty, Is.True);
            Assert.That(session.State.GetProperty("owned").OwnerId, Is.EqualTo(2));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode,
                Is.EqualTo(CommandErrorCode.PropertyUnavailable));
        }

        [Test]
        public void StartUpgradeRejectsInsufficientFunds()
        {
            var session = CreateStartUpgradeSession(120, true);

            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(20));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode,
                Is.EqualTo(CommandErrorCode.InsufficientFunds));
            Assert.That(session.State.GetProperty("owned").UpgradeLevel, Is.Zero);
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(20));
        }

        [Test]
        public void StartUpgradeRejectsAPropertyAtItsMaximumLevel()
        {
            var session = CreateSession(new GameModeConfig("start-upgrade-limit", 2, 2, 500, 0, 50, 25),
                new[]
                {
                    Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                    Property("owned", 100, 20, 50, 2, 20, 40, 60), Tile("rest-3", BoardTileType.Rest, 3)
                },
                new ScriptedDiceRoller(
                    new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1),
                    new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1)); session.Execute(new BuyPropertyCommand(1)); session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "owned")).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "owned")).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2)); session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));

            Assert.That(session.State.CanUpgradeOwnedProperty, Is.True);
            Assert.That(session.State.GetProperty("owned").UpgradeLevel, Is.EqualTo(2));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode,
                Is.EqualTo(CommandErrorCode.PropertyAlreadyMaxLevel));
        }

        private static GameSession CreateStartUpgradeSession(int startingMoney, bool playerOneOwns)
        {
            var session = CreateSession(new GameModeConfig("start-upgrade-validation", 2, 2, startingMoney, 0, 50, 25),
                new[]
                {
                    Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                    Property("owned", 100, 20, 50, 2, 20, 40), Tile("rest-3", BoardTileType.Rest, 3)
                },
                new ScriptedDiceRoller(new DiceResult(1, 1), playerOneOwns ? new DiceResult(1, 2) : new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            if (playerOneOwns) session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            if (!playerOneOwns) session.Execute(new BuyPropertyCommand(2));
            session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            return session;
        }

        private static BoardDefinition PrisonRoute()
        {
            var tiles = new List<BoardTileDefinition>
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Effect("prison", BoardTileType.Prison, 2, 1)
            };
            for (var i = 3; i < 10; i++) tiles.Add(Tile("rest-" + i, BoardTileType.Rest, i));
            return new BoardDefinition(tiles);
        }

        [Test]
        public void CourtRollsForAcquittalAndAppliesOneRandomJudgment()
        {
            var court = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Effect("court", BoardTileType.Court, 2, 100), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(3, 4)));
            court.Execute(new RollDiceCommand(1));
            Assert.That(court.State.LastCourtDiceResult.Total, Is.EqualTo(7));
            Assert.That(court.State.GetPlayer(1).Money, Is.EqualTo(500));
            // T3（去字串化）：改以結構化狀態斷言（無罰款、未入獄），不再依賴 LastEventMessage
            // 的 "acquittal" / "3 + 4 = 7" 字串。
            Assert.That(court.State.GetPlayer(1).PrisonAttemptsRemaining, Is.Zero);
            Assert.That(court.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Active));
        }

        [TestCase(0, true)]
        [TestCase(1, false)]
        public void CourtLowRollSelectsPrisonOrOneHundredDollarFine(int judgment, bool prison)
        {
            var mode = new GameModeConfig("test", 2, 2, 500, 0, 50, 25);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                Effect("court", BoardTileType.Court, 2, 100), Tile("rest-2", BoardTileType.Rest, 3)
            });
            var session = new GameSession(mode, board, new[] { new PlayerSetup(1, "P1"), new PlayerSetup(2, "P2") },
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)),
                new ScriptedRandom(0, 0, 0, 0, 0, 0, judgment));
            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.LastCourtDiceResult.Total, Is.EqualTo(2));
            Assert.That(session.State.GetPlayer(1).PrisonAttemptsRemaining > 0, Is.EqualTo(prison));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(prison ? 500 : 400));
        }

        [Test]
        public void ChanceLandingDrawsCardAndAppliesItsEffect()
        {
            var session = CreateSession(
                new GameModeConfig("test", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Tile("chance-2", BoardTileType.Chance, 2), Tile("rest-2", BoardTileType.Rest, 3) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(session.State.LastDrawnCard, Is.Not.Null);
            var drawnCard = session.State.LastDrawnCard;
            // Phase 1c：改以結構化事件斷言取代 LastEventMessage 字串斷言（ADR-0003 決策2），
            // 驗證「抽到哪張牌」與「效果確實被套用」各自獨立可驗證，不再依賴拼出來的顯示字串。
            var cardDrawn = roll.Events.OfType<CardDrawn>().Single();
            Assert.That(cardDrawn.CardId, Is.EqualTo(drawnCard.Id));
            Assert.That(cardDrawn.DeckId, Is.EqualTo(drawnCard.DeckId));
            var cardEffectApplied = roll.Events.OfType<CardEffectApplied>().Single();
            Assert.That(cardEffectApplied.CardId, Is.EqualTo(drawnCard.Id));
            Assert.That(cardEffectApplied.Effect, Is.EqualTo(drawnCard.Effect));
            Assert.That(cardEffectApplied.Amount, Is.EqualTo(drawnCard.Amount));
            if (session.State.LastDrawnCard.Effect == MapCardEffect.ReceiveMoney)
                Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(500 + session.State.LastDrawnCard.Amount));
            else if (session.State.LastDrawnCard.Effect == MapCardEffect.PayBank)
                Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(500 - session.State.LastDrawnCard.Amount));
        }

        [Test]
        public void ChanceAndDestinyUseIndependentShuffledPiles()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("chance", BoardTileType.Chance, 2), Tile("rest-3", BoardTileType.Rest, 3),
                Tile("destiny", BoardTileType.Destiny, 4), Tile("rest-5", BoardTileType.Rest, 5)
            });
            var session = CreateSession(new GameModeConfig("separate-decks", 2, 2, 1000, 0, 50, 25), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)), new ScriptedRandom(0));

            session.Execute(new RollDiceCommand(1));
            var chanceCard = session.State.LastDrawnCard;
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            var destinyCard = session.State.LastDrawnCard;

            Assert.That(chanceCard.DeckId, Is.EqualTo("chance"));
            Assert.That(destinyCard.DeckId, Is.EqualTo("destiny"));
            Assert.That(chanceCard.Id, Does.StartWith("chance-"));
            Assert.That(destinyCard.Id, Does.StartWith("destiny-"));
        }

        [Test]
        public void ChanceDiscardPileIsReusedOnlyAfterEveryCardWasDrawn()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("chance", BoardTileType.Chance, 2), Tile("rest-3", BoardTileType.Rest, 3)
            });
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(2, 2), new DiceResult(2, 2),
                new DiceResult(2, 2), new DiceResult(2, 2), new DiceResult(2, 2), new DiceResult(2, 2),
                new DiceResult(2, 2));
            var session = CreateSession(new GameModeConfig("chance-cycle", 2, 2, 1000, 0, 50, 25), board.Tiles, dice, new ScriptedRandom(0));
            var drawn = new HashSet<string>();
            for (var roll = 0; roll < 9; roll++)
            {
                var playerId = roll % 2 + 1;
                session.Execute(new RollDiceCommand(playerId));
                if (session.State.LastDrawnCard != null) drawn.Add(session.State.LastDrawnCard.Id);
                if (roll < 8) session.Execute(new EndTurnCommand(playerId));
            }

            Assert.That(drawn.Count, Is.EqualTo(4));
            Assert.That(session.State.LastDrawnCard, Is.Not.Null);
            Assert.That(session.State.LastDrawnCard.DeckId, Is.EqualTo("chance"));
        }

        [Test]
        public void CourtFineShortfallBankruptsPlayerAndFinishesTheGame()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Effect("court", BoardTileType.Court, 2, 100), Tile("rest-3", BoardTileType.Rest, 3)
            });
            var session = CreateSession(new GameModeConfig("court-bankruptcy", 2, 2, 50, 0, 50, 25), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)),
                new ScriptedRandom(0, 0, 0, 0, 0, 0, 1));

            var result = session.Execute(new RollDiceCommand(1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetPlayer(2).Status, Is.EqualTo(PlayerStatus.Winner));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished));
            // T3（去字串化）：改以結構化狀態斷言（破產後現金歸零），不再依賴 LastEventMessage
            // 的 "paid $100" 字串。
            Assert.That(session.State.GetPlayer(1).Money, Is.Zero);
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
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(800));
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

        private static GameSession CreateSession(GameModeConfig mode, IEnumerable<BoardTileDefinition> tiles, IDiceRoller diceRoller, IGameRandom random = null)
        {
            var players = new List<PlayerSetup>();
            for (var i = 1; i <= mode.MaxPlayers; i++) players.Add(new PlayerSetup(i, "Player " + i));
            // GameSession 的 random 現在是必填（IRng 強制注入，ADR-0003 決策5）。這裡的
            // `random = null` 只是這個測試 helper 自己的「我不在乎亂數」語意糖，實際上一定會
            // 給 GameSession 一個固定、非 null 的 seed，不會變成 Core 內部的隱性 fallback。
            return new GameSession(mode, new BoardDefinition(tiles), players, diceRoller, random ?? new SeededRandom(1));
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Effect(string id, BoardTileType type, int positionIndex, int amount)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, amount);
        }

        private static BoardTileDefinition Airport(string id, int positionIndex, string destinationId)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, id, positionIndex, 0, 0, 0, null,
                string.Empty, id, 0, null, true, null, destinationId);
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

        private sealed class ScriptedRandom : IGameRandom
        {
            private readonly Queue<int> _values;
            public ScriptedRandom(params int[] values) { _values = new Queue<int>(values); }
            public int NextInt(int minInclusive, int maxExclusive)
            {
                var value = _values.Count == 0 ? 0 : _values.Dequeue();
                return minInclusive + Math.Abs(value) % (maxExclusive - minInclusive);
            }
        }
    }
}
