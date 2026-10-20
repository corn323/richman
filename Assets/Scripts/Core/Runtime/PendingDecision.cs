using System;
using System.Collections.Generic;

namespace Richman.Core
{
    // ADR-0003 決策3：統一 PendingDecision 型別，取代原本 PendingAction 枚舉＋散落欄位
    // （RouteChoiceTileIds 等）各自表意的設計。
    //
    // 刻意設計成純粹的投影（projection）：GameState.PendingDecision 由現有的
    // Phase/PendingAction/RouteChoiceTileIds/CurrentPlayerId/Board 狀態即時算出，
    // 不是額外儲存的狀態，因此不會引入新的確定性風險，也不會動到黃金雜湊依賴的
    // Phase/PendingAction 欄位本身。
    //
    // 範圍刻意限縮：只涵蓋「落地後、Phase==AwaitingAction 時的強制決策」
    // （ChooseRoute／BuyProperty／UpgradeProperty）。CanUpgradeOwnedProperty 觸發的
    // 「停在 Start 可任選一塊自有地產升級」屬於可選動作（optional action，不是互斥的單一
    // 待決事項，詳見 CharacterizationTests.cs 的
    // CurrentBehavior_LandingOnStartGrantsUpgradeRightToAnyOwnedPropertyNotJustTheCurrentTile），
    // 目前不建模為 PendingDecision，留待 Phase 1d/1e 視情況擴充。
    public enum DecisionKind
    {
        None,
        BuyProperty,
        UpgradeProperty,
        ChooseRoute
    }

    public sealed class DecisionOption
    {
        public DecisionOption(string optionId)
        {
            if (string.IsNullOrWhiteSpace(optionId)) throw new ArgumentException("An option id is required.", nameof(optionId));
            OptionId = optionId;
        }

        public string OptionId { get; }
    }

    public sealed class PendingDecision
    {
        private readonly IReadOnlyList<DecisionOption> _options;

        public PendingDecision(DecisionKind kind, int playerId, IReadOnlyList<DecisionOption> options, string contextTileId)
        {
            Kind = kind;
            PlayerId = playerId;
            _options = options ?? Array.Empty<DecisionOption>();
            ContextTileId = contextTileId ?? string.Empty;
        }

        public DecisionKind Kind { get; }
        public int PlayerId { get; }
        public IReadOnlyList<DecisionOption> Options => _options;
        public string ContextTileId { get; }

        public bool ContainsOption(string optionId)
        {
            for (var i = 0; i < _options.Count; i++)
            {
                if (string.Equals(_options[i].OptionId, optionId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
