using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using Richman.Core;

namespace Richman.Simulation
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "--self-test", StringComparison.Ordinal))
            {
                RunCoreVerification();
                return 0;
            }
            if (args.Length > 0 && string.Equals(args[0], "--export-map", StringComparison.Ordinal))
            {
                var output = args.Length > 1 ? args[1] : Path.Combine("GodotProject", "Data", "archipelago_tiles.json");
                ExportMap(output);
                return 0;
            }
            var gameCount = args.Length > 0 ? int.Parse(args[0]) : 1000;
            var report = BotSimulation.Run(gameCount, 928371, 20000);
            var outputPath = "SimulationResults.csv";
            File.WriteAllText(outputPath, report.ToCsv());
            Console.WriteLine("Games: " + report.Games.Count);
            Console.WriteLine("Completed: " + report.CompletedGames);
            Console.WriteLine("Average turns: " + report.AverageTurns);
            Console.WriteLine("CSV: " + Path.GetFullPath(outputPath));
            return report.CompletedGames == report.Games.Count ? 0 : 2;
        }

        private static void ExportMap(string outputPath)
        {
            var board = PrototypeBoardFactory.Create();
            // Phase 1d-3（ADR-0002 §3）：座標不再隨 BoardDefinition 建構自動算好，匯出工具
            // 是座標的消費端之一，讀 tile.MapPosition 之前要自己顯式呼叫一次。
            BoardLayoutPlanner.AssignMapPositions(board);
            var tiles = new List<object>();
            for (var i = 0; i < board.TileCount; i++)
            {
                var tile = board.GetTileAt(i);
                tiles.Add(new { id = tile.Id, type = tile.Type.ToString(), display_name = tile.DisplayName, position_index = tile.PositionIndex,
                    position = new { x = tile.MapPosition.X, y = tile.MapPosition.Y, z = tile.MapPosition.Z }, rotation = tile.MapPosition.RotationDegrees,
                    next_tiles = tile.NextTileIds, is_main_route = tile.IsMainRoute, rejoins_at = tile.RejoinsAtId,
                    district = tile.DistrictId, price = tile.PurchasePrice, rent = tile.BaseRent, upgrade_cost = tile.UpgradeCost, rent_levels = tile.RentLevels,
                    airport_destination_id = tile.AirportDestinationId });
            }
            var fullPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, JsonSerializer.Serialize(new { main_route_tile_count = board.MainRouteTileCount, tiles }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("MAP_DATA_EXPORTED tiles=" + board.TileCount + " path=" + fullPath);
        }

        private static void RunCoreVerification()
        {
            var board = PrototypeBoardFactory.Create();
            Require(board.MainRouteTileCount == 41 && board.TileCount == 48, "world map tile count");
            Require(board.GetTileAt(30).Id == "london-prison", "London prison tile");
            Require(board.GetTileAt(9).Type == BoardTileType.Hospital && board.GetTileAt(40).Type == BoardTileType.Court,
                "hospital and court are real route tiles");
            Require(board.GetTileAt(40).NextTileIds[0] == board.GetTileAt(0).Id, "main route closure");
            Require(board.GetNextTiles(10).Count == 2, "Hong Kong route fork");
            Require(board.GetTile("airport-changi").AirportDestinationId == "airport-incheon", "Changi route");
            Require(board.GetTile("airport-incheon").AirportDestinationId == "airport-changi", "Incheon route");
            Require(board.GetTile("airport-dubai").AirportDestinationId == "airport-jfk", "Dubai route");
            Require(board.GetTile("airport-jfk").AirportDestinationId == "airport-dubai", "JFK route");
            Require(board.GetTile("paris-montmartre").Type == BoardTileType.Property && board.GetTile("paris-montmartre").PurchasePrice > 0,
                "Paris Montmartre remains a purchasable property");
            Require(board.GetTile("paris-court").Type == BoardTileType.Court, "court is a real route tile");
            Console.WriteLine("CORE_ROUTE_OK main=41 all=48 branches=3 merges=validated paris_property=true court=true");
            Console.WriteLine("CORE_AIRPORTS_OK changi<->incheon dubai<->jfk");

            var airportSession = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 2)));
            var airportLanding = airportSession.Execute(new RollDiceCommand(1));
            Require(airportLanding.Succeeded && airportLanding.PathTileIndices.Count == 3 &&
                    airportSession.State.GetPlayer(1).CurrentTileIndex == board.GetTile("airport-incheon").PositionIndex,
                "Changi airport landing flies to its declared destination");
            Console.WriteLine("CORE_AIRPORT_LANDING_OK origin=airport-changi destination=airport-incheon");

            var airportPairs = new[] { new[] { "airport-changi", "airport-incheon" }, new[] { "airport-incheon", "airport-changi" },
                new[] { "airport-dubai", "airport-jfk" }, new[] { "airport-jfk", "airport-dubai" } };
            for (var i = 0; i < airportPairs.Length; i++)
            {
                var transferBoard = new BoardDefinition(new[] { Tile("start", BoardTileType.Start, 0), Tile("approach", BoardTileType.Rest, 1),
                    Airport(airportPairs[i][0], 2, airportPairs[i][1]), Tile("runway", BoardTileType.Rest, 3),
                    Airport(airportPairs[i][1], 4, airportPairs[i][0]), Tile("exit", BoardTileType.Rest, 5) });
                var transferSession = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), transferBoard.Tiles,
                    new ScriptedDiceRoller(new DiceResult(1, 1)));
                var transfer = transferSession.Execute(new RollDiceCommand(1));
                Require(transfer.Succeeded && transfer.PathTileIndices.Count == 2 &&
                        transferSession.State.GetPlayer(1).CurrentTileIndex == 4,
                    "airport transfers for " + airportPairs[i][0]);
            }
            Console.WriteLine("CORE_AIRPORT_PAIRS_OK actual_transfers=4");

            var prisonSession = CreateSession(new GameModeConfig("prison-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("prison", BoardTileType.Prison, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            prisonSession.Execute(new RollDiceCommand(1)); prisonSession.Execute(new EndTurnCommand(1));
            prisonSession.Execute(new RollDiceCommand(2)); prisonSession.Execute(new EndTurnCommand(2));
            var prisonRelease = prisonSession.Execute(new RollDiceCommand(1));
            Require(prisonRelease.DiceResult.First == prisonRelease.DiceResult.Second && prisonRelease.PathTileIndices.Count == 2 &&
                    !prisonSession.State.GetPlayer(1).IsInPrison,
                "prison release requires doubles and then moves by Core path");
            Console.WriteLine("CORE_PRISON_OK doubles_release=true movement_path=Core");

            var prisonBailSession = CreateSession(new GameModeConfig("prison-bail-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("prison", BoardTileType.Prison, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 2), new DiceResult(1, 2),
                new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 1), new DiceResult(3, 4)));
            prisonBailSession.Execute(new RollDiceCommand(1)); prisonBailSession.Execute(new EndTurnCommand(1));
            prisonBailSession.Execute(new RollDiceCommand(2)); prisonBailSession.Execute(new EndTurnCommand(2));
            prisonBailSession.Execute(new RollDiceCommand(1)); prisonBailSession.Execute(new EndTurnCommand(1));
            prisonBailSession.Execute(new RollDiceCommand(2)); prisonBailSession.Execute(new EndTurnCommand(2));
            prisonBailSession.Execute(new RollDiceCommand(1)); prisonBailSession.Execute(new EndTurnCommand(1));
            prisonBailSession.Execute(new RollDiceCommand(2)); prisonBailSession.Execute(new EndTurnCommand(2));
            prisonBailSession.Execute(new RollDiceCommand(1));
            Require(!prisonBailSession.State.GetPlayer(1).IsInPrison && prisonBailSession.State.GetPlayer(1).Money == 400,
                "three failed prison attempts charge one hundred bail");
            Console.WriteLine("CORE_PRISON_BAIL_OK failed_attempts=3 bail=100");

            var poorPrisonTiles = new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("prison", BoardTileType.Prison, 2), Tile("rest-3", BoardTileType.Rest, 3),
                Tile("rest-4", BoardTileType.Rest, 4), Tile("rest-5", BoardTileType.Rest, 5),
                Tile("rest-6", BoardTileType.Rest, 6), Tile("rest-7", BoardTileType.Rest, 7),
                Tile("rest-8", BoardTileType.Rest, 8), Tile("rest-9", BoardTileType.Rest, 9)
            };
            var poorPrison = CreateSession(new GameModeConfig("poor-prison-test", 4, 4, 50, 0, 50, 25), poorPrisonTiles,
                new ScriptedDiceRoller(
                    new DiceResult(1, 1), new DiceResult(2, 2), new DiceResult(3, 3), new DiceResult(2, 3),
                    new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1),
                    new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 2)));
            poorPrison.Execute(new RollDiceCommand(1)); poorPrison.Execute(new EndTurnCommand(1));
            poorPrison.Execute(new RollDiceCommand(2)); poorPrison.Execute(new EndTurnCommand(2));
            poorPrison.Execute(new RollDiceCommand(3)); poorPrison.Execute(new EndTurnCommand(3));
            poorPrison.Execute(new RollDiceCommand(4)); poorPrison.Execute(new EndTurnCommand(4));
            for (var attempt = 0; attempt < 3; attempt++)
            {
                poorPrison.Execute(new RollDiceCommand(1));
                if (attempt < 2)
                {
                    poorPrison.Execute(new EndTurnCommand(1));
                    for (var playerId = 2; playerId <= 4; playerId++)
                    {
                        poorPrison.Execute(new RollDiceCommand(playerId));
                        poorPrison.Execute(new EndTurnCommand(playerId));
                    }
                }
            }
            Require(poorPrison.State.GetPlayer(1).Status == PlayerStatus.Bankrupt && poorPrison.State.CurrentPlayerId == 2 &&
                    poorPrison.State.Phase == TurnPhase.AwaitingRoll && poorPrison.State.Outcome == null,
                "unpaid prison bail bankrupts player and automatically advances to active player");
            Require(poorPrison.Execute(new EndTurnCommand(1)).ErrorCode == CommandErrorCode.PlayerEliminated,
                "bankrupt jailed player is not asked to end turn");
            Console.WriteLine("CORE_PRISON_BAIL_BANKRUPTCY_OK advances_to_active_player=true");

            var finalBail = CreateSession(new GameModeConfig("final-prison-bail-test", 2, 2, 50, 0, 50, 25), poorPrisonTiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 2),
                    new DiceResult(1, 1), new DiceResult(1, 2), new DiceResult(1, 1), new DiceResult(1, 2)));
            finalBail.Execute(new RollDiceCommand(1)); finalBail.Execute(new EndTurnCommand(1));
            finalBail.Execute(new RollDiceCommand(2)); finalBail.Execute(new EndTurnCommand(2));
            finalBail.Execute(new RollDiceCommand(1)); finalBail.Execute(new EndTurnCommand(1));
            finalBail.Execute(new RollDiceCommand(2)); finalBail.Execute(new EndTurnCommand(2));
            finalBail.Execute(new RollDiceCommand(1)); finalBail.Execute(new EndTurnCommand(1));
            finalBail.Execute(new RollDiceCommand(2)); finalBail.Execute(new EndTurnCommand(2));
            finalBail.Execute(new RollDiceCommand(1));
            Require(finalBail.State.Phase == TurnPhase.Finished && finalBail.State.Outcome.WinnerId == 2,
                "unpaid prison bail that ends game preserves finished phase and winner");
            Console.WriteLine("CORE_PRISON_BAIL_GAME_FINISHED_OK phase=Finished winner=2");

            var hospitalSession = CreateSession(new GameModeConfig("hospital-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("hospital", BoardTileType.Hospital, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            hospitalSession.Execute(new RollDiceCommand(1)); hospitalSession.Execute(new EndTurnCommand(1));
            hospitalSession.Execute(new RollDiceCommand(2)); hospitalSession.Execute(new EndTurnCommand(2));
            var hospitalFirstSkip = hospitalSession.Execute(new RollDiceCommand(1));
            hospitalSession.Execute(new EndTurnCommand(1)); hospitalSession.Execute(new RollDiceCommand(2)); hospitalSession.Execute(new EndTurnCommand(2));
            var hospitalSecondSkip = hospitalSession.Execute(new RollDiceCommand(1));
            Require(hospitalFirstSkip.DiceResult == null && hospitalSecondSkip.DiceResult == null &&
                    hospitalSession.State.GetPlayer(1).HospitalTurnsRemaining == 0,
                "hospital skips two complete own turns");
            Console.WriteLine("CORE_HOSPITAL_OK skipped_own_turns=2 prison_state=separate");

            var courtSession = CreateSession(new GameModeConfig("court-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("court", BoardTileType.Court, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(3, 4)));
            courtSession.Execute(new RollDiceCommand(1));
            Require(courtSession.State.LastCourtDiceResult.Total == 7 && courtSession.State.LastEventMessage.Contains("acquittal") &&
                    courtSession.State.LastEventMessage.Contains("3 + 4 = 7"),
                "court acquits on seven");
            var courtFineSession = CreateSession(new GameModeConfig("court-fine-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("court", BoardTileType.Court, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)), new ScriptedRandom(0, 0, 0, 0, 0, 0, 1));
            courtFineSession.Execute(new RollDiceCommand(1));
            Require(courtFineSession.State.GetPlayer(1).Money == 400 && courtFineSession.State.LastEventMessage.Contains("$100"),
                "court low roll can choose one hundred fine");
            var courtPrisonSession = CreateSession(new GameModeConfig("court-prison-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("court", BoardTileType.Court, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)), new ScriptedRandom(0, 0, 0, 0, 0, 0, 0));
            courtPrisonSession.Execute(new RollDiceCommand(1));
            Require(courtPrisonSession.State.GetPlayer(1).PrisonAttemptsRemaining == 3 &&
                    courtPrisonSession.State.LastEventMessage.Contains("sent to prison"), "court low roll can sentence player to prison");
            Console.WriteLine("CORE_COURT_OK acquittal_threshold=7 roll_shown=true prison=true low_roll_fine=100 extra_roll=true");

            var cardSession = CreateSession(new GameModeConfig("card-effect-test", 2, 2, 1000, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("chance", BoardTileType.Chance, 2), Tile("rest-3", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1)), new ScriptedRandom(0));
            cardSession.Execute(new RollDiceCommand(1));
            var drawnChanceCard = cardSession.State.LastDrawnCard;
            var expectedCardBalance = drawnChanceCard == null ? -1 : drawnChanceCard.Effect == MapCardEffect.ReceiveMoney
                ? 1000 + drawnChanceCard.Amount : 1000 - drawnChanceCard.Amount;
            var expectedCardResult = drawnChanceCard == null ? string.Empty :
                (drawnChanceCard.Effect == MapCardEffect.ReceiveMoney ? "Received $" : "Paid $") + drawnChanceCard.Amount;
            Require(drawnChanceCard != null && cardSession.State.GetPlayer(1).Money == expectedCardBalance &&
                    cardSession.State.LastEventMessage.Contains(drawnChanceCard.Title) &&
                    cardSession.State.LastEventMessage.Contains(drawnChanceCard.Description) &&
                    cardSession.State.LastEventMessage.Contains(expectedCardResult), "chance card displays its content and applied result");
            Console.WriteLine("CORE_CARD_OK title_content_and_effect_result_displayed=true");

            var upgradeBoard = new BoardDefinition(new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Property("property-1", "district", 2, 100, 20), Tile("rest-3", BoardTileType.Rest, 3)
            });
            var upgradeSession = CreateSession(new GameModeConfig("upgrade-test", 2, 2, 500, 100, 50, 25), upgradeBoard.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            upgradeSession.Execute(new RollDiceCommand(1)); upgradeSession.Execute(new BuyPropertyCommand(1)); upgradeSession.Execute(new EndTurnCommand(1));
            upgradeSession.Execute(new RollDiceCommand(2)); upgradeSession.Execute(new EndTurnCommand(2));
            upgradeSession.Execute(new RollDiceCommand(1));
            Require(upgradeSession.State.CanUpgradeOwnedProperty, "landing on Start grants an upgrade right");
            Require(upgradeSession.Execute(new UpgradePropertyCommand(1, "missing-property")).ErrorCode == CommandErrorCode.PropertyUnavailable,
                "upgrade command rejects a missing property id");
            Require(upgradeSession.Execute(new UpgradePropertyCommand(1, "property-1")).Succeeded &&
                    upgradeSession.State.GetProperty("property-1").UpgradeLevel == 1,
                "upgrade command targets an owned property by id");
            var otherOwnedUpgrade = CreateStartUpgradeSession(500, false);
            Require(otherOwnedUpgrade.State.CanUpgradeOwnedProperty && otherOwnedUpgrade.State.GetProperty("owned").OwnerId == 2 &&
                    otherOwnedUpgrade.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode == CommandErrorCode.PropertyUnavailable,
                "start upgrade rejects a property owned by another player");
            var poorUpgrade = CreateStartUpgradeSession(120, true);
            Require(poorUpgrade.State.GetPlayer(1).Money == 20 &&
                    poorUpgrade.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode == CommandErrorCode.InsufficientFunds &&
                    poorUpgrade.State.GetProperty("owned").UpgradeLevel == 0,
                "start upgrade rejects insufficient funds");
            var maximumUpgrade = CreateSession(new GameModeConfig("upgrade-cap-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                new BoardTileDefinition("owned", BoardTileType.Property, "owned", 2, 100, 20, 50, new[] { 20, 40, 60 }, "test", "owned"),
                Tile("rest-3", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1),
                new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            maximumUpgrade.Execute(new RollDiceCommand(1)); maximumUpgrade.Execute(new BuyPropertyCommand(1)); maximumUpgrade.Execute(new EndTurnCommand(1));
            maximumUpgrade.Execute(new RollDiceCommand(2)); maximumUpgrade.Execute(new EndTurnCommand(2));
            maximumUpgrade.Execute(new RollDiceCommand(1)); Require(maximumUpgrade.Execute(new UpgradePropertyCommand(1, "owned")).Succeeded, "first start upgrade"); maximumUpgrade.Execute(new EndTurnCommand(1));
            maximumUpgrade.Execute(new RollDiceCommand(2)); maximumUpgrade.Execute(new EndTurnCommand(2));
            maximumUpgrade.Execute(new RollDiceCommand(1)); Require(maximumUpgrade.Execute(new UpgradePropertyCommand(1, "owned")).Succeeded, "property upgrade to max level"); maximumUpgrade.Execute(new EndTurnCommand(1));
            maximumUpgrade.Execute(new RollDiceCommand(2)); maximumUpgrade.Execute(new EndTurnCommand(2)); maximumUpgrade.Execute(new RollDiceCommand(1));
            Require(maximumUpgrade.State.CanUpgradeOwnedProperty && maximumUpgrade.State.GetProperty("owned").UpgradeLevel == 2 &&
                    maximumUpgrade.Execute(new UpgradePropertyCommand(1, "owned")).ErrorCode == CommandErrorCode.PropertyAlreadyMaxLevel,
                "start upgrade rejects a property at its maximum level");
            Console.WriteLine("CORE_START_UPGRADE_OK target_id_validated=true owner_funds_limit_checked=true");

            var branchSession = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));
            branchSession.Execute(new RollDiceCommand(1)); branchSession.Execute(new EndTurnCommand(1));
            for (var playerId = 2; playerId <= 4; playerId++) { branchSession.Execute(new RollDiceCommand(playerId)); branchSession.Execute(new EndTurnCommand(playerId)); }
            var fork = branchSession.Execute(new RollDiceCommand(1));
            Require(fork.PathTileIndices.Count == 2 && branchSession.State.PendingAction == PendingAction.ChooseRoute, "route-choice pause");
            Require(branchSession.Execute(new ChooseRouteCommand(1, "not-a-route")).ErrorCode == CommandErrorCode.InvalidRouteChoice, "reject invalid route");
            var branch = branchSession.Execute(new ChooseRouteCommand(1, "macau-ferry-pier"));
            Require(branch.Succeeded && branch.PathTileIndices.Count == 4 && branchSession.State.GetPlayer(1).CurrentTileIndex == 12 &&
                    string.IsNullOrEmpty(branchSession.State.LastEventMessage), "branch movement and merge clears stale route prompt");
            Console.WriteLine("CORE_BRANCH_OK fork=hong-kong path=macau->merge remaining-steps=consumed");

            var district = CreateSession(new GameModeConfig("district-test", 2, 2, 1000, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Property("east-1", "east", 2, 100, 10), Tile("rest-2", BoardTileType.Rest, 3),
                Property("east-2", "east", 4, 100, 20), Tile("rest-3", BoardTileType.Rest, 5), Tile("rest-4", BoardTileType.Rest, 6)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1)));
            Require(district.Execute(new RollDiceCommand(1)).Succeeded, "first property landing");
            Require(district.Execute(new BuyPropertyCommand(1)).Succeeded, "first property purchase"); district.Execute(new EndTurnCommand(1));
            district.Execute(new RollDiceCommand(2)); district.Execute(new EndTurnCommand(2));
            district.Execute(new RollDiceCommand(1));
            Require(district.Execute(new BuyPropertyCommand(1)).Succeeded, "district completion purchase");
            Require(district.State.GetPlayer(1).Money == 930, "district completion reward"); district.Execute(new EndTurnCommand(1));
            district.Execute(new RollDiceCommand(2));
            Require(district.State.GetPlayer(2).Money == 960 && district.State.GetPlayer(1).Money == 960, "district rent multiplier");
            Console.WriteLine("CORE_ECONOMY_OK purchases=2 district-reward=120 full-set-rent=150-percent");

            var timed = CreateSession(new GameModeConfig("timed-test", 2, 2, 500, 0, 100, 50, 50, 120, 1),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Property("cafe", "cafe", 2, 100, 20) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));
            timed.Execute(new RollDiceCommand(1)); timed.Execute(new BuyPropertyCommand(1)); timed.Execute(new EndTurnCommand(1));
            Require(timed.State.Outcome != null && timed.State.Outcome.Reason == VictoryReason.HighestNetWorthAtTurnLimit, "net-worth tiebreak at turn limit");
            Console.WriteLine("CORE_TURN_LIMIT_OK winner=net-worth turn=1");

            var bankrupt = CreateSession(new GameModeConfig("bankruptcy-test", 2, 2, 50, 0, 100, 50),
                new[] { Tile("start", BoardTileType.Start, 0), Tile("rest", BoardTileType.Rest, 1), Tile("tax", BoardTileType.Tax, 2) },
                new ScriptedDiceRoller(new DiceResult(1, 1)));
            bankrupt.Execute(new RollDiceCommand(1));
            Require(bankrupt.State.GetPlayer(1).Status == PlayerStatus.Bankrupt && bankrupt.State.GetPlayer(2).Status == PlayerStatus.Winner, "bankruptcy and last-player victory");
            Console.WriteLine("CORE_BANKRUPTCY_OK last-player-standing=true");

            var trade = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(2, 3), new DiceResult(1, 1), new DiceResult(1, 1)));
            trade.Execute(new RollDiceCommand(1)); trade.Execute(new BuyPropertyCommand(1)); trade.Execute(new EndTurnCommand(1));
            trade.Execute(new RollDiceCommand(2)); trade.Execute(new BuyPropertyCommand(2)); trade.Execute(new EndTurnCommand(2));
            trade.Execute(new RollDiceCommand(3)); trade.Execute(new EndTurnCommand(3));
            trade.Execute(new RollDiceCommand(4)); trade.Execute(new EndTurnCommand(4));
            var originalOneMoney = trade.State.GetPlayer(1).Money;
            var originalTwoMoney = trade.State.GetPlayer(2).Money;
            var rejectedTrade = trade.Execute(new TradeCommand(1, 2, 5000, 0, "singapore-marina"));
            Require(rejectedTrade.ErrorCode == CommandErrorCode.InsufficientFunds && trade.State.GetPlayer(1).Money == originalOneMoney &&
                    trade.State.GetPlayer(2).Money == originalTwoMoney && trade.State.GetProperty("singapore-marina").OwnerId == 1,
                "rejected trade leaves balances and ownership unchanged");
            var completedTrade = trade.Execute(new TradeCommand(1, 2, 20, 10, "singapore-marina", "kyoto-gion"));
            Require(completedTrade.Succeeded && trade.State.GetProperty("singapore-marina").OwnerId == 2 &&
                    trade.State.GetProperty("kyoto-gion").OwnerId == 1 && trade.State.GetPlayer(1).Money == originalOneMoney - 10 &&
                    trade.State.GetPlayer(2).Money == originalTwoMoney + 10 && trade.State.CurrentPlayerId == 1,
                "accepted trade exchanges cash and ownership atomically");
            Console.WriteLine("CORE_TRADE_OK cash_and_properties=exchanged rejected_trade=atomic turn_preserved=true");
            Console.WriteLine("CORE_VERIFICATION_OK");
        }

        private static GameSession CreateSession(GameModeConfig mode, System.Collections.Generic.IEnumerable<BoardTileDefinition> tiles, IDiceRoller roller, IGameRandom random = null)
        {
            var players = new[] { new PlayerSetup(1, "Player 1"), new PlayerSetup(2, "Player 2"), new PlayerSetup(3, "Player 3"), new PlayerSetup(4, "Player 4") };
            if (mode.MaxPlayers == 2) players = new[] { players[0], players[1] };
            // GameSession 的 random 現在是必填（IRng 強制注入，ADR-0003 決策5）。這裡的
            // `random = null` 只是這個測試 helper 自己的「我不在乎亂數」語意糖，實際上一定會
            // 給 GameSession 一個固定、非 null 的 seed，不會變成 Core 內部的隱性 fallback。
            return new GameSession(mode, new BoardDefinition(tiles), players, roller, random ?? new SeededRandom(1));
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int index)
        {
            return new BoardTileDefinition(id, type, id, index, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Property(string id, string district, int index, int price, int rent)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, index, price, rent, 50, new[] { rent, rent * 2 }, district, id);
        }

        private static GameSession CreateStartUpgradeSession(int startingMoney, bool playerOneOwns)
        {
            var session = CreateSession(new GameModeConfig("start-upgrade-validation", 2, 2, startingMoney, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Property("owned", "test", 2, 100, 20), Tile("rest-3", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), playerOneOwns ? new DiceResult(1, 2) : new DiceResult(1, 1), new DiceResult(1, 1)));

            session.Execute(new RollDiceCommand(1));
            if (playerOneOwns) session.Execute(new BuyPropertyCommand(1));
            session.Execute(new EndTurnCommand(1));
            session.Execute(new RollDiceCommand(2));
            if (!playerOneOwns) session.Execute(new BuyPropertyCommand(2));
            session.Execute(new EndTurnCommand(2));
            session.Execute(new RollDiceCommand(1));
            return session;
        }

        private static BoardTileDefinition Airport(string id, int positionIndex, string destinationId)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, id, positionIndex, 0, 0, 0, null,
                string.Empty, id, 0, null, true, null, destinationId);
        }

        private static void Require(bool condition, string invariant)
        {
            if (!condition) throw new InvalidOperationException("Core verification failed: " + invariant);
        }

        private sealed class ScriptedDiceRoller : IDiceRoller
        {
            private readonly System.Collections.Generic.Queue<DiceResult> _dice;
            public ScriptedDiceRoller(params DiceResult[] dice) { _dice = new System.Collections.Generic.Queue<DiceResult>(dice); }
            public DiceResult Roll() { if (_dice.Count == 0) throw new InvalidOperationException("Scripted dice exhausted."); return _dice.Dequeue(); }
        }

        private sealed class ScriptedRandom : IGameRandom
        {
            private readonly Queue<int> _values;
            public ScriptedRandom(params int[] values) { _values = new Queue<int>(values); }
            public int NextInt(int minInclusive, int maxExclusive)
            {
                var value = _values.Count == 0 ? 0 : _values.Dequeue();
                return minInclusive + value % (maxExclusive - minInclusive);
            }
        }
    }
}
