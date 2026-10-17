using System;
using System.Collections.Generic;

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
        InvalidTrade
    }

    public sealed class CommandResult
    {
        private CommandResult(bool succeeded, CommandErrorCode errorCode, string errorMessage, DiceResult diceResult, int? fromTileIndex, int? toTileIndex, IReadOnlyList<int> pathTileIndices)
        {
            Succeeded = succeeded;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            DiceResult = diceResult;
            FromTileIndex = fromTileIndex;
            ToTileIndex = toTileIndex;
            PathTileIndices = pathTileIndices ?? Array.Empty<int>();
        }

        public bool Succeeded { get; }
        public CommandErrorCode ErrorCode { get; }
        public string ErrorMessage { get; }
        public DiceResult DiceResult { get; }
        public int? FromTileIndex { get; }
        public int? ToTileIndex { get; }
        public IReadOnlyList<int> PathTileIndices { get; }

        internal static CommandResult Success(DiceResult diceResult = null, int? fromTileIndex = null, int? toTileIndex = null, IReadOnlyList<int> pathTileIndices = null)
        {
            return new CommandResult(true, CommandErrorCode.None, string.Empty, diceResult, fromTileIndex, toTileIndex, pathTileIndices);
        }

        internal static CommandResult Failure(CommandErrorCode errorCode, string errorMessage)
        {
            return new CommandResult(false, errorCode, errorMessage, null, null, null, null);
        }
    }
}
