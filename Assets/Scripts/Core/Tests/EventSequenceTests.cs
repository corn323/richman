using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Richman.Core.Events;

namespace Richman.Core.Tests
{
    // Phase 1c 事件序列測試（ADR-0003 決策1/2/3/4）。
    //
    // 每個測試鎖住一條關鍵劇本在事件層的可觀察結果：事件型別、順序與關鍵欄位。
    // 刻意使用 ScriptedDiceRoller／ScriptedRandom 讓場景可重現，且不依賴
    // LastEventMessage 等即將退役的顯示字串欄位（ADR-0003 決策2）。
    public sealed class EventSequenceTests
    {
        [Test]
        public void RollingMovingAndBuyingAnUnownedPropertyEmitsTheExpectedEventSequence()
        {
            var session = CreateSession(
                new GameModeConfig("event-roll-buy", 2, 2, 500, 0, 50, 25),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));
            Assert.That(roll.Succeeded, Is.True);

            // 擲骰移動兩格（dice total=2）落地在未持有的地產：
            // PreRoll → Roll(+DiceRolled) → Move → 兩次 PawnStepped → Land(+PawnLanded) →
            // ResolveTile → DecisionRequested(BuyProperty)。
            var types = roll.Events.Select(e => e.GetType()).ToArray();
            Assert.That(types, Is.EqualTo(new[]
            {
                typeof(PhaseChanged), typeof(PhaseChanged), typeof(DiceRolled), typeof(PhaseChanged),
                typeof(PawnStepped), typeof(PawnStepped), typeof(PhaseChanged), typeof(PawnLanded),
                typeof(PhaseChanged), typeof(DecisionRequested)
            }));
            var requested = roll.Events.OfType<DecisionRequested>().Single();
            Assert.That(requested.Kind, Is.EqualTo(DecisionKind.BuyProperty));
            Assert.That(requested.OptionIds, Is.EqualTo(new[] { "p1" }));
            Assert.That(requested.ContextTileId, Is.EqualTo("p1"));

            var buy = session.Execute(new BuyPropertyCommand(1));
            Assert.That(buy.Succeeded, Is.True);

            var resolved = buy.Events.OfType<DecisionResolved>().Single();
            Assert.That(resolved.Kind, Is.EqualTo(DecisionKind.BuyProperty));
            Assert.That(resolved.OptionId, Is.EqualTo("p1"));
            var moneyChanged = buy.Events.OfType<MoneyChanged>().Single();
            Assert.That(moneyChanged.Delta, Is.EqualTo(-100));
            Assert.That(moneyChanged.Reason, Is.EqualTo(MoneyChangeReason.PropertyPurchase));
            var purchased = buy.Events.OfType<PropertyPurchased>().Single();
            Assert.That(purchased.PropertyId, Is.EqualTo("p1"));
            Assert.That(purchased.Amount, Is.EqualTo(100));
            var postAction = buy.Events.OfType<PhaseChanged>().Single();
            Assert.That(postAction.Stage, Is.EqualTo(TurnStage.PostAction));
        }

