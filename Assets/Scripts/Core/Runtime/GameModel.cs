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
        UpgradeProperty
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
            int bonusAmount)
        {
            if (string.IsNullOrWhiteSpace(gameModeId)) throw new ArgumentException("Game mode id is required.", nameof(gameModeId));
            if (minPlayers < 1) throw new ArgumentOutOfRangeException(nameof(minPlayers));
            if (maxPlayers < minPlayers) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (startBonus < 0) throw new ArgumentOutOfRangeException(nameof(startBonus));
            if (taxAmount < 0) throw new ArgumentOutOfRangeException(nameof(taxAmount));
            if (bonusAmount < 0) throw new ArgumentOutOfRangeException(nameof(bonusAmount));

            GameModeId = gameModeId;
            MinPlayers = minPlayers;
            MaxPlayers = maxPlayers;
            StartingMoney = startingMoney;
            StartBonus = startBonus;
            TaxAmount = taxAmount;
            BonusAmount = bonusAmount;
        }

        public string GameModeId { get; }
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public int StartingMoney { get; }
        public int StartBonus { get; }
        public int TaxAmount { get; }
        public int BonusAmount { get; }

        public static GameModeConfig ClassicBankruptcyFourPlayer()
        {
            return new GameModeConfig("classic-bankruptcy", 4, 4, 1500, 200, 100, 75);
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
        internal GameOutcome(int winnerId, int turnNumber)
        {
            WinnerId = winnerId;
            TurnNumber = turnNumber;
        }

        public int WinnerId { get; }
        public int TurnNumber { get; }
    }

    public sealed class GameState
    {
        private readonly List<PlayerState> _players;
        private readonly List<PropertyState> _properties;
        private readonly ReadOnlyCollection<PlayerState> _playersView;
        private readonly ReadOnlyCollection<PropertyState> _propertiesView;

        internal GameState(GameModeConfig mode, BoardDefinition board, List<PlayerState> players, List<PropertyState> properties)
        {
            Mode = mode;
            Board = board;
            _players = players;
            _properties = properties;
            _playersView = _players.AsReadOnly();
            _propertiesView = _properties.AsReadOnly();
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
        public GameOutcome Outcome { get; internal set; }

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
