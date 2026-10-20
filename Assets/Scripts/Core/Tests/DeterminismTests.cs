using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T2 確定性測試（docs/AUDIT.md §7.2 T2，Phase 1a 保護測試網）。
    //
    // 涵蓋兩件事：
    //   1. 「正常」路徑：同 seed 經由 PrototypeGameFactory（顯式注入 SeededRandom）
    //      跑兩次完整對局，逐 command 比對完整狀態快照必須完全相同。
    //   2. 「已知問題」路徑（KnownIssue，B1，docs/AUDIT.md Blocker B1 / ADR-0003 決策 5）：
    //      GameSession 建構子在未顯式注入 IGameRandom 時 fallback 到
    //      new SeededRandom(Environment.TickCount)（GameSession.cs:34），
    //      導致卡牌洗牌／法院判決在「未注入 random」時，每次建局都不同。
    //      docs/AUDIT.md §8 決策記錄已授權此問題於 Phase 1 修復（移除 TickCount fallback）。
    //      本測試故意鎖住「現狀」（兩次應該不同），讓這個 fallback 的存在本身被測試覆蓋。
    //      修復後此測試的前提會反過來：建構子若強制要求 random（不再允許 null），
    //      這個測試應該被改寫為「不帶 random 呼叫建構子應該丟 ArgumentNullException」，
    //      而不是繼續比較兩次卡序是否不同——請勿刪除本測試，請依此註解反轉斷言語意。
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
        public void KnownIssueB1_UnseededConstructorFallsBackToTickCountAndProducesDivergentGames()
        {
            var mode = GameModeConfig.ClassicBankruptcyFourPlayer();
            var players = BuildPlayers(mode.MaxPlayers);
            const int fixedDiceSeed = 55443322;

            // 兩個 session 使用相同的骰子 seed（隔離移動路徑的變因），
            // 但都「不」傳入 random，讓兩者各自落入 TickCount fallback。
            var sessionA = new GameSession(mode, PrototypeBoardFactory.Create(), players, new SeededDiceRoller(fixedDiceSeed));
            Thread.Sleep(50); // 盡量讓 Environment.TickCount 在兩次建構之間不同。
            var sessionB = new GameSession(mode, PrototypeBoardFactory.Create(), players, new SeededDiceRoller(fixedDiceSeed));

            var hashA = GoldenReplayTests.PlayToFinishAndHash(sessionA, out _);
            var hashB = GoldenReplayTests.PlayToFinishAndHash(sessionB, out _);

            Assert.That(hashB, Is.Not.EqualTo(hashA),
                "已知問題 B1（GameSession.cs:34）：未顯式注入 IGameRandom 時 fallback 到 Environment.TickCount，" +
                "應導致卡牌洗牌／法院判決在兩次建局之間不同，使最終狀態雜湊不同。" +
                "若此斷言意外失敗（兩次雜湊相同），常見原因是 Environment.TickCount 解析度不足導致巧合碰撞（已知的 flaky 風險，可重跑確認）；" +
                "但若 Phase 1b 已依 ADR-0003 決策 5 移除 TickCount fallback，則本測試的前提不再成立——" +
                "請依本檔頂部註解反轉斷言語意，不要直接刪除本測試。");
        }

        private static List<PlayerSetup> BuildPlayers(int count)
        {
            var players = new List<PlayerSetup>(count);
            for (var i = 0; i < count; i++) players.Add(new PlayerSetup(i + 1, "Player " + (i + 1)));
            return players;
        }
    }
}
