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
        public UpgradePropertyCommand(int playerId) : base(playerId) { }
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
        InvalidRouteChoice
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
