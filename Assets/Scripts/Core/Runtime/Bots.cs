using System;

namespace Richman.Core
{
    public interface IBotStrategy
    {
        GameCommand ChooseCommand(GameState state, int playerId);
    }

    public sealed class SimpleBotStrategy : IBotStrategy
    {
        public GameCommand ChooseCommand(GameState state, int playerId)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var player = state.GetPlayer(playerId);
            if (player == null) throw new ArgumentException("The bot player is not in the game.", nameof(playerId));
            if (state.CurrentPlayerId != playerId) throw new InvalidOperationException("A bot can only choose for the current player.");

            switch (state.Phase)
            {
                case TurnPhase.AwaitingRoll: return new RollDiceCommand(playerId);
                case TurnPhase.AwaitingAction:
                    if (state.PendingAction == PendingAction.ChooseRoute && state.RouteChoiceTileIds.Count > 0)
                        return new ChooseRouteCommand(playerId, state.RouteChoiceTileIds[0]);
                    var tile = state.Board.GetTileAt(player.CurrentTileIndex);
                    if (state.PendingAction == PendingAction.BuyProperty && player.Money >= tile.PurchasePrice)
                    {
                        return new BuyPropertyCommand(playerId);
                    }
                    var property = state.GetProperty(tile.Id);
                    if (state.PendingAction == PendingAction.UpgradeProperty && property != null &&
                        property.UpgradeLevel < tile.RentLevels.Count - 1 && player.Money >= tile.UpgradeCost)
                    {
                        return new UpgradePropertyCommand(playerId);
                    }
                    return new EndTurnCommand(playerId);
                case TurnPhase.AwaitingEndTurn: return new EndTurnCommand(playerId);
                default: throw new InvalidOperationException("The game no longer accepts bot commands.");
            }
        }
    }
}
