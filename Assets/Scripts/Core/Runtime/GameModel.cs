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
            int turnLimit = int.MaxValue,
            int maxTradeProposalsPerTurn = 3)
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
            if (maxTradeProposalsPerTurn < 1) throw new ArgumentOutOfRangeException(nameof(maxTradeProposalsPerTurn));

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
            MaxTradeProposalsPerTurn = maxTradeProposalsPerTurn;
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
        // Phase 1e（ADR-0003 決策8）：同一位玩家在同一次輪到自己的回合內，最多能提出幾次
        // 交易提案（不論最後是被接受、拒絕還是因快照失效而作廢，都算一次）。
        public int MaxTradeProposalsPerTurn { get; }

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
        public int HospitalTurnsRemaining { get; private set; }
        public int PrisonAttemptsRemaining { get; private set; }
        public bool IsInPrison => PrisonAttemptsRemaining > 0;

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

        internal void Hospitalize(int turns)
        {
            HospitalTurnsRemaining = Math.Max(HospitalTurnsRemaining, turns);
            DetentionReason = "Hospital";
            DetainedTurnsRemaining = HospitalTurnsRemaining;
        }

        internal void Imprison(int attempts)
        {
            PrisonAttemptsRemaining = Math.Max(PrisonAttemptsRemaining, attempts);
            DetentionReason = "Prison";
            DetainedTurnsRemaining = PrisonAttemptsRemaining;
        }

        internal void ServeHospitalTurn()
        {
            if (HospitalTurnsRemaining > 0) HospitalTurnsRemaining--;
            DetainedTurnsRemaining = HospitalTurnsRemaining;
            if (HospitalTurnsRemaining == 0) DetentionReason = string.Empty;
        }

        internal void FailPrisonAttempt()
        {
            if (PrisonAttemptsRemaining > 0) PrisonAttemptsRemaining--;
            DetainedTurnsRemaining = PrisonAttemptsRemaining;
            if (PrisonAttemptsRemaining == 0) DetentionReason = string.Empty;
        }

        internal void ReleaseFromPrison()
        {
            PrisonAttemptsRemaining = 0;
            DetainedTurnsRemaining = 0;
            DetentionReason = string.Empty;
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

        // Phase 1e（授權行為變更）：銀行收回破產玩家的地產時呼叫（見 GameSession.
        // DeclareBankruptcy）——重設回從未被升級過的狀態，而不是讓下一個買家撿到便宜。
        internal void ResetUpgradeLevel()
        {
            UpgradeLevel = 0;
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

    // Phase 1e（ADR-0003 決策8 / SEC-CMD-02,03）：兩階段交易的待決提案。由
    // GameSession.ExecuteProposeTrade 建立，存進 GameState.PendingTradeProposal（一次只允許
    // 一筆）。*Snapshot 系列欄位是提案當下雙方的資產快照——ExecuteRespondTrade 接受時會
    // 拿「現在」的值跟這份快照逐一比對，任何一個對不上就視為提案已失效
    // （TradeProposalExpired，Reason=SnapshotStale），不會执行任何資源異動。
    public sealed class TradeProposal
    {
        internal TradeProposal(
            int proposalId, int initiatorId, int targetId, int offeredMoney, int requestedMoney,
            string offeredPropertyId, string requestedPropertyId, int proposedTurnNumber,
            int initiatorMoneySnapshot, int targetMoneySnapshot,
            int? offeredPropertyOwnerSnapshot, int? requestedPropertyOwnerSnapshot)
        {
            ProposalId = proposalId;
            InitiatorId = initiatorId;
            TargetId = targetId;
            OfferedMoney = offeredMoney;
            RequestedMoney = requestedMoney;
            OfferedPropertyId = offeredPropertyId ?? string.Empty;
            RequestedPropertyId = requestedPropertyId ?? string.Empty;
            ProposedTurnNumber = proposedTurnNumber;
            InitiatorMoneySnapshot = initiatorMoneySnapshot;
            TargetMoneySnapshot = targetMoneySnapshot;
            OfferedPropertyOwnerSnapshot = offeredPropertyOwnerSnapshot;
            RequestedPropertyOwnerSnapshot = requestedPropertyOwnerSnapshot;
        }

        public int ProposalId { get; }
        public int InitiatorId { get; }
        public int TargetId { get; }
        public int OfferedMoney { get; }
        public int RequestedMoney { get; }
        public string OfferedPropertyId { get; }
        public string RequestedPropertyId { get; }
        public int ProposedTurnNumber { get; }
        internal int InitiatorMoneySnapshot { get; }
        internal int TargetMoneySnapshot { get; }
        internal int? OfferedPropertyOwnerSnapshot { get; }
        internal int? RequestedPropertyOwnerSnapshot { get; }
    }

    public sealed class GameState
    {
        private readonly List<PlayerState> _players;
        private readonly List<PropertyState> _properties;
        private readonly ReadOnlyCollection<PlayerState> _playersView;
        private readonly ReadOnlyCollection<PropertyState> _propertiesView;
        private readonly HashSet<string> _districtRewardsPaid = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> _boardFlags = new HashSet<string>(StringComparer.Ordinal);
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
            // Phase 1d-2：地圖定義的 InitialFlags 是一開局就開啟的 BoardFlags；之後的變動
            // 一律經由 SetBoardFlag/ClearBoardFlag（GameSession 呼叫，會發 BoardFlagChanged）。
            foreach (var flag in board.InitialFlags) _boardFlags.Add(flag);
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
        public DiceResult LastCourtDiceResult { get; internal set; }
        public MapCard LastDrawnCard { get; internal set; }
        public string LastEventMessage { get; internal set; } = string.Empty;
        public int MovementStepsRemaining { get; internal set; }
        public bool CanUpgradeOwnedProperty { get; internal set; }
        public IReadOnlyList<string> RouteChoiceTileIds => _routeChoiceTileIdsView;
        public GameOutcome Outcome { get; internal set; }

        // Phase 1e：目前唯一一筆未決的交易提案（null＝沒有）。刻意不是 PendingDecision——
        // 提案不阻塞發起方的回合，發起方送出提案後仍能繼續擲骰/升級/結束回合。
        public TradeProposal PendingTradeProposal { get; private set; }
        // 目前這一回合（CurrentPlayerId 這一輪）已經提出過幾次交易提案，AdvanceTurn 換人時
        // 歸零（見 GameSession.AdvanceTurn）。
        public int TradeProposalsThisTurn { get; internal set; }

        internal void SetPendingTradeProposal(TradeProposal proposal) => PendingTradeProposal = proposal;
        internal void ClearPendingTradeProposal() => PendingTradeProposal = null;

        // Phase 1d-2（條件邊）：目前開啟的 BoardFlags，由地圖的 InitialFlags 播種，之後
        // 經由 GameSession.SetBoardFlag/ClearBoardFlag 變動（內部呼叫，Phase 3 Effect 系統
        // 接手後會是唯一的呼叫端）。HasBoardFlag 是移動解析（GetOpenNextTiles）實際查詢的
        // 入口，刻意用方法而不是直接暴露 HashSet，避免呼叫端拿到可變集合。
        public IReadOnlyCollection<string> BoardFlags => _boardFlags;
        public bool HasBoardFlag(string flag) => !string.IsNullOrEmpty(flag) && _boardFlags.Contains(flag);

        // 回傳值代表「狀態是否真的改變了」（HashSet.Add/Remove 的語意），讓呼叫端
        // （GameSession.SetBoardFlag/ClearBoardFlag）決定要不要發 BoardFlagChanged——
        // 重複設定/清除同一個已經是該狀態的 flag 不應該產生雜訊事件。
        internal bool AddBoardFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) throw new ArgumentException("A board flag needs a non-empty name.", nameof(flag));
            return _boardFlags.Add(flag);
        }

        internal bool RemoveBoardFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) throw new ArgumentException("A board flag needs a non-empty name.", nameof(flag));
            return _boardFlags.Remove(flag);
        }

        // ADR-0003 決策3：PendingDecision 是 PendingAction 的投影（見 PendingDecision.cs header
        // 註解的範圍說明），每次存取都即時重算，不是額外儲存的狀態。PendingAction 本身保留
        // 不變，因為黃金雜湊（GoldenReplayTests.cs）直接序列化它。
        public PendingDecision PendingDecision
        {
            get
            {
                if (Phase != TurnPhase.AwaitingAction) return null;
                var currentPlayer = GetPlayer(CurrentPlayerId);
                if (currentPlayer == null) return null;

                switch (PendingAction)
                {
                    case PendingAction.ChooseRoute:
                        return new PendingDecision(DecisionKind.ChooseRoute, CurrentPlayerId, BuildOptions(RouteChoiceTileIds), null);
                    case PendingAction.BuyProperty:
                    {
                        var tile = Board.GetTileAt(currentPlayer.CurrentTileIndex);
                        return new PendingDecision(DecisionKind.BuyProperty, CurrentPlayerId, BuildOptions(new[] { tile.Id }), tile.Id);
                    }
                    case PendingAction.UpgradeProperty:
                    {
                        var tile = Board.GetTileAt(currentPlayer.CurrentTileIndex);
                        return new PendingDecision(DecisionKind.UpgradeProperty, CurrentPlayerId, BuildOptions(new[] { tile.Id }), tile.Id);
                    }
                    default:
                        return null;
                }
            }
        }

        private static IReadOnlyList<DecisionOption> BuildOptions(IReadOnlyList<string> ids)
        {
            var options = new List<DecisionOption>(ids.Count);
            for (var i = 0; i < ids.Count; i++) options.Add(new DecisionOption(ids[i]));
            return options.AsReadOnly();
        }

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

        // ADR-0003 決策7：單一合法命令來源。這裡重現 GameSession 各 Execute* 方法在
        // 送出命令「之前」就能確定答案的驗證（回合歸屬、Phase、PendingAction、資金、
        // 升級上限），讓呼叫端不用各自重做一次。資金不足/已達上限等「值得送出但會被
        // 拒絕」的情況，本來就不在這裡回報——那是 Execute 的 CommandErrorCode 職責。
        public IReadOnlyList<LegalCommandOption> GetLegalCommands(int playerId)
        {
            var options = new List<LegalCommandOption>();
            if (Phase == TurnPhase.Finished) return options.AsReadOnly();

            var player = GetPlayer(playerId);
            if (player == null || player.Status != PlayerStatus.Active || playerId != CurrentPlayerId)
                return options.AsReadOnly();

            switch (Phase)
            {
                case TurnPhase.AwaitingRoll:
                    options.Add(new LegalCommandOption(LegalCommandKind.RollDice));
                    break;

                case TurnPhase.AwaitingAction:
                    if (PendingAction == PendingAction.ChooseRoute)
                    {
                        // Core 在等待岔路選擇時拒絕其他一切命令類型（見 ExecuteChooseRoute 以外
                        // 各 Execute* 的 Phase/PendingAction 檢查），所以這裡直接回傳、不往下加。
                        options.Add(new LegalCommandOption(LegalCommandKind.ChooseRoute, RouteChoiceTileIds));
                        return options.AsReadOnly();
                    }
                    if (PendingAction == PendingAction.BuyProperty)
                    {
                        var tile = Board.GetTileAt(player.CurrentTileIndex);
                        if (player.Money >= tile.PurchasePrice)
                            options.Add(new LegalCommandOption(LegalCommandKind.BuyProperty));
                    }
                    if (PendingAction == PendingAction.UpgradeProperty)
                    {
                        var tile = Board.GetTileAt(player.CurrentTileIndex);
                        var property = GetProperty(tile.Id);
                        if (property != null && property.UpgradeLevel < tile.RentLevels.Count - 1 && player.Money >= tile.UpgradeCost)
                            options.Add(new LegalCommandOption(LegalCommandKind.UpgradeProperty, new[] { tile.Id }));
                    }
                    options.Add(new LegalCommandOption(LegalCommandKind.EndTurn));
                    options.Add(new LegalCommandOption(LegalCommandKind.Trade));
                    break;

                case TurnPhase.AwaitingEndTurn:
                    if (CanUpgradeOwnedProperty)
                    {
                        var upgradeableIds = new List<string>();
                        for (var i = 0; i < _properties.Count; i++)
                        {
                            var owned = _properties[i];
                            if (owned.OwnerId != player.Id) continue;
                            var ownedTile = Board.GetProperty(owned.PropertyId);
                            if (ownedTile == null || owned.UpgradeLevel >= ownedTile.RentLevels.Count - 1) continue;
                            if (player.Money < ownedTile.UpgradeCost) continue;
                            upgradeableIds.Add(ownedTile.Id);
                        }
                        if (upgradeableIds.Count > 0)
                            options.Add(new LegalCommandOption(LegalCommandKind.UpgradeProperty, upgradeableIds));
                    }
                    options.Add(new LegalCommandOption(LegalCommandKind.EndTurn));
                    options.Add(new LegalCommandOption(LegalCommandKind.Trade));
                    break;
            }

            return options.AsReadOnly();
        }
    }
}
