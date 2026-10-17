using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Richman.Core
{
    public enum PlayerStatus
    {
        Active,
        Bankrupt,
        Winner
    }

    public enum TurnPhase
    {
        AwaitingRoll,
        AwaitingAction,
        AwaitingEndTurn,
        Finished
    }

    public enum PendingAction
    {
        None,
        BuyProperty,
        UpgradeProperty,
        ChooseRoute
    }

    public sealed class GameModeConfig
    {
        public GameModeConfig(
            string gameModeId,
            int minPlayers,
            int maxPlayers,
            int startingMoney,
            int startBonus,
            int taxAmount,
            int bonusAmount,
            int completeDistrictRentBonusPercent = 50,
            int completeDistrictReward = 120,
            int turnLimit = int.MaxValue)
        {
            if (string.IsNullOrWhiteSpace(gameModeId)) throw new ArgumentException("Game mode id is required.", nameof(gameModeId));
            if (minPlayers < 1) throw new ArgumentOutOfRangeException(nameof(minPlayers));
            if (maxPlayers < minPlayers) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (startBonus < 0) throw new ArgumentOutOfRangeException(nameof(startBonus));
            if (taxAmount < 0) throw new ArgumentOutOfRangeException(nameof(taxAmount));
            if (bonusAmount < 0) throw new ArgumentOutOfRangeException(nameof(bonusAmount));
            if (completeDistrictRentBonusPercent < 0) throw new ArgumentOutOfRangeException(nameof(completeDistrictRentBonusPercent));
            if (completeDistrictReward < 0) throw new ArgumentOutOfRangeException(nameof(completeDistrictReward));
            if (turnLimit < 1) throw new ArgumentOutOfRangeException(nameof(turnLimit));

            GameModeId = gameModeId;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            StartingMoney = startingMoney;
            StartBonus = startBonus;
            TaxAmount = taxAmount;
            BonusAmount = bonusAmount;
            CompleteDistrictRentBonusPercent = completeDistrictRentBonusPercent;
            CompleteDistrictReward = completeDistrictReward;
            TurnLimit = turnLimit;
        }

        public string GameModeId { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public int StartingMoney { get; }
        public int StartBonus { get; }
        public int TaxAmount { get; }
        public int BonusAmount { get; }
        public int CompleteDistrictRentBonusPercent { get; }
        public int CompleteDistrictReward { get; }
        public int TurnLimit { get; }

        public static GameModeConfig ClassicBankruptcyFourPlayer()
        {
            return new GameModeConfig("world-tour-classic", 4, 4, 1500, 200, 100, 75, 50, 120, 240);
        }
    }

    public sealed class PlayerSetup
    {
        public PlayerSetup(int id, string displayName)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Player name is required.", nameof(displayName));
            Id = id;
            DisplayName = displayName;
        }

        public int Id { get; }
        public string DisplayName { get; }
    }

    public sealed class PlayerState
    {
        internal PlayerState(PlayerSetup setup, int startingMoney, int startingTileIndex)
        {
            Id = setup.Id;
            DisplayName = setup.DisplayName;
            Money = startingMoney;
            CurrentTileIndex = startingTileIndex;
            Status = PlayerStatus.Active;
        }

        public int Id { get; }
        public string DisplayName { get; }
        public int Money { get; private set; }
        public int CurrentTileIndex { get; private set; }
        public PlayerStatus Status { get; private set; }
        public int DetainedTurnsRemaining { get; private set; }
        public string DetentionReason { get; private set; } = string.Empty;

        internal void MoveTo(int tileIndex)
        {
            CurrentTileIndex = tileIndex;
        }

        internal void AddMoney(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            checked { Money += amount; }
        }

        internal int TakeAllMoney()
        {
            var amount = Money;
            Money = 0;
            return amount;
        }

        internal void Spend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Money) throw new InvalidOperationException("Player does not have enough money.");
            Money -= amount;
        }

        internal void MarkBankrupt()
        {
            Status = PlayerStatus.Bankrupt;
        }

        internal void MarkWinner()
        {
            Status = PlayerStatus.Winner;
        }

        internal void Detain(string reason, int turns)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A detention reason is required.", nameof(reason));
            if (turns <= 0) throw new ArgumentOutOfRangeException(nameof(turns));
            DetentionReason = reason;
            DetainedTurnsRemaining = Math.Max(DetainedTurnsRemaining, turns);
        }

        internal string ServeDetainedTurn()
        {
            if (DetainedTurnsRemaining <= 0) return string.Empty;
            var reason = DetentionReason;
            DetainedTurnsRemaining--;
            if (DetainedTurnsRemaining == 0) DetentionReason = string.Empty;
            return reason;
        }
    }

    public sealed class PropertyState
    {
        internal PropertyState(BoardTileDefinition definition)
        {
            PropertyId = definition.Id;
            OwnerId = null;
            UpgradeLevel = 0;
        }

        public string PropertyId { get; }
        public int? OwnerId { get; private set; }
        public int UpgradeLevel { get; private set; }

        internal void SetOwner(int? ownerId)
        {
            OwnerId = ownerId;
        }

        internal void Upgrade()
        {
            UpgradeLevel++;
        }
    }

    public sealed class GameOutcome
    {
        internal GameOutcome(int winnerId, int turnNumber, VictoryReason reason)
        {
            WinnerId = winnerId;
            TurnNumber = turnNumber;
            Reason = reason;
        }

        public int WinnerId { get; }
        public int TurnNumber { get; }
        public VictoryReason Reason { get; }
    }

    public enum VictoryReason
    {
        LastPlayerStanding,
        HighestNetWorthAtTurnLimit
    }

    public sealed class GameState
    {
        private readonly List<PlayerState> _players;
        private readonly List<PropertyState> _properties;
        private readonly ReadOnlyCollection<PlayerState> _playersView;
        private readonly ReadOnlyCollection<PropertyState> _propertiesView;
        private readonly HashSet<string> _districtRewardsPaid = new HashSet<string>(StringComparer.Ordinal);
        private string[] _routeChoiceTileIds = Array.Empty<string>();
        private IReadOnlyList<string> _routeChoiceTileIdsView;

        internal GameState(GameModeConfig mode, BoardDefinition board, List<PlayerState> players, List<PropertyState> properties)
        {
            Mode = mode;
            Board = board;
            _players = players;
            _properties = properties;
            _playersView = _players.AsReadOnly();
            _propertiesView = _properties.AsReadOnly();
            _routeChoiceTileIdsView = Array.AsReadOnly(_routeChoiceTileIds);
            CurrentPlayerId = players[0].Id;
            TurnNumber = 1;
            Phase = TurnPhase.AwaitingRoll;
            PendingAction = PendingAction.None;
        }

        public GameModeConfig Mode { get; }
        public BoardDefinition Board { get; }
        public IReadOnlyList<PlayerState> Players => _playersView;
        public IReadOnlyList<PropertyState> Properties => _propertiesView;
        public int CurrentPlayerId { get; internal set; }
        public int TurnNumber { get; internal set; }
        public TurnPhase Phase { get; internal set; }
        public PendingAction PendingAction { get; internal set; }
        public DiceResult LastDiceResult { get; internal set; }
        public MapCard LastDrawnCard { get; internal set; }
        public string LastEventMessage { get; internal set; } = string.Empty;
        public int MovementStepsRemaining { get; internal set; }
        public IReadOnlyList<string> RouteChoiceTileIds => _routeChoiceTileIdsView;
        public GameOutcome Outcome { get; internal set; }

        internal void SetRouteChoices(IReadOnlyList<BoardTileDefinition> tiles)
        {
            _routeChoiceTileIds = new string[tiles == null ? 0 : tiles.Count];
            for (var i = 0; i < _routeChoiceTileIds.Length; i++) _routeChoiceTileIds[i] = tiles[i].Id;
            _routeChoiceTileIdsView = Array.AsReadOnly(_routeChoiceTileIds);
        }

        internal bool TryMarkDistrictRewardPaid(int playerId, string districtId)
        {
            return _districtRewardsPaid.Add(playerId + ":" + districtId);
        }

        public PlayerState GetPlayer(int playerId)
        {
            for (var i = 0; i < _players.Count; i++)
            {
                if (_players[i].Id == playerId) return _players[i];
            }

            return null;
        }

        public PropertyState GetProperty(string propertyId)
        {
            for (var i = 0; i < _properties.Count; i++)
            {
                if (string.Equals(_properties[i].PropertyId, propertyId, StringComparison.Ordinal)) return _properties[i];
            }

            return null;
        }
    }
}
