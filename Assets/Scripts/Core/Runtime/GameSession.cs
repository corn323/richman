using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Richman.Core.Events;

namespace Richman.Core
{
    public sealed class GameSession
    {
        private static readonly MapCard[] ChanceCards =
        {
            new MapCard("chance-festival-grant", "chance", "Festival Grant", "Collect $80 from the city fund.", MapCardEffect.ReceiveMoney, 80),
            new MapCard("chance-repair-bill", "chance", "Canal Repairs", "Pay $50 for urgent canal repairs.", MapCardEffect.PayBank, 50),
            new MapCard("chance-public-art", "chance", "Public Art Award", "Collect $100 for your public art project.", MapCardEffect.ReceiveMoney, 100),
            new MapCard("chance-late-fee", "chance", "Late Fee", "Pay $75 in late fees.", MapCardEffect.PayBank, 75)
        };

        private static readonly MapCard[] DestinyCards =
        {
            new MapCard("destiny-inheritance", "destiny", "Family Inheritance", "Collect $120.", MapCardEffect.ReceiveMoney, 120),
            new MapCard("destiny-tax-audit", "destiny", "Tax Audit", "Pay $100 to the bank.", MapCardEffect.PayBank, 100),
            new MapCard("destiny-hospital", "destiny", "Recovery Order", "Skip two full turns in hospital.", MapCardEffect.Hospitalize, 2),
            new MapCard("destiny-prison", "destiny", "Court Summons", "Roll doubles to escape prison, up to three attempts.", MapCardEffect.Imprison, 3)
        };

        private readonly IDiceRoller _diceRoller;
        private readonly IGameRandom _random;
        private readonly CardDeck _chanceDeck;
        private readonly CardDeck _destinyDeck;
        // Phase 1d-3（ADR-0002）：逐格走圖的移動解析抽到獨立類別（MovementResolver.cs）。
        // 建構一次即可——Board/State 在整個 Session 生命週期內是同一個參照，Mode.StartBonus
        // 不會變。Emit 是 GameSession 自己的私有方法（蓋 Sequence/TurnNumber、登錄
        // _eventLog/_callEvents），用 lambda 包成 Action<GameEvent> 傳進去，讓事件管道仍然
        // 只有這一份實作。
        private readonly MovementResolver _movementResolver;

        // ADR-0003 決策1：事件骨架。_eventLog 是整個 GameSession 生命週期內的唯讀累積序列
        // （GameSession.EventLog 對外曝光），_callEvents 是「本次 Execute() 呼叫期間新增了哪些
        // 事件」的暫存清單，只在 Execute() 執行期間非 null，執行完畢即清空並掛回
        // CommandResult.Events。兩者裡存的是同一批事件物件參照，不是複製。
        private readonly List<GameEvent> _eventLog = new List<GameEvent>();
        private readonly ReadOnlyCollection<GameEvent> _eventLogView;
        private int _nextEventSequence = 1;
        private List<GameEvent> _callEvents;
        // Phase 1e（兩階段交易）：提案 id 全域單調遞增（貫穿整場對局，跟 _nextEventSequence
        // 同一套精神），讓 RespondTradeCommand.ProposalId 永遠對應到唯一一筆提案，不會跟
        // 同一局內更早、已經被回應/過期的提案搞混。
        private int _nextTradeProposalId = 1;

        // IRng 強制注入（ADR-0003 決策5 / docs/AUDIT.md Blocker B1）：random 不再有預設值、
        // 不再 fallback 到 Environment.TickCount。呼叫端必須明確決定並記錄 seed 的來源，
        // 否則同一份卡牌/法院亂數序列在每次啟動時都會不同，破壞可重播性。
        public GameSession(GameModeConfig mode, BoardDefinition board, IReadOnlyList<PlayerSetup> playerSetups, IDiceRoller diceRoller, IGameRandom random)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _diceRoller = diceRoller ?? throw new ArgumentNullException(nameof(diceRoller));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _chanceDeck = new CardDeck(ChanceCards, _random);
            _destinyDeck = new CardDeck(DestinyCards, _random);
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
            _eventLogView = _eventLog.AsReadOnly();
            _movementResolver = new MovementResolver(Board, State, Mode.StartBonus, e => Emit(e));
        }

        public GameModeConfig Mode { get; }
        public BoardDefinition Board { get; }
        public GameState State { get; }

        // ADR-0003 決策1/2：全局累積、唯讀的事件序列，供重播/展示層消費。
        public IReadOnlyList<GameEvent> EventLog => _eventLogView;

        // 蓋上 Sequence（全域單調遞增，貫穿整場對局）與 TurnNumber（事件建立當下的
        // GameState.TurnNumber 快照），登錄進 _eventLog，並在 Execute() 執行期間同時
        // 累積進 _callEvents（供本次 CommandResult.Events 使用）。
        private T Emit<T>(T gameEvent) where T : GameEvent
        {
            gameEvent.Sequence = _nextEventSequence++;
            gameEvent.TurnNumber = State.TurnNumber;
            _eventLog.Add(gameEvent);
            _callEvents?.Add(gameEvent);
            return gameEvent;
        }

        // ADR-0003 決策4：8 階段回合狀態機的事件層敘事（TurnStart→PreRoll→Roll→Move→Land→
        // ResolveTile→PostAction→TurnEnd）。這些 PhaseChanged 事件只是疊加在既有流程上的
        // 觀察點，刻意不引入一個真正的狀態機物件去取代現有的 if/else 流程控制——對外
        // GameState.Phase（AwaitingRoll/AwaitingAction/AwaitingEndTurn/Finished）的值與
        // 改變時機完全不受影響，黃金雜湊不序列化 TurnStage，所以這裡的新增對它不可見。
        private void EmitPhase(TurnStage stage) => Emit(new PhaseChanged(stage));

