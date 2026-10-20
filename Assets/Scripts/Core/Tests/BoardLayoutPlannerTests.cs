using NUnit.Framework;

namespace Richman.Core.Tests
{
    // Phase 1d-3（ADR-0002 §3）：BoardLayoutPlanner.AssignMapPositions 不再是 BoardDefinition
    // 建構流程的一部分——規則路徑完全不讀取 MapPosition。本測試鎖住這個承諾的可觀察結果：
    // 從頭到尾不呼叫 BoardLayoutPlanner，一整場由 Bot 驅動的完整對局（跟 GoldenReplayTests
    // 用的是同一個 48 格世界地圖）應該完全照常跑完，而且每一格的 MapPosition 全程維持
    // BoardMapPosition 的預設值（全 0）——證明佈局計算真的只是「有人要畫面才去算」的
    // 旁路，不是遊戲規則的隱性依賴。
    public sealed class BoardLayoutPlannerTests
    {
        [Test]
        public void GameplayWorksCompletelyWithoutEverCallingBoardLayoutPlanner()
        {
            var session = PrototypeGameFactory.CreateFourPlayerSession(928371);
            AssertAllTilesHaveDefaultMapPosition(session.Board);

            var hash = GoldenReplayTests.PlayToFinishAndHash(session, out var commandCount);

            Assert.That(hash, Is.Not.Null.And.Not.Empty);
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished),
                "The bot-driven game did not finish within the command budget (" + commandCount + " commands executed).");
            Assert.That(session.State.Outcome, Is.Not.Null);
            AssertAllTilesHaveDefaultMapPosition(session.Board);
        }

        private static void AssertAllTilesHaveDefaultMapPosition(BoardDefinition board)
        {
            for (var i = 0; i < board.TileCount; i++)
            {
                var position = board.GetTileAt(i).MapPosition;
                Assert.That(position.X, Is.EqualTo(0f));
                Assert.That(position.Y, Is.EqualTo(0f));
                Assert.That(position.Z, Is.EqualTo(0f));
                Assert.That(position.RotationDegrees, Is.EqualTo(0f));
            }
        }
    }
}
