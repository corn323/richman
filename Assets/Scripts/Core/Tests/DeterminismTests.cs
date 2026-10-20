using System.Collections.Generic;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T2 確定性測試（docs/AUDIT.md §7.2 T2，Phase 1a 保護測試網）。
    //
    // 涵蓋兩件事：
    //   1. 「正常」路徑：同 seed 經由 PrototypeGameFactory（顯式注入 SeededRandom）
    //      跑兩次完整對局，逐 command 比對完整狀態快照必須完全相同。
    //   2. B1 修復驗證（docs/AUDIT.md Blocker B1 / ADR-0003 決策 5，Phase 1b 已修復）：
    //      GameSession 建構子過去在未顯式注入 IGameRandom 時會 fallback 到
    //      new SeededRandom(Environment.TickCount)，導致卡牌洗牌／法院判決在「未注入
    //      random」時每次建局都不同。Phase 1b 已移除該 fallback，random 改為必填參數，
    //      不帶 random 呼叫建構子現在必須立即丟出 ArgumentNullException——
    //      本測試驗證的就是這件事，取代原本「鎖住現狀（兩次應該不同）」的版本。
    public sealed class DeterminismTests
    {
        private const int MaxCommandsPerGame = 20000;

        [TestCase(928371)]
        [TestCase(7)]
        [TestCase(42)]
        public void SameSeedProducesIdenticalStateSequenceCommandByCommand(int seed)
        {
            var sessionA = PrototypeGameFactory.CreateFourPlayerSession(seed);
            var sessionB = PrototypeGameFactory.CreateFourPlayerSession(seed);
            var strategy = new SimpleBotStrategy();
            var commandIndex = 0;

            while (sessionA.State.Phase != TurnPhase.Finished && commandIndex < MaxCommandsPerGame)
            {
                Assert.That(sessionB.State.Phase, Is.Not.EqualTo(TurnPhase.Finished),
                    "Command #" + commandIndex + ": session B finished before session A.");
                var playerId = sessionA.State.CurrentPlayerId;
                Assert.That(sessionB.State.CurrentPlayerId, Is.EqualTo(playerId),
                    "Command #" + commandIndex + ": current player diverged.");

                var commandA = strategy.ChooseCommand(sessionA.State, playerId);
                var commandB = strategy.ChooseCommand(sessionB.State, playerId);
                var resultA = sessionA.Execute(commandA);
                var resultB = sessionB.Execute(commandB);

                Assert.That(resultB.Succeeded, Is.EqualTo(resultA.Succeeded),
                    "Command #" + commandIndex + ": command success diverged (A=" + resultA.ErrorCode + ", B=" + resultB.ErrorCode + ").");
                Assert.That(GoldenReplayTests.SnapshotState(sessionB.State), Is.EqualTo(GoldenReplayTests.SnapshotState(sessionA.State)),
                    "Command #" + commandIndex + ": full state snapshot diverged between two sessions built from the same seed.");

                commandIndex++;
            }

            Assert.That(sessionA.State.Phase, Is.EqualTo(TurnPhase.Finished), "Session A did not finish within " + MaxCommandsPerGame + " commands.");
            Assert.That(sessionB.State.Phase, Is.EqualTo(TurnPhase.Finished), "Session B did not finish within " + MaxCommandsPerGame + " commands.");
        }

        [Test]
        public void FixedB1_ConstructorRequiresRandomAndThrowsArgumentNullExceptionWhenOmitted()
        {
            var mode = GameModeConfig.ClassicBankruptcyFourPlayer();
            var players = BuildPlayers(mode.MaxPlayers);
            const int fixedDiceSeed = 55443322;

            var ex = Assert.Throws<System.ArgumentNullException>(() =>
                new GameSession(mode, PrototypeBoardFactory.Create(), players, new SeededDiceRoller(fixedDiceSeed), null));

            Assert.That(ex.ParamName, Is.EqualTo("random"),
                "B1 修復（GameSession.cs，ADR-0003 決策5）：random 現在是必填參數，不再 fallback 到 " +
                "Environment.TickCount。呼叫端必須在 composition root 明確決定並記錄 seed 的來源。");
        }

        private static List<PlayerSetup> BuildPlayers(int count)
        {
            var players = new List<PlayerSetup>(count);
            for (var i = 0; i < count; i++) players.Add(new PlayerSetup(i + 1, "Player " + (i + 1)));
            return players;
        }
    }
}
