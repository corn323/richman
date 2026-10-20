using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Richman.Core.Events;

namespace Richman.Core.Tests
{
    // Phase 1e（ADR-0003 決策8 / SEC-CMD-02,03，授權行為變更①）：兩階段交易。
    //
    // 舊版單階段 TradeCommand 已經整個移除（本檔完全不引用它——這本身就是「單方
    // TradeCommand 不再可行」的證明：它甚至無法通過編譯）。新流程一律是
    // ProposeTradeCommand（建立 pending proposal，不搬動任何資源，不阻塞發起方回合）
    // 接著 RespondTradeCommand(playerId, proposalId, accept)（只有提案的 TargetId 能送出；
    // accept=true 時先核對資產快照仍然吻合才會原子執行，否則自動失效）。
    public sealed class TradeNegotiationTests
    {
        [Test]
        public void ProposeThenAccept_ExecutesTheTradeAtomicallyOnlyAfterAcceptance()
        {
            var session = CreateTwoPlayerSessionWithP1OwningP1Property(out var proposeTurnStartMoney);

            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 50, "p1", null));
            Assert.That(propose.Succeeded, Is.True);
            var proposed = propose.Events.OfType<TradeProposed>().Single();
            Assert.That(proposed.InitiatorId, Is.EqualTo(1));
            Assert.That(proposed.TargetId, Is.EqualTo(2));
            // 提出不應該立刻搬動任何資源。
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.EqualTo(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(proposeTurnStartMoney));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(1000));

            var proposalId = session.State.PendingTradeProposal.ProposalId;
            var accept = session.Execute(new RespondTradeCommand(2, proposalId, true));

            Assert.That(accept.Succeeded, Is.True);
            Assert.That(session.State.PendingTradeProposal, Is.Null);
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.EqualTo(2));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(proposeTurnStartMoney + 50));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(950));
            var executed = accept.Events.OfType<TradeExecuted>().Single();
            Assert.That(executed.InitiatorId, Is.EqualTo(1));
            Assert.That(executed.TargetId, Is.EqualTo(2));
            Assert.That(executed.RequestedMoney, Is.EqualTo(50));
            Assert.That(executed.OfferedPropertyId, Is.EqualTo("p1"));
        }

        [Test]
        public void ProposeThenReject_LeavesMoneyAndOwnershipUnchanged()
        {
            var session = CreateTwoPlayerSessionWithP1OwningP1Property(out var proposeTurnStartMoney);
            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 50, "p1", null));
            var proposalId = session.State.PendingTradeProposal.ProposalId;

            var reject = session.Execute(new RespondTradeCommand(2, proposalId, false));

            Assert.That(reject.Succeeded, Is.True);
            Assert.That(session.State.PendingTradeProposal, Is.Null);
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.EqualTo(1));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(proposeTurnStartMoney));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(1000));
            var rejected = reject.Events.OfType<TradeRejected>().Single();
            Assert.That(rejected.ProposalId, Is.EqualTo(proposalId));
            Assert.That(rejected.InitiatorId, Is.EqualTo(1));
            Assert.That(rejected.TargetId, Is.EqualTo(2));
        }

        [Test]
        public void OnlyTheProposalsTargetPlayerCanRespond()
        {
            var mode = new GameModeConfig("trade-non-target", 3, 3, 1000, 0, 0, 0);
            var board = new BoardDefinition(new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1) });
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller());

            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 10, null, null));
            Assert.That(propose.Succeeded, Is.True);
            var proposalId = session.State.PendingTradeProposal.ProposalId;

            var wrongResponder = session.Execute(new RespondTradeCommand(3, proposalId, true));

            Assert.That(wrongResponder.ErrorCode, Is.EqualTo(CommandErrorCode.NotTradeProposalTarget));
            Assert.That(session.State.PendingTradeProposal, Is.Not.Null,
                "非目標玩家的無效回應不應該影響原本待決的提案。");

            var correctResponder = session.Execute(new RespondTradeCommand(2, proposalId, true));
            Assert.That(correctResponder.Succeeded, Is.True, "真正的目標玩家之後仍然可以正常回應同一筆提案。");
        }

        // 快照失效：P1 提出交易之後（還沒等 P2 回應），P1 自己在同一回合內把錢花在升級
        // 地產上——發起方的資產快照因此對不上，P2 接受時應該自動失效，而不是照舊執行。
        [Test]
        public void AcceptingAStaleProposalAfterRelevantStateChangedExpiresItInsteadOfExecuting()
        {
            var mode = new GameModeConfig("trade-snapshot-stale", 2, 2, 1000, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Property("p1", "d", 2, 100, 20, 40, new[] { 20, 40 }),
                Tile("rest2", BoardTileType.Rest, 3)
            });
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), // P1 turn1: 0 -> 2 (p1，未持有，購買)
                new DiceResult(1, 1), // P2 turn1: 0 -> 2 (p1，P1 持有，付租 20)
                new DiceResult(2, 2)  // P1 turn2: 2 -> (2+4)%4=2（繞一圈回到自己的 p1，觸發 UpgradeProperty）
            );
            var session = CreateSession(mode, board.Tiles, dice);

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1)); // P1: 1000 - 100 = 900

            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2)); // P2 付租 20 給 P1：P1 920，P2 980

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.UpgradeProperty));

            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 50, null, null));
            Assert.That(propose.Succeeded, Is.True);
            var proposalId = session.State.PendingTradeProposal.ProposalId;

            Assert.That(session.Execute(new UpgradePropertyCommand(1, "p1")).Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(880), "920 - 40 升級費用。");

            var accept = session.Execute(new RespondTradeCommand(2, proposalId, true));

            // 命令本身被妥善處理（target、proposalId 都對），Succeeded=true；但處理結果是
            // 「提案已過期」而不是「交易成立」——用 events 裡是哪種事件分辨，而不是看
            // Succeeded/ErrorCode（見 GameSession.ExecuteRespondTrade 的設計註解）。
            Assert.That(accept.Succeeded, Is.True);
            Assert.That(accept.Events.OfType<TradeExecuted>(), Is.Empty, "過期的提案不應該真的執行交易。");
            Assert.That(session.State.PendingTradeProposal, Is.Null);
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(880), "失效的交易不應該額外異動金錢。");
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(980));
            var expired = accept.Events.OfType<TradeProposalExpired>().Single();
            Assert.That(expired.ProposalId, Is.EqualTo(proposalId));
            Assert.That(expired.Reason, Is.EqualTo(TradeProposalExpiryReason.SnapshotStale));
        }

        [Test]
        public void EndingTheInitiatorsTurnExpiresTheStillPendingProposal()
        {
            // 加一顆骰子給 P1 的第三回合：total=3 從 p1(索引1) 走回 start(索引0)，沒有副作用，
            // 單純是為了讓 P1 先擲骰（AwaitingRoll 不能直接 EndTurn）再提出交易、再結束回合。
            var session = CreateTwoPlayerSessionWithP1OwningP1Property(out _, new DiceResult(1, 2));
            session.Execute(new RollDiceCommand(1));
            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 50, "p1", null));
            var proposalId = session.State.PendingTradeProposal.ProposalId;

            var endTurn = session.Execute(new EndTurnCommand(1));

            Assert.That(endTurn.Succeeded, Is.True);
            Assert.That(session.State.PendingTradeProposal, Is.Null);
            var expired = endTurn.Events.OfType<TradeProposalExpired>().Single();
            Assert.That(expired.ProposalId, Is.EqualTo(proposalId));
            Assert.That(expired.Reason, Is.EqualTo(TradeProposalExpiryReason.TurnEnded));

            var tooLate = session.Execute(new RespondTradeCommand(2, proposalId, true));
            Assert.That(tooLate.ErrorCode, Is.EqualTo(CommandErrorCode.TradeProposalNotFound));
        }

        [Test]
        public void ProposalsPerTurnAreCappedByGameModeConfigMaxTradeProposalsPerTurn()
        {
            var mode = new GameModeConfig("trade-proposal-cap", 2, 2, 1000, 0, 0, 0);
            Assert.That(mode.MaxTradeProposalsPerTurn, Is.EqualTo(3), "預設每回合提案上限應該是 3。");
            var board = new BoardDefinition(new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1) });
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller());

            for (var i = 0; i < mode.MaxTradeProposalsPerTurn; i++)
            {
                var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 1, null, null));
                Assert.That(propose.Succeeded, Is.True, "第 " + (i + 1) + " 次提案應該在上限內成功。");
                session.Execute(new RespondTradeCommand(2, session.State.PendingTradeProposal.ProposalId, false));
            }

            var overLimit = session.Execute(new ProposeTradeCommand(1, 2, 0, 1, null, null));

            Assert.That(overLimit.ErrorCode, Is.EqualTo(CommandErrorCode.TradeProposalLimitReached));
        }

        [Test]
        public void OnlyOneProposalCanBePendingAtATime()
        {
            var session = CreateTwoPlayerSessionWithP1OwningP1Property(out _);
            Assert.That(session.Execute(new ProposeTradeCommand(1, 2, 0, 10, null, null)).Succeeded, Is.True);

            var secondWhilePending = session.Execute(new ProposeTradeCommand(1, 2, 0, 5, null, null));

            Assert.That(secondWhilePending.ErrorCode, Is.EqualTo(CommandErrorCode.TradeProposalAlreadyPending));
        }

        private static GameSession CreateTwoPlayerSessionWithP1OwningP1Property(out int p1MoneyAfterPurchase, params DiceResult[] additionalDice)
        {
            var mode = new GameModeConfig("trade-accept-reject", 2, 2, 1000, 0, 0, 0);
            var board = new BoardDefinition(new[] { Tile("start", BoardTileType.Start, 0), Property("p1", "d", 1, 100, 20, 40, new[] { 20, 40 }) });
            var diceResults = new List<DiceResult>
            {
                new DiceResult(1, 2), // P1: 0 -> (0+3)%2=1 (p1，未持有，購買)
                new DiceResult(1, 1)  // P2: 0 -> (0+2)%2=0（繞回 start，無副作用）
            };
            diceResults.AddRange(additionalDice);
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller(diceResults.ToArray()));
            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            p1MoneyAfterPurchase = session.State.GetPlayer(1).Money;
            return session;
        }

        private static GameSession CreateSession(GameModeConfig mode, IEnumerable<BoardTileDefinition> tiles, IDiceRoller diceRoller)
        {
            var players = new List<PlayerSetup>();
            for (var i = 1; i <= mode.MaxPlayers; i++) players.Add(new PlayerSetup(i, "Player " + i));
            return new GameSession(mode, new BoardDefinition(tiles), players, diceRoller, new SeededRandom(1));
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Property(string id, string districtId, int positionIndex, int price, int baseRent, int upgradeCost, int[] rentLevels)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, positionIndex, price, baseRent, upgradeCost, rentLevels, districtId, id);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly Queue<DiceResult> _results;
            public ScriptedDiceRoller(params DiceResult[] results) { _results = new Queue<DiceResult>(results); }
            public DiceResult Roll()
            {
                if (_results.Count == 0) throw new System.InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
