using System;
using System.Collections.Generic;
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
        private Button _endTurn;
        private VBoxContainer _routeOptions;
        private VBoxContainer _upgradeOptions;
        private Action<string> _onChooseRoute;
        private Action _onRoll;
        private Action _onBuy;
        private Action<string> _onUpgrade;
        private Action _onEndTurn;

        public void Build(Action onRoll, Action onBuy, Action<string> onUpgrade, Action onEndTurn, Action<string> onChooseRoute)
        {
            _onRoll = onRoll; _onBuy = onBuy; _onUpgrade = onUpgrade; _onEndTurn = onEndTurn;
            _onChooseRoute = onChooseRoute;
            var margin = new MarginContainer { OffsetLeft = 24f, OffsetTop = 24f, OffsetRight = 360f, OffsetBottom = 690f };
            var panel = new PanelContainer();
            var column = new VBoxContainer { Theme = MakeTheme() };
            _status = new Label { Text = "世界巡遊大富翁" };
            _status.AddThemeFontSizeOverride("font_size", 22);
            _dice = new Label { Text = "骰子 --" };
            _players = new Label { Text = "玩家" };
            _property = new Label { Text = "地格" };
            _roll = Button("擲骰", onRoll);
            _buy = Button("購買地產", onBuy);
            _endTurn = Button("結束回合", onEndTurn);
            _routeOptions = new VBoxContainer();
            _upgradeOptions = new VBoxContainer();
            column.AddChild(_status); column.AddChild(_dice); column.AddChild(_players); column.AddChild(_property);
            column.AddChild(_roll); column.AddChild(_buy); column.AddChild(_upgradeOptions); column.AddChild(_routeOptions); column.AddChild(_endTurn);
            panel.AddChild(column); margin.AddChild(panel); AddChild(margin);
        }

        public void Render(GameState state, string message, bool busy)
        {
            if (_status == null || state == null) return;
            var current = state.GetPlayer(state.CurrentPlayerId);
            _status.Text = "世界巡遊大富翁\n" + message + "\n玩家 " + current.Id + "  現金 $" + current.Money + "  回合 " + state.TurnNumber;
            _dice.Text = state.LastDiceResult == null ? "骰子 --" : "骰子 " + state.LastDiceResult.First + " + " + state.LastDiceResult.Second + " = " + state.LastDiceResult.Total;
            if (state.LastCourtDiceResult != null)
                _dice.Text += "\n法院擲骰 " + state.LastCourtDiceResult.First + " + " + state.LastCourtDiceResult.Second + " = " + state.LastCourtDiceResult.Total;
            var players = "玩家\n";
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                var detention = player.IsInPrison ? "  PRISON " + player.PrisonAttemptsRemaining + "次嘗試" : player.HospitalTurnsRemaining > 0 ? "  HOSPITAL " + player.HospitalTurnsRemaining + "回合" : string.Empty;
                players += "玩家" + player.Id + "  $" + player.Money + "  " + state.Board.GetTileAt(player.CurrentTileIndex).DisplayName + "  " + player.Status + detention + (player.Id == state.CurrentPlayerId ? "  ◀" : "") + "\n";
            }
            _players.Text = players;
            var tile = state.Board.GetTileAt(current.CurrentTileIndex);
            var property = state.GetProperty(tile.Id);
            _property.Text = tile.Type == BoardTileType.Property
                ? "地產\n" + tile.DisplayName + "\n價格 $" + tile.PurchasePrice + "  租金 $" + tile.GetRent(property == null ? 0 : property.UpgradeLevel)
                : "目前地格\n" + tile.DisplayName + "\n" + tile.Type + (state.LastDrawnCard == null ? string.Empty : "\n\n" + state.LastDrawnCard.Title + "\n" + state.LastDrawnCard.Description);
            var canAct = !busy && state.Outcome == null;
            _roll.Disabled = !(canAct && state.Phase == TurnPhase.AwaitingRoll);
            _buy.Disabled = !(canAct && state.PendingAction == PendingAction.BuyProperty);
            foreach (var child in _upgradeOptions.GetChildren()) { _upgradeOptions.RemoveChild(child); child.QueueFree(); }
            if ((state.Phase == TurnPhase.AwaitingAction && state.PendingAction == PendingAction.UpgradeProperty) ||
                (state.CanUpgradeOwnedProperty && state.Phase == TurnPhase.AwaitingEndTurn))
            {
                _upgradeOptions.AddChild(new Label { Text = state.CanUpgradeOwnedProperty ? "起點升級：選擇自有地產" : "升級目前地產" });
                for (var i = 0; i < state.Properties.Count; i++)
                {
                    var owned = state.Properties[i];
                    if (owned.OwnerId != current.Id) continue;
                    var ownedTile = state.Board.GetProperty(owned.PropertyId);
                    if (ownedTile == null || owned.UpgradeLevel >= ownedTile.RentLevels.Count - 1) continue;
                    var id = ownedTile.Id;
                    var button = Button(ownedTile.DisplayName + " · $" + ownedTile.UpgradeCost, () => _onUpgrade?.Invoke(id));
                    button.Disabled = !canAct || current.Money < ownedTile.UpgradeCost;
                    _upgradeOptions.AddChild(button);
                }
            }
            _endTurn.Disabled = !(canAct && state.PendingAction != PendingAction.ChooseRoute &&
                (state.Phase == TurnPhase.AwaitingAction || state.Phase == TurnPhase.AwaitingEndTurn));
            foreach (var child in _routeOptions.GetChildren()) { _routeOptions.RemoveChild(child); child.QueueFree(); }
            if (state.PendingAction == PendingAction.ChooseRoute)
            {
                _routeOptions.AddChild(new Label { Text = "選擇前進路線" });
                for (var i = 0; i < state.RouteChoiceTileIds.Count; i++)
                {
                    var destination = state.Board.GetTile(state.RouteChoiceTileIds[i]);
                    if (destination == null) continue;
                    var id = destination.Id;
                    var routeButton = Button(destination.DisplayName, () => _onChooseRoute?.Invoke(id));
                    routeButton.Disabled = !canAct;
                    _routeOptions.AddChild(routeButton);
                }
            }
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
