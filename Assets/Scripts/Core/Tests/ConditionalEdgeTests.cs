using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Richman.Core.Events;

namespace Richman.Core.Tests
{
    // Phase 1d-2（docs/adr/0002-graph-map-structure.md 決策2：條件邊、傳送邊、pass-through
    // 與 landing 觸發分別掛鉤）。本檔鎖住三件事：
    //   1. 條件邊——RequiredFlag 未滿足的邊在移動解析時視為不存在；分岔決策（ChooseRoute）
    //      看的是「現在開著」的邊數，不是宣告邊數；某格因 flag 而一條開啟的邊都沒有時，
    //      玩家停在原地並發 MovementBlocked。
    //   2. BoardFlags——地圖的 InitialFlags 在建構時播種進 GameState；之後只能透過
    //      GameSession.SetBoardFlag/ClearBoardFlag（internal，供 Phase 3 Effect 呼叫）變動，
    //      且只有「真的改變了狀態」才會發 BoardFlagChanged（見 AssemblyInfo.cs 的
    //      InternalsVisibleTo，讓這個測試組件可以直接呼叫這兩個 internal 方法）。
    //   3. 通用 pass-through 觸發——PassThroughTriggerKind.GrantStartSalary 不再綁死
    //      PositionIndex == StartPositionIndex，任何格都可以結構上掛同一種觸發。
    public sealed class ConditionalEdgeTests
    {
        [Test]
        public void ClosedConditionalEdge_CollapsesToASingleOpenEdgeAndWalksDirectlyWithoutADecision()
        {
            var session = BuildForkSession(initialFlags: null);

            Assert.That(session.State.HasBoardFlag("scenic-route-open"), Is.False, "旗標預設不開啟。");

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            Assert.That(session.State.PendingAction, Is.Not.EqualTo(PendingAction.ChooseRoute),
                "flag 關閉時 mid 只剩一條開啟的邊（forward），不應該產生分岔決策。");
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex,
                Is.EqualTo(session.Board.GetTile("forward").PositionIndex));
        }

