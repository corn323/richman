using System.Collections.Generic;
using Godot;
using Richman.Core;

namespace Richman.GodotPresentation
{
    public partial class GodotBoardView : Node3D
    {
        private readonly List<MeshInstance3D> _tiles = new List<MeshInstance3D>();
        private readonly Dictionary<int, Node3D> _pawns = new Dictionary<int, Node3D>();
        private readonly Dictionary<int, StandardMaterial3D> _tileMaterials = new Dictionary<int, StandardMaterial3D>();
        private BoardDefinition _board;

        public void Build(BoardDefinition board, IReadOnlyList<PlayerState> players)
        {
            _board = board;
            BuildGround();
            for (var i = 0; i < board.TileCount; i++) BuildTile(board.GetTileAt(i));
            BuildCity();
            for (var i = 0; i < players.Count; i++) BuildPawn(players[i]);
        }

        public Vector3 GetTilePosition(int tileIndex)
        {
            var side = 9;
            var spacing = 1.35f;
            var half = side * spacing * 0.5f;
            if (tileIndex < side) return new Vector3(-half + tileIndex * spacing, 0.35f, -half);
            if (tileIndex < side * 2) return new Vector3(half, 0.35f, -half + (tileIndex - side) * spacing);
            if (tileIndex < side * 3) return new Vector3(half - (tileIndex - side * 2) * spacing, 0.35f, half);
            return new Vector3(-half, 0.35f, half - (tileIndex - side * 3) * spacing);
        }

        public Node3D GetPawn(int playerId)
        {
            return _pawns.TryGetValue(playerId, out var pawn) ? pawn : null;
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
            for (var i = 0; i < _tiles.Count; i++)
            {
                var definition = state.Board.GetTileAt(i);
                var property = state.GetProperty(definition.Id);
                var material = _tileMaterials[i];
                material.AlbedoColor = TileColor(definition, property);
                UpdatePropertyVisual(_tiles[i], definition, property);
            }
        }

        public async System.Threading.Tasks.Task AnimatePawn(int playerId, int fromTileIndex, int steps)
        {
            if (!_pawns.TryGetValue(playerId, out var pawn) || _board == null) return;
            var start = pawn.Position;
            for (var step = 1; step <= steps; step++)
            {
                var target = GetPawnPosition((fromTileIndex + step) % _board.TileCount, playerId);
                var tween = CreateTween();
                tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
                tween.TweenProperty(pawn, "position", target, 0.18f);
                await ToSignal(tween, Tween.SignalName.Finished);
            }
        }

        private void BuildGround()
        {
            var ground = new MeshInstance3D { Name = "Ground" };
            ground.Mesh = new BoxMesh { Size = new Vector3(20f, 0.4f, 20f) };
            ground.Position = new Vector3(0f, -0.25f, 0f);
            ground.MaterialOverride = Material(new Color("#263b49"));
            AddChild(ground);
        }

        private void BuildTile(BoardTileDefinition definition)
        {
            var tile = new MeshInstance3D { Name = "Tile_" + definition.PositionIndex };
            tile.Mesh = new BoxMesh { Size = new Vector3(1.08f, 0.55f, 1.08f) };
            tile.Position = GetTilePosition(definition.PositionIndex);
            var material = Material(TileColor(definition, null));
            tile.MaterialOverride = material;
            _tileMaterials[definition.PositionIndex] = material;
            AddChild(tile);
            var label = new Label3D
            {
                Text = definition.PositionIndex + "\n" + definition.DisplayName,
                FontSize = 22,
                PixelSize = 0.0028f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Position = new Vector3(0f, 0.32f, 0f),
                Modulate = new Color("#10202e")
            };
            tile.AddChild(label);
            _tiles.Add(tile);
        }

        private void BuildCity()
        {
            var city = new Node3D { Name = "CentralToyCity" };
            AddChild(city);
            AddBuilding(city, new Vector3(-2.4f, 0.7f, -1.2f), new Vector3(2.2f, 1.4f, 1.8f), "#f3b562");
            AddBuilding(city, new Vector3(1.8f, 1.0f, -1.4f), new Vector3(1.6f, 2.0f, 1.5f), "#6ec5b8");
            AddBuilding(city, new Vector3(-1.4f, 0.55f, 1.8f), new Vector3(1.5f, 1.1f, 1.4f), "#f28f8f");
            AddBuilding(city, new Vector3(2.2f, 0.45f, 1.8f), new Vector3(2.0f, 0.9f, 1.3f), "#a7b9f2");
            var park = new MeshInstance3D { Name = "CentralPark", Mesh = new CylinderMesh { TopRadius = 1.0f, BottomRadius = 1.15f, Height = 0.12f }, Position = new Vector3(0f, 0.42f, 0.5f), MaterialOverride = Material(new Color("#6bbf7b")) };
            city.AddChild(park);
        }

