using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Richman.Core
{
    public enum BoardTileType
    {
        Start,
        Property,
        Tax,
        Bonus,
        Rest,
        Transit,
        Special
    }

    public sealed class BoardTileDefinition
    {
        private readonly int[] _rentLevels;
        private readonly ReadOnlyCollection<int> _rentLevelsView;

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
            int effectAmount = 0)
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

        public int GetRent(int upgradeLevel)
        {
            if (Type != BoardTileType.Property) throw new InvalidOperationException("Only properties have rent.");
            if (upgradeLevel < 0 || upgradeLevel >= _rentLevels.Length) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            return _rentLevels[upgradeLevel];
        }
    }

    public sealed class BoardDefinition
    {
        private readonly List<BoardTileDefinition> _tiles;
        private readonly ReadOnlyCollection<BoardTileDefinition> _tilesView;

        public BoardDefinition(IEnumerable<BoardTileDefinition> tiles)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _tiles = new List<BoardTileDefinition>(tiles);
            if (_tiles.Count < 2) throw new ArgumentException("A board needs at least two tiles.", nameof(tiles));

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var startCount = 0;
            var startPositionIndex = -1;
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i] ?? throw new ArgumentException("Board tiles cannot be null.", nameof(tiles));
                if (!ids.Add(tile.Id)) throw new ArgumentException("Board tile ids must be unique.", nameof(tiles));
                if (tile.PositionIndex != i) throw new ArgumentException("Tile position indexes must be contiguous and match list order.", nameof(tiles));
                if (tile.Type == BoardTileType.Start)
                {
                    startCount++;
                    startPositionIndex = tile.PositionIndex;
                }
            }

            if (startCount != 1) throw new ArgumentException("A board must have exactly one Start tile.", nameof(tiles));

            StartPositionIndex = startPositionIndex;
            _tilesView = _tiles.AsReadOnly();
        }

        public int TileCount => _tiles.Count;
        public int StartPositionIndex { get; }
        public IReadOnlyList<BoardTileDefinition> Tiles => _tilesView;

        public BoardTileDefinition GetTileAt(int positionIndex)
        {
            if (positionIndex < 0 || positionIndex >= _tiles.Count) throw new ArgumentOutOfRangeException(nameof(positionIndex));
            return _tiles[positionIndex];
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

    public static class PrototypeBoardFactory
    {
        public static BoardDefinition Create()
        {
            var tiles = new List<BoardTileDefinition>();
            tiles.Add(Simple(tiles, "start", BoardTileType.Start, "Central Station"));
            tiles.Add(Property(tiles, "lantern-market", "Lantern Market", "market", 120, 12, 60, 12, 30, 60, 100));
            tiles.Add(Effect(tiles, "garden", BoardTileType.Bonus, "Community Garden", 50));
            tiles.Add(Property(tiles, "canal-row", "Canal Row", "harbor", 140, 14, 70, 14, 35, 70, 115));
            tiles.Add(Simple(tiles, "rooftop-park", BoardTileType.Rest, "Rooftop Park"));
            tiles.Add(Simple(tiles, "sky-tram", BoardTileType.Transit, "Sky Tram"));
            tiles.Add(Property(tiles, "copper-workshop", "Copper Workshop", "industrial", 160, 16, 80, 16, 40, 80, 130));
            tiles.Add(Effect(tiles, "city-maintenance", BoardTileType.Tax, "City Maintenance", -100));
            tiles.Add(Property(tiles, "sunset-apartments", "Sunset Apartments", "residential", 180, 18, 90, 18, 45, 90, 145));
            tiles.Add(Simple(tiles, "clock-tower", BoardTileType.Special, "Old Clock Tower"));
            tiles.Add(Property(tiles, "pixel-arcade", "Pixel Arcade", "entertainment", 200, 20, 100, 20, 50, 100, 160));
            tiles.Add(Effect(tiles, "street-fair", BoardTileType.Bonus, "Street Fair", 60));
            tiles.Add(Property(tiles, "skyline-lab", "Skyline Lab", "technology", 220, 22, 110, 22, 55, 110, 175));
            tiles.Add(Simple(tiles, "courtyard", BoardTileType.Rest, "Quiet Courtyard"));
            tiles.Add(Property(tiles, "harbor-exchange", "Harbor Exchange", "harbor", 240, 24, 120, 24, 60, 120, 190));
            tiles.Add(Simple(tiles, "ferry-terminal", BoardTileType.Transit, "Ferry Terminal"));
            tiles.Add(Property(tiles, "bamboo-court", "Bamboo Court", "residential", 260, 26, 130, 26, 65, 130, 205));
            tiles.Add(Effect(tiles, "transit-fund", BoardTileType.Tax, "Transit Fund", -125));
            tiles.Add(Property(tiles, "mosaic-plaza", "Mosaic Plaza", "market", 280, 28, 140, 28, 70, 140, 220));
            tiles.Add(Simple(tiles, "observation-deck", BoardTileType.Special, "Observation Deck"));
            tiles.Add(Property(tiles, "rainy-day-theater", "Rainy Day Theater", "entertainment", 300, 30, 150, 30, 75, 150, 235));
            tiles.Add(Effect(tiles, "public-art-fund", BoardTileType.Bonus, "Public Art Fund", 75));
            tiles.Add(Property(tiles, "foundry-lane", "Foundry Lane", "industrial", 320, 32, 160, 32, 80, 160, 250));
            tiles.Add(Simple(tiles, "rooftop-garden", BoardTileType.Rest, "Rooftop Garden"));
            tiles.Add(Property(tiles, "solaris-heights", "Solaris Heights", "residential", 340, 34, 170, 34, 85, 170, 265));
            tiles.Add(Simple(tiles, "monorail-loop", BoardTileType.Transit, "Monorail Loop"));
            tiles.Add(Property(tiles, "tideglass-pier", "Tideglass Pier", "harbor", 360, 36, 180, 36, 90, 180, 280));
            tiles.Add(Effect(tiles, "infrastructure-works", BoardTileType.Tax, "Infrastructure Works", -175));
            tiles.Add(Property(tiles, "northlight-studios", "Northlight Studios", "technology", 380, 38, 190, 38, 95, 190, 295));
            tiles.Add(Simple(tiles, "quiet-alley", BoardTileType.Special, "Quiet Alley"));
            tiles.Add(Property(tiles, "crescent-bazaar", "Crescent Bazaar", "market", 400, 40, 200, 40, 100, 200, 310));
            tiles.Add(Effect(tiles, "festival-grant", BoardTileType.Bonus, "Festival Grant", 100));
            tiles.Add(Property(tiles, "neon-garden", "Neon Garden", "entertainment", 420, 42, 210, 42, 105, 210, 325));
            tiles.Add(Simple(tiles, "riverside-rest", BoardTileType.Rest, "Riverside Rest"));
            tiles.Add(Property(tiles, "aster-foundry", "Aster Foundry", "industrial", 440, 44, 220, 44, 110, 220, 340));
            tiles.Add(Property(tiles, "summit-forum", "Summit Forum", "civic", 500, 50, 250, 50, 125, 250, 390));

            return new BoardDefinition(tiles);
        }

        private static BoardTileDefinition Simple(List<BoardTileDefinition> tiles, string id, BoardTileType type, string name)
        {
            return new BoardTileDefinition(id, type, name, tiles.Count, 0, 0, 0, null, string.Empty, id);
        }

        private static BoardTileDefinition Effect(List<BoardTileDefinition> tiles, string id, BoardTileType type, string name, int amount)
        {
            return new BoardTileDefinition(id, type, name, tiles.Count, 0, 0, 0, null, string.Empty, id, amount);
        }

        private static BoardTileDefinition Property(List<BoardTileDefinition> tiles, string id, string name, string district, int price, int baseRent, int upgradeCost, params int[] rents)
        {
            return new BoardTileDefinition(id, BoardTileType.Property, name, tiles.Count, price, baseRent, upgradeCost, rents, district, id);
        }
    }
}
