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
        private Label _title;
        private Label _subtitle;
        private Label _turn;
        private Label _money;
        private Label _diceResult;
        private Label _playerList;
        private Label _playersLabel;
        private Label _ownedProperties;
        private Label _currentTile;
        private Label _message;
        private VisualElement _propertyPanel;
        private Label _propertyName;
        private Label _propertyDistrict;
        private Label _propertyPrice;
        private Label _propertyRent;
        private Label _propertyUpgradeCost;
        private Label _propertyHeader;
        private Button _rollButton;
        private Button _buyButton;
        private Button _upgradeButton;
        private Button _endTurnButton;
        private Button _newGameButton;
        private Button _languageZh;
        private Button _languageEn;
        private Button _languageJa;
        private Action _setChinese;
        private Action _setEnglish;
        private Action _setJapanese;
        private bool _initialized;

        public void SetDocumentReferences(UIDocument uiDocument, StyleSheet uiStyleSheet)
        {
            document = uiDocument;
            styleSheet = uiStyleSheet;
            _initialized = false;
        }

        public void BindActions(Action roll, Action buy, Action upgrade, Action endTurn, Action newGame, Action<RichmanLanguage> languageChanged)
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
            if (_setChinese != null) _languageZh.clicked -= _setChinese;
            if (_setEnglish != null) _languageEn.clicked -= _setEnglish;
            if (_setJapanese != null) _languageJa.clicked -= _setJapanese;
            _setChinese = () => languageChanged(RichmanLanguage.TraditionalChinese);
            _setEnglish = () => languageChanged(RichmanLanguage.English);
            _setJapanese = () => languageChanged(RichmanLanguage.Japanese);
            _languageZh.clicked += _setChinese;
            _languageEn.clicked += _setEnglish;
            _languageJa.clicked += _setJapanese;
        }

        public void Render(GameState state, string message, bool busy)
        {
            if (state == null || !EnsureReferences()) return;
            var current = state.GetPlayer(state.CurrentPlayerId);
            if (current == null) return;

            _title.text = RichmanLocalization.Text("title");
            _subtitle.text = RichmanLocalization.Text("subtitle");
            _currentPlayer.text = RichmanLocalization.Format("current-player", current.Id,
                RichmanLocalization.Format("player-name", current.Id));
            _turn.text = RichmanLocalization.Format("turn", state.TurnNumber, RichmanLocalization.Text("phase." + state.Phase));
            _money.text = RichmanLocalization.Format("money", current.Money);
            _message.text = message ?? string.Empty;
            _diceResult.text = state.LastDiceResult == null
                ? RichmanLocalization.Text("dice.none")
                : RichmanLocalization.Format("dice.result", state.LastDiceResult.First, state.LastDiceResult.Second, state.LastDiceResult.Total);

            _playersLabel.text = RichmanLocalization.Text("players");
            RenderStaticText();
            RenderPlayers(state);
            RenderOwnedProperties(state, current.Id);
            var tile = state.Board.GetTileAt(current.CurrentTileIndex);
            _currentTile.text = RichmanLocalization.Format("current-tile", RichmanLocalization.TileName(tile), RichmanLocalization.TileType(tile.Type));
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
            _title = root.Q<Label>("Title");
            _subtitle = root.Q<Label>("Subtitle");
            _turn = root.Q<Label>("Turn");
            _money = root.Q<Label>("Money");
            _diceResult = root.Q<Label>("DiceResult");
            _playerList = root.Q<Label>("PlayerList");
            _playersLabel = root.Q<Label>("PlayersLabel");
            _ownedProperties = root.Q<Label>("OwnedProperties");
            _currentTile = root.Q<Label>("CurrentTile");
            _message = root.Q<Label>("Message");
            _propertyPanel = root.Q<VisualElement>("PropertyPanel");
            _propertyName = root.Q<Label>("PropertyName");
            _propertyDistrict = root.Q<Label>("PropertyDistrict");
            _propertyPrice = root.Q<Label>("PropertyPrice");
            _propertyRent = root.Q<Label>("PropertyRent");
            _propertyUpgradeCost = root.Q<Label>("PropertyUpgradeCost");
            _propertyHeader = root.Q<Label>("PropertyHeader");
            _rollButton = root.Q<Button>("RollButton");
            _buyButton = root.Q<Button>("BuyButton");
            _upgradeButton = root.Q<Button>("UpgradeButton");
            _endTurnButton = root.Q<Button>("EndTurnButton");
            _newGameButton = root.Q<Button>("NewGameButton");
            _languageZh = root.Q<Button>("LanguageZh");
            _languageEn = root.Q<Button>("LanguageEn");
            _languageJa = root.Q<Button>("LanguageJa");
            _initialized = _currentPlayer != null && _turn != null && _money != null && _rollButton != null &&
                           _title != null && _subtitle != null && _playersLabel != null && _languageZh != null &&
                           _languageEn != null && _languageJa != null;
            return _initialized;
        }

        private void RenderPlayers(GameState state)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                builder.Append(RichmanLocalization.Format("player.entry", player.Id, player.Money, player.CurrentTileIndex,
                    RichmanLocalization.PlayerStatus(player.Status)));
                if (player.Id == state.CurrentPlayerId) builder.Append("  <");
                if (i + 1 < state.Players.Count) builder.Append('\n');
            }

            _playerList.text = builder.ToString();
        }

        private void RenderOwnedProperties(GameState state, int playerId)
        {
            var builder = new StringBuilder(RichmanLocalization.Text("owned-properties")).Append('\n');
            var count = 0;
            for (var i = 0; i < state.Properties.Count; i++)
            {
                var property = state.Properties[i];
                if (property.OwnerId != playerId) continue;
                var tile = state.Board.GetProperty(property.PropertyId);
                builder.Append("• ").Append(RichmanLocalization.TileName(tile)).Append("  Lv").Append(property.UpgradeLevel).Append('\n');
                count++;
            }

            if (count == 0) builder.Append(RichmanLocalization.Text("none-yet"));
            _ownedProperties.text = builder.ToString().TrimEnd();
        }

        private void RenderPropertyPanel(GameState state, BoardTileDefinition tile)
        {
            var isProperty = tile.Type == BoardTileType.Property;
            _propertyPanel.style.display = isProperty ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isProperty) return;

            var property = state.GetProperty(tile.Id);
            _propertyName.text = RichmanLocalization.TileName(tile);
            _propertyDistrict.text = RichmanLocalization.Format("district", RichmanLocalization.District(tile.DistrictId));
            _propertyPrice.text = RichmanLocalization.Format("price", tile.PurchasePrice);
            var level = property == null ? 0 : property.UpgradeLevel;
            _propertyRent.text = RichmanLocalization.Format("rent", tile.GetRent(level));
            _propertyUpgradeCost.text = RichmanLocalization.Format("upgrade-cost", tile.UpgradeCost);
        }

        private void RenderStaticText()
        {
            _propertyHeader.text = RichmanLocalization.Text("property");
            _rollButton.text = RichmanLocalization.Text("roll");
            _buyButton.text = RichmanLocalization.Text("buy");
            _upgradeButton.text = RichmanLocalization.Text("upgrade");
            _endTurnButton.text = RichmanLocalization.Text("end-turn");
            _newGameButton.text = RichmanLocalization.Text("new-game");
            _languageZh.text = RichmanLocalization.Text("language.zh");
            _languageEn.text = RichmanLocalization.Text("language.en");
            _languageJa.text = RichmanLocalization.Text("language.ja");
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
