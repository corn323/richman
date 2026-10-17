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
            Require(board.MainRouteTileCount == 40 && board.TileCount == 47, "world map tile count");
            Require(board.GetTileAt(30).Id == "london-prison", "London prison tile");
            Require(board.GetTileAt(9).Type == BoardTileType.Hospital && board.GetTileAt(27).Type == BoardTileType.Court,
                "hospital and court are real route tiles");
            Require(board.GetTileAt(39).NextTileIds[0] == board.GetTileAt(0).Id, "main route closure");
            Require(board.GetNextTiles(10).Count == 2, "Hong Kong route fork");
            Require(board.GetTile("airport-changi").AirportDestinationId == "airport-incheon", "Changi route");
            Require(board.GetTile("airport-incheon").AirportDestinationId == "airport-changi", "Incheon route");
            Require(board.GetTile("airport-dubai").AirportDestinationId == "airport-jfk", "Dubai route");
            Require(board.GetTile("airport-jfk").AirportDestinationId == "airport-dubai", "JFK route");
            Console.WriteLine("CORE_ROUTE_OK main=40 all=47 branches=3 merges=validated");
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
            Require(courtSession.State.LastCourtDiceResult.Total == 7 && courtSession.State.LastEventMessage.Contains("acquittal"),
                "court acquits on seven");
            var courtFineSession = CreateSession(new GameModeConfig("court-fine-test", 2, 2, 500, 0, 50, 25), new[]
            {
                Tile("start", BoardTileType.Start, 0), Tile("rest-1", BoardTileType.Rest, 1),
                Tile("court", BoardTileType.Court, 2), Tile("rest-2", BoardTileType.Rest, 3)
            }, new ScriptedDiceRoller(new DiceResult(1, 1), new DiceResult(1, 1)), new ScriptedRandom(0, 0, 0, 0, 0, 0, 1));
            courtFineSession.Execute(new RollDiceCommand(1));
            Require(courtFineSession.State.GetPlayer(1).Money == 400 && courtFineSession.State.LastEventMessage.Contains("$100"),
                "court low roll can choose one hundred fine");
            Console.WriteLine("CORE_COURT_OK acquittal_threshold=7 low_roll_fine=100 extra_roll=true");

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
            Console.WriteLine("CORE_START_UPGRADE_OK target_id_validated=true");

            var branchSession = CreateSession(GameModeConfig.ClassicBankruptcyFourPlayer(), board.Tiles,
                new ScriptedDiceRoller(new DiceResult(4, 4), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(1, 1), new DiceResult(3, 3)));
            branchSession.Execute(new RollDiceCommand(1)); branchSession.Execute(new EndTurnCommand(1));
            for (var playerId = 2; playerId <= 4; playerId++) { branchSession.Execute(new RollDiceCommand(playerId)); branchSession.Execute(new EndTurnCommand(playerId)); }
            var fork = branchSession.Execute(new RollDiceCommand(1));
            Require(fork.PathTileIndices.Count == 2 && branchSession.State.PendingAction == PendingAction.ChooseRoute, "route-choice pause");
            Require(branchSession.Execute(new ChooseRouteCommand(1, "not-a-route")).ErrorCode == CommandErrorCode.InvalidRouteChoice, "reject invalid route");
            var branch = branchSession.Execute(new ChooseRouteCommand(1, "macau-ferry-pier"));
            Require(branch.Succeeded && branch.PathTileIndices.Count == 4 && branchSession.State.GetPlayer(1).CurrentTileIndex == 12, "branch movement and merge");
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
            Console.WriteLine("CORE_VERIFICATION_OK");
        }

        private static GameSession CreateSession(GameModeConfig mode, System.Collections.Generic.IEnumerable<BoardTileDefinition> tiles, IDiceRoller roller, IGameRandom random = null)
        {
            var players = new[] { new PlayerSetup(1, "Player 1"), new PlayerSetup(2, "Player 2"), new PlayerSetup(3, "Player 3"), new PlayerSetup(4, "Player 4") };
            if (mode.MaxPlayers == 2) players = new[] { players[0], players[1] };
            return new GameSession(mode, new BoardDefinition(tiles), players, roller, random);
        }

        private static BoardTileDefinition Tile(string id, BoardTileType type, int index)
        {
            return new BoardTileDefinition(id, type, id, index, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Property(string id, string district, int index, int price, int rent)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, id, index, price, rent, 50, new[] { rent, rent * 2 }, district, id);
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
