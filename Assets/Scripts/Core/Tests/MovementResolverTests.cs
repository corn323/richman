using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Richman.Core.Events;

namespace Richman.Core.Tests
{
    // Phase 1d-3（ADR-0002）：MovementResolver 的單元測試，不經過 GameSession/Execute()，
    // 直接建構 GameState/PlayerState（internal 建構子，透過 AssemblyInfo.cs 的
    // InternalsVisibleTo 對本測試組件開放）並呼叫 MovementResolver，驗證「逐格走圖」本身
    // 的行為：直線多步、分岔暫停、條件邊封鎖。GameSession 端的等價行為（事件序列、
    // 黃金雜湊）已經由 EventSequenceTests/ConditionalEdgeTests/GoldenReplayTests 鎖住，
    // 這裡的目的是證明抽出來的 MovementResolver 本身可以獨立運作、獨立測試。
    public sealed class MovementResolverTests
    {
        [Test]
        public void WalksAStraightLineForTheFullDiceTotalWhenEveryTileHasExactlyOneOpenEdge()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "a" }),
                Tile("a", BoardTileType.Rest, 1, new[] { "b" }),
                Tile("b", BoardTileType.Rest, 2, new[] { "c" }),
                Tile("c", BoardTileType.Rest, 3, new[] { "start" })
            });
            var (state, player) = BuildState(board);
            var events = new List<GameEvent>();
            var resolver = new MovementResolver(board, state, startBonus: 0, emit: events.Add);
            state.MovementStepsRemaining = 3;
            var path = new List<int>();

            resolver.ContinueMovement(player, path);

            Assert.That(state.MovementStepsRemaining, Is.EqualTo(0), "三步應該全部走完，沒有被分岔或封鎖打斷。");
            Assert.That(player.CurrentTileIndex, Is.EqualTo(board.GetTile("c").PositionIndex));
            Assert.That(path, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(events.OfType<PhaseChanged>().Single().Stage, Is.EqualTo(TurnStage.Move));
            Assert.That(events.OfType<PawnStepped>().Count(), Is.EqualTo(3));
            Assert.That(events.OfType<DecisionRequested>(), Is.Empty);
            Assert.That(events.OfType<MovementBlocked>(), Is.Empty);
        }

        [Test]
        public void PausesWithAChooseRouteDecisionAtAForkAndResumesAfterMoveOneStep()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "fork" }),
                Tile("fork", BoardTileType.Rest, 1, new[] { "left", "right" }),
                Tile("left", BoardTileType.Rest, 2, new[] { "start" }),
                Tile("right", BoardTileType.Rest, 3, new[] { "start" })
            });
            var (state, player) = BuildState(board);
            var events = new List<GameEvent>();
            var resolver = new MovementResolver(board, state, startBonus: 0, emit: events.Add);
            // 總步數 3：第一步 start -> fork 直接走掉；第二步在 fork 遇到分岔暫停（這一步
            // 還沒消耗）；手動選 right 之後，還剩第三步可以繼續走到 start，證明「續走」
            // 不是只消耗掉分岔那一步、後面就不管了。
            state.MovementStepsRemaining = 3;
            var path = new List<int>();

            resolver.ContinueMovement(player, path);

            Assert.That(player.CurrentTileIndex, Is.EqualTo(board.GetTile("fork").PositionIndex),
                "第一步（start -> fork，唯一出邊）應該直接走掉；第二步在 fork 遇到兩條開啟的邊，暫停等待選擇。");
            Assert.That(state.MovementStepsRemaining, Is.EqualTo(2), "暫停時剩餘步數不應該被消耗。");
            Assert.That(state.PendingAction, Is.EqualTo(PendingAction.ChooseRoute));
            Assert.That(state.RouteChoiceTileIds, Is.EquivalentTo(new[] { "left", "right" }));
            var requested = events.OfType<DecisionRequested>().Single();
            Assert.That(requested.Kind, Is.EqualTo(DecisionKind.ChooseRoute));
            Assert.That(requested.OptionIds, Is.EquivalentTo(new[] { "left", "right" }));

            // 模擬 GameSession.ExecuteChooseRoute：先把選到的那一步走掉，再呼叫 ContinueMovement
            // 把剩下的步數走完——這正是 MovementResolver 對外暴露 MoveOneStep 的理由。
            resolver.MoveOneStep(player, board.GetTile("right"), path);
            resolver.ContinueMovement(player, path);

            Assert.That(player.CurrentTileIndex, Is.EqualTo(board.GetTile("start").PositionIndex),
                "選完 right 之後還剩一步，應該繼續走到 start，而不是停在 right。");
            Assert.That(state.MovementStepsRemaining, Is.EqualTo(0));
            Assert.That(path, Is.EqualTo(new[] { board.GetTile("fork").PositionIndex, board.GetTile("right").PositionIndex, board.GetTile("start").PositionIndex }));
        }

        [Test]
        public void StopsThePlayerInPlaceAndEmitsMovementBlockedWhenAConditionalEdgeIsClosed()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "blocker" }),
                TileWithConditionalEdge("blocker", BoardTileType.Rest, 1, "beyond", "never-open"),
                Tile("beyond", BoardTileType.Rest, 2, new[] { "start" })
            });
            var (state, player) = BuildState(board);
            Assert.That(state.HasBoardFlag("never-open"), Is.False);
            var events = new List<GameEvent>();
            var resolver = new MovementResolver(board, state, startBonus: 0, emit: events.Add);
            state.MovementStepsRemaining = 2;
            var path = new List<int>();

            resolver.ContinueMovement(player, path);

            Assert.That(player.CurrentTileIndex, Is.EqualTo(board.GetTile("blocker").PositionIndex),
                "第一步 start -> blocker 正常走掉；第二步 blocker 唯一的出邊因條件邊關閉而消失，應停在原地。");
            Assert.That(state.MovementStepsRemaining, Is.EqualTo(0), "被封鎖時剩餘步數直接作廢，不是暫停等待。");
            var blocked = events.OfType<MovementBlocked>().Single();
            Assert.That(blocked.PlayerId, Is.EqualTo(player.Id));
            Assert.That(blocked.TileId, Is.EqualTo("blocker"));
            Assert.That(events.OfType<DecisionRequested>(), Is.Empty);
        }

        private static (GameState state, PlayerState player) BuildState(BoardDefinition board)
        {
            var mode = new GameModeConfig("movement-resolver-test", 1, 1, 500, 0, 0, 0);
            var player = new PlayerState(new PlayerSetup(1, "P1"), mode.StartingMoney, board.StartPositionIndex);
            var state = new GameState(mode, board, new List<PlayerState> { player }, new List<PropertyState>());
            return (state, player);
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex, IEnumerable<string> next = null)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, 0, next);
        }

        private static BoardTileDefinition TileWithConditionalEdge(string id, BoardTileType type, int positionIndex, string toTileId, string requiredFlag)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id,
                effectAmount: 0, nextTileIds: null, isMainRoute: true, rejoinsAtId: null, airportDestinationId: null,
                conditionalEdges: new[] { new BoardEdgeDefinition(toTileId, requiredFlag) });
        }
    }
}