        // ADR-0003 決策7：單一合法命令來源，供 HUD／Bot 共用，取代各自重複的
        // 「現在能做什麼」判斷（docs/AUDIT.md Major：GodotGameplayHud.cs:155-181、
        // GameplayHUD.cs:153-165、Bots.cs:19-38）。實際邏輯在 GameState.GetLegalCommands，
        // 這裡只是 GameSession 對外的便利入口。
        public IReadOnlyList<LegalCommandOption> GetLegalCommands(int playerId) => State.GetLegalCommands(playerId);

        // Phase 1d-2（條件邊）：目前唯一的呼叫端是測試與（未來的）Phase 3 Effect 系統，
        // 刻意不走 Command/Execute 管線——改變 BoardFlags 本身不是玩家的回合內動作，而是
        // 地圖規則/事件的副作用，不需要走「命令→驗證→事件」那一套回合制檢查。回傳值是
        // GameState.AddBoardFlag/RemoveBoardFlag 的「狀態是否真的改變」語意，供呼叫端判斷
        // 是否真的發生了變化；沒有變化（flag 早已是該狀態）就不會發 BoardFlagChanged。
        internal bool SetBoardFlag(string flag)
        {
            if (!State.AddBoardFlag(flag)) return false;
            Emit(new BoardFlagChanged(flag, true));
            return true;
        }

        internal bool ClearBoardFlag(string flag)
        {
            if (!State.RemoveBoardFlag(flag)) return false;
            Emit(new BoardFlagChanged(flag, false));
            return true;
        }

        private sealed class CardDeck
        {
            private readonly List<MapCard> _drawPile;
            private readonly List<MapCard> _discardPile = new List<MapCard>();

            public CardDeck(MapCard[] cards, IGameRandom random)
            {
                _drawPile = new List<MapCard>(cards);
                ShuffleDrawPile(random);
            }

            public MapCard Draw(IGameRandom random)
            {
                if (_drawPile.Count == 0)
                {
                    _drawPile.AddRange(_discardPile);
                    _discardPile.Clear();
                    ShuffleDrawPile(random);
                }

                var drawn = _drawPile[0];
                _drawPile.RemoveAt(0);
                _discardPile.Add(drawn);
                return drawn;
            }

            private void ShuffleDrawPile(IGameRandom random)
            {
                for (var i = _drawPile.Count - 1; i > 0; i--)
                {
                    var j = random.NextInt(0, i + 1);
                    var card = _drawPile[i];
                    _drawPile[i] = _drawPile[j];
                    _drawPile[j] = card;
                }
            }
        }

        public CommandResult Execute(GameCommand command)
        {
            if (command == null) return CommandResult.Failure(CommandErrorCode.InvalidCommand, "A command is required.");
            if (State.Phase == TurnPhase.Finished) return CommandResult.Failure(CommandErrorCode.GameFinished, "The game has already finished.");

            var player = State.GetPlayer(command.PlayerId);
            if (player == null) return CommandResult.Failure(CommandErrorCode.PlayerNotFound, "The player does not belong to this session.");
            if (player.Status != PlayerStatus.Active) return CommandResult.Failure(CommandErrorCode.PlayerEliminated, "The player is no longer active.");

            // ADR-0003 決策1：每次 Execute() 呼叫期間暫存本次新增的事件，執行完畢後掛回
            // CommandResult.Events（只有成功的命令才附帶——驗證失敗時本來就不會有任何事件）。
            _callEvents = new List<GameEvent>();
            CommandResult result;
            if (command is RollDiceCommand) result = ExecuteRoll(player);
            else if (command is BuyPropertyCommand) result = ExecuteBuy(player);
            else if (command is UpgradePropertyCommand upgrade) result = ExecuteUpgrade(player, upgrade);
            else if (command is ChooseRouteCommand chooseRoute) result = ExecuteChooseRoute(player, chooseRoute);
            else if (command is ResolveDecisionCommand resolveDecision) result = ExecuteResolveDecision(player, resolveDecision);
            else if (command is ProposeTradeCommand proposeTrade) result = ExecuteProposeTrade(player, proposeTrade);
            else if (command is RespondTradeCommand respondTrade) result = ExecuteRespondTrade(player, respondTrade);
            else if (command is EndTurnCommand) result = ExecuteEndTurn(player);
            else result = CommandResult.Failure(CommandErrorCode.InvalidCommand, "The command type is not supported by M1.");

            var callEvents = _callEvents;
            _callEvents = null;
            return result.Succeeded ? result.WithEvents(callEvents) : result;
        }

