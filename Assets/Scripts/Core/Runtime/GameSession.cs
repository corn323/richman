using System;
using System.Collections.Generic;

namespace Richman.Core
{
    public sealed class GameSession
    {
        private readonly IDiceRoller _diceRoller;

        public GameSession(GameModeConfig mode, BoardDefinition board, IReadOnlyList<PlayerSetup> playerSetups, IDiceRoller diceRoller)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
            if (playerSetups == null) throw new ArgumentNullException(nameof(playerSetups));
            if (playerSetups.Count < mode.MinPlayers || playerSetups.Count > mode.MaxPlayers)
            {
                throw new ArgumentException("Player count is outside the game mode limits.", nameof(playerSetups));
            }

            var players = new List<PlayerState>(playerSetups.Count);
            var playerIds = new HashSet<int>();
            for (var i = 0; i < playerSetups.Count; i++)
            {
                var setup = playerSetups[i] ?? throw new ArgumentException("Player setups cannot be null.", nameof(playerSetups));
                if (!playerIds.Add(setup.Id)) throw new ArgumentException("Player ids must be unique.", nameof(playerSetups));
                players.Add(new PlayerState(setup, mode.StartingMoney, board.StartPositionIndex));
            }

            var properties = new List<PropertyState>();
            for (var i = 0; i < board.Tiles.Count; i++)
            {
                if (board.Tiles[i].Type == BoardTileType.Property) properties.Add(new PropertyState(board.Tiles[i]));
            }

            State = new GameState(mode, board, players, properties);
        }

        public GameModeConfig Mode { get; }
        public BoardDefinition Board { get; }
        public GameState State { get; }

        public CommandResult Execute(GameCommand command)
        {
            if (command == null) return CommandResult.Failure(CommandErrorCode.InvalidCommand, "A command is required.");
            if (State.Phase == TurnPhase.Finished) return CommandResult.Failure(CommandErrorCode.GameFinished, "The game has already finished.");

            var player = State.GetPlayer(command.PlayerId);
            if (player == null) return CommandResult.Failure(CommandErrorCode.PlayerNotFound, "The player does not belong to this session.");
            if (player.Status != PlayerStatus.Active) return CommandResult.Failure(CommandErrorCode.PlayerEliminated, "The player is no longer active.");
            if (command is RollDiceCommand) return ExecuteRoll(player);
            if (command is BuyPropertyCommand) return ExecuteBuy(player);
            if (command is UpgradePropertyCommand) return ExecuteUpgrade(player);
            if (command is EndTurnCommand) return ExecuteEndTurn(player);
            return CommandResult.Failure(CommandErrorCode.InvalidCommand, "The command type is not supported by M1.");
        }

