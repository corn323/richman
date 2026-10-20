using System;
using System.Collections.Generic;
using Richman.Core;

namespace Richman.Core.Events
{
    // ADR-0003 決策1/2：Core 的事件模型。
    //
    // 事件只攜帶結構化資料（id、數字、enum）——不帶顯示字串。顯示文字（哪種語言、
    // 怎麼組句）一律是 Presentation 層的職責，Core 不應該知道任何字串怎麼組。
    // 這與既有的 LastEventMessage（標注 TODO(Phase4) 待退役）形成對比：LastEventMessage
    // 暫時保留是為了不破壞既有測試與 HUD，但新的程式碼不應該再依賴它。
    //
    // Sequence 是整個 GameSession 生命週期內單調遞增的事件序號（不會因為回合、對局重置），
    // 可用來對一次 Execute() 呼叫或整場對局重播排序。TurnNumber 記錄事件發生當下的
    // GameState.TurnNumber（事件建立當下的快照值，之後 TurnNumber 若改變不會回溯更新舊事件）。
    public abstract class GameEvent
    {
        public int Sequence { get; internal set; }
        public int TurnNumber { get; internal set; }
    }

    // 8 階段回合狀態機（ADR-0003 決策4）的事件層敘事。對外的 GameState.Phase 仍然只有
    // AwaitingRoll/AwaitingAction/AwaitingEndTurn/Finished 四種「等待輸入狀態」投影
    // （黃金雜湊依賴它，語意與時機不可變）；TurnStage 是細顆粒度的內部敘事，供事件消費端
    // （Presentation、重播、未來的 Effect/Modifier 掛鉤點）觀察「現在正發生什麼」。
    public enum TurnStage
    {
        TurnStart,
        PreRoll,
        Roll,
        Move,
        Land,
        ResolveTile,
        PostAction,
        TurnEnd
    }

    public enum DiceRollPurpose
    {
        Movement,
        PrisonEscape,
        CourtJudgment
    }

    public enum MoneyChangeReason
    {
        Unspecified,
        Salary,
        PropertyPurchase,
        PropertyUpgrade,
        RentPaid,
        RentReceived,
        Tax,
        Bonus,
        CardEffect,
        CourtFine,
        PrisonBail,
        DistrictReward,
        TradeOffered,
        TradeReceived,
        Bankruptcy
    }

    public enum TeleportReason
    {
        Airport
    }

    public enum DetentionKind
    {
        Hospital,
        Prison
    }

    public enum TurnSkipReason
    {
        Hospital
    }

    public enum CourtRuling
    {
        Acquittal,
        Prison,
        Fine
    }

    public sealed class TurnStarted : GameEvent
    {
        public TurnStarted(int turnNumber, int playerId) { StartedTurnNumber = turnNumber; PlayerId = playerId; }
        public int StartedTurnNumber { get; }
        public int PlayerId { get; }
    }

    public sealed class PhaseChanged : GameEvent
    {
        public PhaseChanged(TurnStage stage) { Stage = stage; }
        public TurnStage Stage { get; }
    }

    public sealed class DiceRolled : GameEvent
    {
        public DiceRolled(int playerId, int first, int second, DiceRollPurpose purpose)
        { PlayerId = playerId; First = first; Second = second; Purpose = purpose; }
        public int PlayerId { get; }
        public int First { get; }
        public int Second { get; }
        public int Total => First + Second;
        public DiceRollPurpose Purpose { get; }
    }

    public sealed class PawnStepped : GameEvent
    {
        public PawnStepped(int playerId, int fromTileIndex, int toTileIndex, bool passedStart)
        { PlayerId = playerId; FromTileIndex = fromTileIndex; ToTileIndex = toTileIndex; PassedStart = passedStart; }
        public int PlayerId { get; }
        public int FromTileIndex { get; }
        public int ToTileIndex { get; }
        public bool PassedStart { get; }
    }

    public sealed class PawnLanded : GameEvent
    {
        public PawnLanded(int playerId, int tileIndex, string tileId, BoardTileType tileType)
        { PlayerId = playerId; TileIndex = tileIndex; TileId = tileId; TileType = tileType; }
        public int PlayerId { get; }
        public int TileIndex { get; }
        public string TileId { get; }
        public BoardTileType TileType { get; }
    }

    public sealed class SalaryCollected : GameEvent
    {
        public SalaryCollected(int playerId, int amount) { PlayerId = playerId; Amount = amount; }
        public int PlayerId { get; }
        public int Amount { get; }
    }

    public sealed class PropertyPurchased : GameEvent
    {
        public PropertyPurchased(int playerId, string propertyId, int amount)
        { PlayerId = playerId; PropertyId = propertyId; Amount = amount; }
        public int PlayerId { get; }
        public string PropertyId { get; }
        public int Amount { get; }
    }

    // Breakdown 欄位預留給未來 Modifier 管線（ADR-0003 決策6）：BaseRent 是未套用任何
    // modifier 前的租金，DistrictBonusAmount 是現行「集齊區域 +N% 租金」規則套用後的差額。
    // Phase 3 導入完整 Modifier 管線後，這裡應該改成完整的 modifier 來源清單。
    public sealed class RentPaid : GameEvent
    {
        public RentPaid(int payerId, int receiverId, string propertyId, int amount, int baseRent, int districtBonusAmount)
        { PayerId = payerId; ReceiverId = receiverId; PropertyId = propertyId; Amount = amount; BaseRent = baseRent; DistrictBonusAmount = districtBonusAmount; }
        public int PayerId { get; }
        public int ReceiverId { get; }
        public string PropertyId { get; }
        public int Amount { get; }
        public int BaseRent { get; }
        public int DistrictBonusAmount { get; }
    }

