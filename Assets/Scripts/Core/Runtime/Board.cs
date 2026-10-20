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

    // Phase 1d-2（條件邊）：一條出邊，帶一個可選的 RequiredFlag。RequiredFlag 空字串＝
    // 永遠開啟（相容既有的無條件 NextTileIds）；非空代表這條邊只有在 GameState.BoardFlags
    // 含有這個 flag 時才「打開」，移動解析（GameSession.ContinueMovement）才會把它納入
    // 候選。building-time 的圖驗證（連通性）把所有邊都當作開啟——flag 關閉造成的「暫時
    // 不可達」是執行期語意，不是建構期的圖結構錯誤（見 BoardDefinition 建構子的註解）。
    public readonly struct BoardEdgeDefinition
    {
        public BoardEdgeDefinition(string toTileId, string requiredFlag = null)
        {
            if (string.IsNullOrWhiteSpace(toTileId)) throw new ArgumentException("An edge needs a destination tile id.", nameof(toTileId));
            ToTileId = toTileId;
            RequiredFlag = requiredFlag ?? string.Empty;
        }

        public string ToTileId { get; }
        public string RequiredFlag { get; }
        public bool IsConditional => RequiredFlag.Length > 0;
    }

    // Phase 1d-2（通用 pass-through 觸發）：取代硬編碼的「PositionIndex == StartPositionIndex
    // 才給薪」。目前只有 GrantStartSalary 這一種觸發（Start 格），但任何格子都可以結構上掛
    // 這個概念——Phase 3 的 Effect 系統接手後，這裡會換成一個通用的 effect 參照，而不是
    // 寫死的 enum 列舉。
    public enum PassThroughTriggerKind
    {
        None,
        GrantStartSalary
    }

    public sealed class BoardTileDefinition
    {
        private readonly int[] _rentLevels;
        private readonly ReadOnlyCollection<int> _rentLevelsView;
        private BoardEdgeDefinition[] _edges;
        private ReadOnlyCollection<BoardEdgeDefinition> _edgesView;
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
            string airportDestinationId = null,
            IEnumerable<BoardEdgeDefinition> conditionalEdges = null,
            PassThroughTriggerKind? passThroughTrigger = null)
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
            SetEdges(nextTileIds, conditionalEdges);
            IsMainRoute = isMainRoute;
            RejoinsAtId = rejoinsAtId ?? string.Empty;
            AirportDestinationId = airportDestinationId ?? string.Empty;
            // 目前唯一的 pass-through 觸發是「經過/停在 Start 給薪」。沒有明確指定時，
            // 依 Type 自動解析（Start 格預設 GrantStartSalary），其餘格預設 None——這與
            // 舊行為（以 PositionIndex == StartPositionIndex 判斷）完全等價，因為一張
            // BoardDefinition 永遠恰好只有一個 Start 格。結構上允許任何格顯式覆寫
            // passThroughTrigger，供未來（Phase 3 Effect）掛任意格的 pass-through 效果。
            PassThroughTrigger = passThroughTrigger ?? (type == BoardTileType.Start ? PassThroughTriggerKind.GrantStartSalary : PassThroughTriggerKind.None);

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
        // 所有宣告的出邊目的地 id，無論是否帶 RequiredFlag——圖結構驗證（唯一性、missing id、
        // 連通性）全部用這份「全部邊視為開啟」的清單把關（見 BoardDefinition 建構子註解）。
        // 要知道「現在」哪些邊真的開著，請用 Edges（看 RequiredFlag）或
        // BoardDefinition.GetOpenNextTiles（套用 GameState.BoardFlags 之後的結果）。
        public IReadOnlyList<string> NextTileIds => _nextTileIdsView;
        public IReadOnlyList<BoardEdgeDefinition> Edges => _edgesView;
        public bool IsMainRoute { get; }
        public string RejoinsAtId { get; }
        public string AirportDestinationId { get; }
        public PassThroughTriggerKind PassThroughTrigger { get; }
        public BoardMapPosition MapPosition { get; private set; }

        public int GetRent(int upgradeLevel)
        {
            if (Type != BoardTileType.Property) throw new InvalidOperationException("Only properties have rent.");
            if (upgradeLevel < 0 || upgradeLevel >= _rentLevels.Length) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            return _rentLevels[upgradeLevel];
        }

        // 只被 BoardDefinition 建構子的「省略 next 時方便預設」用到（見該處註解），只接受
        // 無條件邊：這條路徑只在 tile 完全沒有宣告任何邊（條件或無條件）時才會被呼叫，
        // 不會有覆寫掉既有條件邊的風險。
        internal void SetNextTileIds(IEnumerable<string> nextTileIds)
        {
            SetEdges(nextTileIds, null);
        }

        private void SetEdges(IEnumerable<string> unconditionalNextTileIds, IEnumerable<BoardEdgeDefinition> conditionalEdges)
        {
            var edgeList = new List<BoardEdgeDefinition>();
            if (unconditionalNextTileIds != null)
            {
                foreach (var toTileId in unconditionalNextTileIds) edgeList.Add(new BoardEdgeDefinition(toTileId));
            }
            if (conditionalEdges != null) edgeList.AddRange(conditionalEdges);

            _edges = edgeList.ToArray();
            _edgesView = Array.AsReadOnly(_edges);
            _nextTileIds = new string[_edges.Length];
            for (var i = 0; i < _edges.Length; i++) _nextTileIds[i] = _edges[i].ToTileId;
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
        private readonly HashSet<string> _initialFlags;

        // initialFlags：地圖定義本身要求「一開局就開啟」的 BoardFlags（Phase 1d-2）。
        // GameSession 建構 GameState 時會用這份清單播種 GameState.BoardFlags；之後的變動
        // （Phase 3 Effect 透過 GameSession.SetBoardFlag/ClearBoardFlag）不會回寫這裡——
        // BoardDefinition 本身在整個 Session 生命週期內是不可變的靜態地圖資料。
        public BoardDefinition(IEnumerable<BoardTileDefinition> tiles, IEnumerable<string> initialFlags = null)
        {
            if (tiles == null) throw new ArgumentNullException(nameof(tiles));
            _tiles = new List<BoardTileDefinition>(tiles);
            if (_tiles.Count < 2) throw new ArgumentException("A board needs at least two tiles.", nameof(tiles));

            _initialFlags = new HashSet<string>(StringComparer.Ordinal);
            if (initialFlags != null)
            {
                foreach (var flag in initialFlags)
                {
                    if (string.IsNullOrWhiteSpace(flag)) throw new ArgumentException("A board flag needs a non-empty name.", nameof(initialFlags));
                    _initialFlags.Add(flag);
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            _tilesById = new Dictionary<string, BoardTileDefinition>(StringComparer.Ordinal);
            var startCount = 0;
            var startPositionIndex = -1;
            string startTileId = null;
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i] ?? throw new ArgumentException("Board tiles cannot be null.", nameof(tiles));
                if (!ids.Add(tile.Id)) throw new ArgumentException("Board tile ids must be unique.", nameof(tiles));
                // 規則（保留）：PositionIndex == 陣列索引。ADR-0002 之後這只是一個儲存層
                // 不變量——它讓 tile 有一個穩定、可當 array handle 用的 int（PlayerState.
                // CurrentTileIndex 等下游 API 現在還靠它），不再隱含「這是環上第幾格」的
                // 環序語意。真正的拓撲（連通性、分岔、合流）全部改由 NextTileIds 這張圖決定。
                if (tile.PositionIndex != i) throw new ArgumentException("Tile position indexes must be contiguous and match list order.", nameof(tiles));
                _tilesById.Add(tile.Id, tile);
                if (tile.IsMainRoute) _mainRouteTileCount++;
                if (tile.Type == BoardTileType.Start)
                {
                    startCount++;
                    startPositionIndex = tile.PositionIndex;
                    startTileId = tile.Id;
                }
            }

            if (startCount != 1) throw new ArgumentException("A board must have exactly one Start tile.", nameof(tiles));

            // ADR-0002：移除「主環必佔前 N 個連續 index」約束。節點的身分只靠 id；
            // IsMainRoute 降級為佈局/標注提示（見 BoardLayoutPlanner.cs 與 MainRouteTileCount），
            // 不再是規則驗證的一部分。
            //
            // 省略 NextTileIds 的「主環」格（IsMainRoute=true）預設接到陣列順序的下一格
            // （含 wrap 回第一格）：這純粹是「沒寫 next 時」的方便預設寫法，不代表地圖真的
            // 構成一個環——地圖是否真的連通、每格是否真的有出路，由下面的一般圖驗證
            // （出邊數、可達性）把關，不是由這個預設填值本身保證的。
            //
            // 這裡只對 IsMainRoute=true 的格子套用預設，刻意不管 IsMainRoute=false 的分支格：
            // 分支格的出邊必須自己明確宣告，省略（或刻意傳空陣列）就是測試/地圖作者
            // 想表達「這格沒有出路」，不應該被這個便利預設悄悄蓋過去。
            //
            // Phase 1f review 修正（M6）：取模範圍用 _mainRouteTileCount（主環格數），
            // 不是 _tiles.Count（全部格數）——恢復 Phase 1d-1 之前的舊語意。對「所有格都是
            // 主環」的棋盤（絕大多數測試/PrototypeBoardFactory 皆如此）兩者數值相同，沒有
            // 差別；只有「混了非主環分支格」的棋盤才看得出差異，這時候用 _mainRouteTileCount
            // 才是「這格 i 省略 next 時，想接到下一個主環格」這句話原本要表達的意思。
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (_tiles[i].IsMainRoute && _tiles[i].NextTileIds.Count == 0)
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
            }

            // ADR-0002：一般圖驗證取代「主環必須形成單一封閉迴圈」——只要求從 Start 可以
            // 走到每一格（連通性）；每格至少一條出邊已在上面逐格檢查過。分岔（一節點多出邊）
            // 與合流（多節點指向同一節點）都是合法拓撲，不要求迴圈本身是單一封閉環。
            ValidateReachableFromStart(startTileId);

            // Phase 1d-3（ADR-0002 §3）：視覺佈局計算（橢圓＋Bezier）不再是建構流程的一部分，
            // 搬到獨立的 BoardLayoutPlanner.AssignMapPositions(board)，由需要座標的呼叫端
            // （GodotBoardView.Build、Tools/Simulation --export-map）自己顯式呼叫。建構完
            // 成後，所有 tile 的 MapPosition 維持預設值（全 0）直到有人呼叫它——這是刻意的：
            // 證明規則路徑完全不依賴座標（見 BoardLayoutPlanner.cs 開頭的說明）。

            StartPositionIndex = startPositionIndex;
            _tilesView = _tiles.AsReadOnly();
        }

        public int TileCount => _tiles.Count;
        public int MainRouteTileCount => _mainRouteTileCount;
        public int StartPositionIndex { get; }
        public IReadOnlyList<BoardTileDefinition> Tiles => _tilesView;
        public IReadOnlyCollection<string> InitialFlags => _initialFlags;

        public BoardTileDefinition GetTileAt(int positionIndex)
        {
            if (positionIndex < 0 || positionIndex >= _tiles.Count) throw new ArgumentOutOfRangeException(nameof(positionIndex));
            return _tiles[positionIndex];
        }

        public BoardTileDefinition GetTile(string id) => id != null && _tilesById.TryGetValue(id, out var tile) ? tile : null;

        // 全部宣告的出邊（不管 RequiredFlag），依宣告順序——圖結構工具／驗證／佈局使用。
        public IReadOnlyList<BoardTileDefinition> GetNextTiles(int positionIndex)
        {
            var source = GetTileAt(positionIndex);
            var next = new List<BoardTileDefinition>(source.NextTileIds.Count);
            for (var i = 0; i < source.NextTileIds.Count; i++) next.Add(_tilesById[source.NextTileIds[i]]);
            return next.AsReadOnly();
        }

        // Phase 1d-2（條件邊）：移動解析實際要用的版本——只回傳「現在開著」的邊
        // （RequiredFlag 是空字串，或 isFlagOpen(flag) 回傳 true）。isFlagOpen 刻意用委派
        // 而不是直接吃 GameState，讓 Board 維持對 GameState 零依賴（GameSession 呼叫時
        // 直接傳 State.HasBoardFlag 這個方法群組即可）。
        public IReadOnlyList<BoardTileDefinition> GetOpenNextTiles(int positionIndex, Func<string, bool> isFlagOpen)
        {
            var source = GetTileAt(positionIndex);
            var next = new List<BoardTileDefinition>(source.Edges.Count);
            for (var i = 0; i < source.Edges.Count; i++)
            {
                var edge = source.Edges[i];
                if (!edge.IsConditional || (isFlagOpen != null && isFlagOpen(edge.RequiredFlag))) next.Add(_tilesById[edge.ToTileId]);
            }
            return next.AsReadOnly();
        }

        public int GetDistrictPropertyCount(string districtId)
        {
            var count = 0;
            for (var i = 0; i < _tiles.Count; i++)
                if (_tiles[i].Type == BoardTileType.Property && string.Equals(_tiles[i].DistrictId, districtId, StringComparison.Ordinal)) count++;
            return count;
        }

        // ADR-0002：取代舊有「每個支線格各自 BFS 驗證能走到自己宣告的 RejoinsAtId」。
        // 新規則只要求一件事——從 Start 出發，一般圖的 BFS 可以走到棋盤上每一格；不再
        // 要求存在某個特定的「主環」或每條支線各自匯合到哪裡。
        private void ValidateReachableFromStart(string startId)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { startId };
            var pending = new Queue<string>();
            pending.Enqueue(startId);
            while (pending.Count > 0)
            {
                var tile = _tilesById[pending.Dequeue()];
                for (var edge = 0; edge < tile.NextTileIds.Count; edge++)
                {
                    var nextId = tile.NextTileIds[edge];
                    if (visited.Add(nextId)) pending.Enqueue(nextId);
                }
            }
            for (var i = 0; i < _tiles.Count; i++)
            {
                if (!visited.Contains(_tiles[i].Id))
                    throw new ArgumentException("Tile is not reachable from the Start tile: " + _tiles[i].Id, nameof(_tiles));
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

    public static class PrototypeBoardFactory
    {
        private const int MainRouteCount = 41;

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
                "rio-carnival", "cape-town-table-mountain", "lisbon-yellow-tram", "airport-jfk", "madrid-retiro-park",
                "paris-court"
            };

            for (var i = 0; i < MainRouteCount; i++)
            {
                var next = new List<string> { ids[(i + 1) % MainRouteCount] };
                if (i == 10) next.Add("macau-ferry-pier");
                if (i == 20) next.Add("ubud-ridge-rail");
                if (i == 31) next.Add("thames-river-cruise");
                tiles.Add(CreateMainTile(i, ids[i], next.ToArray()));
            }

            tiles.Add(Simple(41, "macau-ferry-pier", BoardTileType.Transit, "澳門渡輪碼頭", new[] { "macau-heritage-quarter" }, false, "hong-kong-victoria-peak"));
            tiles.Add(Property(42, "macau-heritage-quarter", "澳門歷史城區", "macau", 180, 18, 70, new[] { 18, 36, 65, 110, 170 }, new[] { "hong-kong-victoria-peak" }, false, "hong-kong-victoria-peak"));
            tiles.Add(Simple(43, "ubud-ridge-rail", BoardTileType.Transit, "烏布山脊觀景列車", new[] { "bali-rice-terraces" }, false, "bali-cliff-resort"));
            tiles.Add(Property(44, "bali-rice-terraces", "峇里島梯田步道", "bali", 200, 20, 80, new[] { 20, 40, 72, 120, 185 }, new[] { "bali-cliff-resort" }, false, "bali-cliff-resort"));
            tiles.Add(Simple(45, "thames-river-cruise", BoardTileType.Transit, "泰晤士河遊船", new[] { "shakespeare-globe" }, false, "destiny-open-ticket"));
            tiles.Add(Property(46, "shakespeare-globe", "莎士比亞環球劇場", "london", 240, 24, 95, new[] { 24, 48, 86, 145, 220 }, new[] { "london-southbank" }, false, "destiny-open-ticket"));
            tiles.Add(Property(47, "london-southbank", "倫敦南岸藝文區", "london", 260, 26, 105, new[] { 26, 52, 94, 158, 240 }, new[] { "destiny-open-ticket" }, false, "destiny-open-ticket"));

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
                case 9: return Effect(index, id, BoardTileType.Hospital, "意外住院・東京醫療中心", 2, next);
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
                case 27: return Property(index, id, "巴黎蒙馬特街區", "france", 250, 25, 100, new[] { 25, 50, 90, 150, 230 }, next);
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
                case 40: return Effect(index, id, BoardTileType.Court, "巴黎國際法院", 100, next);
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
