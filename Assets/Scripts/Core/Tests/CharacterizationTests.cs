using System.Collections.Generic;
using NUnit.Framework;

namespace Richman.Core.Tests
{
    // T4 邊界特徵測試（docs/AUDIT.md §7.2 T4，Phase 1a 保護測試網）。
    //
    // 每個測試鎖住一條「現狀」，並在方法上方註明其性質：
    //   - 「已知問題，已於 docs/AUDIT.md §8 決策記錄授權 Phase 1 修復」：
    //     重構刻意修正時，必須更新/刪除對應測試，並在 PR 說明中列出此為計畫內變更。
    //   - 「現行規則」：目前視為刻意設計，Phase 1 不預期改變；若要改，需先修 ADR 並
    //     明確核准，而非順手调整。
    //   - 「現狀未決，待產品/規則決策」：docs/AUDIT.md 標注為「是否刻意，待確認」，
    //     本測試僅鎖住現狀，不代表結論。
    public sealed class CharacterizationTests
    {
        // (a) 已於 Phase 1e 修復（docs/AUDIT.md §8 決策記錄授權，授權行為變更②）。
        // GameSession.cs DeclareBankruptcy 把地產 OwnerId 設回 creditorId（可能是 null＝銀行
        // 收回）時，creditorId==null（歸還銀行）現在會把 PropertyState.UpgradeLevel 重設為 0；
        // creditorId 有值（轉給債權玩家）則刻意維持原等級不變（債權人接收完整資產，是刻意
        // 規則，不是 bug）。本測試原本鎖住「銀行收回後地產仍保留原升級等級」的舊現狀，
        // Phase 1e 反轉為「銀行收回後等級歸零」。
        //
        // 棋盤（4 格閉環）：0=start, 1=rest, 2=property(p1), 3=tax(金額 2000，遠超雙方身家)。
        [Test]
        public void Fixed_BankForeclosureResetsUpgradeLevelToZero()
        {
            var mode = new GameModeConfig("t4-bankruptcy-upgrade", 2, 2, 2000, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Property("p1", "district", 2, 100, 20, 40, new[] { 20, 40 }),
                Effect("tax", BoardTileType.Tax, 3, 2000)
            });
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), // P1 turn1: 0 -> 2 (p1，未持有，購買)
                new DiceResult(1, 1), // P2 turn1: 0 -> 2 (p1，P1 持有，付租 20)
                new DiceResult(2, 2), // P1 turn2: 2 -> (2+4)%4=2（繞一圈回到自己的 p1，觸發 UpgradeProperty）
                new DiceResult(2, 2), // P2 turn2: 2 -> (2+4)%4=2（繞一圈回到 p1，付租 40，因為已升級）
                new DiceResult(2, 3)  // P1 turn3: 2 -> (2+5)%4=3（繞到 tax，身家 1920 不足付 2000，破產）
            );
            var session = CreateSession(mode, board.Tiles, dice);

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.BuyProperty));
            Assert.That(session.Execute(new BuyPropertyCommand(1)).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(1));

            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(1920), "P1 money after purchase (-100) and first rent collection (+20).");

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.UpgradeProperty));
            Assert.That(session.Execute(new UpgradePropertyCommand(1, "p1")).Succeeded, Is.True);
            Assert.That(session.State.GetProperty("p1").UpgradeLevel, Is.EqualTo(1));
            session.Execute(new EndTurnCommand(1));

            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(1920), "After -40 upgrade and +40 upgraded rent, net unchanged.");

            session.Execute(new RollDiceCommand(1));

            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.GetProperty("p1").OwnerId, Is.Null,
                "Bank foreclosure should clear ownership.");
            Assert.That(session.State.GetProperty("p1").UpgradeLevel, Is.EqualTo(0),
                "Phase 1e 行為變更：銀行收回地產時重設 UpgradeLevel（GameSession.DeclareBankruptcy，" +
                "docs/AUDIT.md §8 已授權）。下一個買家不應該撿到已經升級好的資產。");
        }

        // (b) 已於 Phase 1e 修復（docs/AUDIT.md §8 決策記錄授權，ADR-0003 決策8 / SEC-CMD-02,03，
        // 授權行為變更①：兩階段交易）。GameSession 舊版 ExecuteTrade 只驗證發起方自身的資源
        // 與目標玩家狀態，從未要求目標玩家同意；本測試原本示範一次 $0 交易即可片面拿走
        // 對方地產。Phase 1e 改為 ProposeTradeCommand/RespondTradeCommand 兩階段：提出不會
        // 立刻搬動任何資源，目標玩家明確拒絕（RespondTradeCommand(accept:false)）時地產
        // 保留在原持有人身上——反轉原本「單方面 $0 交易即可生效」的現狀。
        //
        // 棋盤（2 格閉環）：0=start, 1=property("target-property")。
        [Test]
        public void Fixed_ZeroCostTradeProposalRequiresTargetPlayerConsentAndCanBeRejected()
        {
            var mode = new GameModeConfig("t4-trade-consent", 2, 2, 1000, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Property("target-property", "district", 1, 100, 20, 40, new[] { 20, 40 })
            });
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), // P1: 0 -> (0+2)%2=0（繞回 start，無副作用，純粹只是為了能合法結束回合）
                new DiceResult(1, 2)  // P2: 0 -> (0+3)%2=1（落地 target-property，未持有，購買）
            );
            var session = CreateSession(mode, board.Tiles, dice);

            session.Execute(new RollDiceCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            Assert.That(session.Execute(new BuyPropertyCommand(2)).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(2));

            var player1MoneyBefore = session.State.GetPlayer(1).Money;
            var player2MoneyBefore = session.State.GetPlayer(2).Money;

            var propose = session.Execute(new ProposeTradeCommand(1, 2, 0, 0, null, "target-property"));

            Assert.That(propose.Succeeded, Is.True);
            Assert.That(session.State.PendingTradeProposal, Is.Not.Null);
            // 提出後、目標玩家尚未回應前，地產所有權與雙方現金都不應該被動到。
            Assert.That(session.State.GetProperty("target-property").OwnerId, Is.EqualTo(2));
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(player1MoneyBefore));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(player2MoneyBefore));

            var proposalId = session.State.PendingTradeProposal.ProposalId;
            var reject = session.Execute(new RespondTradeCommand(2, proposalId, false));

            Assert.That(reject.Succeeded, Is.True,
                "Phase 1e 行為變更：目標玩家可以明確拒絕一筆對自己不利（甚至是 $0）的交易提案。");
            Assert.That(session.State.PendingTradeProposal, Is.Null);
            Assert.That(session.State.GetProperty("target-property").OwnerId, Is.EqualTo(2),
                "拒絕後地產應該留在原持有人手上，不會被片面拿走。");
            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(player1MoneyBefore));
            Assert.That(session.State.GetPlayer(2).Money, Is.EqualTo(player2MoneyBefore));
        }

        // (c) 現狀未決，待規則決策（ADR-0002 後記：「是否改變屬規則決策、另行審批」）。
        // GameSession.cs:422-429 ResolveAirport 只搬動玩家並設定訊息，不會對目的地格再跑一次
        // ResolveLanding（即「目的格自身的落地效果」不會觸發）。由於 BoardDefinition 目前強制
        // 機場目的地必須也是 Airport 型別（Board.cs:182-184），現行地圖下沒有「目的地其實是
        // Property/Chance 等格」的情況，這個特徵測試只能鎖住「降落後直接進入 AwaitingEndTurn，
        // 沒有第二次 ResolveLanding 產生的 PendingAction」這個可觀察結果。
        // ADR-0002 把機場將改為通用傳送 Effect，屆時目的地可以是任意格型，
        // 這個「不觸發目的格效果」的行為是否保留，需要另行審批（不在 docs/AUDIT.md §8 的三項自動授權之列）。
        [Test]
        public void CurrentBehavior_AirportArrivalDoesNotRunASecondLandingResolutionOnTheDestinationTile()
        {
            var mode = new GameModeConfig("t4-airport-no-destination-effect", 2, 2, 500, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("approach", BoardTileType.Rest, 1),
                Airport("airport-origin", 2, "airport-destination"),
                Tile("runway", BoardTileType.Rest, 3),
                Airport("airport-destination", 4, "airport-origin"),
                Tile("exit", BoardTileType.Rest, 5)
            });
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller(new DiceResult(1, 1)));

            var result = session.Execute(new RollDiceCommand(1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(4),
                "Player should have been teleported to the paired airport tile.");
            Assert.That(session.State.PendingAction, Is.EqualTo(PendingAction.None),
                "現行行為：抵達目的地機場格後不會再對該格跑一次 ResolveLanding，因此不會產生任何新的待決動作。");
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.AwaitingEndTurn),
                "現行行為：單趟移動只對『起飛』那一格做一次落地解析，目的地格本身沒有第二次解析。");
        }

        // (d) 現行規則。GameSession.cs:556-592 AdvanceTurn 在 TurnNumber 達到 Mode.TurnLimit 時，
        // 改以 FinishByNetWorth（現金 + 地產淨值，FinishByNetWorth 把持有地產算成
        // "PurchasePrice/2 + UpgradeCost*UpgradeLevel/2" 的半價資產）裁定勝者，而非「最後存活玩家」。
        // 注意：docs/AUDIT.md §2.3 已指出 TurnNumber 是「每換人 +1」而非「每輪 +1」，
        // 因此 TurnLimit 的語意容易被誤讀，但機制本身（淨值裁定）是現行規則，非 bug。
        // 刻意用「獎勵格現金」而非「購買地產」讓 P1 淨值領先：地產用半價計入淨值，
        // 買地產反而會讓淨值變低（花 100 換回半價 50 的資產認列），不適合用來示範「誰更有錢」。
        [Test]
        public void CurrentRule_TurnLimitReachedDecidesWinnerByNetWorthEvenWhenAllPlayersAreStillActive()
        {
            var mode = new GameModeConfig("t4-turn-limit-net-worth", 2, 2, 500, 0, 100, 50, 50, 120, turnLimit: 1);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest", BoardTileType.Rest, 1),
                Effect("bonus", BoardTileType.Bonus, 2, 200)
            });
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller(new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1)); // P1: 0 -> 2 (bonus，現金 500+200=700)
            session.Execute(new EndTurnCommand(1)); // TurnNumber(1) >= TurnLimit(1) → 立刻以淨值裁定，不等 P2 行動。

            Assert.That(session.State.GetPlayer(1).Money, Is.EqualTo(700));
            Assert.That(session.State.Phase, Is.EqualTo(TurnPhase.Finished));
            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Winner),
                "雙方都還活著（沒有人破產），遊戲卻已結束並選出贏家——證明是 TurnLimit 裁定，不是最後存活判定。");
            Assert.That(session.State.Outcome, Is.Not.Null);
            Assert.That(session.State.Outcome.Reason, Is.EqualTo(VictoryReason.HighestNetWorthAtTurnLimit));
            Assert.That(session.State.Outcome.WinnerId, Is.EqualTo(1),
                "P1 現金 700 > P2 現金 500，依淨值裁定應為贏家，即使 P2 從未破產也未曾行動。");
        }

        // (e) 現行規則。GameSession.cs:556-592 AdvanceTurn 以 modulo 掃描尋找下一位 Status==Active 的玩家，
        // 自動跳過已破產玩家，不會把回合交還給他們。
        //
        // 棋盤（4 格閉環）：0=start, 1=rest1, 2=rest2, 3=tax(金額 500，遠超起始資金 150)。
        [Test]
        public void CurrentRule_TurnRotationSkipsBankruptPlayersAndCyclesOnlyAmongActiveOnes()
        {
            var mode = new GameModeConfig("t4-turn-skip-bankrupt", 3, 3, 150, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest1", BoardTileType.Rest, 1),
                Tile("rest2", BoardTileType.Rest, 2),
                Effect("tax", BoardTileType.Tax, 3, 500)
            });
            var session = CreateSession(mode, board.Tiles, new ScriptedDiceRoller(
                new DiceResult(1, 2), // P1: 0 -> 3 (tax，破產，自動 AdvanceTurn 到 P2)
                new DiceResult(1, 1), // P2: 0 -> 2 (rest2，安全)
                new DiceResult(1, 1)  // P3: 0 -> 2 (rest2，安全)
            ));

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.GetPlayer(1).Status, Is.EqualTo(PlayerStatus.Bankrupt));
            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(2),
                "P1 破產後應自動輪轉到下一位存活玩家 P2，不需要 P1 呼叫 EndTurn。");

            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));

            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(3),
                "P2 結束回合後應輪轉到 P3（P1 已破產，不在輪轉名單內）。");

            session.Execute(new RollDiceCommand(3));
            session.Execute(new EndTurnCommand(3));

            Assert.That(session.State.CurrentPlayerId, Is.EqualTo(2),
                "P3 結束回合後應跳過已破產的 P1，直接輪轉回 P2（只在存活玩家之間循環）。");
        }

        // (f) 現狀未決，待產品確認（docs/AUDIT.md §3.4 Minor：「停在自己地產可升級『任意』自有地產
        // （GameSession.cs:282），是否刻意待產品確認」）。本測試僅鎖住現狀，不代表結論。
        // 當玩家因落地於 Start 而取得 PendingAction 以外的 CanUpgradeOwnedProperty 權限時
        // （GameSession.cs:275-276 的 startUpgrade 分支），ExecuteUpgrade 並未限制
        // UpgradePropertyCommand.PropertyId 必須是玩家目前所在格——任何一塊玩家自己持有的
        // 地產都可以被指定來升級。
        //
        // 棋盤（5 格閉環）：0=start, 1=rest, 2=propA, 3=rest, 4=propB。
        [Test]
        public void CurrentBehavior_LandingOnStartGrantsUpgradeRightToAnyOwnedPropertyNotJustTheCurrentTile()
        {
            var mode = new GameModeConfig("t4-upgrade-any-owned", 2, 2, 1000, 0, 0, 0);
            var board = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0),
                Tile("rest1", BoardTileType.Rest, 1),
                Property("propA", "district", 2, 100, 20, 40, new[] { 20, 40 }),
                Tile("rest2", BoardTileType.Rest, 3),
                Property("propB", "district", 4, 100, 20, 40, new[] { 20, 40 })
            });
            var dice = new ScriptedDiceRoller(
                new DiceResult(1, 1), // P1 turn1: 0 -> 2 (propA，未持有，購買)
                new DiceResult(1, 2), // P2 turn1: 0 -> 3 (rest2，安全，不干擾)
                new DiceResult(1, 1), // P1 turn2: 2 -> 4 (propB，未持有，購買)
                new DiceResult(1, 2), // P2 turn2: 3 -> (3+3)%5=1 (rest1，安全)
                new DiceResult(3, 3)  // P1 turn3: 4 -> (4+6)%5=0 (繞路回到 start，CanUpgradeOwnedProperty=true)
            );
            var session = CreateSession(mode, board.Tiles, dice);

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.Execute(new BuyPropertyCommand(1)).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.Execute(new BuyPropertyCommand(1)).Succeeded, Is.True);
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            session.Execute(new EndTurnCommand(2));

            session.Execute(new RollDiceCommand(1));
            Assert.That(session.State.GetPlayer(1).CurrentTileIndex, Is.EqualTo(0), "P1 現在應站在 start 上。");
            Assert.That(session.State.CanUpgradeOwnedProperty, Is.True);

            // 現行行為：即使站在 start（不是任何一塊地產），也可以指定升級 propB（而非 propA）。
            var upgradeChoice = session.Execute(new UpgradePropertyCommand(1, "propB"));

            Assert.That(upgradeChoice.Succeeded, Is.True,
                "現狀：Start 觸發的升級權利，其 PropertyId 未被限制為玩家目前所在格（本來就不在任何地產上），" +
                "可指定任何自己持有的地產（GameSession.cs:275-282）。");
            Assert.That(session.State.GetProperty("propB").UpgradeLevel, Is.EqualTo(1));
            Assert.That(session.State.GetProperty("propA").UpgradeLevel, Is.EqualTo(0),
                "propA 未被升級，證明選擇權確實是『任意』自有地產，而非固定目標。");
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

        private static BoardTileDefinition Effect(string id, BoardTileType type, int positionIndex, int amount)
        {
            return new BoardTileDefinition(id, type, id, positionIndex, 0, 0, 0, null, string.Empty, id, amount);
        }

        private static BoardTileDefinition Airport(string id, int positionIndex, string destinationId)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, id, positionIndex, 0, 0, 0, null,
                string.Empty, id, 0, null, true, null, destinationId);
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