    public sealed class PropertyUpgraded : GameEvent
    {
        public PropertyUpgraded(int playerId, string propertyId, int newLevel)
        { PlayerId = playerId; PropertyId = propertyId; NewLevel = newLevel; }
        public int PlayerId { get; }
        public string PropertyId { get; }
        public int NewLevel { get; }
    }

    public sealed class MoneyChanged : GameEvent
    {
        public MoneyChanged(int playerId, int delta, int newBalance, MoneyChangeReason reason)
        { PlayerId = playerId; Delta = delta; NewBalance = newBalance; Reason = reason; }
        public int PlayerId { get; }
        public int Delta { get; }
        public int NewBalance { get; }
        public MoneyChangeReason Reason { get; }
    }

    public sealed class CardDrawn : GameEvent
    {
        public CardDrawn(int playerId, string cardId, string deckId) { PlayerId = playerId; CardId = cardId; DeckId = deckId; }
        public int PlayerId { get; }
        public string CardId { get; }
        public string DeckId { get; }
    }

    public sealed class CardEffectApplied : GameEvent
    {
        public CardEffectApplied(int playerId, string cardId, MapCardEffect effect, int amount)
        { PlayerId = playerId; CardId = cardId; Effect = effect; Amount = amount; }
        public int PlayerId { get; }
        public string CardId { get; }
        public MapCardEffect Effect { get; }
        public int Amount { get; }
    }

    public sealed class PlayerTeleported : GameEvent
    {
        public PlayerTeleported(int playerId, int fromTileIndex, int toTileIndex, TeleportReason reason)
        { PlayerId = playerId; FromTileIndex = fromTileIndex; ToTileIndex = toTileIndex; Reason = reason; }
        public int PlayerId { get; }
        public int FromTileIndex { get; }
        public int ToTileIndex { get; }
        public TeleportReason Reason { get; }
    }

    public sealed class PlayerDetained : GameEvent
    {
        public PlayerDetained(int playerId, DetentionKind kind, int turns) { PlayerId = playerId; Kind = kind; Turns = turns; }
        public int PlayerId { get; }
        public DetentionKind Kind { get; }
        public int Turns { get; }
    }

    public sealed class TurnSkipped : GameEvent
    {
        public TurnSkipped(int playerId, TurnSkipReason reason, int turnsRemaining)
        { PlayerId = playerId; Reason = reason; TurnsRemaining = turnsRemaining; }
        public int PlayerId { get; }
        public TurnSkipReason Reason { get; }
        public int TurnsRemaining { get; }
    }

    public sealed class PrisonEscapeAttempted : GameEvent
    {
        public PrisonEscapeAttempted(int playerId, bool success, int attemptsRemaining)
        { PlayerId = playerId; Success = success; AttemptsRemaining = attemptsRemaining; }
        public int PlayerId { get; }
        public bool Success { get; }
        public int AttemptsRemaining { get; }
    }

    public sealed class CourtRuled : GameEvent
    {
        public CourtRuled(int playerId, int diceTotal, CourtRuling ruling, int amount)
        { PlayerId = playerId; DiceTotal = diceTotal; Ruling = ruling; Amount = amount; }
        public int PlayerId { get; }
        public int DiceTotal { get; }
        public CourtRuling Ruling { get; }
        public int Amount { get; }
    }

    public sealed class TradeExecuted : GameEvent
    {
        public TradeExecuted(int initiatorId, int targetId, int offeredMoney, int requestedMoney, string offeredPropertyId, string requestedPropertyId)
        {
            InitiatorId = initiatorId; TargetId = targetId; OfferedMoney = offeredMoney; RequestedMoney = requestedMoney;
            OfferedPropertyId = offeredPropertyId ?? string.Empty; RequestedPropertyId = requestedPropertyId ?? string.Empty;
        }
        public int InitiatorId { get; }
        public int TargetId { get; }
        public int OfferedMoney { get; }
        public int RequestedMoney { get; }
        public string OfferedPropertyId { get; }
        public string RequestedPropertyId { get; }
    }

    public sealed class DecisionRequested : GameEvent
    {
        public DecisionRequested(DecisionKind kind, int playerId, IReadOnlyList<string> optionIds, string contextTileId)
        {
            Kind = kind; PlayerId = playerId; OptionIds = optionIds ?? Array.Empty<string>(); ContextTileId = contextTileId ?? string.Empty;
        }
        public DecisionKind Kind { get; }
        public int PlayerId { get; }
        public IReadOnlyList<string> OptionIds { get; }
        public string ContextTileId { get; }
    }

    public sealed class DecisionResolved : GameEvent
    {
        public DecisionResolved(DecisionKind kind, int playerId, string optionId)
        { Kind = kind; PlayerId = playerId; OptionId = optionId ?? string.Empty; }
        public DecisionKind Kind { get; }
        public int PlayerId { get; }
        public string OptionId { get; }
    }

    public sealed class PlayerBankrupt : GameEvent
    {
        public PlayerBankrupt(int playerId, int? creditorId) { PlayerId = playerId; CreditorId = creditorId; }
        public int PlayerId { get; }
        public int? CreditorId { get; }
    }

    public sealed class GameFinished : GameEvent
    {
        public GameFinished(int winnerId, int finishedTurnNumber, VictoryReason reason)
        { WinnerId = winnerId; FinishedTurnNumber = finishedTurnNumber; Reason = reason; }
        public int WinnerId { get; }
        public int FinishedTurnNumber { get; }
        public VictoryReason Reason { get; }
    }

    public sealed class TurnEnded : GameEvent
    {
        public TurnEnded(int playerId, int turnNumber) { PlayerId = playerId; EndedTurnNumber = turnNumber; }
        public int PlayerId { get; }
        public int EndedTurnNumber { get; }
    }
}