        private void AddBuilding(Node3D parent, Vector3 position, Vector3 size, string color)
        {
            var building = new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = position, MaterialOverride = Material(new Color(color)) };
            parent.AddChild(building);
            var roof = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = size.X * 0.36f, BottomRadius = size.X * 0.55f, Height = 0.22f, RadialSegments = 6 }, Position = position + Vector3.Up * (size.Y * 0.5f + 0.13f), MaterialOverride = Material(new Color("#334b63")) };
            parent.AddChild(roof);
        }

        private void BuildPawn(PlayerState player)
        {
            var pawn = new Node3D { Name = "Pawn_" + player.Id, Position = GetPawnPosition(player.CurrentTileIndex, player.Id) };
            var body = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.25f, Height = 0.75f }, MaterialOverride = Material(PlayerColor(player.Id)) };
            body.Position = Vector3.Up * 0.45f;
            pawn.AddChild(body);
            var head = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.32f, Height = 0.64f }, Position = Vector3.Up * 0.95f, MaterialOverride = Material(new Color("#ffe0bd")) };
            pawn.AddChild(head);
            var label = new Label3D { Text = "P" + player.Id, FontSize = 28, PixelSize = 0.003f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Position = Vector3.Up * 1.5f, Modulate = PlayerColor(player.Id) };
            pawn.AddChild(label);
            AddChild(pawn);
            _pawns[player.Id] = pawn;
        }

        private Vector3 GetPawnPosition(int tileIndex, int playerId)
        {
            var offset = new Vector3(((playerId - 1) % 2) * 0.28f - 0.14f, 0f, ((playerId - 1) / 2) * 0.28f - 0.14f);
            return GetTilePosition(tileIndex) + offset;
        }

        private static void UpdatePropertyVisual(Node3D tile, BoardTileDefinition definition, PropertyState property)
        {
            if (definition.Type != BoardTileType.Property) return;
            var existing = tile.GetNodeOrNull<Node3D>("Building");
            if (existing != null) existing.QueueFree();
            if (property == null || !property.OwnerId.HasValue || property.UpgradeLevel == 0) return;
            var building = new MeshInstance3D { Name = "Building", Position = Vector3.Up * (0.45f + property.UpgradeLevel * 0.18f), Mesh = new BoxMesh { Size = new Vector3(0.45f + property.UpgradeLevel * 0.08f, 0.35f + property.UpgradeLevel * 0.28f, 0.45f + property.UpgradeLevel * 0.08f) }, MaterialOverride = Material(PlayerColor(property.OwnerId.Value)) };
            tile.AddChild(building);
        }

        private static Color TileColor(BoardTileDefinition definition, PropertyState property)
        {
            if (property != null && property.OwnerId.HasValue) return PlayerColor(property.OwnerId.Value).Lerp(Colors.White, 0.18f);
            if (definition.Type == BoardTileType.Start) return new Color("#4bb3c3");
            if (definition.Type != BoardTileType.Property) return new Color("#d6c5a5");
            var colors = new Dictionary<string, Color> { ["market"] = new Color("#f4c95d"), ["harbor"] = new Color("#76b5d8"), ["industrial"] = new Color("#a7a9ac"), ["residential"] = new Color("#88c999"), ["entertainment"] = new Color("#d58bd7"), ["technology"] = new Color("#8d9ff0"), ["civic"] = new Color("#ef9d76") };
            return colors.TryGetValue(definition.DistrictId, out var color) ? color : new Color("#c9c9c9");
        }

        private static Color PlayerColor(int playerId)
        {
            var colors = new[] { new Color("#ef6262"), new Color("#4d96ff"), new Color("#54c878"), new Color("#b678e6") };
            return colors[(playerId - 1) % colors.Length];
        }

        private static StandardMaterial3D Material(Color color)
        {
            return new StandardMaterial3D { AlbedoColor = color, Roughness = 0.78f };
        }
    }
}
