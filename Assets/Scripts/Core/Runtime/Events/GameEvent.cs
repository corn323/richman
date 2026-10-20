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

    // Phase 1d-2（條件邊）：移動解析在某一格發現「目前開著的邊」數量是 0（所有出邊都
    // 因為 RequiredFlag 未滿足而關閉）時發出。玩家停在這一格（TileIndex/TileId 就是
    // 玩家停下來的位置，不是某個「原本想去但去不了」的目的地），本次移動的剩餘步數
    // 直接作廢，接著照常對這一格跑 ResolveLanding（跟步數正常走完時一樣）。
    public sealed class MovementBlocked : GameEvent
    {
        public MovementBlocked(int playerId, int tileIndex, string tileId)
        { PlayerId = playerId; TileIndex = tileIndex; TileId = tileId; }
        public int PlayerId { get; }
        public int TileIndex { get; }
        public string TileId { get; }
    }

    // Phase 1d-2（條件邊）：GameSession.SetBoardFlag/ClearBoardFlag 實際改變了
    // GameState.BoardFlags（而不是對一個已經是該狀態的 flag 重複呼叫）時發出。
    // IsSet=true 代表這個 flag 剛被加入（開啟相關條件邊），false 代表剛被移除（關閉）。
    public sealed class BoardFlagChanged : GameEvent
    {
        public BoardFlagChanged(string flag, bool isSet) { Flag = flag; IsSet = isSet; }
        public string Flag { get; }
        public bool IsSet { get; }
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

    // Phase 1e（ADR-0003 決策8 / SEC-CMD-02,03，授權行為變更）：兩階段交易。
    public enum TradeProposalExpiryReason
    {
        // ExecuteRespondTrade(accept:true) 時，雙方資產快照跟提案當下對不上。
        SnapshotStale,
        // 發起方的回合結束（AdvanceTurn）了，提案還沒被回應。
        TurnEnded
    }

    public sealed class TradeProposed : GameEvent
    {
        public TradeProposed(int proposalId, int initiatorId, int targetId, int offeredMoney, int requestedMoney,
            string offeredPropertyId, string requestedPropertyId)
        {
            ProposalId = proposalId; InitiatorId = initiatorId; TargetId = targetId;
            OfferedMoney = offeredMoney; RequestedMoney = requestedMoney;
            OfferedPropertyId = offeredPropertyId ?? string.Empty; RequestedPropertyId = requestedPropertyId ?? string.Empty;
        }
        public int ProposalId { get; }
        public int InitiatorId { get; }
        public int TargetId { get; }
        public int OfferedMoney { get; }
        public int RequestedMoney { get; }
        public string OfferedPropertyId { get; }
        public string RequestedPropertyId { get; }
    }

    public sealed class TradeRejected : GameEvent
    {
        public TradeRejected(int proposalId, int initiatorId, int targetId)
        { ProposalId = proposalId; InitiatorId = initiatorId; TargetId = targetId; }
        public int ProposalId { get; }
        public int InitiatorId { get; }
        public int TargetId { get; }
    }

    public sealed class TradeProposalExpired : GameEvent
    {
        public TradeProposalExpired(int proposalId, int initiatorId, int targetId, TradeProposalExpiryReason reason)
        { ProposalId = proposalId; InitiatorId = initiatorId; TargetId = targetId; Reason = reason; }
        public int ProposalId { get; }
        public int InitiatorId { get; }
        public int TargetId { get; }
        public TradeProposalExpiryReason Reason { get; }
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

    // TransferredMoney：破產時實際被沒收的金額（通常是玩家當下的全部現金，可能小於
    // 名義欠款，因為規則是「拿光現有的、不足就算了」，見 GameSession.PayToBank /
    // ResolveProperty 的 rent 短缺分支）。CreditorId 為 null 時代表這筆錢直接消失（繳給
    // 銀行，稅金/罰款/卡牌情境），非 null 時代表這筆錢已經轉給該玩家（租金短缺情境）。
    // TransferredPropertyIds：破產當下被查封（轉給 CreditorId，或 CreditorId 為 null 時
    // 歸還銀行）的地產 id 清單，依地產清單原始順序。
    public sealed class PlayerBankrupt : GameEvent
    {
        public PlayerBankrupt(int playerId, int? creditorId, int transferredMoney, IReadOnlyList<string> transferredPropertyIds)
        {
            PlayerId = playerId;
            CreditorId = creditorId;
            TransferredMoney = transferredMoney;
            TransferredPropertyIds = transferredPropertyIds ?? Array.Empty<string>();
        }
        public int PlayerId { get; }
        public int? CreditorId { get; }
        public int TransferredMoney { get; }
        public IReadOnlyList<string> TransferredPropertyIds { get; }
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
