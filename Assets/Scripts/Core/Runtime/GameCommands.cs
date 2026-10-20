using System;
using System.Collections.Generic;
using Richman.Core.Events;

namespace Richman.Core
{
    public abstract class GameCommand
    {
        protected GameCommand(int playerId)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));
            PlayerId = playerId;
        }

        public int PlayerId { get; }
    }

    public sealed class RollDiceCommand : GameCommand
    {
        public RollDiceCommand(int playerId) : base(playerId) { }
    }

    public sealed class BuyPropertyCommand : GameCommand
    {
        public BuyPropertyCommand(int playerId) : base(playerId) { }
    }

    public sealed class UpgradePropertyCommand : GameCommand
    {
        public UpgradePropertyCommand(int playerId) : this(playerId, string.Empty) { }
        public UpgradePropertyCommand(int playerId, string propertyId) : base(playerId)
        {
            PropertyId = propertyId ?? string.Empty;
        }

        public string PropertyId { get; }
    }

    public sealed class ChooseRouteCommand : GameCommand
    {
        public ChooseRouteCommand(int playerId, string nextTileId) : base(playerId)
        {
            if (string.IsNullOrWhiteSpace(nextTileId)) throw new ArgumentException("A next tile id is required.", nameof(nextTileId));
            NextTileId = nextTileId;
        }

        public string NextTileId { get; }
    }

    // ADR-0003 決策3：統一的決策解析命令。BuyPropertyCommand/UpgradePropertyCommand/
    // ChooseRouteCommand 保留為語法糖（行為不變），內部一律路由到同一套「驗證
    // GameState.PendingDecision、比對 optionId、執行對應核心邏輯」的路徑。
    public sealed class ResolveDecisionCommand : GameCommand
    {
        public ResolveDecisionCommand(int playerId, string optionId) : base(playerId)
        {
            if (string.IsNullOrWhiteSpace(optionId)) throw new ArgumentException("An option id is required.", nameof(optionId));
            OptionId = optionId;
        }

        public string OptionId { get; }
    }

    public sealed class EndTurnCommand : GameCommand
    {
        public EndTurnCommand(int playerId) : base(playerId) { }
    }

    public sealed class TradeCommand : GameCommand
    {
        public TradeCommand(int playerId, int targetPlayerId, int offeredMoney, int requestedMoney,
            string offeredPropertyId = null, string requestedPropertyId = null) : base(playerId)
        {
            if (targetPlayerId <= 0) throw new ArgumentOutOfRangeException(nameof(targetPlayerId));
            if (offeredMoney < 0) throw new ArgumentOutOfRangeException(nameof(offeredMoney));
            if (requestedMoney < 0) throw new ArgumentOutOfRangeException(nameof(requestedMoney));
            TargetPlayerId = targetPlayerId;
            OfferedMoney = offeredMoney;
            RequestedMoney = requestedMoney;
            OfferedPropertyId = offeredPropertyId ?? string.Empty;
            RequestedPropertyId = requestedPropertyId ?? string.Empty;
        }

        public int TargetPlayerId { get; }
        public int OfferedMoney { get; }
        public int RequestedMoney { get; }
        public string OfferedPropertyId { get; }
        public string RequestedPropertyId { get; }
    }

    public enum CommandErrorCode
    {
        None,
        InvalidCommand,
        GameFinished,
        PlayerNotFound,
        WrongTurn,
        InvalidPhase,
        PlayerEliminated,
        PropertyUnavailable,
        InsufficientFunds,
        PropertyAlreadyMaxLevel,
        InvalidRouteChoice,
        InvalidTrade,
        // ADR-0003 決策3：ResolveDecisionCommand 的 optionId 與目前 GameState.PendingDecision
        // 不符時使用。純新增，不影響既有錯誤碼的數值或既有分支。
        InvalidDecisionOption
    }

    public sealed class CommandResult
    {
        private static readonly IReadOnlyList<GameEvent> NoEvents = Array.Empty<GameEvent>();

        private CommandResult(bool succeeded, CommandErrorCode errorCode, string errorMessage, DiceResult diceResult, int? fromTileIndex, int? toTileIndex, IReadOnlyList<int> pathTileIndices, IReadOnlyList<GameEvent> events)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            DiceResult = diceResult;
            FromTileIndex = fromTileIndex;
            ToTileIndex = toTileIndex;
            PathTileIndices = pathTileIndices ?? Array.Empty<int>();
            Events = events ?? NoEvents;
        }

        public bool Succeeded { get; }
        public CommandErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }
        public DiceResult DiceResult { get; }
        public int? FromTileIndex { get; }
        public int? ToTileIndex { get; }
        public IReadOnlyList<int> PathTileIndices { get; }

        // ADR-0003 決策1：CommandResult 作為過渡期相容層保留（最終去留待 Phase 4 後再議），
        // 但現在同時攜帶本次 Execute() 產生的有序事件序列，讓呼叫端不必再從狀態差異「猜」
        // 發生了什麼。失敗的命令一定沒有任何事件（Core 在驗證失敗時直接回傳，不會有任何
        // 狀態變化發生在驗證之後）。
        public IReadOnlyList<GameEvent> Events { get; }

        internal static CommandResult Success(DiceResult diceResult = null, int? fromTileIndex = null, int? toTileIndex = null, IReadOnlyList<int> pathTileIndices = null, IReadOnlyList<GameEvent> events = null)
        {
            return new CommandResult(true, CommandErrorCode.None, string.Empty, diceResult, fromTileIndex, toTileIndex, pathTileIndices, events);
        }

        internal static CommandResult Failure(CommandErrorCode errorCode, string errorMessage)
        {
            return new CommandResult(false, errorCode, errorMessage, null, null, null, null, null);
        }

        // GameSession.Execute 用這個把本次呼叫期間累積的事件掛回一個已經建好的成功結果，
        // 不需要每個 Execute* 分支各自在建構時手動帶入事件清單。
        internal CommandResult WithEvents(IReadOnlyList<GameEvent> events)
        {
            return new CommandResult(Succeeded, ErrorCode, ErrorMessage, DiceResult, FromTileIndex, ToTileIndex, PathTileIndices, events);
        }
    }
}
