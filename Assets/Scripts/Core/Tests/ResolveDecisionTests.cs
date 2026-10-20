using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // Phase 1c：ResolveDecisionCommand 與既有語法糖命令（BuyPropertyCommand/
    // UpgradePropertyCommand/ChooseRouteCommand）的等價測試（ADR-0003 決策3）。
    //
    // 做法：用兩個完全相同設定（board/mode/scripted dice）的孿生 GameSession，
    // 跑到同一個待決點後分流——一邊送語法糖命令，另一邊送等價的
    // ResolveDecisionCommand(playerId, optionId)——然後比對兩邊的 CommandResult.Succeeded、
    // 完整狀態快照（GoldenReplayTests.SnapshotState）與事件序列
    // （GoldenReplayTests.SerializeEvents）是否逐一相同。
    public sealed class ResolveDecisionTests
    {
        [Test]
        public void ResolveDecisionBuyPropertyMatchesBuyPropertyCommand()
        {
            var mode = new GameModeConfig("resolve-buy", 2, 2, 500, 0, 50, 25);
            var tiles = new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) };

            var sessionSugar = CreateSession(mode, tiles, new ScriptedDiceRoller(new DiceResult(1, 1)));
            var sessionResolve = CreateSession(mode, tiles, new ScriptedDiceRoller(new DiceResult(1, 1)));
            sessionSugar.Execute(new RollDiceCommand(1));
            sessionResolve.Execute(new RollDiceCommand(1));

            var sugarResult = sessionSugar.Execute(new BuyPropertyCommand(1));
            var resolveResult = sessionResolve.Execute(new ResolveDecisionCommand(1, "p1"));

            Assert.That(resolveResult.Succeeded, Is.EqualTo(sugarResult.Succeeded));
            Assert.That(GoldenReplayTests.SnapshotState(sessionResolve.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionSugar.State)));
            Assert.That(GoldenReplayTests.SerializeEvents(resolveResult.Events), Is.EqualTo(GoldenReplayTests.SerializeEvents(sugarResult.Events)));
        }

        [Test]
        public void ResolveDecisionUpgradePropertyMatchesUpgradePropertyCommand()
        {
            var mode = new GameModeConfig("resolve-upgrade", 2, 2, 500, 0, 50, 25);
            var tiles = new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) };

            var sessionSugar = CreateSession(mode, tiles, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 2)));
            var sessionResolve = CreateSession(mode, tiles, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 2)));
            DriveToOwnPropertyLandingAgain(sessionSugar);
            DriveToOwnPropertyLandingAgain(sessionResolve);
            Assert.That(sessionSugar.State.PendingAction, Is.EqualTo(PendingAction.UpgradeProperty));
            Assert.That(GoldenReplayTests.SnapshotState(sessionResolve.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionSugar.State)));

            var sugarResult = sessionSugar.Execute(new UpgradePropertyCommand(1, "p1"));
            var resolveResult = sessionResolve.Execute(new ResolveDecisionCommand(1, "p1"));

            Assert.That(resolveResult.Succeeded, Is.EqualTo(sugarResult.Succeeded));
            Assert.That(GoldenReplayTests.SnapshotState(sessionResolve.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionSugar.State)));
            Assert.That(GoldenReplayTests.SerializeEvents(resolveResult.Events), Is.EqualTo(GoldenReplayTests.SerializeEvents(sugarResult.Events)));
        }

        [Test]
        public void ResolveDecisionChooseRouteMatchesChooseRouteCommand()
        {
            var board = PrototypeBoardFactory.Create();
            var mode = GameModeConfig.ClassicBankruptcyFourPlayer();
            var sessionSugar = CreateSession(mode, board.Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));
            var sessionResolve = CreateSession(mode, board.Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));
            DriveToRouteFork(sessionSugar);
            DriveToRouteFork(sessionResolve);
            Assert.That(sessionSugar.State.PendingAction, Is.EqualTo(PendingAction.ChooseRoute));
            Assert.That(GoldenReplayTests.SnapshotState(sessionResolve.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionSugar.State)));

            var sugarResult = sessionSugar.Execute(new ChooseRouteCommand(1, "macau-ferry-pier"));
            var resolveResult = sessionResolve.Execute(new ResolveDecisionCommand(1, "macau-ferry-pier"));

            Assert.That(resolveResult.Succeeded, Is.EqualTo(sugarResult.Succeeded));
            Assert.That(GoldenReplayTests.SnapshotState(sessionResolve.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionSugar.State)));
            Assert.That(GoldenReplayTests.SerializeEvents(resolveResult.Events), Is.EqualTo(GoldenReplayTests.SerializeEvents(sugarResult.Events)));
        }

        [Test]
        public void ResolveDecisionRejectsAnOptionIdOutsideThePendingDecision()
        {
            var mode = new GameModeConfig("resolve-invalid-option", 2, 2, 500, 0, 50, 25);
            var tiles = new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) };
            var session = CreateSession(mode, tiles, new ScriptedDiceRoller(new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            var rejected = session.Execute(new ResolveDecisionCommand(1, "not-the-pending-option"));

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.ErrorCode, Is.EqualTo(CommandErrorCode.InvalidDecisionOption));
        }

        private static void DriveToOwnPropertyLandingAgain(GameSession session)
        {
            session.Execute(new RollDiceCommand(1)); // 0 -> 2 (p1, unowned, buy)
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1)); // 2 -> (2+3)%3=2, lands back on own p1
        }

        private static void DriveToRouteFork(GameSession session)
        {
            session.Execute(new RollDiceCommand(1));
            session.Execute(new EndTurnCommand(1));
            for (var playerId = 2; playerId <= 4; playerId++)
            {
                session.Execute(new RollDiceCommand(playerId));
                session.Execute(new EndTurnCommand(playerId));
            }
            session.Execute(new RollDiceCommand(1));
        }

        private static GameSession CreateSession(GameModeConfig mode, IEnumerable<BoardTileDefinition> tiles, IDiceRoller diceRoller, IGameRandom random = null)
        {
            var players = new List<PlayerSetup>();
            for (var i = 1; i <= mode.MaxPlayers; i++) players.Add(new PlayerSetup(i, "Player " + i));
            return new GameSession(mode, new BoardDefinition(tiles), players, diceRoller, random ?? new SeededRandom(1));
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id);
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
                if (_results.Count == 0) throw new InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