        [Test]
        public void OpenConditionalEdge_ProducesChooseRouteDecisionBetweenBothOptions()
        {
            // InitialFlags 示範「地圖定義本身要求一開局就開啟」這條路徑。
            var session = BuildForkSession(initialFlags: new[] { "scenic-route-open" });

            Assert.That(session.State.HasBoardFlag("scenic-route-open"), Is.True,
                "InitialFlags 應該在建構時就播種進 GameState。");

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.ChooseRoute),
                "flag 開啟時 mid 有兩條開啟的邊（forward、gate），應該產生分岔決策。");
            Assert.That(session.State.RouteChoiceTileIds, Is.EquivalentTo(new[] { "forward", "gate" }));

            var choice = session.Execute(new ChooseRouteCommand(1, "gate"));

            Assert.That(choice.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex,
                Is.EqualTo(session.Board.GetTile("gate").PositionIndex));
        }

        private static GameSession BuildForkSession(IEnumerable<string> initialFlags)
        {
            var mode = new GameModeConfig("edge-fork", 2, 2, 500, 0, 0, 0);
            var tiles = new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "mid" }),
                TileWithEdges("mid", BoardTileType.Rest, 1,
                    unconditionalNext: new[] { "forward" },
                    conditionalNext: new[] { new BoardEdgeDefinition("gate", "scenic-route-open") }),
                Tile("forward", BoardTileType.Rest, 2, new[] { "start" }),
                Tile("gate", BoardTileType.Rest, 3, new[] { "start" })
            };
            var board = new BoardDefinition(tiles, initialFlags);
            var players = new List<PlayerSetup> { new PlayerSetup(1, "P1"), new PlayerSetup(2, "P2") };
            return new GameSession(mode, board, players, new ScriptedDiceRoller(new DiceResult(1, 1)), new SeededRandom(1));
        }

        [Test]
        public void ZeroOpenEdges_StopsThePlayerInPlaceAndEmitsMovementBlocked()
        {
            var mode = new GameModeConfig("edge-blocked", 2, 2, 500, 0, 0, 0);
            var tiles = new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "blocker" }),
                TileWithEdges("blocker", BoardTileType.Rest, 1,
                    conditionalNext: new[] { new BoardEdgeDefinition("beyond", "never-open") }),
                Tile("beyond", BoardTileType.Rest, 2, new[] { "start" })
            };
            var board = new BoardDefinition(tiles);
            var players = new List<PlayerSetup> { new PlayerSetup(1, "P1"), new PlayerSetup(2, "P2") };
            var session = new GameSession(mode, board, players, new ScriptedDiceRoller(new DiceResult(1, 1)), new SeededRandom(1));

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            var blocked = roll.Events.OfType<MovementBlocked>().Single();
            Assert.That(blocked.PlayerId, Is.EqualTo(1));
            Assert.That(blocked.TileId, Is.EqualTo("blocker"));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(board.GetTile("blocker").PositionIndex),
                "沒有任何開著的出邊時，玩家應該停在原地，不是消失或丟例外。");
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn),
                "卡住之後仍然照常跑落地解析（這裡是 Rest 格，沒有副作用，直接進入可結束回合狀態）。");
        }

        [Test]
        public void SetAndClearBoardFlag_OnlyEmitBoardFlagChangedWhenTheStateActuallyChanges()
        {
            var mode = new GameModeConfig("edge-flags", 2, 2, 500, 0, 0, 0);
            var tiles = new[] { Tile("start", BoardTileType.Start, 0, new[] { "rest" }), Tile("rest", BoardTileType.Rest, 1, new[] { "start" }) };
            var board = new BoardDefinition(tiles);
            var players = new List<PlayerSetup> { new PlayerSetup(1, "P1"), new PlayerSetup(2, "P2") };
            var session = new GameSession(mode, board, players, new ScriptedDiceRoller(new DiceResult(1, 1)), new SeededRandom(1));

            Assert.That(session.State.HasBoardFlag("gate-open"), Is.False);

            Assert.That(session.SetBoardFlag("gate-open"), Is.True);
            Assert.That(session.State.HasBoardFlag("gate-open"), Is.True);
            Assert.That(session.SetBoardFlag("gate-open"), Is.False, "已經開啟時重複設定應該是沒有效果的 no-op。");

            Assert.That(session.ClearBoardFlag("gate-open"), Is.True);
            Assert.That(session.State.HasBoardFlag("gate-open"), Is.False);
            Assert.That(session.ClearBoardFlag("gate-open"), Is.False, "已經關閉時重複清除應該是沒有效果的 no-op。");

            var flagEvents = session.EventLog.OfType<BoardFlagChanged>().ToList();
            Assert.That(flagEvents.Count, Is.EqualTo(2),
                "只有兩次真正改變狀態的呼叫（開、關各一次）應該留下事件，兩次 no-op 不應該。");
            Assert.That(flagEvents[0].Flag, Is.EqualTo("gate-open"));
            Assert.That(flagEvents[0].IsSet, Is.True);
            Assert.That(flagEvents[1].Flag, Is.EqualTo("gate-open"));
            Assert.That(flagEvents[1].IsSet, Is.False);
        }

        [Test]
        public void ConnectivityValidationTreatsConditionalEdgesAsAlwaysOpenEvenIfNeverInitiallySet()
        {
            // 載入驗證（BFS 可達性）把所有邊都當作開啟，包括條件邊——這裡 start 到 island
            // 唯一的路徑是一條平常關閉的條件邊（沒有出現在 InitialFlags），建構仍然合法；
            // 這條邊「現在」走不走得通，是移動解析的執行期語意，不是建構期的圖結構錯誤。
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                TileWithEdges("start", BoardTileType.Start, 0,
                    conditionalNext: new[] { new BoardEdgeDefinition("island", "never-open-at-construction") }),
                Tile("island", BoardTileType.Rest, 1, new[] { "start" })
            }));
            Assert.That(board.TileCount, Is.EqualTo(2));
            Assert.That(board.GetOpenNextTiles(board.GetTile("start").PositionIndex, _ => false).Count, Is.EqualTo(0),
                "執行期若 flag 真的沒開，開啟邊數應該是 0——證明建構期允許、不代表執行期一定走得通。");
        }

        [Test]
        public void PassThroughTriggerCanBeAssignedToATileOtherThanStart()
        {
            // 證明 PassThroughTriggerKind.GrantStartSalary 已經不是寫死的
            // 「PositionIndex == StartPositionIndex」——一個不是 Start 的格子也可以顯式
            // 掛同一種觸發，經過/停在它一樣會給薪，PawnStepped.PassedStart 一樣會是 true。
            var mode = new GameModeConfig("edge-pass-through", 2, 2, 500, 50, 0, 0);
            var tiles = new[]
            {
                Tile("start", BoardTileType.Start, 0, new[] { "toll-gate" }),
                TileWithPassThrough("toll-gate", BoardTileType.Rest, 1, new[] { "rest" }, PassThroughTriggerKind.GrantStartSalary),
                Tile("rest", BoardTileType.Rest, 2, new[] { "start" })
            };
            var board = new BoardDefinition(tiles);
            var players = new List<PlayerSetup> { new PlayerSetup(1, "P1"), new PlayerSetup(2, "P2") };
            var session = new GameSession(mode, board, players, new ScriptedDiceRoller(new DiceResult(1, 1)), new SeededRandom(1));

            var roll = session.Execute(new RollDiceCommand(1)); // dice=2: start -> toll-gate -> rest。

            Assert.That(roll.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(board.GetTile("rest").PositionIndex));
            var salary = roll.Events.OfType<SalaryCollected>().Single();
            Assert.That(salary.PlayerId, Is.EqualTo(1));
            Assert.That(salary.Amount, Is.EqualTo(50));
            var steppedOnTollGate = roll.Events.OfType<PawnStepped>()
                .Single(e => e.ToTileIndex == board.GetTile("toll-gate").PositionIndex);
            Assert.That(steppedOnTollGate.PassedStart, Is.True,
                "PassedStart 欄位現在反映的是 PassThroughTrigger==GrantStartSalary，不是「這格真的是 Start」。");
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex, IEnumerable<string> next = null)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, 0, next);
        }

        private static BoardTileDefinition TileWithEdges(string id, BoardTileType type, int positionIndex,
            IEnumerable<string> unconditionalNext = null, IEnumerable<BoardEdgeDefinition> conditionalNext = null)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id,
                effectAmount: 0, nextTileIds: unconditionalNext, isMainRoute: true, rejoinsAtId: null,
                airportDestinationId: null, conditionalEdges: conditionalNext);
        }

        private static BoardTileDefinition TileWithPassThrough(string id, BoardTileType type, int positionIndex,
            IEnumerable<string> next, PassThroughTriggerKind trigger)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id,
                effectAmount: 0, nextTileIds: next, isMainRoute: true, rejoinsAtId: null,
                airportDestinationId: null, conditionalEdges: null, passThroughTrigger: trigger);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly Queue<DiceResult> _results;
            public ScriptedDiceRoller(params DiceResult[] results) { _results = new Queue<DiceResult>(results); }
            public DiceResult Roll()
            {
                if (_results.Count == 0) throw new System.InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
