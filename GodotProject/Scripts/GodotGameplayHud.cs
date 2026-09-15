using System;
using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotGameplayHud : CanvasLayer
    {
        private Label _status;
        private Label _players;
        private Label _property;
        private Label _dice;
        private Button _roll;
        private Button _buy;
        private Button _upgrade;
        private Button _endTurn;
        private Action _onRoll;
        private Action _onBuy;
        private Action _onUpgrade;
        private Action _onEndTurn;

        public void Build(Action onRoll, Action onBuy, Action onUpgrade, Action onEndTurn)
        {
            _onRoll = onRoll; _onBuy = onBuy; _onUpgrade = onUpgrade; _onEndTurn = onEndTurn;
            var margin = new MarginContainer { OffsetLeft = 24f, OffsetTop = 24f, OffsetRight = 360f, OffsetBottom = 690f };
            var panel = new PanelContainer();
            var column = new VBoxContainer { Theme = MakeTheme() };
            _status = new Label { Text = "RICHMAN" };
            _status.AddThemeFontSizeOverride("font_size", 22);
            _dice = new Label { Text = "DICE --" };
            _players = new Label { Text = "PLAYERS" };
            _property = new Label { Text = "PROPERTY" };
            _roll = Button("ROLL DICE", onRoll);
            _buy = Button("BUY", onBuy);
            _upgrade = Button("UPGRADE", onUpgrade);
            _endTurn = Button("END TURN", onEndTurn);
            column.AddChild(_status); column.AddChild(_dice); column.AddChild(_players); column.AddChild(_property);
            column.AddChild(_roll); column.AddChild(_buy); column.AddChild(_upgrade); column.AddChild(_endTurn);
            panel.AddChild(column); margin.AddChild(panel); AddChild(margin);
        }

        public void Render(GameState state, string message, bool busy)
        {
            if (_status == null || state == null) return;
            var current = state.GetPlayer(state.CurrentPlayerId);
            _status.Text = "RICHMAN\n" + message + "\nP" + current.Id + "  CASH $" + current.Money + "  TURN " + state.TurnNumber;
            _dice.Text = state.LastDiceResult == null ? "DICE --" : "DICE " + state.LastDiceResult.First + " + " + state.LastDiceResult.Second + " = " + state.LastDiceResult.Total;
            var players = "PLAYERS\n";
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                players += "P" + player.Id + "  $" + player.Money + "  TILE " + player.CurrentTileIndex + "  " + player.Status + (player.Id == state.CurrentPlayerId ? "  <" : "") + "\n";
            }
            _players.Text = players;
            var tile = state.Board.GetTileAt(current.CurrentTileIndex);
            var property = state.GetProperty(tile.Id);
            _property.Text = tile.Type == BoardTileType.Property
                ? "PROPERTY\n" + tile.DisplayName + "\nPRICE $" + tile.PurchasePrice + "  RENT $" + tile.GetRent(property == null ? 0 : property.UpgradeLevel)
                : "CURRENT TILE\n" + tile.DisplayName + "\n" + tile.Type;
            var canAct = !busy && state.Outcome == null;
            _roll.Disabled = !(canAct && state.Phase == TurnPhase.AwaitingRoll);
            _buy.Disabled = !(canAct && state.PendingAction == PendingAction.BuyProperty);
            _upgrade.Disabled = !(canAct && state.PendingAction == PendingAction.UpgradeProperty);
            _endTurn.Disabled = !(canAct && (state.Phase == TurnPhase.AwaitingAction || state.Phase == TurnPhase.AwaitingEndTurn));
        }

        private static Button Button(string text, Action action)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(280f, 42f) };
            button.Pressed += action;
            return button;
        }

        private static Theme MakeTheme()
        {
            var theme = new Theme();
            theme.DefaultFontSize = 15;
            return theme;
        }
    }
}
