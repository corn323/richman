using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotBoardView : Node3D
    {
        private readonly Dictionary<int, Node3D> _tiles = new Dictionary<int, Node3D>();
        private readonly Dictionary<int, Node3D> _pawns = new Dictionary<int, Node3D>();
        private readonly Dictionary<int, StandardMaterial3D> _tileMaterials = new Dictionary<int, StandardMaterial3D>();
        private readonly Dictionary<int, Dictionary<string, object>> _metadata = new Dictionary<int, Dictionary<string, object>>();
        private BoardDefinition _board;

        public void Build(BoardDefinition board, IReadOnlyList<PlayerState> players)
        {
            _board = board;
            var root = new Node3D { Name = "ImportedArchipelago" };
            AddChild(root);
            var gltf = new GltfDocument();
            var gltfState = new GltfState();
            var gltfError = gltf.AppendFromFile(ProjectSettings.GlobalizePath("res://Assets/archipelago_map.glb"), gltfState);
            if (gltfError == Error.Ok)
            {
                var model = gltf.GenerateScene(gltfState);
                if (model == null) throw new InvalidDataException("Godot could not create a scene from archipelago_map.glb.");
                root.AddChild(model);
                foreach (var child in model.FindChildren("TileSocket_*", "MeshInstance3D", true, false))
                    if (child is VisualInstance3D visual) visual.Visible = false;
            }
            else throw new InvalidDataException("Godot GLB load failed: " + gltfError);
            LoadTileData(board);
            for (var i = 0; i < board.TileCount; i++) BuildTile(board.GetTileAt(i));
            for (var i = 0; i < players.Count; i++) BuildPawn(players[i]);
            ValidateRoute();
        }

        public Vector3 GetTilePosition(int tileIndex)
        {
            var data = _metadata[tileIndex];
            return new Vector3(Number(data, "x"), Number(data, "y"), Number(data, "z"));
        }

        public Node3D GetPawn(int playerId) => _pawns.TryGetValue(playerId, out var pawn) ? pawn : null;

        public void SyncPawns(GameState state)
        {
            for (var i = 0; i < state.Players.Count; i++)
            {
                var player = state.Players[i];
                if (_pawns.TryGetValue(player.Id, out var pawn)) pawn.Position = GetPawnPosition(player.CurrentTileIndex, player.Id);
            }
        }

        public void ApplyState(GameState state)
        {
            for (var i = 0; i < state.Board.TileCount; i++)
            {
                var tile = state.Board.GetTileAt(i);
                var property = state.GetProperty(tile.Id);
                _tileMaterials[i].AlbedoColor = TileColor(tile, property);
                UpdatePropertyVisual(_tiles[i], tile, property);
            }
        }

        public async System.Threading.Tasks.Task AnimatePawn(int playerId, int fromTileIndex, int steps)
        {
            if (!_pawns.TryGetValue(playerId, out var pawn) || _board == null) return;
            for (var step = 1; step <= steps; step++)
            {
                var targetIndex = (fromTileIndex + step) % _board.TileCount;
                var target = GetPawnPosition(targetIndex, playerId);
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(pawn, "position", target, 0.22f);
                await ToSignal(tween, Tween.SignalName.Finished);
            }
        }

        public async System.Threading.Tasks.Task AnimateTransfer(int playerId, int destinationTileIndex)
        {
            if (!_pawns.TryGetValue(playerId, out var pawn)) return;
            var target = GetPawnPosition(destinationTileIndex, playerId);
            var tween = CreateTween();
            tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(pawn, "position", target + Vector3.Up * 1.6f, 0.35f);
            tween.TweenProperty(pawn, "position", target, 0.3f);
            await ToSignal(tween, Tween.SignalName.Finished);
        }

        public async System.Threading.Tasks.Task AnimateBranch(int playerId, int fromTileIndex)
        {
            if (!_metadata.TryGetValue(fromTileIndex, out var source) || !source.TryGetValue("branch_tiles", out var branchValue) || ((List<string>)branchValue).Count == 0) return;
            var branchId = ((List<string>)branchValue)[0];
            var mainNextId = ((List<string>)source["next_tiles"])[0];
            if (!_pawns.TryGetValue(playerId, out var pawn)) return;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (branchId != mainNextId)
            {
                if (!visited.Add(branchId)) throw new InvalidDataException("Branch does not rejoin main route: " + branchId);
                var branch = FindById(branchId);
                var target = new Vector3(Number(branch, "x"), Number(branch, "y") + .18f, Number(branch, "z"));
                var tween = CreateTween(); tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut); tween.TweenProperty(pawn, "position", target, .28f);
                await ToSignal(tween, Tween.SignalName.Finished);
                branchId = ((List<string>)branch["next_tiles"])[0];
            }
            var destination = FindById(mainNextId);
            pawn.Position = GetPawnPosition(Convert.ToInt32(destination["position_index"], CultureInfo.InvariantCulture), playerId);
        }

        private Dictionary<string, object> FindById(string id)
        {
            foreach (var row in _metadata.Values) if ((string)row["id"] == id) return row;
            throw new InvalidDataException("Missing map tile id: " + id);
        }

        private void LoadTileData(BoardDefinition board)
        {
            var path = ProjectSettings.GlobalizePath("res://Data/archipelago_tiles.json");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var item in document.RootElement.GetProperty("tiles").EnumerateArray())
            {
                var index = item.GetProperty("position_index").GetInt32();
                var values = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var property in item.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String) values[property.Name] = property.Value.GetString();
                    else if (property.Value.ValueKind == JsonValueKind.Number) values[property.Name] = property.Value.GetSingle();
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var next = new List<string>();
                        foreach (var v in property.Value.EnumerateArray()) next.Add(v.GetString());
                        values[property.Name] = next;
                    }
                    else if (property.Value.ValueKind == JsonValueKind.True || property.Value.ValueKind == JsonValueKind.False) values[property.Name] = property.Value.GetBoolean();
                }
                _metadata.Add(index, values);
            }
            if (_metadata.Count < board.TileCount) throw new InvalidDataException("Map tile data must contain every active board tile.");
            for (var i = 0; i < board.TileCount; i++)
            {
                var definition = board.GetTileAt(i);
                var row = _metadata[i];
                if (!string.Equals(definition.Id, (string)row["id"], StringComparison.Ordinal))
                    throw new InvalidDataException("Core/JSON tile id mismatch at " + i + ": " + definition.Id + " != " + row["id"]);
                if (!string.Equals(JsonType(definition.Type), (string)row["type"], StringComparison.Ordinal))
                    throw new InvalidDataException("Core/JSON tile type mismatch at " + i + ": " + definition.Type + " != " + row["type"]);
            }
        }

        private static string JsonType(BoardTileType type)
        {
            switch (type)
            {
                case BoardTileType.Start: return "start";
                case BoardTileType.Property: return "property";
                case BoardTileType.Tax: return "tax";
                case BoardTileType.Bonus: return "bonus";
                case BoardTileType.Rest: return "special";
                case BoardTileType.Transit: return "transit";
                case BoardTileType.Special: return "landmark";
                case BoardTileType.Airport: return "airport";
                case BoardTileType.Hospital: return "hospital";
                case BoardTileType.Prison: return "prison";
                case BoardTileType.Court: return "court";
                case BoardTileType.Chance: return "chance";
                case BoardTileType.Destiny: return "destiny";
                default: throw new InvalidDataException("Unsupported board tile type: " + type);
            }
        }

        private void BuildTile(BoardTileDefinition definition)
        {
            var data = _metadata[definition.PositionIndex];
            var tile = new Node3D { Name = "Tile_" + definition.Id, Position = GetTilePosition(definition.PositionIndex), RotationDegrees = new Vector3(0, Number(data, "rotation"), 0) };
            AddChild(tile);
            var special = (string)data["type"];
            var colors = special == "start" ? "#ffe16b" : special == "airport" ? "#ff914d" : special == "prison" ? "#566579" : special == "hospital" ? "#fff4ef" : special == "court" ? "#d7c5aa" : special == "chance" ? "#d79af2" : special == "destiny" ? "#77a8e8" : TileColor(definition, null).ToHtml();
            var material = Material(new Color(colors));
            var plate = new MeshInstance3D { Name = "WalkableTile", Mesh = new BoxMesh { Size = new Vector3(1.25f, 0.3f, 1.05f) }, Position = new Vector3(0, -0.05f, 0), MaterialOverride = material };
            tile.AddChild(plate);
            _tileMaterials[definition.PositionIndex] = material;
            _tiles[definition.PositionIndex] = tile;
            var label = new Label3D
            {
                Text = definition.PositionIndex.ToString("00", CultureInfo.InvariantCulture) + "  " + ShortName((string)data["display_name"]),
                FontSize = 22, PixelSize = 0.0034f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, 0.34f, 0), Modulate = new Color("#182b3b"), NoDepthTest = true
            };
            tile.AddChild(label);
            if (special == "start") AddMarker(tile, "START", new Color("#182b3b"), 0.38f);
            if (special == "airport") AddMarker(tile, "AIRPORT", new Color("#fff0c4"), 0.20f);
            if (special == "hospital") AddMarker(tile, "+", new Color("#e94b52"), 0.32f);
            if (special == "prison") AddMarker(tile, "JAIL", new Color("#fff4d6"), 0.22f);
            if (special == "court") AddMarker(tile, "§", new Color("#a36d30"), 0.32f);
            if (special == "chance") AddMarker(tile, "?", new Color("#fff0ff"), 0.32f);
            if (special == "destiny") AddMarker(tile, "DESTINY", new Color("#f3f8ff"), 0.18f);
        }

        private void AddMarker(Node3D tile, string text, Color color, float fontSize)
        {
            tile.AddChild(new Label3D { Text = text, FontSize = (int)(fontSize * 100), PixelSize = 0.006f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 0.42f, -0.23f), Modulate = color, NoDepthTest = true });
        }

        private void ValidateRoute()
        {
            var id = "start";
            for (var i = 0; i < _board.TileCount; i++)
            {
                var row = _metadata[i];
                if (!string.Equals((string)row["id"], id, StringComparison.Ordinal)) throw new InvalidDataException("Main route order is not contiguous at " + i);
                id = ((List<string>)row["next_tiles"])[0];
            }
            if (id != "start") throw new InvalidDataException("Main route does not return to START.");
            var byId = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var row in _metadata.Values) byId[(string)row["id"]] = row;
            foreach (var row in _metadata.Values)
            {
                foreach (var next in (List<string>)row["branch_tiles"])
                {
                    var branchId = next;
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var mergeId = ((List<string>)row["next_tiles"])[0];
                    var merged = false;
                    while (visited.Add(branchId))
                    {
                        if (!byId.TryGetValue(branchId, out var branch)) throw new InvalidDataException("Branch is disconnected: " + branchId);
                        if (branchId == mergeId) { merged = true; break; }
                        if (branch.TryGetValue("main_route", out var isMain) && (bool)isMain) break;
                        branchId = ((List<string>)branch["next_tiles"])[0];
                    }
                    if (!merged) throw new InvalidDataException("Branch does not rejoin the declared main route: " + next);
                }
            }
            GD.Print("ARCHIPELAGO_ROUTE_OK main=", _board.TileCount, " branches=", _metadata.Count - _board.TileCount, " closed=start");
        }

        private static string ShortName(string value)
        {
            var separator = value.IndexOf('·');
            if (separator >= 0 && separator + 1 < value.Length) return value.Substring(separator + 1).Trim();
            return value.Length > 16 ? value.Substring(0, 14) + "…" : value;
        }

        private void BuildPawn(PlayerState player)
        {
            var pawn = new Node3D { Name = "Pawn_" + player.Id, Position = GetPawnPosition(player.CurrentTileIndex, player.Id) };
            pawn.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.22f, Height = 0.7f }, Position = Vector3.Up * 0.35f, MaterialOverride = Material(PlayerColor(player.Id)) });
            pawn.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.22f, Height = 0.44f }, Position = Vector3.Up * 0.79f, MaterialOverride = Material(new Color("#ffe0bd")) });
            pawn.AddChild(new Label3D { Text = "P" + player.Id, FontSize = 28, PixelSize = 0.003f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = Vector3.Up * 1.15f, Modulate = PlayerColor(player.Id) });
            AddChild(pawn);
            _pawns[player.Id] = pawn;
        }

        private Vector3 GetPawnPosition(int tileIndex, int playerId)
        {
            var offset = new Vector3(((playerId - 1) % 2) * 0.22f - 0.11f, 0.18f, ((playerId - 1) / 2) * 0.22f - 0.11f);
            return GetTilePosition(tileIndex) + offset;
        }

        private static float Number(Dictionary<string, object> data, string key) => Convert.ToSingle(data[key], CultureInfo.InvariantCulture);

        private static void UpdatePropertyVisual(Node3D tile, BoardTileDefinition definition, PropertyState property)
        {
            var existing = tile.GetNodeOrNull<Node3D>("Building");
            if (existing != null) existing.QueueFree();
            if (definition.Type != BoardTileType.Property || property == null || !property.OwnerId.HasValue || property.UpgradeLevel == 0) return;
            tile.AddChild(new MeshInstance3D { Name = "Building", Position = Vector3.Up * 0.5f, Mesh = new BoxMesh { Size = new Vector3(0.42f, 0.55f, 0.42f) }, MaterialOverride = Material(PlayerColor(property.OwnerId.Value)) });
        }

        private static Color TileColor(BoardTileDefinition definition, PropertyState property)
        {
            if (property != null && property.OwnerId.HasValue) return PlayerColor(property.OwnerId.Value).Lerp(Colors.White, 0.18f);
            if (definition.Type == BoardTileType.Start) return new Color("#ffe16b");
            if (definition.Type != BoardTileType.Property) return new Color("#d6c5a5");
            var colors = new Dictionary<string, Color> { ["market"] = new Color("#f4c95d"), ["harbor"] = new Color("#76b5d8"), ["industrial"] = new Color("#a7a9ac"), ["residential"] = new Color("#88c999"), ["entertainment"] = new Color("#d58bd7"), ["technology"] = new Color("#8d9ff0"), ["civic"] = new Color("#ef9d76") };
            return colors.TryGetValue(definition.DistrictId, out var color) ? color : new Color("#c9c9c9");
        }

        private static Color PlayerColor(int id)
        {
            var colors = new[] { new Color("#ef6262"), new Color("#4d96ff"), new Color("#54c878"), new Color("#b678e6") };
            return colors[(id - 1) % colors.Length];
        }

        private static StandardMaterial3D Material(Color color) => new StandardMaterial3D { AlbedoColor = color, Roughness = 0.78f };
    }
}
