using System;
using System.Collections.Generic;
using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotGameplayHud : CanvasLayer
    {
        private static readonly Color[] ColorsByPlayer = { new Color("#bf705f"), new Color("#6387a5"), new Color("#82956f"), new Color("#a080a0") };
        private readonly string[] _characters = { "旅人・小徑", "建築師・石榴", "園藝師・苔苔", "觀星者・弦月" };
        private readonly int[] _selectedCharacters = { 0, 1, 2, 3 };
        private readonly Color[] _selectedColors = { ColorsByPlayer[0], ColorsByPlayer[1], ColorsByPlayer[2], ColorsByPlayer[3] };
        private readonly List<OptionButton> _characterPickers = new List<OptionButton>();
        private readonly List<OptionButton> _colorPickers = new List<OptionButton>();
        private Control _setup;
        private Control _game;
        private Label _setupSummary;
        private Label _turn;
        private Label _status;
        private Label _players;
        private Label _owned;
        private Label _property;
        private Label _dice;
        private Button _roll;
        private Button _buy;
        private Button _trade;
        private Button _endTurn;
        private PanelContainer _bottom;
        private VBoxContainer _decision;
        private Action<TradeCommand> _onTrade;
        private Action _onStart;
        private Action _onRoll;
        private Action _onBuy;
        private Action<string> _onUpgrade;
        private Action _onEndTurn;
        private Action<string> _onChooseRoute;
        private GameState _lastState;
        private string _lastMessage;
        private bool _lastBusy;
        private Action<int, int, Color> _onPreviewCharacter;

        public void Build(Action onStart, Action<int, int, Color> onPreviewCharacter, Action onRoll, Action onBuy,
            Action<string> onUpgrade, Action onEndTurn, Action<string> onChooseRoute, Action<TradeCommand> onTrade)
        {
            _onStart = onStart; _onPreviewCharacter = onPreviewCharacter; _onRoll = onRoll; _onBuy = onBuy;
            _onUpgrade = onUpgrade; _onEndTurn = onEndTurn; _onChooseRoute = onChooseRoute; _onTrade = onTrade;
            var theme = MakeTheme();
            _setup = new CenterContainer { Name = "SetupScreen", AnchorRight = 1, AnchorBottom = 1 };
            var setupPanel = new PanelContainer { CustomMinimumSize = new Vector2(760, 0), Theme = theme };
            var setupColumn = new VBoxContainer { Theme = theme };
            setupColumn.AddChild(new Label { Text = "世界巡遊　／　開局準備", Theme = theme });
            setupColumn.AddChild(new Label { Text = "四位旅人即將出發。每位玩家選擇不同角色與棋子顏色。", Theme = theme });
            for (var i = 0; i < 4; i++)
            {
                var row = new HBoxContainer { Theme = theme };
                row.AddChild(new Label { Text = "玩家 " + (i + 1), CustomMinimumSize = new Vector2(110, 42), VerticalAlignment = VerticalAlignment.Center });
                var character = new OptionButton { CustomMinimumSize = new Vector2(280, 42) };
                for (var c = 0; c < _characters.Length; c++) character.AddItem(_characters[c]);
                character.Select(i);
                var index = i;
                character.ItemSelected += selected => { _selectedCharacters[index] = (int)selected; UpdateSetup(); };
                row.AddChild(character); _characterPickers.Add(character);
                var color = new OptionButton { CustomMinimumSize = new Vector2(190, 42) };
                for (var c = 0; c < ColorsByPlayer.Length; c++) color.AddItem(new[] { "陶土紅", "霧海藍", "鼠尾草綠", "灰紫" }[c]);
                color.Select(i);
                color.ItemSelected += selected => { _selectedColors[index] = ColorsByPlayer[(int)selected]; UpdateSetup(); };
                row.AddChild(color); _colorPickers.Add(color);
                var preview = Button("預覽棋子", () => _onPreviewCharacter?.Invoke(index, _selectedCharacters[index], _selectedColors[index]));
                preview.CustomMinimumSize = new Vector2(120, 42); row.AddChild(preview);
                setupColumn.AddChild(row);
            }
            _setupSummary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, Theme = theme };
            setupColumn.AddChild(_setupSummary);
            var start = Button("開始世界巡遊", () => _onStart?.Invoke());
            start.CustomMinimumSize = new Vector2(0, 54); setupColumn.AddChild(start);
            setupPanel.AddChild(setupColumn); _setup.AddChild(setupPanel); AddChild(_setup);

            _game = new Control { Name = "GameHud", AnchorRight = 1, AnchorBottom = 1, Visible = false };
            var top = new PanelContainer { AnchorLeft = .025f, AnchorTop = .025f, AnchorRight = .47f, AnchorBottom = .20f, Theme = theme };
            var topColumn = new VBoxContainer { Theme = theme };
            _turn = new Label { Text = "世界巡遊", Theme = theme };
            _status = new Label { Text = "準備開始", Theme = theme, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _dice = new Label { Text = "骰子　—", Theme = theme };
            topColumn.AddChild(_turn); topColumn.AddChild(_status); topColumn.AddChild(_dice); top.AddChild(topColumn); _game.AddChild(top);
            var side = new PanelContainer { AnchorLeft = .73f, AnchorTop = .025f, AnchorRight = .975f, AnchorBottom = .65f, Theme = theme };
            var sideColumn = new VBoxContainer { Theme = theme };
            sideColumn.AddChild(new Label { Text = "旅人與資產", Theme = theme });
            _players = new Label { Theme = theme, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _owned = new Label { Theme = theme, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _property = new Label { Theme = theme, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            sideColumn.AddChild(_players); sideColumn.AddChild(new HSeparator()); sideColumn.AddChild(_owned);
            sideColumn.AddChild(new HSeparator()); sideColumn.AddChild(_property);
            sideColumn.AddChild(new Label { Text = "視角　右鍵拖曳　｜　滾輪縮放", Theme = theme });
            var sideScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            sideScroll.AddChild(sideColumn); side.AddChild(sideScroll); _game.AddChild(side);
            _bottom = new PanelContainer { AnchorLeft = .12f, AnchorTop = .83f, AnchorRight = .88f, AnchorBottom = .975f, Theme = theme };
            var controls = new VBoxContainer { Theme = theme };
            _decision = new VBoxContainer { Theme = theme };
            controls.AddChild(_decision);
            var actions = new HBoxContainer { Theme = theme };
            _roll = Button("擲骰", () => _onRoll?.Invoke());
            _buy = Button("購買地產", () => Confirm("確認以目前資金購買這筆地產？", () => _onBuy?.Invoke()));
            _trade = Button("交易", ShowTradeForm);
            _endTurn = Button("結束回合", () => Confirm("結束目前回合並交給下一位旅人？", () => _onEndTurn?.Invoke()));
            actions.AddChild(_roll); actions.AddChild(_buy); actions.AddChild(_trade); actions.AddChild(_endTurn); controls.AddChild(actions);
            _bottom.AddChild(controls); _game.AddChild(_bottom); AddChild(_game);
            UpdateSetup();
        }

        public int[] SelectedCharacters => (int[])_selectedCharacters.Clone();
        public Color[] SelectedColors => (Color[])_selectedColors.Clone();

        public void ShowGame()
        {
            _setup.Visible = false;
            _game.Visible = true;
        }

        public void Render(GameState state, string message, bool busy)
        {
            if (_turn == null || state == null) return;
            _lastState = state; _lastMessage = message; _lastBusy = busy;
            var current = state.GetPlayer(state.CurrentPlayerId);
            _turn.Text = "第 " + state.TurnNumber + " 回合　｜　玩家 " + current.Id + "　｜　現金 $" + current.Money;
            _status.Text = string.IsNullOrEmpty(message) ? NextStep(state) : message + "\n" + NextStep(state);
            _dice.Text = state.LastDiceResult == null ? "骰子　—" : "骰子　" + state.LastDiceResult.First + " + " + state.LastDiceResult.Second + " = " + state.LastDiceResult.Total;
            if (state.LastCourtDiceResult != null) _dice.Text += "　｜　法院 " + state.LastCourtDiceResult.First + " + " + state.LastCourtDiceResult.Second + " = " + state.LastCourtDiceResult.Total;
            var players = "";
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                var owned = 0;
                for (var p = 0; p < state.Properties.Count; p++) if (state.Properties[p].OwnerId == player.Id) owned++;
                players += (player.Id == state.CurrentPlayerId ? "▶ " : "　") + player.DisplayName + "　$" + player.Money + "　地產 " + owned + "　" + state.Board.GetTileAt(player.CurrentTileIndex).DisplayName;
                if (player.IsInPrison) players += "　監獄：剩餘 " + player.PrisonAttemptsRemaining + " 次嘗試";
                if (player.HospitalTurnsRemaining > 0) players += "　醫院：休養 " + player.HospitalTurnsRemaining + " 回合";
                players += "\n";
            }
            _players.Text = players;
            var ownedText = "地產清單\n";
            for (var i = 0; i < state.Properties.Count; i++)
            {
                var ownedProperty = state.Properties[i]; if (!ownedProperty.OwnerId.HasValue) continue;
                var ownedTile = state.Board.GetProperty(ownedProperty.PropertyId);
                ownedText += "玩家 " + ownedProperty.OwnerId.Value + "　" + (ownedTile == null ? ownedProperty.PropertyId : ownedTile.DisplayName) + "　Lv." + ownedProperty.UpgradeLevel + "\n";
            }
            _owned.Text = ownedText == "地產清單\n" ? "地產清單\n尚無地產" : ownedText;
            var tile = state.Board.GetTileAt(current.CurrentTileIndex);
            var property = state.GetProperty(tile.Id);
            var owner = property?.OwnerId.HasValue == true ? "玩家 " + property.OwnerId.Value : "尚未有人持有";
            _property.Text = tile.Type == BoardTileType.Property
                ? "目前地格\n" + tile.DisplayName + "\n價格 $" + tile.PurchasePrice + "　租金 $" + tile.GetRent(property == null ? 0 : property.UpgradeLevel) + "\n持有人：" + owner + (property == null ? "" : "　｜　升級 " + property.UpgradeLevel)
                : "目前地格\n" + tile.DisplayName + "\n" + tile.Type + (state.LastDrawnCard == null ? "" : "\n\n" + state.LastDrawnCard.Title + "\n" + state.LastDrawnCard.Description);
            var canAct = !busy && state.Outcome == null;
            _roll.Disabled = !(canAct && state.Phase == TurnPhase.AwaitingRoll);
            _buy.Disabled = !(canAct && state.PendingAction == PendingAction.BuyProperty);
            _endTurn.Disabled = !(canAct && state.PendingAction != PendingAction.ChooseRoute && (state.Phase == TurnPhase.AwaitingAction || state.Phase == TurnPhase.AwaitingEndTurn));
            _trade.Disabled = !(canAct && state.PendingAction != PendingAction.ChooseRoute);
            ClearDecision();
            if (state.PendingAction == PendingAction.ChooseRoute)
            {
                AddDecisionTitle("岔路選擇　｜　請選擇接回主線的路徑");
                for (var i = 0; i < state.RouteChoiceTileIds.Count; i++)
                {
                    var destination = state.Board.GetTile(state.RouteChoiceTileIds[i]); if (destination == null) continue;
                    var id = destination.Id; AddDecisionButton("前往　" + destination.DisplayName, () => Confirm("確認沿此路線繼續移動？", () => _onChooseRoute?.Invoke(id)), canAct);
                }
            }
            else if (state.PendingAction == PendingAction.BuyProperty)
                AddDecisionTitle("地產決策　｜　檢視右側價格後購買，或結束回合");
            if ((state.Phase == TurnPhase.AwaitingAction && state.PendingAction == PendingAction.UpgradeProperty) || (state.CanUpgradeOwnedProperty && state.Phase == TurnPhase.AwaitingEndTurn))
            {
                AddDecisionTitle(state.CanUpgradeOwnedProperty ? "起點獎勵　｜　選擇自有地產升級" : "地產升級　｜　選擇要改善的地產");
                for (var i = 0; i < state.Properties.Count; i++)
                {
                    var owned = state.Properties[i]; if (owned.OwnerId != current.Id) continue;
                    var ownedTile = state.Board.GetProperty(owned.PropertyId); if (ownedTile == null || owned.UpgradeLevel >= ownedTile.RentLevels.Count - 1) continue;
                    var id = ownedTile.Id; AddDecisionButton(ownedTile.DisplayName + "　＋升級　$" + ownedTile.UpgradeCost, () => Confirm("確認支付 $" + ownedTile.UpgradeCost + " 升級「" + ownedTile.DisplayName + "」？", () => _onUpgrade?.Invoke(id)), canAct && current.Money >= ownedTile.UpgradeCost);
                }
            }
            _bottom.AnchorTop = _decision.GetChildCount() > 0 ? .69f : .83f;
        }

        private static string NextStep(GameState state)
        {
            if (state.Outcome != null) return "遊戲結束　｜　玩家 " + state.Outcome.WinnerId + " 獲勝";
            if (state.PendingAction == PendingAction.ChooseRoute) return "下一步：選擇一條路線繼續移動。";
            if (state.PendingAction == PendingAction.BuyProperty) return "下一步：購買目前地產，或結束回合。";
            if (state.PendingAction == PendingAction.UpgradeProperty) return "下一步：選擇升級，或結束回合。";
            if (state.Phase == TurnPhase.AwaitingRoll) return "下一步：按「擲骰」開始移動。";
            return state.CanUpgradeOwnedProperty ? "下一步：領取起點升級，或結束回合。" : "下一步：結束回合交給下一位旅人。";
        }

        private void UpdateSetup()
        {
            var usedCharacters = new HashSet<int>(); var usedColors = new HashSet<int>(); var duplicate = false;
            for (var i = 0; i < 4; i++)
            {
                if (!usedCharacters.Add(_selectedCharacters[i]) || !usedColors.Add(ColorIndex(_selectedColors[i]))) duplicate = true;
                if (_characterPickers.Count > i) SetPickerAvailability(_characterPickers[i], _selectedCharacters[i], _selectedCharacters, i);
                if (_colorPickers.Count > i) SetPickerAvailability(_colorPickers[i], ColorIndex(_selectedColors[i]), _selectedColors, i);
            }
            _setupSummary.Text = duplicate ? "每位玩家都需要不同角色與棋子顏色，請調整重複選擇。" : "角色與顏色均已確認；按「預覽棋子」可在棋盤上查看所選外觀。";
            _setupSummary.Modulate = duplicate ? new Color("#9a4c40") : Colors.White;
            foreach (var child in _setup.GetChildren())
                if (child is CenterContainer center && center.GetChild(0) is PanelContainer panel && panel.GetChild(0) is VBoxContainer column)
                    foreach (var node in column.GetChildren()) if (node is Button button && button.Text == "開始世界巡遊") button.Disabled = duplicate;
        }

        private static void SetPickerAvailability(OptionButton picker, int selected, int[] all, int own)
        {
            for (var option = 0; option < picker.ItemCount; option++)
            {
                var disabled = false; for (var player = 0; player < all.Length; player++) if (player != own && all[player] == option) disabled = true;
                picker.SetItemDisabled(option, disabled);
            }
            picker.Select(selected);
        }

        private static void SetPickerAvailability(OptionButton picker, int selected, Color[] all, int own)
        {
            for (var option = 0; option < picker.ItemCount; option++)
            {
                var disabled = false; for (var player = 0; player < all.Length; player++) if (player != own && ColorIndex(all[player]) == option) disabled = true;
                picker.SetItemDisabled(option, disabled);
            }
            picker.Select(selected);
        }

        private static int ColorIndex(Color color)
        {
            for (var i = 0; i < ColorsByPlayer.Length; i++) if (ColorsByPlayer[i].IsEqualApprox(color)) return i;
            return 0;
        }

        private void ClearDecision() { foreach (var child in _decision.GetChildren()) { _decision.RemoveChild(child); child.QueueFree(); } }
        private void Confirm(string message, Action onConfirm, string confirmText = "確認")
        {
            ClearDecision();
            _roll.Disabled = true; _buy.Disabled = true; _trade.Disabled = true; _endTurn.Disabled = true;
            AddDecisionTitle(message);
            var row = new HBoxContainer { Theme = MakeTheme() };
            var confirm = Button(confirmText, () => onConfirm?.Invoke()); confirm.Disabled = _lastBusy;
            var cancel = Button("取消", () => Render(_lastState, _lastMessage, _lastBusy));
            row.AddChild(confirm); row.AddChild(cancel); _decision.AddChild(row);
            _bottom.AnchorTop = .69f;
        }

        private void ShowTradeForm()
        {
            if (_lastState == null || _lastBusy) return;
            var state = _lastState;
            var current = state.GetPlayer(state.CurrentPlayerId);
            _bottom.AnchorTop = .69f;
            ClearDecision(); _roll.Disabled = true; _buy.Disabled = true; _trade.Disabled = true; _endTurn.Disabled = true;
            AddDecisionTitle("交易　｜　選擇對象、現金與地產；雙方確認後才會執行");
            var fields = new HBoxContainer { Theme = MakeTheme() };
            var target = new OptionButton { CustomMinimumSize = new Vector2(170, 42) };
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i]; if (player.Id == current.Id || player.Status != PlayerStatus.Active) continue;
                target.AddItem("玩家 " + player.Id + "　" + player.DisplayName); target.SetItemMetadata(target.ItemCount - 1, player.Id);
            }
            if (target.ItemCount == 0) { AddDecisionTitle("目前沒有其他可交易的玩家。"); AddDecisionButton("取消", () => Render(state, _lastMessage, false), true); return; }
            target.Select(0);
            var offeredProperty = new OptionButton { CustomMinimumSize = new Vector2(170, 42) };
            PopulatePropertyOptions(offeredProperty, state, current.Id);
            var requestedProperty = new OptionButton { CustomMinimumSize = new Vector2(170, 42) };
            var targetPlayerId = (int)target.GetItemMetadata(0).AsInt64();
            PopulatePropertyOptions(requestedProperty, state, targetPlayerId);
            target.ItemSelected += selected => PopulatePropertyOptions(requestedProperty, state, (int)target.GetItemMetadata((int)selected).AsInt64());
            fields.AddChild(target); fields.AddChild(offeredProperty); fields.AddChild(requestedProperty); _decision.AddChild(fields);
            var terms = new HBoxContainer { Theme = MakeTheme() };
            var offeredMoney = new SpinBox { MinValue = 0, MaxValue = current.Money, Step = 10, CustomMinimumSize = new Vector2(125, 42), Prefix = "付 $" };
            var requestedMoney = new SpinBox { MinValue = 0, MaxValue = state.GetPlayer(targetPlayerId).Money, Step = 10, CustomMinimumSize = new Vector2(125, 42), Prefix = "收 $" };
            target.ItemSelected += selected => requestedMoney.MaxValue = state.GetPlayer((int)target.GetItemMetadata((int)selected).AsInt64()).Money;
            terms.AddChild(offeredMoney); terms.AddChild(requestedMoney);
            var propose = Button("提出交易", () =>
            {
                var targetId = (int)target.GetItemMetadata(target.Selected).AsInt64();
                var offerId = GetSelectedPropertyId(offeredProperty); var requestId = GetSelectedPropertyId(requestedProperty);
                var offeredTile = string.IsNullOrEmpty(offerId) ? null : state.Board.GetProperty(offerId);
                var requestedTile = string.IsNullOrEmpty(requestId) ? null : state.Board.GetProperty(requestId);
                var description = "玩家 " + current.Id + " 提出：" + (string.IsNullOrEmpty(offerId) ? "" : (offeredTile?.DisplayName ?? offerId) + "、") + "支付 $" + (int)offeredMoney.Value + "；玩家 " + targetId + " 提供：" + (string.IsNullOrEmpty(requestId) ? "" : (requestedTile?.DisplayName ?? requestId) + "、") + "支付 $" + (int)requestedMoney.Value + "。\n請雙方閱讀後共同確認。";
                var trade = new TradeCommand(current.Id, targetId, (int)offeredMoney.Value, (int)requestedMoney.Value, offerId, requestId);
                Confirm(description, () => _onTrade?.Invoke(trade), "雙方確認交易");
            });
            terms.AddChild(propose);
            terms.AddChild(Button("取消", () => Render(state, _lastMessage, false)));
            _decision.AddChild(terms);
        }

        private static void PopulatePropertyOptions(OptionButton picker, GameState state, int ownerId)
        {
            picker.Clear(); picker.AddItem("不交換地產"); picker.SetItemMetadata(0, string.Empty);
            for (var i = 0; i < state.Properties.Count; i++)
            {
                var property = state.Properties[i]; if (property.OwnerId != ownerId) continue;
                var tile = state.Board.GetProperty(property.PropertyId);
                picker.AddItem(tile == null ? property.PropertyId : tile.DisplayName + "　Lv." + property.UpgradeLevel);
                picker.SetItemMetadata(picker.ItemCount - 1, property.PropertyId);
            }
            picker.Select(0);
        }

        private static string GetSelectedPropertyId(OptionButton picker)
        {
            return picker.Selected < 0 ? string.Empty : picker.GetItemMetadata(picker.Selected).AsString();
        }
        private void AddDecisionTitle(string text) => _decision.AddChild(new Label { Text = text, Theme = MakeTheme() });
        private void AddDecisionButton(string text, Action action, bool enabled) { var button = Button(text, action); button.Disabled = !enabled; _decision.AddChild(button); }
        private static Button Button(string text, Action action) { var button = new Button { Text = text, CustomMinimumSize = new Vector2(150, 42) }; button.Pressed += action; return button; }
        private static Theme MakeTheme() { var theme = new Theme(); theme.DefaultFontSize = 17; return theme; }
    }
}
