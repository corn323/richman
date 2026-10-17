using System.Collections.Generic;
using System.IO;
using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotBoardView : Node3D
    {
        private readonly Dictionary<int, Node3D> _tiles = new Dictionary<int, Node3D>();
        private readonly Dictionary<int, Node3D> _pawns = new Dictionary<int, Node3D>();
        private readonly Dictionary<int, StandardMaterial3D> _tileMaterials = new Dictionary<int, StandardMaterial3D>();
        private BoardDefinition _board;

        public void Build(BoardDefinition board, IReadOnlyList<PlayerState> players)
        {
            _board = board;
            var root = new Node3D { Name = "ImportedWorldTour" };
            AddChild(root);
            var importedMap = GD.Load<PackedScene>("res://Assets/archipelago_map.glb");
            if (importedMap == null) throw new InvalidDataException("Godot could not load the imported archipelago_map.glb scene.");
            var model = importedMap.Instantiate<Node3D>();
            if (model == null) throw new InvalidDataException("Godot could not instantiate archipelago_map.glb.");
            root.AddChild(model);
            foreach (var pattern in new[] { "TileSocket_*", "TileTop_*", "DistrictBand_*", "TileLabel_*", "Icon_*" })
                foreach (var child in model.FindChildren(pattern, "Node3D", true, false))
                    if (child is Node3D visual) visual.Visible = false;

            for (var i = 0; i < board.TileCount; i++) BuildTile(board.GetTileAt(i));
            for (var i = 0; i < players.Count; i++) BuildPawn(players[i]);
            ValidateRoute();
        }

        public Vector3 GetTilePosition(int tileIndex)
        {
            var p = _board.GetTileAt(tileIndex).MapPosition;
            return new Vector3(p.X, p.Y, p.Z);
        }

        public Vector3 GetTilePosition(string tileId)
        {
            var tile = _board.GetTile(tileId);
            if (tile == null) throw new InvalidDataException("Missing tile id: " + tileId);
            return GetTilePosition(tile.PositionIndex);
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

        public async System.Threading.Tasks.Task AnimatePawn(int playerId, IReadOnlyList<int> pathTileIndices)
        {
            if (!_pawns.TryGetValue(playerId, out var pawn) || pathTileIndices == null) return;
            for (var step = 0; step < pathTileIndices.Count; step++)
            {
                var target = GetPawnPosition(pathTileIndices[step], playerId);
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(pawn, "position", target + Vector3.Up * 0.32f, 0.11f);
                tween.TweenProperty(pawn, "position", target, 0.11f);
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

        private void BuildTile(BoardTileDefinition definition)
        {
            var tile = new Node3D
            {
                Name = "Tile_" + definition.Id,
                Position = GetTilePosition(definition.PositionIndex),
                RotationDegrees = new Vector3(0, definition.MapPosition.RotationDegrees, 0)
            };
            AddChild(tile);
            var material = Material(TileColor(definition, null));
            var plate = new MeshInstance3D
            {
                Name = "WalkableTile",
                Mesh = new BoxMesh { Size = new Vector3(1.25f, 0.3f, 1.05f) },
                Position = new Vector3(0, -0.05f, 0),
                MaterialOverride = material
            };
            tile.AddChild(plate);
            _tileMaterials[definition.PositionIndex] = material;
            _tiles[definition.PositionIndex] = tile;
            tile.AddChild(new Label3D
            {
                Text = definition.DisplayName,
                FontSize = 22, PixelSize = 0.0030f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0, 0.34f, 0), Modulate = new Color("#182b3b"), NoDepthTest = true
            });
            var marker = MarkerFor(definition.Type);
            if (!string.IsNullOrEmpty(marker))
                tile.AddChild(new Label3D { Text = marker, FontSize = 22, PixelSize = 0.004f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = new Vector3(0, 0.43f, -0.25f), Modulate = new Color("#182b3b"), NoDepthTest = true });
        }

        private static string MarkerFor(BoardTileType type)
        {
            switch (type)
            {
                case BoardTileType.Start: return "出發";
                case BoardTileType.Airport: return "✈";
                case BoardTileType.Prison: return "監獄";
                case BoardTileType.Hospital: return "醫院";
                case BoardTileType.Court: return "法院";
                case BoardTileType.Chance: return "機會";
                case BoardTileType.Destiny: return "命運";
                case BoardTileType.Transit: return "交通";
                default: return string.Empty;
            }
        }

        private void ValidateRoute()
        {
            if (_board.MainRouteTileCount != 41 || _board.TileCount != 48) throw new InvalidDataException("World tour Core data should define 41 main route tiles and 48 total tiles.");
            for (var i = 0; i < _board.MainRouteTileCount; i++)
            {
                var expected = _board.GetTileAt((i + 1) % _board.MainRouteTileCount).Id;
                if (!_board.GetTileAt(i).NextTileIds.ContainsOrdinal(expected)) throw new InvalidDataException("Main route is not closed at " + _board.GetTileAt(i).Id);
            }
            GD.Print("WORLD_TOUR_ROUTE_OK main=", _board.MainRouteTileCount, " all=", _board.TileCount, " graph=Core-authoritative");
        }

        private void BuildPawn(PlayerState player)
        {
            var pawn = new Node3D { Name = "Pawn_" + player.Id, Position = GetPawnPosition(player.CurrentTileIndex, player.Id) };
            pawn.AddChild(new MeshInstance3D { Name = "PawnBase", Mesh = new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.31f, Height = 0.12f, RadialSegments = 10 }, Position = Vector3.Up * 0.08f, MaterialOverride = Material(PlayerColor(player.Id).Darkened(0.18f)) });
            pawn.AddChild(new MeshInstance3D { Name = "PawnBody", Mesh = new SphereMesh { Radius = 0.22f, Height = 0.68f, RadialSegments = 10, Rings = 6 }, Position = Vector3.Up * 0.42f, MaterialOverride = Material(PlayerColor(player.Id)) });
            pawn.AddChild(new MeshInstance3D { Name = "PawnCrown", Mesh = new SphereMesh { Radius = 0.16f, Height = 0.28f, RadialSegments = 8, Rings = 4 }, Position = Vector3.Up * 0.76f, MaterialOverride = Material(new Color("#fff0bf")) });
            pawn.AddChild(new Label3D { Text = "P" + player.Id, FontSize = 26, PixelSize = 0.003f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = Vector3.Up * 1.08f, Modulate = PlayerColor(player.Id) });
            AddChild(pawn);
            _pawns[player.Id] = pawn;
        }

        private Vector3 GetPawnPosition(int tileIndex, int playerId)
        {
            var offset = new Vector3(((playerId - 1) % 2) * 0.22f - 0.11f, 0.18f, ((playerId - 1) / 2) * 0.22f - 0.11f);
            return GetTilePosition(tileIndex) + offset;
        }

        private static void UpdatePropertyVisual(Node3D tile, BoardTileDefinition definition, PropertyState property)
        {
            var existing = tile.GetNodeOrNull<Node3D>("Building");
            if (existing != null) existing.QueueFree();
            if (definition.Type != BoardTileType.Property || property == null || !property.OwnerId.HasValue || property.UpgradeLevel <= 0) return;

            var level = property.UpgradeLevel;
            var building = new Node3D { Name = "Building" };
            var color = PlayerColor(property.OwnerId.Value);
            var width = level == 1 ? .40f : level == 2 ? .55f : .66f;
            var height = level == 1 ? .40f : level == 2 ? .65f : .88f;
            building.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(width, height, width) }, Position = new Vector3(0, .15f + height * .5f, 0), MaterialOverride = Material(new Color("#fff0d3")) });
            building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { BottomRadius = width * .75f, TopRadius = .04f, Height = .27f, RadialSegments = 6 }, Position = new Vector3(0, .2f + height + .13f, 0), MaterialOverride = Material(color) });
            if (level >= 3)
                building.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(width * .72f, .12f, width * .72f) }, Position = new Vector3(0, .2f + height * .58f, 0), MaterialOverride = Material(color.Lightened(.16f)) });
            if (level >= 4)
                building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .10f, BottomRadius = .18f, Height = .48f, RadialSegments = 8 }, Position = new Vector3(0, .2f + height + .42f, 0), MaterialOverride = Material(new Color("#e5aa4f")) });
            tile.AddChild(building);
        }

        private static Color TileColor(BoardTileDefinition definition, PropertyState property)
        {
            if (property != null && property.OwnerId.HasValue) return PlayerColor(property.OwnerId.Value).Lerp(Colors.White, 0.18f);
            switch (definition.Type)
            {
                case BoardTileType.Start: return new Color("#ffe16b");
                case BoardTileType.Airport: return new Color("#ff914d");
                case BoardTileType.Prison: return new Color("#566579");
                case BoardTileType.Hospital: return new Color("#fff4ef");
                case BoardTileType.Court: return new Color("#d7c5aa");
                case BoardTileType.Chance: return new Color("#d79af2");
                case BoardTileType.Destiny: return new Color("#77a8e8");
                case BoardTileType.Property:
                    var colors = new Dictionary<string, Color>
                    {
                        ["singapore"] = new Color("#70c7b0"), ["japan"] = new Color("#ef9a86"), ["hong-kong"] = new Color("#e9bd5f"),
                        ["macau"] = new Color("#e9bd5f"), ["korea"] = new Color("#7d9fe0"), ["china"] = new Color("#da665f"),
                        ["bali"] = new Color("#7ac583"), ["italy"] = new Color("#a5ca78"), ["france"] = new Color("#7ab7da"),
                        ["london"] = new Color("#df8c6d"), ["united-states"] = new Color("#7897d1"), ["south-america"] = new Color("#c17cbd"),
                        ["southern-stops"] = new Color("#79b8a7"), ["spain"] = new Color("#e9a958"), ["southeast-asia"] = new Color("#78b978"),
                        ["silk-road"] = new Color("#d99656")
                    };
                    if (colors.TryGetValue(definition.DistrictId, out var districtColor)) return districtColor;
                    return new Color("#c9c9c9");
                default: return new Color("#d6c5a5");
            }
        }

        private static Color PlayerColor(int id)
        {
            var colors = new[] { new Color("#ef6262"), new Color("#4d96ff"), new Color("#54c878"), new Color("#b678e6") };
            return colors[(id - 1) % colors.Length];
        }

        private static StandardMaterial3D Material(Color color) => new StandardMaterial3D { AlbedoColor = color, Roughness = 0.78f };
    }
}
