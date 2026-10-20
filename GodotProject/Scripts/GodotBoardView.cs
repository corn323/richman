using System;
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
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, int> _playerCharacters = new Dictionary<int, int>();
        private BoardDefinition _board;

        public void Build(BoardDefinition board, IReadOnlyList<PlayerState> players, int[] characters, Color[] colors)
        {
            _board = board;
            // Phase 1d-3（ADR-0002 §3）：BoardLayoutPlanner.AssignMapPositions 不再是
            // BoardDefinition 建構流程的一部分（規則路徑不讀座標）——展示層是座標的唯一
            // 消費端之一，必須在讀任何 tile.MapPosition（BuildTile 以下）之前自己顯式呼叫。
            BoardLayoutPlanner.AssignMapPositions(board);
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
            for (var i = 0; i < players.Count; i++)
            {
                _playerCharacters[players[i].Id] = characters[i]; _playerColors[players[i].Id] = colors[i]; BuildPawn(players[i]);
            }
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

        public void SetPreview(int playerId, int character, Color color)
        {
            _playerCharacters[playerId] = character; _playerColors[playerId] = color;
            if (_pawns.TryGetValue(playerId, out var old)) { old.QueueFree(); _pawns.Remove(playerId); }
            BuildPawn(playerId, _board.StartPositionIndex, character, color);
        }

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
                if (!ContainsOrdinal(_board.GetTileAt(i).NextTileIds, expected)) throw new InvalidDataException("Main route is not closed at " + _board.GetTileAt(i).Id);
            }
            GD.Print("WORLD_TOUR_ROUTE_OK main=", _board.MainRouteTileCount, " all=", _board.TileCount, " graph=Core-authoritative");
        }

        // Richman.Core 現在是獨立組件（ADR-0003 決策6），Core 內部同等用途的
        // BoardStringExtensions.ContainsOrdinal 是 internal，展示層本就不該存取——
        // 這裡維護一份僅供本檔使用的等效 helper，而不是把 Core 的 internal 改成 public。
        private static bool ContainsOrdinal(IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
            return false;
        }

        private void BuildPawn(PlayerState player) => BuildPawn(player, _playerCharacters.TryGetValue(player.Id, out var character) ? character : 0, _playerColors.TryGetValue(player.Id, out var color) ? color : PlayerColor(player.Id));

        private void BuildPawn(PlayerState player, int character, Color color)
        {
            BuildPawn(player.Id, player.CurrentTileIndex, character, color);
        }

        private void BuildPawn(int playerId, int tileIndex, int character, Color color)
        {
            var pawn = new Node3D { Name = "Pawn_" + playerId, Position = GetPawnPosition(tileIndex, playerId) };
            var accent = Material(color);
            var skin = Material(new Color("#d7b99c"));
            var cloth = Material(new[] { new Color("#526b80"), new Color("#8f6558"), new Color("#687a5e"), new Color("#75627f") }[character % 4]);
            var dark = Material(new Color("#35434a"));
            pawn.AddChild(new MeshInstance3D { Name = "PawnBase", Mesh = new CylinderMesh { TopRadius = 0.27f, BottomRadius = 0.31f, Height = 0.12f, RadialSegments = 18 }, Position = Vector3.Up * 0.08f, MaterialOverride = Material(color.Darkened(0.25f)) });
            pawn.AddChild(new MeshInstance3D { Name = "PawnBody", Mesh = new CapsuleMesh { Radius = 0.20f, Height = 0.60f, RadialSegments = 18, Rings = 8 }, Position = Vector3.Up * 0.48f, MaterialOverride = cloth });
            pawn.AddChild(new MeshInstance3D { Name = "PawnHead", Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 18, Rings = 10 }, Position = Vector3.Up * 0.90f, MaterialOverride = skin });
            pawn.AddChild(new MeshInstance3D { Name = "PawnScarf", Mesh = new TorusMesh { InnerRadius = 0.145f, OuterRadius = 0.20f, Rings = 18, RingSegments = 8 }, Position = Vector3.Up * 0.70f, MaterialOverride = accent });
            var leftLeg = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = .065f, Height = .28f }, Position = new Vector3(-.09f, .27f, 0), MaterialOverride = dark };
            var rightLeg = (MeshInstance3D)leftLeg.Duplicate(); rightLeg.Position = new Vector3(.09f, .27f, 0);
            pawn.AddChild(leftLeg); pawn.AddChild(rightLeg);
            var hat = new MeshInstance3D { Name = "CharacterSilhouette", Position = Vector3.Up * 1.13f, MaterialOverride = accent };
            switch (character % 4)
            {
                case 0: hat.Mesh = new CylinderMesh { TopRadius = .06f, BottomRadius = .16f, Height = .24f, RadialSegments = 12 }; break;
                case 1: hat.Mesh = new BoxMesh { Size = new Vector3(.35f, .12f, .30f) }; hat.Position = Vector3.Up * 1.04f; break;
                case 2: hat.Mesh = new SphereMesh { Radius = .19f, Height = .11f, RadialSegments = 16, Rings = 8 }; break;
                case 3: hat.Mesh = new SphereMesh { Radius = .22f, Height = .17f, RadialSegments = 16, Rings = 8 }; hat.Position = Vector3.Up * 1.08f; break;
            }
            pawn.AddChild(hat);
            pawn.AddChild(new Label3D { Text = "P" + playerId, FontSize = 26, PixelSize = 0.003f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = Vector3.Up * 1.38f, Modulate = color });
            AddChild(pawn);
            _pawns[playerId] = pawn;
        }

        private Vector3 GetPawnPosition(int tileIndex, int playerId)
        {
            var offset = PreviewOffset(playerId);
            return GetTilePosition(tileIndex) + offset;
        }

        private static Vector3 PreviewOffset(int playerId) => new Vector3(((playerId - 1) % 2) * 0.22f - 0.11f, 0.18f, ((playerId - 1) / 2) * 0.22f - 0.11f);

        private void UpdatePropertyVisual(Node3D tile, BoardTileDefinition definition, PropertyState property)
        {
            var existing = tile.GetNodeOrNull<Node3D>("Building");
            if (existing != null) existing.QueueFree();
            if (definition.Type != BoardTileType.Property || property == null || !property.OwnerId.HasValue || property.UpgradeLevel <= 0) return;

            var level = property.UpgradeLevel;
            var building = new Node3D { Name = "Building" };
            var color = property.OwnerId.HasValue && _playerColors.TryGetValue(property.OwnerId.Value, out var selectedColor) ? selectedColor : new Color("#718276");
            var width = level == 1 ? .28f : level == 2 ? .34f : .40f;
            var height = level == 1 ? .30f : level == 2 ? .48f : .64f;
            building.Position = new Vector3(.31f, 0, .25f);
            building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = width * .55f, BottomRadius = width * .72f, Height = height, RadialSegments = 10, Rings = 3 }, Position = new Vector3(0, .10f + height * .5f, 0), MaterialOverride = Material(new Color("#d6d0c3")) });
            building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { BottomRadius = width * .80f, TopRadius = .035f, Height = .23f, RadialSegments = 10 }, Position = new Vector3(0, .10f + height + .115f, 0), MaterialOverride = Material(color) });
            if (level >= 3)
                building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = width * .84f, BottomRadius = width * .84f, Height = .09f, RadialSegments = 10 }, Position = new Vector3(0, .10f + height * .58f, 0), MaterialOverride = Material(color.Lightened(.10f)) });
            if (level >= 4)
                building.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .07f, BottomRadius = .12f, Height = .30f, RadialSegments = 10 }, Position = new Vector3(0, .10f + height + .34f, 0), MaterialOverride = Material(new Color("#b28e62")) });
            tile.AddChild(building);
        }

        private Color TileColor(BoardTileDefinition definition, PropertyState property)
        {
            if (property != null && property.OwnerId.HasValue && _playerColors.TryGetValue(property.OwnerId.Value, out var selectedColor)) return selectedColor.Lerp(new Color("#e8e1d5"), 0.34f);
            switch (definition.Type)
            {
                case BoardTileType.Start: return new Color("#d9c18f");
                case BoardTileType.Airport: return new Color("#c9977c");
                case BoardTileType.Prison: return new Color("#8a918f");
                case BoardTileType.Hospital: return new Color("#ded8d0");
                case BoardTileType.Court: return new Color("#d7c5aa");
                case BoardTileType.Chance: return new Color("#ab98ad");
                case BoardTileType.Destiny: return new Color("#8d9baa");
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
                    if (colors.TryGetValue(definition.DistrictId, out var districtColor)) return districtColor.Lerp(new Color("#e8e1d5"), 0.38f);
                    return new Color("#c9c9c9");
                default: return new Color("#d6c5a5");
            }
        }

        private Color PlayerColor(int id)
        {
            if (_playerColors.TryGetValue(id, out var selectedColor)) return selectedColor;
            var colors = new[] { new Color("#bf705f"), new Color("#6387a5"), new Color("#82956f"), new Color("#a080a0") };
            return colors[(id - 1) % colors.Length];
        }

        private static StandardMaterial3D Material(Color color) => new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f, Metallic = 0f };

    }
}
