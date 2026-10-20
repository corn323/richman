using System;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T1 黃金重播快照測試（docs/AUDIT.md §7.2 T1，Phase 1a 保護測試網）。
    //
    // 目的：在 Phase 1 重構動工前，把「現行規則引擎對固定 seed 跑完整局」的完整狀態序列
    // 鎖成雜湊快照。只要規則、機率分布、回合推進邏輯等在重構中「不小心」被改變，
    // 此測試就會在 CI 失敗。
    //
    // 若重構「刻意」改變了遊戲行為（例如 docs/AUDIT.md §8 決策記錄中已授權的
    // B1 確定性破口、交易無對方同意、破產地產未重設升級等級等修復），
    // 更新下方的雜湊常數「必須」是一個顯式動作：
    //   1. 先確認這個行為變更是故意的、且已被記錄在 ADR 或決策記錄中核准；
    //   2. 在 PR 說明中明確列出「為什麼黃金雜湊改變了」與對應的行為差異；
    //   3. 絕對不要在不理解原因的情況下，直接把測試失敗訊息印出的新雜湊貼上了事——
    //      那樣會讓本測試網徹底喪失保護意義（docs/AUDIT.md Blocker B3）。
    //
    // 快照內容涵蓋：CurrentPlayerId、TurnNumber、Phase、PendingAction、
    // 每位玩家的 Money/CurrentTileIndex/Status、每塊地產的 OwnerId/UpgradeLevel。
    // 刻意不包含 LastEventMessage / LastDrawnCard 等顯示用欄位，
    // 因為這些欄位本身就是 Phase 1 預計汰換的對象（ADR-0003）。
    public sealed class GoldenReplayTests
    {
        private const int MaxCommandsPerGame = 20000;

        private const string Seed928371GoldenHash = "3F8E88C3B0525E4980E7B792ED924ECA5CEADB76337CC7F726CDF5DA702E9BA6";
        private const string Seed7GoldenHash = "82D50DE071027DD30CA35CA983DD31A5C05B97474EDFDA6745039CF3D57708E4";
        private const string Seed42GoldenHash = "835906A1CA31CD625E2680B9CFD1C0463C9D67A4CBE9F47FB71227F57127C754";

        [TestCase(928371, Seed928371GoldenHash)]
        [TestCase(7, Seed7GoldenHash)]
        [TestCase(42, Seed42GoldenHash)]
        public void FullGameStateSequenceMatchesGoldenHash(int seed, string expectedHash)
        {
            var session = PrototypeGameFactory.CreateFourPlayerSession(seed);

            var actualHash = PlayToFinishAndHash(session, out var commandCount);

            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished),
                "Seed " + seed + ": the bot-driven game did not finish within " + MaxCommandsPerGame + " commands.");
            Assert.That(actualHash, Is.EqualTo(expectedHash),
                "Seed " + seed + " produced " + commandCount + " commands with hash " + actualHash +
                ", which differs from the recorded golden hash. See the file header before updating the constant.");
        }

        /// <summary>
        /// Drives a session to completion using <see cref="SimpleBotStrategy"/>, appending a
        /// structured state snapshot after every successfully executed command, then returns the
        /// SHA-256 hash of the concatenated snapshot text. Shared by T1 and T2.
        /// </summary>
        internal static string PlayToFinishAndHash(GameSession session, out int commandCount)
        {
            var strategy = new SimpleBotStrategy();
            var sb = new StringBuilder();
            commandCount = 0;
            while (session.State.Phase != TurnPhase.Finished && commandCount < MaxCommandsPerGame)
            {
                var playerId = session.State.CurrentPlayerId;
                var command = strategy.ChooseCommand(session.State, playerId);
                var result = session.Execute(command);
                if (!result.Succeeded)
                    throw new InvalidOperationException("Bot produced a rejected command: " + result.ErrorCode);
                AppendSnapshot(sb, session.State);
                commandCount++;
            }
            return ComputeSha256(sb.ToString());
        }

        /// <summary>
        /// Returns a single-line structured snapshot of the given state, used for exact
        /// command-by-command comparisons in <see cref="DeterminismTests"/>.
        /// </summary>
        internal static string SnapshotState(GameState state)
        {
            var sb = new StringBuilder();
            AppendSnapshot(sb, state);
            return sb.ToString();
        }

        private static void AppendSnapshot(StringBuilder sb, GameState state)
        {
            sb.Append(state.CurrentPlayerId).Append('|')
              .Append(state.TurnNumber).Append('|')
              .Append(state.Phase).Append('|')
              .Append(state.PendingAction).Append('|');
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                sb.Append(player.Id).Append(':').Append(player.Money).Append(':')
                  .Append(player.CurrentTileIndex).Append(':').Append(player.Status).Append(';');
            }
            for (var i = 0; i < state.Properties.Count; i++)
            {
                var property = state.Properties[i];
                sb.Append(property.PropertyId).Append(':').Append(property.OwnerId).Append(':')
                  .Append(property.UpgradeLevel).Append(';');
            }
            sb.Append('\n');
        }

        internal static string ComputeSha256(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return Convert.ToHexString(bytes);
        }
    }
}
