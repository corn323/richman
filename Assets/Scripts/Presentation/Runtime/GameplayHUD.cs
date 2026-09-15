using System;
using System.Text;
using Richman.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Richman.Presentation
{
    public sealed class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private UIDocument document;
        [SerializeField] private StyleSheet styleSheet;

        private Label _currentPlayer;
        private Label _turn;
        private Label _money;
        private Label _diceResult;
        private Label _playerList;
        private Label _ownedProperties;
        private Label _currentTile;
        private Label _message;
        private VisualElement _propertyPanel;
        private Label _propertyName;
        private Label _propertyDistrict;
        private Label _propertyPrice;
        private Label _propertyRent;
        private Label _propertyUpgradeCost;
        private Button _rollButton;
        private Button _buyButton;
        private Button _upgradeButton;
        private Button _endTurnButton;
        private Button _newGameButton;
        private bool _initialized;

        public void SetDocumentReferences(UIDocument uiDocument, StyleSheet uiStyleSheet)
        {
            document = uiDocument;
            styleSheet = uiStyleSheet;
            _initialized = false;
        }

        public void BindActions(Action roll, Action buy, Action upgrade, Action endTurn, Action newGame)
        {
            if (!EnsureReferences()) return;
            _rollButton.clicked -= roll;
            _rollButton.clicked += roll;
            _buyButton.clicked -= buy;
            _buyButton.clicked += buy;
            _upgradeButton.clicked -= upgrade;
            _upgradeButton.clicked += upgrade;
            _endTurnButton.clicked -= endTurn;
            _endTurnButton.clicked += endTurn;
            _newGameButton.clicked -= newGame;
            _newGameButton.clicked += newGame;
        }

        public void Render(GameState state, string message, bool busy)
        {
            if (state == null || !EnsureReferences()) return;
            var current = state.GetPlayer(state.CurrentPlayerId);
            if (current == null) return;

            _currentPlayer.text = "CURRENT PLAYER\nP" + current.Id + "  " + current.DisplayName;
            _turn.text = "TURN " + state.TurnNumber + "   " + state.Phase;
            _money.text = "CASH  $" + current.Money;
            _message.text = message ?? string.Empty;
            _diceResult.text = state.LastDiceResult == null
                ? "DICE  --"
                : "DICE  " + state.LastDiceResult.First + " + " + state.LastDiceResult.Second +
                  " = " + state.LastDiceResult.Total;

            RenderPlayers(state);
            RenderOwnedProperties(state, current.Id);
            var tile = state.Board.GetTileAt(current.CurrentTileIndex);
            _currentTile.text = "CURRENT TILE\n" + tile.DisplayName + "\n" + tile.Type;
            RenderPropertyPanel(state, tile);
            RenderButtons(state, busy);
        }

        private bool EnsureReferences()
        {
            if (_initialized) return true;
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null) return false;
            var root = document.rootVisualElement;
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet)) root.styleSheets.Add(styleSheet);

            _currentPlayer = root.Q<Label>("CurrentPlayer");
            _turn = root.Q<Label>("Turn");
            _money = root.Q<Label>("Money");
            _diceResult = root.Q<Label>("DiceResult");
            _playerList = root.Q<Label>("PlayerList");
            _ownedProperties = root.Q<Label>("OwnedProperties");
            _currentTile = root.Q<Label>("CurrentTile");
            _message = root.Q<Label>("Message");
            _propertyPanel = root.Q<VisualElement>("PropertyPanel");
            _propertyName = root.Q<Label>("PropertyName");
            _propertyDistrict = root.Q<Label>("PropertyDistrict");
            _propertyPrice = root.Q<Label>("PropertyPrice");
            _propertyRent = root.Q<Label>("PropertyRent");
            _propertyUpgradeCost = root.Q<Label>("PropertyUpgradeCost");
            _rollButton = root.Q<Button>("RollButton");
            _buyButton = root.Q<Button>("BuyButton");
            _upgradeButton = root.Q<Button>("UpgradeButton");
            _endTurnButton = root.Q<Button>("EndTurnButton");
            _newGameButton = root.Q<Button>("NewGameButton");
            _initialized = _currentPlayer != null && _turn != null && _money != null && _rollButton != null;
            return _initialized;
        }

        private void RenderPlayers(GameState state)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                builder.Append("P").Append(player.Id).Append("   $").Append(player.Money)
                    .Append("   Tile ").Append(player.CurrentTileIndex).Append("   ").Append(player.Status);
                if (player.Id == state.CurrentPlayerId) builder.Append("  <");
                if (i + 1 < state.Players.Count) builder.Append('\n');
            }

            _playerList.text = builder.ToString();
        }

        private void RenderOwnedProperties(GameState state, int playerId)
        {
            var builder = new StringBuilder("OWNED PROPERTIES\n");
            var count = 0;
            for (var i = 0; i < state.Properties.Count; i++)
            {
                var property = state.Properties[i];
                if (property.OwnerId != playerId) continue;
                var tile = state.Board.GetProperty(property.PropertyId);
                builder.Append("• ").Append(tile.DisplayName).Append("  Lv").Append(property.UpgradeLevel).Append('\n');
                count++;
            }

            if (count == 0) builder.Append("None yet");
            _ownedProperties.text = builder.ToString().TrimEnd();
        }

        private void RenderPropertyPanel(GameState state, BoardTileDefinition tile)
        {
            var isProperty = tile.Type == BoardTileType.Property;
            _propertyPanel.style.display = isProperty ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isProperty) return;

            var property = state.GetProperty(tile.Id);
            _propertyName.text = tile.DisplayName;
            _propertyDistrict.text = "District  " + tile.DistrictId;
            _propertyPrice.text = "Price  $" + tile.PurchasePrice;
            var level = property == null ? 0 : property.UpgradeLevel;
            _propertyRent.text = "Rent  $" + tile.GetRent(level);
            _propertyUpgradeCost.text = "Upgrade  $" + tile.UpgradeCost;
        }

        private void RenderButtons(GameState state, bool busy)
        {
            var canAct = !busy && state.Outcome == null;
            _rollButton.SetEnabled(canAct && state.Phase == TurnPhase.AwaitingRoll);
            _buyButton.SetEnabled(canAct && state.Phase == TurnPhase.AwaitingAction &&
                                  state.PendingAction == PendingAction.BuyProperty);
            _upgradeButton.SetEnabled(canAct && state.Phase == TurnPhase.AwaitingAction &&
                                      state.PendingAction == PendingAction.UpgradeProperty);
            _endTurnButton.SetEnabled(canAct &&
                                       (state.Phase == TurnPhase.AwaitingAction || state.Phase == TurnPhase.AwaitingEndTurn));
            _newGameButton.SetEnabled(!busy);
        }
    }
}