        private CommandResult ExecuteRoll(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingRoll)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "The player must finish the current turn before rolling again.");
            }

            var dice = _diceRoller.Roll();
            var from = player.CurrentTileIndex;
            var rawDestination = from + dice.Total;
            var destination = rawDestination % Board.TileCount;
            player.MoveTo(destination);
            State.LastDiceResult = dice;

            if (PassedStart(from, dice.Total)) player.AddMoney(Mode.StartBonus);

            ResolveLanding(player, Board.GetTileAt(destination));
            return CommandResult.Success(dice, from, destination);
        }

        private bool PassedStart(int fromTileIndex, int steps)
        {
            for (var step = 1; step <= steps; step++)
            {
                if ((fromTileIndex + step) % Board.TileCount == Board.StartPositionIndex) return true;
            }

            return false;
        }

        private CommandResult ExecuteBuy(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingAction || State.PendingAction != PendingAction.BuyProperty)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "There is no property available to buy right now.");
            }

            var tile = Board.GetTileAt(player.CurrentTileIndex);
            var property = State.GetProperty(tile.Id);
            if (property == null || property.OwnerId.HasValue)
            {
                return CommandResult.Failure(CommandErrorCode.PropertyUnavailable, "The property is no longer available.");
            }
            if (player.Money < tile.PurchasePrice)
            {
                return CommandResult.Failure(CommandErrorCode.InsufficientFunds, "The player cannot afford this property.");
            }

            player.Spend(tile.PurchasePrice);
            property.SetOwner(player.Id);
            State.PendingAction = PendingAction.None;
            State.Phase = TurnPhase.AwaitingEndTurn;
            return CommandResult.Success();
        }

        private CommandResult ExecuteUpgrade(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingAction || State.PendingAction != PendingAction.UpgradeProperty)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "There is no property available to upgrade right now.");
            }

            var tile = Board.GetTileAt(player.CurrentTileIndex);
            var property = State.GetProperty(tile.Id);
            if (property == null || property.OwnerId != player.Id)
            {
                return CommandResult.Failure(CommandErrorCode.PropertyUnavailable, "The player does not own this property.");
            }
            if (property.UpgradeLevel >= tile.RentLevels.Count - 1)
            {
                return CommandResult.Failure(CommandErrorCode.PropertyAlreadyMaxLevel, "The property is already at its maximum level.");
            }
            if (player.Money < tile.UpgradeCost)
            {
                return CommandResult.Failure(CommandErrorCode.InsufficientFunds, "The player cannot afford this upgrade.");
            }

            player.Spend(tile.UpgradeCost);
            property.Upgrade();
            State.PendingAction = PendingAction.None;
            State.Phase = TurnPhase.AwaitingEndTurn;
            return CommandResult.Success();
        }

        private CommandResult ExecuteEndTurn(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingAction && State.Phase != TurnPhase.AwaitingEndTurn)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "The player has not rolled yet.");
            }

            State.PendingAction = PendingAction.None;
            AdvanceTurn();
            return CommandResult.Success();
        }

        private CommandResult ValidateCurrentPlayer(PlayerState player)
        {
            if (player.Id != State.CurrentPlayerId)
            {
                return CommandResult.Failure(CommandErrorCode.WrongTurn, "It is not this player's turn.");
            }

            return null;
        }

        private void ResolveLanding(PlayerState player, BoardTileDefinition tile)
        {
            switch (tile.Type)
            {
                case BoardTileType.Property:
                    ResolveProperty(player, tile);
                    return;
                case BoardTileType.Tax:
                    PayToBank(player, tile.EffectAmount == 0 ? Mode.TaxAmount : Math.Abs(tile.EffectAmount));
                    break;
                case BoardTileType.Bonus:
                    player.AddMoney(tile.EffectAmount == 0 ? Mode.BonusAmount : tile.EffectAmount);
                    break;
            }

            FinishActionOrAdvance(player);
        }

        private void ResolveProperty(PlayerState player, BoardTileDefinition tile)
        {
            var property = State.GetProperty(tile.Id);
            if (property == null) throw new InvalidOperationException("The board property has no state.");

            if (!property.OwnerId.HasValue)
            {
                State.PendingAction = PendingAction.BuyProperty;
                State.Phase = TurnPhase.AwaitingAction;
                return;
            }

            if (property.OwnerId.Value == player.Id)
            {
                State.PendingAction = PendingAction.UpgradeProperty;
                State.Phase = TurnPhase.AwaitingAction;
                return;
            }

            var owner = State.GetPlayer(property.OwnerId.Value);
            if (owner == null || owner.Status != PlayerStatus.Active) throw new InvalidOperationException("A property owner must be active.");
            var rent = tile.GetRent(property.UpgradeLevel);
            if (player.Money >= rent)
            {
                player.Spend(rent);
                owner.AddMoney(rent);
            }
            else
            {
                owner.AddMoney(player.TakeAllMoney());
                DeclareBankruptcy(player, owner.Id);
            }

            FinishActionOrAdvance(player);
        }

        private void PayToBank(PlayerState player, int amount)
        {
            if (amount <= 0) return;
            if (player.Money >= amount)
            {
                player.Spend(amount);
                return;
            }

            player.TakeAllMoney();
            DeclareBankruptcy(player, null);
        }

        private void DeclareBankruptcy(PlayerState player, int? creditorId)
        {
            var properties = State.Properties;
            for (var i = 0; i < properties.Count; i++)
            {
                if (properties[i].OwnerId == player.Id) properties[i].SetOwner(creditorId);
            }

            player.MarkBankrupt();
            State.PendingAction = PendingAction.None;
            CheckVictory();
        }

        private void FinishActionOrAdvance(PlayerState player)
        {
            if (State.Phase == TurnPhase.Finished) return;
            if (player.Status != PlayerStatus.Active)
            {
                AdvanceTurn();
                return;
            }

            State.PendingAction = PendingAction.None;
            State.Phase = TurnPhase.AwaitingEndTurn;
        }

        private void AdvanceTurn()
        {
            if (State.Phase == TurnPhase.Finished) return;
            CheckVictory();
            if (State.Phase == TurnPhase.Finished) return;

            var currentIndex = -1;
            for (var i = 0; i < State.Players.Count; i++)
            {
                if (State.Players[i].Id == State.CurrentPlayerId)
                {
                    currentIndex = i;
                    break;
                }
            }

            for (var offset = 1; offset <= State.Players.Count; offset++)
            {
                var candidate = State.Players[(currentIndex + offset) % State.Players.Count];
                if (candidate.Status == PlayerStatus.Active)
                {
                    State.CurrentPlayerId = candidate.Id;
                    State.TurnNumber++;
                    State.Phase = TurnPhase.AwaitingRoll;
                    State.PendingAction = PendingAction.None;
                    return;
                }
            }

            throw new InvalidOperationException("No active player remains, but the game did not finish.");
        }

        private void CheckVictory()
        {
            var activeCount = 0;
            PlayerState lastActive = null;
            for (var i = 0; i < State.Players.Count; i++)
            {
                if (State.Players[i].Status == PlayerStatus.Active)
                {
                    activeCount++;
                    lastActive = State.Players[i];
                }
            }

            if (activeCount != 1) return;
            lastActive.MarkWinner();
            State.CurrentPlayerId = lastActive.Id;
            State.Outcome = new GameOutcome(lastActive.Id, State.TurnNumber);
            State.Phase = TurnPhase.Finished;
            State.PendingAction = PendingAction.None;
        }
    }

    public static class PrototypeGameFactory
    {
        public static GameSession CreateFourPlayerSession(int seed)
        {
            var mode = GameModeConfig.ClassicBankruptcyFourPlayer();
            var players = new List<PlayerSetup>(mode.MaxPlayers);
            for (var i = 0; i < mode.MaxPlayers; i++)
            {
                var playerId = i + 1;
                players.Add(new PlayerSetup(playerId, "Player " + playerId));
            }

            return new GameSession(mode, PrototypeBoardFactory.Create(), players, new SeededDiceRoller(seed));
        }
    }
}
