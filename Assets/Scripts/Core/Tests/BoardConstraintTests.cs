using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T5 地圖約束逐條測試（docs/AUDIT.md §7.2 T5，Phase 1a 保護測試網）。
    //
    // 每條 BoardDefinition 建構子驗證都對應一個「應拒絕」測試，並在方法上方標注
    // 該約束依 ADR-0002 分類屬於：
    //   - 「規則」：圖結構本身的合法性（唯一性、連通性、型別一致性等），
    //     ADR-0002 之後的通用圖結構仍應保留。
    //   - 「版面限制」：ADR-0002 明確指出「這其實是版面限制，卻混在規則驗證裡」的三項
    //     （PositionIndex==陣列索引、主環必佔前 N 連續 index 且成單一封閉迴圈、
    //     支線每格僅一出口），預期在圖結構重構後被移出規則驗證、改由佈局工具把關。
    //
    // 本檔案的目的不是判斷這些約束「該不該」繼續存在，而是先把現狀的每一條邊界鎖住，
    // 讓 ADR-0002 的拆分工作有明確、可驗證的起點。
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

        // 版面限制（ADR-0002：「PositionIndex == 陣列索引」）。
        // 通用圖結構下 node 的身分應該只靠 id，不需要 PositionIndex 與陣列順序一致；
        // 這條約束預期在 ADR-0002 完成後被移除或改為佈局工具檢查。
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

        // 版面限制（ADR-0002：「主環必佔前 N 個連續 index」）。
        // 這裡讓一個 IsMainRoute=false 的分支格排在主環格「中間」，
        // 破壞「主環格必須佔據陣列前 mainRouteTileCount 格」的假設。
        [Test]
        public void Rejects_MainRouteTilesNotOccupyingContiguousFrontIndexes()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, isMainRoute: true),
                Tile("branch", BoardTileType.Rest, 1, next: new[] { "rest-main" }, isMainRoute: false, rejoinsAt: "rest-main"),
                Tile("rest-main", BoardTileType.Rest, 2, isMainRoute: true)
            }));
            Assert.That(ex.Message, Does.Contain("first contiguous position indexes"));
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

        // 規則：非主環（支線）格必須宣告 RejoinsAtId。
        [Test]
        public void Rejects_BranchTileWithoutDeclaredRejoinId()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: new[] { "rest" }, isMainRoute: false, rejoinsAt: null)
            }));
            Assert.That(ex.Message, Does.Contain("must declare its rejoin id"));
        }

        // 版面限制（ADR-0002：「主環必佔前 N 個連續 index 且成單一封閉迴圈」）。
        // 這裡主環格的陣列順序與 IsMainRoute 旗標都正確，只是其中一條邊被刻意接錯，
        // 破壞「主環必須形成單一封閉迴圈」的假設。
        [Test]
        public void Rejects_MainRouteThatDoesNotFormASingleClosedLoop()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-2" }), // 應指向 rest-1，刻意跳接 rest-2。
                Tile("rest-1", BoardTileType.Rest, 1),
                Tile("rest-2", BoardTileType.Rest, 2)
            }));
            Assert.That(ex.Message, Does.Contain("does not form one closed loop"));
        }

        // 規則：分支宣告的 RejoinsAtId 必須指向一個主環格。
        [Test]
        public void Rejects_BranchRejoinIdThatIsNotAMainRouteTile()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-1", "branch" }),
                Tile("rest-1", BoardTileType.Rest, 1),
                Tile("branch", BoardTileType.Rest, 2, next: new[] { "rest-1" }, isMainRoute: false, rejoinsAt: "branch")
            }));
            Assert.That(ex.Message, Does.Contain("must name a main route tile"));
        }

        // 規則：分支必須實際能透過其出邊走到它宣告的匯合點（BFS 可達性）。
        // 這裡讓兩個分支格互相指向對方，形成無法逃脫的小環，永遠走不到宣告的匯合點。
        [Test]
        public void Rejects_BranchThatCannotReachItsDeclaredRejoinPoint()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-1", "branch-a" }),
                Tile("rest-1", BoardTileType.Rest, 1),
                Tile("branch-a", BoardTileType.Rest, 2, next: new[] { "branch-b" }, isMainRoute: false, rejoinsAt: "start"),
                Tile("branch-b", BoardTileType.Rest, 3, next: new[] { "branch-a" }, isMainRoute: false, rejoinsAt: "start")
            }));
            Assert.That(ex.Message, Does.Contain("cannot reach its declared rejoin"));
        }

        // 版面限制（ADR-0002：「支線每格僅一出口」，Board.cs:277）。
        // ADR-0002 明確指出這其實是視覺佈局演算法（AssignMapPositions 的 Bezier 中繼點計算）
        // 的限制，混進了規則驗證：分支格若有多個出口，BFS 可達性驗證可以通過，
        // 但佈局演算法會直接丟例外，而不是把「多出口分支」視為合法的圖結構。
        [Test]
        public void Rejects_BranchWaypointWithMultipleOutgoingRoutes()
        {
            var ex = Assert.Throws<ArgumentException>(() => new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0, next: new[] { "rest-a", "fork" }),
                Tile("rest-a", BoardTileType.Rest, 1),
                Tile("rest-b", BoardTileType.Rest, 2),
                Tile("fork", BoardTileType.Rest, 3, next: new[] { "rest-a", "rest-b" }, isMainRoute: false, rejoinsAt: "rest-a")
            }));
            Assert.That(ex.Message, Does.Contain("must have one onward route"));
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
    }
}