        [Test]
        public void PassingThroughStartMidMoveEmitsSalaryCollectedAndMoneyChanged()
        {
            var mode = new GameModeConfig("event-salary", 2, 2, 500, 100, 50, 25);
            var board = new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("a", BoardTileType.Rest, 1),
                Tile("b", BoardTileType.Rest, 2), Tile("c", BoardTileType.Rest, 3), Tile("d", BoardTileType.Rest, 4)
            };
            var session = CreateSession(mode, board,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(2, 2)));

            session.Execute(new RollDiceCommand(1)); // 0 -> 2 (b)
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var secondRoll = session.Execute(new RollDiceCommand(1)); // 2 -> 3 -> 4 -> 0(pass) -> 1 (a)

            Assert.That(secondRoll.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(1));
            var salary = secondRoll.Events.OfType<SalaryCollected>().Single();
            Assert.That(salary.PlayerId, Is.EqualTo(1));
            Assert.That(salary.Amount, Is.EqualTo(100));
            var moneyChanged = secondRoll.Events.OfType<MoneyChanged>().Single(e => e.Reason == MoneyChangeReason.Salary);
            Assert.That(moneyChanged.Delta, Is.EqualTo(100));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(600));
            var passedStep = secondRoll.Events.OfType<PawnStepped>().Single(e => e.PassedStart);
            Assert.That(passedStep.ToTileIndex, Is.EqualTo(0));
        }

        [Test]
        public void LandingOnPrisonEmitsPlayerDetainedAndFailedEscapeEmitsPrisonEscapeAttempted()
        {
            var mode = new GameModeConfig("event-prison", 2, 2, 500, 0, 50, 25);
            var board = new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                Effect("prison", BoardTileType.Prison, 2, 1), Tile("rest-2", BoardTileType.Rest, 3)
            };
            var session = CreateSession(mode, board,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 2)));

            var landing = session.Execute(new RollDiceCommand(1));
            var detained = landing.Events.OfType<PlayerDetained>().Single();
            Assert.That(detained.PlayerId, Is.EqualTo(1));
            Assert.That(detained.Kind, Is.EqualTo(DetentionKind.Prison));
            Assert.That(detained.Turns, Is.EqualTo(3));

            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var escapeAttempt = session.Execute(new RollDiceCommand(1));

            var diceRolled = escapeAttempt.Events.OfType<DiceRolled>().Single();
            Assert.That(diceRolled.Purpose, Is.EqualTo(DiceRollPurpose.PrisonEscape));
            var attempted = escapeAttempt.Events.OfType<PrisonEscapeAttempted>().Single();
            Assert.That(attempted.Success, Is.False);
            Assert.That(attempted.AttemptsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void HospitalStaySkipTurnEmitsTurnSkipped()
        {
            var mode = new GameModeConfig("event-hospital", 2, 2, 500, 0, 50, 25);
            var board = new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                Effect("hospital", BoardTileType.Hospital, 2, 2), Tile("rest-2", BoardTileType.Rest, 3)
            };
            var session = CreateSession(mode, board, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            var skip = session.Execute(new RollDiceCommand(1));

            Assert.That(skip.Succeeded, Is.True);
            Assert.That(skip.DiceResult, Is.Null);
            var turnSkipped = skip.Events.OfType<TurnSkipped>().Single();
            Assert.That(turnSkipped.PlayerId, Is.EqualTo(1));
            Assert.That(turnSkipped.Reason, Is.EqualTo(TurnSkipReason.Hospital));
            Assert.That(turnSkipped.TurnsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void AirportLandingEmitsPlayerTeleported()
        {
            var mode = new GameModeConfig("event-airport", 2, 2, 500, 0, 50, 25);
            var board = new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1),
                Airport("air1", 2, "air2"), Tile("rest-2", BoardTileType.Rest, 3), Airport("air2", 4, "air1")
            };
            var session = CreateSession(mode, board, new ScriptedDiceRoller(new DiceResult(1, 1)));

            var roll = session.Execute(new RollDiceCommand(1));

            var teleported = roll.Events.OfType<PlayerTeleported>().Single();
            Assert.That(teleported.PlayerId, Is.EqualTo(1));
            Assert.That(teleported.FromTileIndex, Is.EqualTo(2));
            Assert.That(teleported.ToTileIndex, Is.EqualTo(4));
            Assert.That(teleported.Reason, Is.EqualTo(TeleportReason.Airport));
        }

        [Test]
        public void RentPaymentEmitsRentPaidAndMoneyChangedForBothParties()
        {
            var mode = new GameModeConfig("event-rent", 2, 2, 500, 0, 50, 25);
            var board = new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 100, 20, 50, 2, 20, 40) };
            var session = CreateSession(mode, board, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            var rent = session.Execute(new RollDiceCommand(2));

            var rentPaid = rent.Events.OfType<RentPaid>().Single();
            Assert.That(rentPaid.PayerId, Is.EqualTo(2));
            Assert.That(rentPaid.ReceiverId, Is.EqualTo(1));
            Assert.That(rentPaid.PropertyId, Is.EqualTo("p1"));
            Assert.That(rentPaid.Amount, Is.EqualTo(20));
            Assert.That(rentPaid.BaseRent, Is.EqualTo(20));
            Assert.That(rentPaid.DistrictBonusAmount, Is.EqualTo(0));
            var moneyEvents = rent.Events.OfType<MoneyChanged>().ToList();
            Assert.That(moneyEvents.Any(e => e.PlayerId == 2 && e.Delta == -20 && e.Reason == MoneyChangeReason.RentPaid), Is.True);
            Assert.That(moneyEvents.Any(e => e.PlayerId == 1 && e.Delta == 20 && e.Reason == MoneyChangeReason.RentReceived), Is.True);
        }

        [Test]
        public void RentShortfallBankruptcyEmitsPlayerBankruptAndGameFinished()
        {
            var mode = new GameModeConfig("event-bankruptcy", 2, 2, 100, 0, 50, 25);
            var board = new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("p1", 40, 120, 50, 2, 120) };
            var session = CreateSession(mode, board, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            var rent = session.Execute(new RollDiceCommand(2));

            var bankrupt = rent.Events.OfType<PlayerBankrupt>().Single();
            Assert.That(bankrupt.PlayerId, Is.EqualTo(2));
            Assert.That(bankrupt.CreditorId, Is.EqualTo(1));
            Assert.That(bankrupt.TransferredMoney, Is.EqualTo(100));
            Assert.That(bankrupt.TransferredPropertyIds, Is.Empty);

            var finished = rent.Events.OfType<GameFinished>().Single();
            Assert.That(finished.WinnerId, Is.EqualTo(1));
            Assert.That(finished.Reason, Is.EqualTo(VictoryReason.LastPlayerStanding));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished));
        }

        [Test]
        public void RouteForkEmitsDecisionRequestedThenDecisionResolvedOnChooseRoute()
        {
            var session = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), PrototypeBoardFactory.Create().Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));

            session.Execute(new RollDiceCommand(1));
            session.Execute(new EndTurnCommand(1));
            for (var playerId = 2; playerId <= 4; playerId++)
            {
                session.Execute(new RollDiceCommand(playerId));
                session.Execute(new EndTurnCommand(playerId));
            }

            var toJunction = session.Execute(new RollDiceCommand(1));
            var requested = toJunction.Events.OfType<DecisionRequested>().Single();
            Assert.That(requested.Kind, Is.EqualTo(DecisionKind.ChooseRoute));
            Assert.That(requested.OptionIds, Does.Contain("macau-ferry-pier"));

            var branch = session.Execute(new ChooseRouteCommand(1, "macau-ferry-pier"));
            Assert.That(branch.Succeeded, Is.True);
            var resolved = branch.Events.OfType<DecisionResolved>().Single();
            Assert.That(resolved.Kind, Is.EqualTo(DecisionKind.ChooseRoute));
            Assert.That(resolved.OptionId, Is.EqualTo("macau-ferry-pier"));
        }

        private static GameSession CreateSession(GameModeConfig mode, IEnumerable<BoardTileDefinition> tiles, IDiceRoller diceRoller, IGameRandom random = null)
        {
            var players = new List<PlayerSetup>();
            for (var i = 1; i <= mode.MaxPlayers; i++) players.Add(new PlayerSetup(i, "Player " + i));
            return new GameSession(mode, new BoardDefinition(tiles), players, diceRoller, random ?? new SeededRandom(1));
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int positionIndex)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Effect(string id, BoardTileType type, int positionIndex, int amount)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, amount);
        }

        private static BoardTileDefinition Airport(string id, int positionIndex, string destinationId)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, id, positionIndex, 0, 0, 0, null,
                string.Empty, id, 0, null, true, null, destinationId);
        }

        private static BoardTileDefinition Property(string id, int price, int baseRent, int upgradeCost, int positionIndex, params int[] rents)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, positionIndex, price, baseRent, upgradeCost, rents, "test", id);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly Queue<DiceResult> _results;

            public ScriptedDiceRoller(params DiceResult[] results)
            {
                _results = new Queue<DiceResult>(results);
            }

            public DiceResult Roll()
            {
                if (_results.Count == 0) throw new InvalidOperationException("The scripted dice ran out of results.");
                return _results.Dequeue();
            }
        }
    }
}
