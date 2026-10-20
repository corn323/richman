using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T5 地圖約束逐條測試（docs/AUDIT.md §7.2 T5，Phase 1a 保護測試網）。
    //
    // Phase 1d-1（ADR-0002）鬆綁了三類「版面/環形殘留」約束，把它們移出規則驗證：
    //   1. 「主環必佔前 N 連續 index 且成單一封閉迴圈」→ 改成一般圖驗證：從 Start
    //      可達所有節點（連通性），每格至少一條出邊（已在別處驗證，無需重複宣告）。
    //   2. 「支線每格只能有一個出口」→ 完全移出規則驗證，是 AssignMapPositions
    //      這個佈局演算法自己的前提，佈局算不出來就放棄佈局（見 Board.cs 的
    //      try/catch），不會擋下合法的圖結構建構。
    //   3. `IsMainRoute` / `RejoinsAtId` 降級為佈局/標注提示，兩者都不再參與規則
    //      驗證（欄位仍保留，供佈局工具或地圖標注使用）。
    //
    // 以下測試方法名稱如果是 `Accepts_...`，代表這條約束已被移除，用同一份棋盤資料
    // 證明「以前會被拒絕的圖形，現在可以合法建構」。`Rejects_...` 則代表這條約束
    // 仍然是規則（或是 Phase 1d-1 新增的一般圖驗證），預期繼續拒絕。
    public sealed class BoardConstraintTests
    {
        // 基礎參數驗證（規則）。
        [Test]
        public void Rejects_FewerThanTwoTiles()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[] { Tile("only", BoardTileType.Start, 0) }));
            Assert.That(ex.Message, Does.Contain("at least two tiles"));
        }

        // 規則：tile id 必須唯一。
        [Test]
        public void Rejects_DuplicateTileIds()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("dup", BoardTileType.Start, 0),
                Tile("dup", BoardTileType.Rest, 1)
            }));
            Assert.That(ex.Message, Does.Contain("unique"));
        }

        // 規則（保留）：PositionIndex == 陣列索引。這不是「版面限制」清單裡的那三項
        // （ADR-0002 §決策1 只點名了主環連續 index／封閉迴圈／支線單出口），而是一個
        // 儲存層不變量：PlayerState.CurrentTileIndex 等下游 API 現在仍然用這個 int
        // 當 array handle。PositionIndex 不再隱含「環序」，但它仍然必須等於陣列索引，
        // 否則 GetTileAt(int) 這種以索引查表的 API 會對不上。
        [Test]
        public void Rejects_NonContiguousPositionIndex()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 2) // 陣列索引是 1，但 PositionIndex 刻意跳號為 2。
            }));
            Assert.That(ex.Message, Does.Contain("contiguous"));
        }

        // 規則：board 恰好必須有一個 Start tile（這裡示範「多個 Start」）。
        [Test]
        public void Rejects_MultipleStartTiles()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start-1", BoardTileType.Start, 0),
                Tile("start-2", BoardTileType.Start, 1),
                Tile("rest", BoardTileType.Rest, 2)
            }));
            Assert.That(ex.Message, Does.Contain("exactly one Start tile"));
        }

        // 規則：board 必須恰好有一個 Start tile（這裡示範「零個 Start」）。
        [Test]
        public void Rejects_NoStartTile()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("rest-1", BoardTileType.Rest, 0),
                Tile("rest-2", BoardTileType.Rest, 1)
            }));
            Assert.That(ex.Message, Does.Contain("exactly one Start tile"));
        }

        // ADR-0002：移除「主環必佔前 N 個連續 index」。IsMainRoute 現在只是佈局提示，
        // 一個 IsMainRoute=false 的分支格排在中間不再是錯誤——這份棋盤資料以前會被拒絕，
        // 現在應該能正常建構（start -> branch -> rest-main -> start 形成一個合法的圖）。
        [Test]
        public void Accepts_MainRouteTilesNotOccupyingContiguousFrontIndexes()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, isMainRoute: true),
                Tile("branch", BoardTileType.Rest, 1, next: new[] { "rest-main" }, isMainRoute: false, rejoinsAt: "rest-main"),
                Tile("rest-main", BoardTileType.Rest, 2, isMainRoute: true)
            }));
            Assert.That(board.TileCount, Is.EqualTo(3));
        }

        // 規則：每一格都必須至少有一條出邊。
        [Test]
        public void Rejects_TileWithNoOutgoingConnection()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: Array.Empty<string>(), isMainRoute: false, rejoinsAt: "start")
            }));
            Assert.That(ex.Message, Does.Contain("must connect to at least one next tile"));
        }

        // 規則：出邊必須指向實際存在的 tile id。
        [Test]
        public void Rejects_NextTileIdPointingToMissingId()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: new[] { "ghost-tile" }, isMainRoute: false, rejoinsAt: "start")
            }));
            Assert.That(ex.Message, Does.Contain("missing id"));
        }

        // 規則：機場必須指向一個「存在」且「同為機場型別」的目的地（此測試：目的地不存在）。
        [Test]
        public void Rejects_AirportWithMissingDestination()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Airport("air1", 2, "nonexistent-destination")
            }));
            Assert.That(ex.Message, Does.Contain("must route to a different valid airport"));
        }

        // 規則：機場目的地必須「同為機場型別」（此測試：目的地存在但型別不對）。
        [Test]
        public void Rejects_AirportWithNonAirportDestination()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Airport("air1", 2, "rest")
            }));
            Assert.That(ex.Message, Does.Contain("must route to a different valid airport"));
        }

        // 規則：機場不可以把自己設為自己的目的地。
        [Test]
        public void Rejects_AirportRoutingToItself()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Airport("air1", 2, "air1")
            }));
            Assert.That(ex.Message, Does.Contain("must route to a different valid airport"));
        }

        // ADR-0002：RejoinsAtId 降級為佈局/標注提示，不再參與規則驗證。非主環格以前
        // 必須宣告 RejoinsAtId，現在不用了——這份棋盤資料以前會被拒絕，現在應該能正常建構。
        [Test]
        public void Accepts_BranchTileWithoutDeclaredRejoinId()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: new[] { "rest" }, isMainRoute: false, rejoinsAt: null)
            }));
            Assert.That(board.GetTile("branch").RejoinsAtId, Is.Empty,
                "欄位仍保留相容（空字串），只是不再被規則驗證要求。");
        }

        // ADR-0002：移除「主環必佔前 N 個連續 index 且成單一封閉迴圈」。這份棋盤資料的
        // 三格不構成單一封閉環（start -> rest-2 -> rest-1 -> rest-2，不是一個乾淨的環），
        // 但從 Start 仍然可以走到每一格，依新規則（一般連通性）應該可以合法建構。
        [Test]
        public void Accepts_GraphThatDoesNotFormASingleClosedLoopButIsFullyConnected()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-2" }),
                Tile("rest-1", BoardTileType.Rest, 1, next: new[] { "rest-2" }),
                Tile("rest-2", BoardTileType.Rest, 2, next: new[] { "rest-1" })
            }));
            Assert.That(board.TileCount, Is.EqualTo(3));
        }

        // Phase 1d-1 新增（ADR-0002 一般圖驗證）：BFS 可達性規則不是裝飾——完全連不到
        // Start 的孤島仍然必須被拒絕，只是理由從「沒有形成主環」改成「無法從 Start 到達」。
        [Test]
        public void Rejects_TileThatIsNotReachableFromStart()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "start" }), // 自環，刻意不連到 island。
                Tile("island", BoardTileType.Rest, 1, next: new[] { "island" }) // 自己的孤島。
            }));
            Assert.That(ex.Message, Does.Contain("not reachable from the Start tile"));
        }

        // ADR-0002：RejoinsAtId 降級為佈局/標注提示，不再要求「必須指向一個主環格」。
        // 這份棋盤資料的 RejoinsAtId 指向自己（branch 自己），以前會被拒絕，現在應該
        // 能正常建構（圖本身仍然是連通的，驗證只看 NextTileIds，不看 RejoinsAtId）。
        [Test]
        public void Accepts_BranchRejoinIdThatIsNotAMainRouteTile()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-1", "branch" }),
                Tile("rest-1", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: new[] { "rest-1" }, isMainRoute: false, rejoinsAt: "branch")
            }));
            Assert.That(board.TileCount, Is.EqualTo(3));
        }

        // ADR-0002：RejoinsAtId 降級為佈局/標注提示，不再要求「分支必須能走到自己宣告的
        // 匯合點」。這份棋盤資料裡 branch-a/branch-b 宣告的匯合點是 start，但它們實際上
        // 互相指向對方——這在舊規則下是錯誤，但新規則只看整張圖是否從 Start 可達每一格
        // （這裡仍然可達：start 直接連到 branch-a，branch-a/branch-b 之間可以互通），
        // 所以現在應該能正常建構。
        [Test]
        public void Accepts_BranchThatCannotReachItsDeclaredRejoinPoint()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-1", "branch-a" }),
                Tile("rest-1", BoardTileType.Rest, 1),
                Tile("branch-a", BoardTileType.Rest, 2, next: new[] { "branch-b" }, isMainRoute: false, rejoinsAt: "start"),
                Tile("branch-b", BoardTileType.Rest, 3, next: new[] { "branch-a" }, isMainRoute: false, rejoinsAt: "start")
            }));
            Assert.That(board.TileCount, Is.EqualTo(4));
        }

        // ADR-0002：移除「支線每格僅一出口」（Board.cs 舊 277 行附近）。這其實是
        // AssignMapPositions 這個視覺佈局演算法自己的前提，不是圖結構規則——fork 格有
        // 兩個出口（rest-a、rest-b）在舊規則下會被拒絕，現在應該能正常建構；佈局演算法
        // 算不出這段佈局時會自己放棄（Board.cs 建構子的 try/catch），不會擋下建構本身。
        [Test]
        public void Accepts_BranchWaypointWithMultipleOutgoingRoutes()
        {
            BoardDefinition board = null;
            Assert.DoesNotThrow(() => board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-a", "fork" }),
                Tile("rest-a", BoardTileType.Rest, 1),
                Tile("rest-b", BoardTileType.Rest, 2),
                Tile("fork", BoardTileType.Rest, 3, next: new[] { "rest-a", "rest-b" }, isMainRoute: false, rejoinsAt: "rest-a")
            }));
            Assert.That(board.GetNextTiles(board.GetTile("fork").PositionIndex).Count, Is.EqualTo(2));
        }

        // Phase 1d-1 正向測試 (a)（ADR-0002）：一張非環形的 DAG＋迴圈混合小地圖可以建構，
        // 而且移動確實是沿著 NextTileIds 這張圖走，不是用「(目前索引+步數) % 總格數」這種
        // 環形索引算術猜下一格。
        //
        // 圖形：start -> hub（單一出邊，直接走掉）；
        //       hub -> loop-back ／ hub -> shortcut（分岔，走到 hub 時剩餘步數還沒走完，
        //       必須靠 ChooseRoute 決策才能決定往哪走）；
        //       shortcut -> loop-back（一條不經過 start 的 DAG 捷徑）；
        //       loop-back -> start（迴圈收尾）。
        [Test]
        public void Accepts_NonRingDagWithLoopMixAndMovementFollowsGraphEdges()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "hub" }),
                Tile("hub", BoardTileType.Rest, 1, next: new[] { "loop-back", "shortcut" }),
                Tile("shortcut", BoardTileType.Rest, 2, next: new[] { "loop-back" }),
                Tile("loop-back", BoardTileType.Rest, 3, next: new[] { "start" })
            });

            var mode = new GameModeConfig("t5-dag-loop-mix", 2, 2, 500, 0, 0, 0);
            var players = new List<PlayerSetup> { new PlayerSetup(1, "Player 1"), new PlayerSetup(2, "Player 2") };
            var session = new GameSession(mode, board, players, new ScriptedDiceRoller(new DiceResult(1, 1)), new SeededRandom(1));

            var roll = session.Execute(new RollDiceCommand(1));

            Assert.That(roll.Succeeded, Is.True);
            // 骰出總步數 2：第一步 start -> hub（單一出邊，直接走完）；
            // 第二步站在 hub 時出邊有兩條，必須停下來等 ChooseRoute，
            // 而不是用索引算術自己猜一條路走掉。
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.ChooseRoute));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(board.GetTile("hub").PositionIndex));

            var choice = session.Execute(new ChooseRouteCommand(1, "shortcut"));

            Assert.That(choice.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(board.GetTile("shortcut").PositionIndex),
                "選擇 shortcut 後應該真的走到 shortcut，證明移動是照 NextTileIds 這張圖走，而不是環形索引算術。");
        }

        // Phase 1d-1 正向測試 (b)（ADR-0002）：多分岔、多合流的地圖可以建構。
        // 分岔：一個節點有多條出邊（start -> fork-a/fork-b；merge -> fork-c/fork-d）。
        // 合流：多個節點指向同一個節點（fork-a/fork-b -> merge；fork-c/fork-d -> start）。
        // 整張圖不是單一封閉環，只是從 Start 可達每一格。
        [Test]
        public void Accepts_MultiForkMultiMergeGraphCanBeConstructed()
        {
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "fork-a", "fork-b" }),
                Tile("fork-a", BoardTileType.Rest, 1, next: new[] { "merge" }),
                Tile("fork-b", BoardTileType.Rest, 2, next: new[] { "merge" }),
                Tile("merge", BoardTileType.Rest, 3, next: new[] { "fork-c", "fork-d" }),
                Tile("fork-c", BoardTileType.Rest, 4, next: new[] { "start" }),
                Tile("fork-d", BoardTileType.Rest, 5, next: new[] { "start" })
            });

            Assert.That(board.TileCount, Is.EqualTo(6));
            Assert.That(board.GetNextTiles(board.GetTile("start").PositionIndex).Count, Is.EqualTo(2),
                "start 分岔出兩條路（fork-a/fork-b）。");
            Assert.That(board.GetNextTiles(board.GetTile("merge").PositionIndex).Count, Is.EqualTo(2),
                "merge 匯合兩條路之後，自己又分岔出兩條新路（fork-c/fork-d）。");
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex,
            IEnumerable<string> next = null, bool isMainRoute = true, string rejoinsAt = null)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, 0, next, isMainRoute, rejoinsAt);
        }

        private static BoardTileDefinition Airport(string id, int positionIndex, string destinationId)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, id, positionIndex, 0, 0, 0, null,
                string.Empty, id, 0, null, true, null, destinationId);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly Queue<DiceResult> _results;
            public ScriptedDiceRoller(params DiceResult[] results) { _results = new Queue<DiceResult>(results); }
            public DiceResult Roll()
            {
                if (_results.Count == 0) throw new InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
