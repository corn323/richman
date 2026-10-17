using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Richman.Core
{
    public readonly struct BoardMapPosition
    {
        public BoardMapPosition(float x, float y, float z, float rotationDegrees)
        { X = x; Y = y; Z = z; RotationDegrees = rotationDegrees; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float RotationDegrees { get; }
    }

    public enum BoardTileType
    {
        Start,
        Property,
        Tax,
        Bonus,
        Rest,
        Transit,
        Special,
        Airport,
        Hospital,
        Prison,
        Court,
        Chance,
        Destiny
    }

    public sealed class BoardTileDefinition
    {
        private readonly int[] _rentLevels;
        private readonly ReadOnlyCollection<int> _rentLevelsView;
        private string[] _nextTileIds;
        private ReadOnlyCollection<string> _nextTileIdsView;

        public BoardTileDefinition(
            string id,
            BoardTileType type,
            string displayName,
            int positionIndex,
            int purchasePrice,
            int baseRent,
            int upgradeCost,
            IEnumerable<int> rentLevels,
            string districtId,
            string visualId,
            int effectAmount = 0,
            IEnumerable<string> nextTileIds = null,
            bool isMainRoute = true,
            string rejoinsAtId = null,
            string airportDestinationId = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Tile id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));
            if (positionIndex < 0) throw new ArgumentOutOfRangeException(nameof(positionIndex));
            if (type == BoardTileType.Property && purchasePrice <= 0) throw new ArgumentOutOfRangeException(nameof(purchasePrice));
            if (type == BoardTileType.Property && baseRent <= 0) throw new ArgumentOutOfRangeException(nameof(baseRent));
            if (type == BoardTileType.Property && upgradeCost <= 0) throw new ArgumentOutOfRangeException(nameof(upgradeCost));

            Id = id;
            Type = type;
            DisplayName = displayName;
            PositionIndex = positionIndex;
            PurchasePrice = purchasePrice;
            BaseRent = baseRent;
            UpgradeCost = upgradeCost;
            DistrictId = districtId ?? string.Empty;
            VisualId = visualId ?? string.Empty;
            EffectAmount = effectAmount;
            _rentLevels = rentLevels == null ? Array.Empty<int>() : new List<int>(rentLevels).ToArray();
            _nextTileIds = nextTileIds == null ? Array.Empty<string>() : new List<string>(nextTileIds).ToArray();
            _nextTileIdsView = Array.AsReadOnly(_nextTileIds);
            IsMainRoute = isMainRoute;
            RejoinsAtId = rejoinsAtId ?? string.Empty;
            AirportDestinationId = airportDestinationId ?? string.Empty;

            if (type == BoardTileType.Property)
            {
                if (_rentLevels.Length == 0) throw new ArgumentException("A property needs at least one rent level.", nameof(rentLevels));
                for (var i = 0; i < _rentLevels.Length; i++)
                {
                    if (_rentLevels[i] <= 0) throw new ArgumentException("Rent levels must be positive.", nameof(rentLevels));
                }

                if (_rentLevels[0] != baseRent)
                {
                    throw new ArgumentException("The first rent level must equal baseRent.", nameof(rentLevels));
                }
            }

            _rentLevelsView = Array.AsReadOnly(_rentLevels);
        }

        public string Id { get; }
        public BoardTileType Type { get; }
        public string DisplayName { get; }
        public int PositionIndex { get; }
        public int PurchasePrice { get; }
        public int BaseRent { get; }
        public int UpgradeCost { get; }
        public IReadOnlyList<int> RentLevels => _rentLevelsView;
        public string DistrictId { get; }
        public string VisualId { get; }
        public int EffectAmount { get; }
        public IReadOnlyList<string> NextTileIds => _nextTileIdsView;
        public bool IsMainRoute { get; }
        public string RejoinsAtId { get; }
        public string AirportDestinationId { get; }
        public BoardMapPosition MapPosition { get; private set; }

        public int GetRent(int upgradeLevel)
        {
            if (Type != BoardTileType.Property) throw new InvalidOperationException("Only properties have rent.");
            if (upgradeLevel < 0 || upgradeLevel >= _rentLevels.Length) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            return _rentLevels[upgradeLevel];
        }

        internal void SetNextTileIds(IEnumerable<string> nextTileIds)
        {
            _nextTileIds = new List<string>(nextTileIds).ToArray();
            _nextTileIdsView = Array.AsReadOnly(_nextTileIds);
        }

        internal void SetMapPosition(BoardMapPosition position) => MapPosition = position;
    }

    public sealed class BoardDefinition
    {
        private readonly List<BoardTileDefinition> _tiles;
        private readonly ReadOnlyCollection<BoardTileDefinition> _tilesView;
        private readonly Dictionary<string, BoardTileDefinition> _tilesById;
        private readonly int _mainRouteTileCount;

        public BoardDefinition(IEnumerable<BoardTileDefinition> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _tiles = new List<BoardTileDefinition>(tiles);
            if (_tiles.Count < 2) throw new ArgumentException("A board needs at least two tiles.", nameof(tiles));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            _tilesById = new Dictionary<string, BoardTileDefinition>(StringComparer.Ordinal);
            var startCount = 0;
            var startPositionIndex = -1;
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i] ?? throw new ArgumentException("Board tiles cannot be null.", nameof(tiles));
                if (!ids.Add(tile.Id)) throw new ArgumentException("Board tile ids must be unique.", nameof(tiles));
                if (tile.PositionIndex != i) throw new ArgumentException("Tile position indexes must be contiguous and match list order.", nameof(tiles));
                _tilesById.Add(tile.Id, tile);
                if (tile.IsMainRoute) _mainRouteTileCount++;
                if (tile.Type == BoardTileType.Start)
                {
                    startCount++;
                    startPositionIndex = tile.PositionIndex;
                }
            }

            if (startCount != 1) throw new ArgumentException("A board must have exactly one Start tile.", nameof(tiles));

            for (var i = 0; i < _mainRouteTileCount; i++)
            {
                if (!_tiles[i].IsMainRoute) throw new ArgumentException("Main route tiles must occupy the first contiguous position indexes.", nameof(tiles));
            }
            for (var i = 0; i < _mainRouteTileCount; i++)
            {
                if (_tiles[i].NextTileIds.Count == 0)
                    _tiles[i].SetNextTileIds(new[] { _tiles[(i + 1) % _mainRouteTileCount].Id });
            }
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (tile.NextTileIds.Count == 0) throw new ArgumentException("Every tile must connect to at least one next tile: " + tile.Id, nameof(tiles));
                for (var edge = 0; edge < tile.NextTileIds.Count; edge++)
                {
                    if (!_tilesById.ContainsKey(tile.NextTileIds[edge])) throw new ArgumentException("Tile connection points to a missing id: " + tile.Id + " -> " + tile.NextTileIds[edge], nameof(tiles));
                }
                if (tile.Type == BoardTileType.Airport &&
                    (!_tilesById.TryGetValue(tile.AirportDestinationId, out var airportDestination) || airportDestination.Type != BoardTileType.Airport || airportDestination.Id == tile.Id))
                    throw new ArgumentException("Every airport must route to a different valid airport: " + tile.Id, nameof(tiles));
                if (!tile.IsMainRoute && string.IsNullOrWhiteSpace(tile.RejoinsAtId)) throw new ArgumentException("A branch tile must declare its rejoin id: " + tile.Id, nameof(tiles));
            }
            for (var i = 0; i < _mainRouteTileCount; i++)
            {
                var expectedNext = _tiles[(i + 1) % _mainRouteTileCount].Id;
                if (!_tiles[i].NextTileIds.ContainsOrdinal(expectedNext)) throw new ArgumentException("Main route does not form one closed loop at " + _tiles[i].Id, nameof(tiles));
            }
            ValidateBranchRejoins();
            AssignMapPositions();

            StartPositionIndex = startPositionIndex;
            _tilesView = _tiles.AsReadOnly();
        }

        public int TileCount => _tiles.Count;
        public int MainRouteTileCount => _mainRouteTileCount;
        public int StartPositionIndex { get; }
        public IReadOnlyList<BoardTileDefinition> Tiles => _tilesView;

        public BoardTileDefinition GetTileAt(int positionIndex)
        {
            if (positionIndex < 0 || positionIndex >= _tiles.Count) throw new ArgumentOutOfRangeException(nameof(positionIndex));
            return _tiles[positionIndex];
        }

        public BoardTileDefinition GetTile(string id) => id != null && _tilesById.TryGetValue(id, out var tile) ? tile : null;

        public IReadOnlyList<BoardTileDefinition> GetNextTiles(int positionIndex)
        {
            var source = GetTileAt(positionIndex);
            var next = new List<BoardTileDefinition>(source.NextTileIds.Count);
            for (var i = 0; i < source.NextTileIds.Count; i++) next.Add(_tilesById[source.NextTileIds[i]]);
            return next.AsReadOnly();
        }

        public int GetDistrictPropertyCount(string districtId)
        {
            var count = 0;
            for (var i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Type == BoardTileType.Property && string.Equals(_tiles[i].DistrictId, districtId, StringComparison.Ordinal)) count++;
            return count;
        }

        private void ValidateBranchRejoins()
        {
            for (var i = _mainRouteTileCount; i < _tiles.Count; i++)
            {
                var branch = _tiles[i];
                if (!_tilesById.TryGetValue(branch.RejoinsAtId, out var merge) || !merge.IsMainRoute)
                    throw new ArgumentException("Branch rejoin must name a main route tile: " + branch.Id, nameof(_tiles));
                var pending = new Queue<string>();
                var visited = new HashSet<string>(StringComparer.Ordinal);
                pending.Enqueue(branch.Id);
                var reachesMerge = false;
                while (pending.Count > 0)
                {
                    var id = pending.Dequeue();
                    if (!visited.Add(id)) continue;
                    if (id == merge.Id) { reachesMerge = true; continue; }
                    var tile = _tilesById[id];
                    for (var edge = 0; edge < tile.NextTileIds.Count; edge++) pending.Enqueue(tile.NextTileIds[edge]);
                }
                if (!reachesMerge) throw new ArgumentException("Branch cannot reach its declared rejoin: " + branch.Id, nameof(_tiles));
            }
        }

        private void AssignMapPositions()
        {
            for (var i = 0; i < _mainRouteTileCount; i++)
            {
                var angle = (Math.PI * 2.0 * i / _mainRouteTileCount) - (Math.PI / 2.0);
                var x = (float)(11.7 * Math.Cos(angle) + 0.42 * Math.Sin(3.0 * angle));
                var z = (float)(8.3 * Math.Sin(angle) + 0.30 * Math.Sin(2.0 * angle));
                var y = (float)(0.62 + 0.16 * Math.Sin(i * 0.71) + 0.07 * Math.Sin(i * 1.89));
                _tiles[i].SetMapPosition(new BoardMapPosition(x, y, z, 0f));
            }

            for (var sourceIndex = 0; sourceIndex < _mainRouteTileCount; sourceIndex++)
            {
                var source = _tiles[sourceIndex];
                for (var edge = 0; edge < source.NextTileIds.Count; edge++)
                {
                    var branchId = source.NextTileIds[edge];
                    var branch = _tilesById[branchId];
                    if (branch.IsMainRoute) continue;
                    var branchNodes = new List<BoardTileDefinition>();
                    var visited = new HashSet<string>(StringComparer.Ordinal);
                    var current = branch;
                    while (!current.IsMainRoute)
                    {
                        if (!visited.Add(current.Id)) throw new ArgumentException("Branch contains a cycle before merging: " + current.Id);
                        branchNodes.Add(current);
                        if (current.NextTileIds.Count != 1) throw new ArgumentException("Each branch waypoint must have one onward route: " + current.Id);
                        current = _tilesById[current.NextTileIds[0]];
                    }
                    var end = current.MapPosition;
                    var start = source.MapPosition;
                    var midX = (start.X + end.X) * 0.5f;
                    var midZ = (start.Z + end.Z) * 0.5f;
                    var length = MathF.Sqrt(midX * midX + midZ * midZ);
                    var controlX = midX - (length > 0.001f ? midX / length * 2.5f : 0f);
                    var controlZ = midZ - (length > 0.001f ? midZ / length * 2.5f : 0f);
                    for (var waypoint = 0; waypoint < branchNodes.Count; waypoint++)
                    {
                        var t = (waypoint + 1f) / (branchNodes.Count + 1f);
                        var inverse = 1f - t;
                        var x = inverse * inverse * start.X + 2f * inverse * t * controlX + t * t * end.X;
                        var z = inverse * inverse * start.Z + 2f * inverse * t * controlZ + t * t * end.Z;
                        branchNodes[waypoint].SetMapPosition(new BoardMapPosition(x, 0.85f, z, 0f));
                    }
                }
            }

            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var next = _tilesById[tile.NextTileIds[0]].MapPosition;
                var position = tile.MapPosition;
                var rotation = (float)(-Math.Atan2(next.Z - position.Z, next.X - position.X) * 180.0 / Math.PI);
                tile.SetMapPosition(new BoardMapPosition(position.X, position.Y, position.Z, rotation));
            }
        }

        public BoardTileDefinition GetProperty(string propertyId)
        {
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].Type == BoardTileType.Property && string.Equals(_tiles[i].Id, propertyId, StringComparison.Ordinal)) return _tiles[i];
            }

            return null;
        }
    }

    internal static class BoardStringExtensions
    {
        public static bool ContainsOrdinal(this IReadOnlyList<string> values, string value)
        {
            for (var i = 0; i < values.Count; i++) if (string.Equals(values[i], value, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public static class PrototypeBoardFactory
    {
        private const int MainRouteCount = 40;

        public static BoardDefinition Create()
        {
            var tiles = new List<BoardTileDefinition>();
            var ids = new[]
            {
                "start-singapore", "singapore-gardens", "singapore-marina", "airport-changi", "chance-global-fair",
                "kyoto-gion", "kyoto-teahouse", "destiny-wayfinder", "osaka-dotonbori", "shinkansen-terminal",
                "hong-kong-star-ferry", "hong-kong-victoria-peak", "hong-kong-harbor-dues", "seoul-insadong", "seoul-gyeongbokgung",
                "airport-incheon", "shanghai-bund", "shanghai-yu-garden", "chance-night-market", "bangkok-floating-market",
                "bali-ubud-terraces", "bali-cliff-resort", "mumbai-crawford-market", "dubai-creek", "airport-dubai",
                "rome-colosseum", "rome-trastevere", "paris-montmartre", "paris-louvre", "london-westminster",
                "london-prison", "london-covent-garden", "destiny-open-ticket", "new-york-central-park", "new-york-broadway",
                "rio-carnival", "cape-town-table-mountain", "lisbon-yellow-tram", "airport-jfk", "madrid-retiro-park"
            };

            for (var i = 0; i < MainRouteCount; i++)
            {
                var next = new List<string> { ids[(i + 1) % MainRouteCount] };
                if (i == 10) next.Add("macau-ferry-pier");
                if (i == 20) next.Add("ubud-ridge-rail");
                if (i == 31) next.Add("thames-river-cruise");
                tiles.Add(CreateMainTile(i, ids[i], next.ToArray()));
            }

            tiles.Add(Simple(40, "macau-ferry-pier", BoardTileType.Transit, "澳門渡輪碼頭", new[] { "macau-heritage-quarter" }, false, "hong-kong-victoria-peak"));
            tiles.Add(Property(41, "macau-heritage-quarter", "澳門歷史城區", "macau", 180, 18, 70, new[] { 18, 36, 65, 110, 170 }, new[] { "hong-kong-victoria-peak" }, false, "hong-kong-victoria-peak"));
            tiles.Add(Simple(42, "ubud-ridge-rail", BoardTileType.Transit, "烏布山脊觀景列車", new[] { "bali-rice-terraces" }, false, "bali-cliff-resort"));
            tiles.Add(Property(43, "bali-rice-terraces", "峇里島梯田步道", "bali", 200, 20, 80, new[] { 20, 40, 72, 120, 185 }, new[] { "bali-cliff-resort" }, false, "bali-cliff-resort"));
            tiles.Add(Simple(44, "thames-river-cruise", BoardTileType.Transit, "泰晤士河遊船", new[] { "shakespeare-globe" }, false, "destiny-open-ticket"));
            tiles.Add(Property(45, "shakespeare-globe", "莎士比亞環球劇場", "london", 240, 24, 95, new[] { 24, 48, 86, 145, 220 }, new[] { "london-southbank" }, false, "destiny-open-ticket"));
            tiles.Add(Property(46, "london-southbank", "倫敦南岸藝文區", "london", 260, 26, 105, new[] { 26, 52, 94, 158, 240 }, new[] { "destiny-open-ticket" }, false, "destiny-open-ticket"));

            return new BoardDefinition(tiles);
        }

        private static BoardTileDefinition CreateMainTile(int index, string id, string[] next)
        {
            switch (index)
            {
                case 0: return Simple(index, id, BoardTileType.Start, "起點・新加坡", next);
                case 1: return Property(index, id, "濱海灣花園", "singapore", 140, 14, 55, new[] { 14, 28, 50, 84, 130 }, next);
                case 2: return Property(index, id, "濱海灣金沙步道", "singapore", 160, 16, 65, new[] { 16, 32, 58, 98, 150 }, next);
                case 3: return Airport(index, id, "樟宜國際機場", "airport-incheon", next);
                case 4: return Simple(index, id, BoardTileType.Chance, "機會・世界博覽會", next);
                case 5: return Property(index, id, "京都祇園町家", "japan", 150, 15, 60, new[] { 15, 30, 54, 90, 140 }, next);
                case 6: return Property(index, id, "京都茶屋小徑", "japan", 170, 17, 68, new[] { 17, 34, 61, 102, 158 }, next);
                case 7: return Simple(index, id, BoardTileType.Destiny, "命運・開放旅券", next);
                case 8: return Property(index, id, "大阪道頓堀商店街", "japan", 190, 19, 76, new[] { 19, 38, 68, 114, 176 }, next);
                case 9: return Simple(index, id, BoardTileType.Transit, "東海道新幹線站", next);
                case 10: return Simple(index, id, BoardTileType.Special, "香港天星小輪碼頭", next);
                case 11: return Property(index, id, "香港太平山頂", "hong-kong", 210, 21, 84, new[] { 21, 42, 76, 126, 195 }, next);
                case 12: return Effect(index, id, BoardTileType.Tax, "香港港口通行費", 90, next);
                case 13: return Property(index, id, "首爾仁寺洞", "korea", 180, 18, 72, new[] { 18, 36, 65, 108, 168 }, next);
                case 14: return Property(index, id, "景福宮文化街區", "korea", 205, 20, 82, new[] { 20, 40, 72, 120, 185 }, next);
                case 15: return Airport(index, id, "仁川國際機場", "airport-changi", next);
                case 16: return Property(index, id, "上海外灘", "china", 220, 22, 88, new[] { 22, 44, 79, 132, 204 }, next);
                case 17: return Property(index, id, "上海豫園", "china", 200, 20, 80, new[] { 20, 40, 72, 120, 185 }, next);
                case 18: return Simple(index, id, BoardTileType.Chance, "機會・夜市奇遇", next);
                case 19: return Property(index, id, "曼谷水上市場", "thailand", 190, 19, 76, new[] { 19, 38, 68, 114, 176 }, next);
                case 20: return Property(index, id, "峇里島烏布梯田", "bali", 180, 18, 72, new[] { 18, 36, 65, 108, 168 }, next);
                case 21: return Property(index, id, "峇里島海崖度假村", "bali", 215, 21, 86, new[] { 21, 42, 76, 126, 195 }, next);
                case 22: return Property(index, id, "孟買克勞福市場", "india", 210, 21, 84, new[] { 21, 42, 76, 126, 195 }, next);
                case 23: return Property(index, id, "杜拜河畔市集", "uae", 230, 23, 92, new[] { 23, 46, 83, 138, 213 }, next);
                case 24: return Airport(index, id, "杜拜國際機場", "airport-jfk", next);
                case 25: return Property(index, id, "羅馬競技場廣場", "italy", 240, 24, 96, new[] { 24, 48, 86, 144, 222 }, next);
                case 26: return Property(index, id, "羅馬特拉斯提弗列", "italy", 225, 22, 90, new[] { 22, 44, 79, 132, 204 }, next);
                case 27: return Property(index, id, "巴黎蒙馬特街區", "france", 250, 25, 100, new[] { 25, 50, 90, 150, 232 }, next);
                case 28: return Property(index, id, "羅浮宮花園", "france", 275, 27, 110, new[] { 27, 54, 97, 162, 250 }, next);
                case 29: return Property(index, id, "倫敦西敏寺區", "london", 260, 26, 104, new[] { 26, 52, 94, 156, 240 }, next);
                case 30: return Effect(index, id, BoardTileType.Prison, "倫敦監獄", 1, next);
                case 31: return Property(index, id, "倫敦柯芬園", "london", 280, 28, 112, new[] { 28, 56, 101, 168, 260 }, next);
                case 32: return Simple(index, id, BoardTileType.Destiny, "命運・泰晤士河之邀", next);
                case 33: return Property(index, id, "紐約中央公園區", "united-states", 280, 28, 112, new[] { 28, 56, 101, 168, 260 }, next);
                case 34: return Property(index, id, "百老匯劇院街", "united-states", 300, 30, 120, new[] { 30, 60, 108, 180, 278 }, next);
                case 35: return Property(index, id, "里約嘉年華大道", "brazil", 245, 24, 98, new[] { 24, 48, 86, 144, 222 }, next);
                case 36: return Property(index, id, "開普敦桌山步道", "south-africa", 270, 27, 108, new[] { 27, 54, 97, 162, 250 }, next);
                case 37: return Property(index, id, "里斯本 28 號電車街", "portugal", 230, 23, 92, new[] { 23, 46, 83, 138, 213 }, next);
                case 38: return Airport(index, id, "甘迺迪國際機場", "airport-dubai", next);
                case 39: return Property(index, id, "馬德里麗池公園", "spain", 235, 23, 94, new[] { 23, 46, 83, 138, 213 }, next);
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static BoardTileDefinition Simple(int index, string id, BoardTileType type, string name, IEnumerable<string> next, bool mainRoute = true, string rejoinsAt = null)
        {
            return new BoardTileDefinition(id, type, name, index, 0, 0, 0, null, string.Empty, id, type == BoardTileType.Prison ? 1 : 0, next, mainRoute, rejoinsAt);
        }

        private static BoardTileDefinition Effect(int index, string id, BoardTileType type, string name, int amount, IEnumerable<string> next)
        {
            return new BoardTileDefinition(id, type, name, index, 0, 0, 0, null, string.Empty, id, amount, next);
        }

        private static BoardTileDefinition Airport(int index, string id, string name, string destinationId, IEnumerable<string> next)
        {
            return new BoardTileDefinition(id, BoardTileType.Airport, name, index, 0, 0, 0, null, string.Empty, id, 0, next, true, null, destinationId);
        }

        private static BoardTileDefinition Property(int index, string id, string name, string district, int price, int rent, int upgradeCost, int[] rents, IEnumerable<string> next, bool mainRoute = true, string rejoinsAt = null)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, name, index, price, rent, upgradeCost, rents, district, id, 0, next, mainRoute, rejoinsAt);
        }
    }
}