        private CommandResult ExecuteRoll(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingRoll)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "The player must finish the current turn before rolling again.");
            }

            EmitPhase(TurnStage.PreRoll);
            State.LastDrawnCard = null;
            State.LastCourtDiceResult = null;
            State.LastEventMessage = string.Empty;
            State.MovementStepsRemaining = 0;
            State.SetRouteChoices(null);
            State.CanUpgradeOwnedProperty = false;
            if (player.IsInPrison)
            {
                var prisonFrom = player.CurrentTileIndex;
                EmitPhase(TurnStage.Roll);
                var prisonDice = _diceRoller.Roll();
                Emit(new DiceRolled(player.Id, prisonDice.First, prisonDice.Second, DiceRollPurpose.PrisonEscape));
                State.LastDiceResult = prisonDice;
                State.PendingAction = PendingAction.None;
                State.Phase = TurnPhase.AwaitingEndTurn;
                if (prisonDice.First == prisonDice.Second)
                {
                    player.ReleaseFromPrison();
                    Emit(new PrisonEscapeAttempted(player.Id, true, 0));
                    State.Phase = TurnPhase.AwaitingRoll;
                    State.LastEventMessage = "Doubles released you from prison; move this turn.";
                    State.MovementStepsRemaining = prisonDice.Total;
                    var prisonPath = new List<int>(prisonDice.Total);
                    _movementResolver.ContinueMovement(player, prisonPath);
                    if (State.MovementStepsRemaining == 0) ResolveLanding(player, Board.GetTileAt(player.CurrentTileIndex));
                    return CommandResult.Success(prisonDice, prisonFrom, player.CurrentTileIndex, prisonPath.AsReadOnly());
                }

                player.FailPrisonAttempt();
                Emit(new PrisonEscapeAttempted(player.Id, false, player.PrisonAttemptsRemaining));
                if (!player.IsInPrison)
                {
                    var couldPayBail = player.Money >= 100;
                    PayToBank(player, 100, MoneyChangeReason.PrisonBail);
                    State.LastEventMessage = couldPayBail
                        ? "Three failed prison attempts; $100 bail paid. You are released."
                        : "Three failed prison attempts; unable to pay $100 bail. Bankruptcy declared.";
                    if (State.Phase != TurnPhase.Finished && player.Status != PlayerStatus.Active)
                        AdvanceTurn();
                }
                else State.LastEventMessage = "Prison escape failed; " + player.PrisonAttemptsRemaining + " attempt(s) remain.";
                if (State.Phase != TurnPhase.Finished && player.Status == PlayerStatus.Active)
                    EnterPostAction();
                return CommandResult.Success(prisonDice, prisonFrom, player.CurrentTileIndex, Array.Empty<int>());
            }
            if (player.HospitalTurnsRemaining > 0)
            {
                player.ServeHospitalTurn();
                Emit(new TurnSkipped(player.Id, TurnSkipReason.Hospital, player.HospitalTurnsRemaining));
                State.LastDiceResult = null;
                EnterPostAction();
                State.LastEventMessage = "Hospital stay: this turn is skipped (" + player.HospitalTurnsRemaining + " turn(s) left).";
                return CommandResult.Success();
            }

            EmitPhase(TurnStage.Roll);
            var dice = _diceRoller.Roll();
            Emit(new DiceRolled(player.Id, dice.First, dice.Second, DiceRollPurpose.Movement));
            var from = player.CurrentTileIndex;
            State.LastDiceResult = dice;
            State.MovementStepsRemaining = dice.Total;
            var path = new List<int>(dice.Total);
            _movementResolver.ContinueMovement(player, path);
            if (State.MovementStepsRemaining == 0) ResolveLanding(player, Board.GetTileAt(player.CurrentTileIndex));
            return CommandResult.Success(dice, from, player.CurrentTileIndex, path.AsReadOnly());
        }

        private CommandResult ExecuteChooseRoute(PlayerState player, ChooseRouteCommand command)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase != TurnPhase.AwaitingAction || State.PendingAction != PendingAction.ChooseRoute || State.MovementStepsRemaining <= 0)
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "There is no route choice waiting.");
            var legal = false;
            for (var i = 0; i < State.RouteChoiceTileIds.Count; i++)
                if (string.Equals(State.RouteChoiceTileIds[i], command.NextTileId, StringComparison.Ordinal)) legal = true;
            if (!legal) return CommandResult.Failure(CommandErrorCode.InvalidRouteChoice, "The selected route is not connected to this junction.");

            Emit(new DecisionResolved(DecisionKind.ChooseRoute, player.Id, command.NextTileId));
            var from = player.CurrentTileIndex;
            State.SetRouteChoices(null);
            State.PendingAction = PendingAction.None;
            State.LastEventMessage = string.Empty;
            var path = new List<int>(State.MovementStepsRemaining);
            _movementResolver.MoveOneStep(player, Board.GetTile(command.NextTileId), path);
            _movementResolver.ContinueMovement(player, path);
            if (State.MovementStepsRemaining == 0) ResolveLanding(player, Board.GetTileAt(player.CurrentTileIndex));
            return CommandResult.Success(null, from, player.CurrentTileIndex, path.AsReadOnly());
        }

        // ADR-0003 決策3：ResolveDecisionCommand 的實際解析路徑。刻意不重新實作任何一種
        // 決策的邏輯，而是把 optionId 轉譯成對應的語法糖命令後，直接呼叫同一個
        // ExecuteBuy/ExecuteUpgrade/ExecuteChooseRoute——兩種命令因此保證是同一份實作、
        // 同一組事件，差別只在呼叫端用哪種命令型別表達同一個意圖。
        // State.PendingDecision 本身是投影（見 GameModel.cs），Phase/PendingAction 在任一時刻
        // 只會對應到最多一種 DecisionKind，所以「一次只允許一個未決 decision」不需要額外狀態，
        // 是這個投影設計自動保證的。
        private CommandResult ExecuteResolveDecision(PlayerState player, ResolveDecisionCommand command)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;

            var decision = State.PendingDecision;
            if (decision == null)
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "There is no pending decision to resolve.");
            if (!decision.ContainsOption(command.OptionId))
                return CommandResult.Failure(CommandErrorCode.InvalidDecisionOption, "The selected option is not part of the pending decision.");

            switch (decision.Kind)
            {
                case DecisionKind.ChooseRoute:
                    return ExecuteChooseRoute(player, new ChooseRouteCommand(player.Id, command.OptionId));
                case DecisionKind.BuyProperty:
                    return ExecuteBuy(player);
                case DecisionKind.UpgradeProperty:
                    return ExecuteUpgrade(player, new UpgradePropertyCommand(player.Id, command.OptionId));
                default:
                    return CommandResult.Failure(CommandErrorCode.InvalidCommand, "Unsupported decision kind.");
            }
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

            Emit(new DecisionResolved(DecisionKind.BuyProperty, player.Id, tile.Id));
            player.Spend(tile.PurchasePrice);
            Emit(new MoneyChanged(player.Id, -tile.PurchasePrice, player.Money, MoneyChangeReason.PropertyPurchase));
            property.SetOwner(player.Id);
            Emit(new PropertyPurchased(player.Id, tile.Id, tile.PurchasePrice));
            EnterPostAction();
            State.LastEventMessage = "已購買「" + tile.DisplayName + "」。";
            if (Board.GetDistrictPropertyCount(tile.DistrictId) > 1 && OwnsCompleteDistrict(player.Id, tile.DistrictId) && State.TryMarkDistrictRewardPaid(player.Id, tile.DistrictId))
            {
                player.AddMoney(Mode.CompleteDistrictReward);
                Emit(new MoneyChanged(player.Id, Mode.CompleteDistrictReward, player.Money, MoneyChangeReason.DistrictReward));
                State.LastEventMessage += " 集齊區域，獲得旅遊獎勵 $" + Mode.CompleteDistrictReward + "。";
            }
            return CommandResult.Success();
        }

        private CommandResult ExecuteUpgrade(PlayerState player, UpgradePropertyCommand command)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            var startUpgrade = State.CanUpgradeOwnedProperty &&
                (State.Phase == TurnPhase.AwaitingAction || State.Phase == TurnPhase.AwaitingEndTurn);
            // ADR-0003 決策3：只有「落地自有地產」觸發的強制 UpgradeProperty 待決才算
            // PendingDecision；CanUpgradeOwnedProperty 觸發的「停在 Start 可任選一塊自有地產
            // 升級」是可選動作，不建模為 PendingDecision（見 PendingDecision.cs 的範圍說明），
            // 所以這裡要在狀態被改掉之前先記住是哪一種，才能正確決定要不要發 DecisionResolved。
            var isPendingDecision = State.Phase == TurnPhase.AwaitingAction && State.PendingAction == PendingAction.UpgradeProperty;
            if (!isPendingDecision && !startUpgrade)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "There is no property available to upgrade right now.");
            }

            var tile = string.IsNullOrEmpty(command.PropertyId) ? Board.GetTileAt(player.CurrentTileIndex) : Board.GetTile(command.PropertyId);
            var property = tile == null ? null : State.GetProperty(tile.Id);
            if (tile == null || property == null || property.OwnerId != player.Id)
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

            if (isPendingDecision) Emit(new DecisionResolved(DecisionKind.UpgradeProperty, player.Id, tile.Id));
            player.Spend(tile.UpgradeCost);
            Emit(new MoneyChanged(player.Id, -tile.UpgradeCost, player.Money, MoneyChangeReason.PropertyUpgrade));
            property.Upgrade();
            Emit(new PropertyUpgraded(player.Id, tile.Id, property.UpgradeLevel));
            State.CanUpgradeOwnedProperty = false;
            EnterPostAction();
            return CommandResult.Success();
        }

        private CommandResult ExecuteEndTurn(PlayerState player)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if ((State.Phase != TurnPhase.AwaitingAction && State.Phase != TurnPhase.AwaitingEndTurn) || State.PendingAction == PendingAction.ChooseRoute)
            {
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "The player has not rolled yet.");
            }

            State.PendingAction = PendingAction.None;
            State.MovementStepsRemaining = 0;
            State.SetRouteChoices(null);
            AdvanceTurn();
            return CommandResult.Success();
        }

        // Phase 1e（ADR-0003 決策8 / SEC-CMD-02,03，授權行為變更）：兩階段交易的第一階段。
        // 驗證通過後只建立 pending proposal（拍下雙方資產快照），不搬動任何金錢或地產——
        // 真正的異動留給 ExecuteRespondTrade。驗證邏輯跟舊版單階段 TradeCommand 完全相同
        // （逐條照搬），只是驗證通過後的結果從「立刻執行」改成「記錄一筆提案」。
        private CommandResult ExecuteProposeTrade(PlayerState player, ProposeTradeCommand command)
        {
            var turnCheck = ValidateCurrentPlayer(player);
            if (turnCheck != null) return turnCheck;
            if (State.Phase == TurnPhase.Finished || State.Phase == TurnPhase.AwaitingAction && State.PendingAction == PendingAction.ChooseRoute)
                return CommandResult.Failure(CommandErrorCode.InvalidPhase, "Finish the required movement choice before proposing a trade.");
            if (State.PendingTradeProposal != null)
                return CommandResult.Failure(CommandErrorCode.TradeProposalAlreadyPending, "Only one trade proposal can be pending at a time.");
            if (State.TradeProposalsThisTurn >= Mode.MaxTradeProposalsPerTurn)
                return CommandResult.Failure(CommandErrorCode.TradeProposalLimitReached, "This turn has already reached its trade proposal limit.");

            var target = State.GetPlayer(command.TargetPlayerId);
            if (target == null || target.Id == player.Id || target.Status != PlayerStatus.Active)
                return CommandResult.Failure(CommandErrorCode.InvalidTrade, "Choose another active player for this trade.");
            if (command.OfferedMoney > player.Money || command.RequestedMoney > target.Money)
                return CommandResult.Failure(CommandErrorCode.InsufficientFunds, "One of the players does not have enough money to complete this trade.");
            var playerFinalMoney = (long)player.Money - command.OfferedMoney + command.RequestedMoney;
            var targetFinalMoney = (long)target.Money + command.OfferedMoney - command.RequestedMoney;
            if (playerFinalMoney > int.MaxValue || targetFinalMoney > int.MaxValue)
                return CommandResult.Failure(CommandErrorCode.InvalidTrade, "This trade would exceed the money limit.");
            if (command.OfferedPropertyId == command.RequestedPropertyId && !string.IsNullOrEmpty(command.OfferedPropertyId))
                return CommandResult.Failure(CommandErrorCode.InvalidTrade, "A property cannot be traded for itself.");

            var offeredProperty = string.IsNullOrEmpty(command.OfferedPropertyId) ? null : State.GetProperty(command.OfferedPropertyId);
            var requestedProperty = string.IsNullOrEmpty(command.RequestedPropertyId) ? null : State.GetProperty(command.RequestedPropertyId);
            if (!string.IsNullOrEmpty(command.OfferedPropertyId) && (offeredProperty == null || offeredProperty.OwnerId != player.Id))
                return CommandResult.Failure(CommandErrorCode.PropertyUnavailable, "You can only offer a property you own.");
            if (!string.IsNullOrEmpty(command.RequestedPropertyId) && (requestedProperty == null || requestedProperty.OwnerId != target.Id))
                return CommandResult.Failure(CommandErrorCode.PropertyUnavailable, "The other player must own the requested property.");
            if (command.OfferedMoney == 0 && command.RequestedMoney == 0 && offeredProperty == null && requestedProperty == null)
                return CommandResult.Failure(CommandErrorCode.InvalidTrade, "A trade must exchange money or property.");

            var proposal = new TradeProposal(_nextTradeProposalId++, player.Id, target.Id, command.OfferedMoney, command.RequestedMoney,
                command.OfferedPropertyId, command.RequestedPropertyId, State.TurnNumber,
                player.Money, target.Money, offeredProperty?.OwnerId, requestedProperty?.OwnerId);
            State.SetPendingTradeProposal(proposal);
            State.TradeProposalsThisTurn++;
            Emit(new TradeProposed(proposal.ProposalId, player.Id, target.Id, command.OfferedMoney, command.RequestedMoney,
                command.OfferedPropertyId, command.RequestedPropertyId));
            State.LastEventMessage = player.DisplayName + " 向 " + target.DisplayName + " 提出交易，等待回應。";
            return CommandResult.Success();
        }

        // Phase 1e：第二階段。只有提案的 TargetId 能回應（刻意不呼叫 ValidateCurrentPlayer——
        // 目標玩家回應時通常不是目前輪到的玩家）。accept=false 一定成功（拒絕不需要資產
        // 仍然有效）；accept=true 會先把「現在」的雙方資產狀態跟提案當下的快照逐一比對。
        // 快照過期的分支刻意回傳 CommandResult.Success()（不是 Failure）：這個 RespondTrade
        // 命令本身是合法、有效處理的（目標玩家、proposalId 都對），只是處理結果是「提案
        // 已過期」而不是「交易成立」——跟 ExecuteRoll 讓玩家破產也回傳 Success 是同一個
        // 道理（CommandResult.Succeeded 代表命令有被妥善處理，不代表玩家喜歡這個結果）。
        // 用 Failure 會違反既有的「失敗命令零事件、零狀態變化」慣例（CommandResult.Events
        // 的 XML 文件），因為這裡確實有清掉 pending proposal、確實發了 TradeProposalExpired。
        // 呼叫端要分辨「交易成立」還是「提案過期」，看 result.Events 裡是哪一種事件
        // （TradeExecuted vs TradeProposalExpired），而不是看 Succeeded/ErrorCode。
        private CommandResult ExecuteRespondTrade(PlayerState player, RespondTradeCommand command)
        {
            var proposal = State.PendingTradeProposal;
            if (proposal == null || proposal.ProposalId != command.ProposalId)
                return CommandResult.Failure(CommandErrorCode.TradeProposalNotFound, "There is no matching trade proposal to respond to.");
            if (player.Id != proposal.TargetId)
                return CommandResult.Failure(CommandErrorCode.NotTradeProposalTarget, "Only the proposal's target player can respond to it.");

            if (!command.Accept)
            {
                State.ClearPendingTradeProposal();
                Emit(new TradeRejected(proposal.ProposalId, proposal.InitiatorId, proposal.TargetId));
                State.LastEventMessage = "交易提案被拒絕。";
                return CommandResult.Success();
            }

            var initiator = State.GetPlayer(proposal.InitiatorId);
            var offeredProperty = string.IsNullOrEmpty(proposal.OfferedPropertyId) ? null : State.GetProperty(proposal.OfferedPropertyId);
            var requestedProperty = string.IsNullOrEmpty(proposal.RequestedPropertyId) ? null : State.GetProperty(proposal.RequestedPropertyId);
            var snapshotStillMatches = initiator != null && initiator.Status == PlayerStatus.Active &&
                initiator.Money == proposal.InitiatorMoneySnapshot &&
                player.Money == proposal.TargetMoneySnapshot &&
                (offeredProperty?.OwnerId) == proposal.OfferedPropertyOwnerSnapshot &&
                (requestedProperty?.OwnerId) == proposal.RequestedPropertyOwnerSnapshot;
            if (!snapshotStillMatches)
            {
                State.ClearPendingTradeProposal();
                Emit(new TradeProposalExpired(proposal.ProposalId, proposal.InitiatorId, proposal.TargetId, TradeProposalExpiryReason.SnapshotStale));
                State.LastEventMessage = "交易提案已失效（雙方資產狀態已變動）。";
                return CommandResult.Success();
            }

            initiator.Spend(proposal.OfferedMoney);
            player.Spend(proposal.RequestedMoney);
            initiator.AddMoney(proposal.RequestedMoney);
            player.AddMoney(proposal.OfferedMoney);
            if (offeredProperty != null) offeredProperty.SetOwner(player.Id);
            if (requestedProperty != null) requestedProperty.SetOwner(initiator.Id);
            // 注意：四筆金額異動（兩邊的 Spend/AddMoney）在上面已經一次做完（行為不變），
            // 所以下面 MoneyChanged.NewBalance 反映的是交易「全部完成後」的最終餘額，而不是
            // 每一步的中繼餘額——兩個方向都有金額時，同一位玩家的兩筆事件會看到同一個
            // NewBalance。這是刻意的簡化（批次套用、批次回報），不影響 Delta 欄位的正確性。
            if (proposal.OfferedMoney > 0)
            {
                Emit(new MoneyChanged(initiator.Id, -proposal.OfferedMoney, initiator.Money, MoneyChangeReason.TradeOffered));
                Emit(new MoneyChanged(player.Id, proposal.OfferedMoney, player.Money, MoneyChangeReason.TradeReceived));
            }
            if (proposal.RequestedMoney > 0)
            {
                Emit(new MoneyChanged(player.Id, -proposal.RequestedMoney, player.Money, MoneyChangeReason.TradeOffered));
                Emit(new MoneyChanged(initiator.Id, proposal.RequestedMoney, initiator.Money, MoneyChangeReason.TradeReceived));
            }
            Emit(new TradeExecuted(initiator.Id, player.Id, proposal.OfferedMoney, proposal.RequestedMoney, proposal.OfferedPropertyId, proposal.RequestedPropertyId));
            State.ClearPendingTradeProposal();
            State.LastEventMessage = initiator.DisplayName + " 與 " + player.DisplayName + " 完成交易。";
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
            State.CanUpgradeOwnedProperty = tile.Type == BoardTileType.Start;
            EmitPhase(TurnStage.Land);
            Emit(new PawnLanded(player.Id, tile.PositionIndex, tile.Id, tile.Type));
            EmitPhase(TurnStage.ResolveTile);
            switch (tile.Type)
            {
                case BoardTileType.Property:
                    ResolveProperty(player, tile);
                    return;
                case BoardTileType.Tax:
                    ResolveTax(player, tile);
                    break;
                case BoardTileType.Bonus:
                    ResolveBonus(player, tile);
                    break;
                case BoardTileType.Airport:
                    ResolveAirport(player, tile);
                    break;
                case BoardTileType.Hospital:
                    ResolveHospital(player, tile);
                    break;
                case BoardTileType.Prison:
                    ResolvePrison(player);
                    break;
                case BoardTileType.Court:
                    ResolveCourt(player);
                    break;
                case BoardTileType.Chance:
                    DrawCard(player, _chanceDeck, "chance");
                    break;
                case BoardTileType.Destiny:
                    DrawCard(player, _destinyDeck, "destiny");
                    break;
            }

            FinishActionOrAdvance(player);
        }

        private void ResolveTax(PlayerState player, BoardTileDefinition tile)
        {
            PayToBank(player, tile.EffectAmount == 0 ? Mode.TaxAmount : Math.Abs(tile.EffectAmount), MoneyChangeReason.Tax);
        }

        private void ResolveBonus(PlayerState player, BoardTileDefinition tile)
        {
            var bonusAmount = tile.EffectAmount == 0 ? Mode.BonusAmount : tile.EffectAmount;
            player.AddMoney(bonusAmount);
            Emit(new MoneyChanged(player.Id, bonusAmount, player.Money, MoneyChangeReason.Bonus));
            State.LastEventMessage = tile.DisplayName + ": collected $" + bonusAmount + ".";
        }

        private void ResolveHospital(PlayerState player, BoardTileDefinition tile)
        {
            player.Hospitalize(tile.EffectAmount > 0 ? tile.EffectAmount : 2);
            Emit(new PlayerDetained(player.Id, DetentionKind.Hospital, player.HospitalTurnsRemaining));
            State.LastEventMessage = "Unexpected admission: " + player.HospitalTurnsRemaining + " full turn(s) in hospital.";
        }

        private void ResolvePrison(PlayerState player)
        {
            player.Imprison(3);
            Emit(new PlayerDetained(player.Id, DetentionKind.Prison, player.PrisonAttemptsRemaining));
            State.LastEventMessage = "Sent to prison: roll doubles to escape, up to three attempts.";
        }

        private void ResolveCourt(PlayerState player)
        {
            var courtDice = _diceRoller.Roll();
            Emit(new DiceRolled(player.Id, courtDice.First, courtDice.Second, DiceRollPurpose.CourtJudgment));
            State.LastCourtDiceResult = courtDice;
            var courtRoll = courtDice.First + " + " + courtDice.Second + " = " + courtDice.Total;
            if (courtDice.Total >= 7)
            {
                State.LastEventMessage = "Court acquittal: " + courtRoll + ".";
                Emit(new CourtRuled(player.Id, courtDice.Total, CourtRuling.Acquittal, 0));
            }
            else if (_random.NextInt(0, 2) == 0)
            {
                player.Imprison(3);
                Emit(new PlayerDetained(player.Id, DetentionKind.Prison, player.PrisonAttemptsRemaining));
                State.LastEventMessage = "Court judgment: " + courtRoll + "; sent to prison.";
                Emit(new CourtRuled(player.Id, courtDice.Total, CourtRuling.Prison, 0));
            }
            else
            {
                PayToBank(player, 100, MoneyChangeReason.CourtFine);
                State.LastEventMessage = "Court judgment: " + courtRoll + "; paid $100" + (player.Status == PlayerStatus.Bankrupt ? "; bankruptcy declared." : " fine.");
                Emit(new CourtRuled(player.Id, courtDice.Total, CourtRuling.Fine, 100));
            }
        }

        private void ResolveAirport(PlayerState player, BoardTileDefinition airport)
        {
            var destination = Board.GetTile(airport.AirportDestinationId);
            if (destination == null || destination.Type != BoardTileType.Airport)
                throw new InvalidOperationException("Airport destination is missing or is not an airport: " + airport.Id + " -> " + airport.AirportDestinationId);
            var from = player.CurrentTileIndex;
            player.MoveTo(destination.PositionIndex);
            Emit(new PlayerTeleported(player.Id, from, destination.PositionIndex, TeleportReason.Airport));
            State.LastEventMessage = "機場航線：「" + airport.DisplayName + "」飛往「" + destination.DisplayName + "」。";
        }

        private void DrawCard(PlayerState player, CardDeck deck, string deckId)
        {
            if (deck == null) throw new InvalidOperationException("A map card deck cannot be empty.");
            var card = deck.Draw(_random);
            State.LastDrawnCard = card;
            Emit(new CardDrawn(player.Id, card.Id, deckId));
            string effectResult;

            switch (card.Effect)
            {
                case MapCardEffect.ReceiveMoney:
                    player.AddMoney(card.Amount);
                    Emit(new MoneyChanged(player.Id, card.Amount, player.Money, MoneyChangeReason.CardEffect));
                    effectResult = "Received $" + card.Amount + "; balance is $" + player.Money + ".";
                    break;
                case MapCardEffect.PayBank:
                    PayToBank(player, card.Amount, MoneyChangeReason.CardEffect);
                    effectResult = player.Status == PlayerStatus.Bankrupt
                        ? "Could not pay $" + card.Amount + "; bankruptcy declared. Balance is $" + player.Money + "."
                        : "Paid $" + card.Amount + "; balance is $" + player.Money + ".";
                    break;
                case MapCardEffect.Hospitalize:
                    player.Hospitalize(card.Amount);
                    Emit(new PlayerDetained(player.Id, DetentionKind.Hospital, player.HospitalTurnsRemaining));
                    effectResult = "Hospital stay set to " + card.Amount + " full turn(s).";
                    break;
                case MapCardEffect.Imprison:
                    player.Imprison(3);
                    Emit(new PlayerDetained(player.Id, DetentionKind.Prison, player.PrisonAttemptsRemaining));
                    effectResult = "Sent to prison with " + player.PrisonAttemptsRemaining + " escape attempt(s).";
                    break;
                default:
                    throw new InvalidOperationException("Unsupported map card effect: " + card.Effect);
            }

            Emit(new CardEffectApplied(player.Id, card.Id, card.Effect, card.Amount));
            State.LastEventMessage = deckId.ToUpperInvariant() + " CARD — " + card.Title + ": " + card.Description + " Result: " + effectResult;
        }

        private void ResolveProperty(PlayerState player, BoardTileDefinition tile)
        {
            var property = State.GetProperty(tile.Id);
            if (property == null) throw new InvalidOperationException("The board property has no state.");

            if (!property.OwnerId.HasValue)
            {
                State.PendingAction = PendingAction.BuyProperty;
                State.Phase = TurnPhase.AwaitingAction;
                Emit(new DecisionRequested(DecisionKind.BuyProperty, player.Id, new[] { tile.Id }, tile.Id));
                return;
            }

            if (property.OwnerId.Value == player.Id)
            {
                State.PendingAction = PendingAction.UpgradeProperty;
                State.Phase = TurnPhase.AwaitingAction;
                Emit(new DecisionRequested(DecisionKind.UpgradeProperty, player.Id, new[] { tile.Id }, tile.Id));
                return;
            }

            var owner = State.GetPlayer(property.OwnerId.Value);
            if (owner == null || owner.Status != PlayerStatus.Active) throw new InvalidOperationException("A property owner must be active.");
            var baseRent = tile.GetRent(property.UpgradeLevel);
            var rent = baseRent;
            var districtBonusAmount = 0;
            if (Board.GetDistrictPropertyCount(tile.DistrictId) > 1 && OwnsCompleteDistrict(owner.Id, tile.DistrictId))
            {
                rent = (rent * (100 + Mode.CompleteDistrictRentBonusPercent) + 99) / 100;
                districtBonusAmount = rent - baseRent;
            }
            if (player.Money >= rent)
            {
                player.Spend(rent);
                owner.AddMoney(rent);
                Emit(new MoneyChanged(player.Id, -rent, player.Money, MoneyChangeReason.RentPaid));
                Emit(new MoneyChanged(owner.Id, rent, owner.Money, MoneyChangeReason.RentReceived));
                Emit(new RentPaid(player.Id, owner.Id, tile.Id, rent, baseRent, districtBonusAmount));
            }
            else
            {
                var taken = player.TakeAllMoney();
                owner.AddMoney(taken);
                if (taken > 0)
                {
                    Emit(new MoneyChanged(player.Id, -taken, player.Money, MoneyChangeReason.RentPaid));
                    Emit(new MoneyChanged(owner.Id, taken, owner.Money, MoneyChangeReason.RentReceived));
                }
                DeclareBankruptcy(player, owner.Id, taken);
            }

            FinishActionOrAdvance(player);
        }

        private void PayToBank(PlayerState player, int amount, MoneyChangeReason reason)
        {
            if (amount <= 0) return;
            if (player.Money >= amount)
            {
                player.Spend(amount);
                Emit(new MoneyChanged(player.Id, -amount, player.Money, reason));
                return;
            }

            var taken = player.TakeAllMoney();
            if (taken > 0) Emit(new MoneyChanged(player.Id, -taken, player.Money, reason));
            DeclareBankruptcy(player, null, taken);
        }

        private bool OwnsCompleteDistrict(int playerId, string districtId)
        {
            var count = 0;
            for (var i = 0; i < Board.TileCount; i++)
            {
                var tile = Board.GetTileAt(i);
                if (tile.Type != BoardTileType.Property || !string.Equals(tile.DistrictId, districtId, StringComparison.Ordinal)) continue;
                count++;
                var property = State.GetProperty(tile.Id);
                if (property == null || property.OwnerId != playerId) return false;
            }
            return count > 1;
        }

        private void DeclareBankruptcy(PlayerState player, int? creditorId, int transferredMoney)
        {
            var properties = State.Properties;
            List<string> transferredPropertyIds = null;
            for (var i = 0; i < properties.Count; i++)
            {
                if (properties[i].OwnerId == player.Id)
                {
                    (transferredPropertyIds ??= new List<string>()).Add(properties[i].PropertyId);
                    properties[i].SetOwner(creditorId);
                    // Phase 1e（授權行為變更）：歸還銀行（creditorId==null）時重設升級等級——
                    // 銀行收回視為「這塊地產回到起點」，下一個買家不應該撿到已經升級好的資產。
                    // 轉給債權玩家（creditorId 有值）則刻意維持現狀：債權人是在這筆債務清償中
                    // 接收地產，視為「接收完整資產」，不重設等級——這是刻意規則，不是 bug。
                    if (creditorId == null) properties[i].ResetUpgradeLevel();
                }
            }

            player.MarkBankrupt();
            Emit(new PlayerBankrupt(player.Id, creditorId, transferredMoney, transferredPropertyIds?.AsReadOnly()));
            State.PendingAction = PendingAction.None;
            CheckVictory();
        }

        // ADR-0003 決策4：落地格的強制解析（ResolveTile 階段）全部跑完、且玩家仍是 active
        // 時，進入 PostAction 階段——對外就是 Phase=AwaitingEndTurn，等玩家主動 EndTurn
        // 或執行選擇性動作（如 CanUpgradeOwnedProperty）。PendingAction 設為 None、
        // Phase 設為 AwaitingEndTurn 的值與時機完全不變，只是多發一個 PhaseChanged 事件。
        private void EnterPostAction()
        {
            State.PendingAction = PendingAction.None;
            State.Phase = TurnPhase.AwaitingEndTurn;
            EmitPhase(TurnStage.PostAction);
        }

        private void FinishActionOrAdvance(PlayerState player)
        {
            if (State.Phase == TurnPhase.Finished) return;
            if (player.Status != PlayerStatus.Active)
            {
                AdvanceTurn();
                return;
            }

            EnterPostAction();
        }

        private void AdvanceTurn()
        {
            if (State.Phase == TurnPhase.Finished) return;
            // Phase 1e（兩階段交易）：提案者（永遠是這個正在結束的回合的 CurrentPlayerId——
            // ProposeTradeCommand 要求 ValidateCurrentPlayer）的回合結束時，任何還沒被回應的
            // 提案即刻失效。SimpleBotStrategy 從不提案，所以 Bot 對局裡
            // State.PendingTradeProposal 永遠是 null，這個分支對黃金雜湊零影響。
            if (State.PendingTradeProposal != null)
            {
                var expiring = State.PendingTradeProposal;
                State.ClearPendingTradeProposal();
                Emit(new TradeProposalExpired(expiring.ProposalId, expiring.InitiatorId, expiring.TargetId, TradeProposalExpiryReason.TurnEnded));
            }
            EmitPhase(TurnStage.TurnEnd);
            Emit(new TurnEnded(State.CurrentPlayerId, State.TurnNumber));
            State.CanUpgradeOwnedProperty = false;
            CheckVictory();
            if (State.Phase == TurnPhase.Finished) return;
            if (State.TurnNumber >= Mode.TurnLimit)
            {
                FinishByNetWorth();
                return;
            }

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
                    State.TradeProposalsThisTurn = 0;
                    EmitPhase(TurnStage.TurnStart);
                    Emit(new TurnStarted(State.TurnNumber, candidate.Id));
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
            State.Outcome = new GameOutcome(lastActive.Id, State.TurnNumber, VictoryReason.LastPlayerStanding);
            State.Phase = TurnPhase.Finished;
            State.PendingAction = PendingAction.None;
            Emit(new GameFinished(lastActive.Id, State.TurnNumber, VictoryReason.LastPlayerStanding));
        }

        private void FinishByNetWorth()
        {
            PlayerState winner = null;
            long bestWorth = long.MinValue;
            for (var i = 0; i < State.Players.Count; i++)
            {
                var player = State.Players[i];
                if (player.Status != PlayerStatus.Active) continue;
                long worth = player.Money;
                for (var propertyIndex = 0; propertyIndex < State.Properties.Count; propertyIndex++)
                {
                    var property = State.Properties[propertyIndex];
                    if (property.OwnerId != player.Id) continue;
                    var tile = Board.GetProperty(property.PropertyId);
                    worth += tile.PurchasePrice / 2L + (long)tile.UpgradeCost * property.UpgradeLevel / 2L;
                }
                if (winner == null || worth > bestWorth || (worth == bestWorth && player.Money > winner.Money))
                {
                    winner = player;
                    bestWorth = worth;
                }
            }
            if (winner == null) throw new InvalidOperationException("No active player remains at the turn limit.");
            winner.MarkWinner();
            State.CurrentPlayerId = winner.Id;
            State.Outcome = new GameOutcome(winner.Id, State.TurnNumber, VictoryReason.HighestNetWorthAtTurnLimit);
            State.Phase = TurnPhase.Finished;
            State.PendingAction = PendingAction.None;
            State.LastEventMessage = "旅程結算：依現金與資產淨值選出優勝者。";
            Emit(new GameFinished(winner.Id, State.TurnNumber, VictoryReason.HighestNetWorthAtTurnLimit));
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

            var random = new SeededRandom(seed);
            return new GameSession(mode, PrototypeBoardFactory.Create(), players, new SeededDiceRoller(random), random);
        }
    }
}
