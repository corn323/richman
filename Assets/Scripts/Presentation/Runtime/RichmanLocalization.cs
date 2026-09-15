using System;
using System.Collections.Generic;
using Richman.Core;
using UnityEngine;

namespace Richman.Presentation
{
    public enum RichmanLanguage
    {
        TraditionalChinese,
        English,
        Japanese
    }

    public static class RichmanLocalization
    {
        private const string PreferenceKey = "Richman.Language";
        private static readonly Dictionary<string, string>[] Tables = CreateTables();
        private static readonly Dictionary<string, string>[] TileTables = CreateTileTables();

        public static event Action LanguageChanged;
        public static RichmanLanguage CurrentLanguage { get; private set; } = LoadLanguage();

        public static void SetLanguage(RichmanLanguage language)
        {
            if (CurrentLanguage == language) return;
            CurrentLanguage = language;
            PlayerPrefs.SetInt(PreferenceKey, (int)language);
            PlayerPrefs.Save();
            LanguageChanged?.Invoke();
        }

        public static string Text(string key)
        {
            var table = Tables[(int)CurrentLanguage];
            if (table.TryGetValue(key, out var value)) return value;
            if (Tables[(int)RichmanLanguage.English].TryGetValue(key, out value)) return value;
            return key;
        }

        public static string Format(string key, params object[] arguments)
        {
            return string.Format(Text(key), arguments);
        }

        public static string TileName(BoardTileDefinition tile)
        {
            if (tile == null) return Text("tile.unknown");
            var table = TileTables[(int)CurrentLanguage];
            if (table.TryGetValue(tile.Id, out var value)) return value;
            return tile.DisplayName;
        }

        public static string District(string districtId)
        {
            return Text("district." + districtId);
        }

        public static string TileType(BoardTileType type)
        {
            return Text("type." + type);
        }

        public static string PlayerStatus(PlayerStatus status)
        {
            return Text("status." + status);
        }

        public static string CommandError(CommandErrorCode code, string fallback)
        {
            var key = "error." + code;
            var value = Text(key);
            return value == key ? fallback : value;
        }

        private static RichmanLanguage LoadLanguage()
        {
            var value = PlayerPrefs.GetInt(PreferenceKey, (int)RichmanLanguage.TraditionalChinese);
            return Enum.IsDefined(typeof(RichmanLanguage), value)
                ? (RichmanLanguage)value
                : RichmanLanguage.TraditionalChinese;
        }

        private static Dictionary<string, string>[] CreateTables()
        {
            return new[]
            {
                CreateTraditionalChinese(),
                CreateEnglish(),
                CreateJapanese()
            };
        }

        private static Dictionary<string, string> CreateTraditionalChinese()
        {
            var t = new Dictionary<string, string>
            {
                ["title"] = "RICHMAN",
                ["player-name"] = "玩家 {0}",
                ["subtitle"] = "玩具城市地產桌遊  |  本機遊玩",
                ["current-player"] = "目前玩家\nP{0}  {1}",
                ["turn"] = "回合 {0}   {1}",
                ["money"] = "現金  0",
                ["dice.none"] = "骰子  --",
                ["dice.result"] = "骰子  {0} + {1} = {2}",
                ["players"] = "玩家列表",
                ["player.entry"] = "P{0}   1   格子 {2}   {3}",
                ["owned-properties"] = "持有地產",
                ["none-yet"] = "尚未持有",
                ["current-tile"] = "目前格子\n{0}\n{1}",
                ["property"] = "地產資訊",
                ["district"] = "區域  {0}",
                ["price"] = "價格  0",
                ["rent"] = "租金  0",
                ["upgrade-cost"] = "升級  0",
                ["roll"] = "擲骰子",
                ["buy"] = "購買",
                ["upgrade"] = "升級",
                ["end-turn"] = "結束回合",
                ["new-game"] = "新遊戲",
                ["welcome"] = "歡迎來到 Richman。玩家 1，請擲骰子。",
                ["rolled"] = "P{0} 擲出 {1}。",
                ["purchased"] = "地產已購買，格子上出現你的所有權標記。",
                ["upgraded"] = "地產已升級，建築外觀已變更。",
                ["finished"] = "遊戲結束。勝利者：P{0}。",
                ["landed-winner"] = "P{0} 抵達「{1}」。勝利者：P{2}。",
                ["landed-buy"] = "請選擇購買或結束回合：「{0}」。",
                ["landed-upgrade"] = "請選擇升級或結束回合：「{0}」。",
                ["landed"] = "P{0} 抵達「{1}」。",
                ["action-unavailable"] = "目前無法執行：{0}",
                ["language.zh"] = "繁中",
                ["language.en"] = "English",
                ["language.ja"] = "日本語",
                ["tile.unknown"] = "未知格子"
            };
            AddTypes(t, new[] { "起點", "地產", "稅務", "獎勵", "休息", "交通", "特殊" });
            AddStatuses(t, new[] { "進行中", "破產", "勝利者" });
            AddDistricts(t, new[] { "市集區", "港灣區", "工業區", "住宅區", "娛樂區", "科技區", "市政區" });
            AddPhases(t, new[] { "等待擲骰", "等待行動", "等待結束回合", "已結束" });
            AddErrors(t, new[] { "無效的指令。", "遊戲已結束。", "找不到玩家。", "現在不是你的回合。", "目前階段不能執行這個動作。", "玩家已被淘汰。", "這塊地產目前無法購買。", "資金不足。", "這塊地產已達最高等級。" });
            return t;
        }

        private static Dictionary<string, string> CreateEnglish()
        {
            var t = CreateTraditionalChinese();
            var values = new Dictionary<string, string>
            {
                ["player-name"] = "Player {0}",
                ["subtitle"] = "STYLIZED TOY CITY  |  LOCAL PLAYTEST", ["current-player"] = "CURRENT PLAYER\nP{0}  {1}",
                ["turn"] = "TURN {0}   {1}", ["money"] = "CASH  0", ["dice.none"] = "DICE  --", ["dice.result"] = "DICE  {0} + {1} = {2}",
                ["players"] = "PLAYERS", ["player.entry"] = "P{0}   1   Tile {2}   {3}", ["owned-properties"] = "OWNED PROPERTIES",
                ["none-yet"] = "None yet", ["current-tile"] = "CURRENT TILE\n{0}\n{1}", ["property"] = "PROPERTY", ["district"] = "District  {0}",
                ["price"] = "Price  0", ["rent"] = "Rent  0", ["upgrade-cost"] = "Upgrade  0", ["roll"] = "ROLL DICE", ["buy"] = "BUY",
                ["upgrade"] = "UPGRADE", ["end-turn"] = "END TURN", ["new-game"] = "NEW GAME", ["welcome"] = "Welcome to Richman. Player 1, roll the dice.",
                ["rolled"] = "P{0} rolled {1}.", ["purchased"] = "Property purchased. Your ownership marker is on the tile.",
                ["upgraded"] = "Property upgraded. The building changed level.", ["finished"] = "Game finished. Winner: P{0}.",
                ["landed-winner"] = "P{0} landed on {1}. Winner: P{2}.", ["landed-buy"] = "Choose Buy or End Turn for {0}.",
                ["landed-upgrade"] = "Choose Upgrade or End Turn for {0}.", ["landed"] = "P{0} landed on {1}.",
                ["action-unavailable"] = "Action unavailable: {0}", ["language.zh"] = "繁中", ["language.en"] = "English", ["language.ja"] = "日本語"
            };
            foreach (var pair in values) t[pair.Key] = pair.Value;
            AddTypes(t, new[] { "Start", "Property", "Tax", "Bonus", "Rest", "Transit", "Special" });
            AddStatuses(t, new[] { "Active", "Bankrupt", "Winner" });
            AddDistricts(t, new[] { "Market", "Harbor", "Industrial", "Residential", "Entertainment", "Technology", "Civic" });
            AddErrors(t, new[] { "Invalid command.", "The game has already finished.", "Player not found.", "It is not your turn.", "That action is not available now.", "This player has been eliminated.", "This property is unavailable.", "Insufficient funds.", "This property is already at max level." });
            AddPhases(t, new[] { "Awaiting Roll", "Awaiting Action", "Awaiting End Turn", "Finished" });
            return t;
        }

        private static Dictionary<string, string> CreateJapanese()
        {
            var t = CreateEnglish();
            var values = new Dictionary<string, string>
            {
                ["player-name"] = "プレイヤー {0}",
                ["subtitle"] = "トイシティの不動産ボードゲーム  |  ローカルプレイ", ["current-player"] = "現在のプレイヤー\nP{0}  {1}",
                ["turn"] = "ターン {0}   {1}", ["money"] = "所持金  0", ["dice.none"] = "ダイス  --", ["dice.result"] = "ダイス  {0} + {1} = {2}",
                ["players"] = "プレイヤー", ["player.entry"] = "P{0}   1   マス {2}   {3}", ["owned-properties"] = "所有物件",
                ["none-yet"] = "まだありません", ["current-tile"] = "現在のマス\n{0}\n{1}", ["property"] = "物件情報", ["district"] = "地区  {0}",
                ["price"] = "価格  0", ["rent"] = "家賃  0", ["upgrade-cost"] = "アップグレード  0", ["roll"] = "ダイスを振る",
                ["buy"] = "購入", ["upgrade"] = "アップグレード", ["end-turn"] = "ターン終了", ["new-game"] = "新しいゲーム",
                ["welcome"] = "Richmanへようこそ。プレイヤー1、ダイスを振ってください。", ["rolled"] = "P{0} は {1} を出しました。",
                ["purchased"] = "物件を購入しました。所有マーカーが表示されます。", ["upgraded"] = "物件をアップグレードしました。建物が変化しました。",
                ["finished"] = "ゲーム終了。勝者：P{0}。", ["landed-winner"] = "P{0} は {1} に到着しました。勝者：P{2}。",
                ["landed-buy"] = "{0} の購入またはターン終了を選んでください。", ["landed-upgrade"] = "{0} のアップグレードまたはターン終了を選んでください。",
                ["landed"] = "P{0} は {1} に到着しました。", ["action-unavailable"] = "実行できません：{0}", ["language.zh"] = "繁中",
                ["language.en"] = "English", ["language.ja"] = "日本語"
            };
            foreach (var pair in values) t[pair.Key] = pair.Value;
            AddTypes(t, new[] { "スタート", "物件", "税金", "ボーナス", "休憩", "交通", "スペシャル" });
            AddStatuses(t, new[] { "プレイ中", "破産", "勝者" });
            AddDistricts(t, new[] { "マーケット", "港湾", "工業", "住宅", "エンタメ", "テクノロジー", "市政" });
            AddErrors(t, new[] { "無効なコマンドです。", "ゲームは終了しています。", "プレイヤーが見つかりません。", "あなたのターンではありません。", "今はその行動を実行できません。", "このプレイヤーは脱落しています。", "この物件は購入できません。", "資金が不足しています。", "この物件は最大レベルです。" });
            AddPhases(t, new[] { "ダイス待ち", "行動待ち", "ターン終了待ち", "終了" });
            return t;
        }

        private static void AddTypes(Dictionary<string, string> t, string[] values)
        {
            var keys = new[] { "Start", "Property", "Tax", "Bonus", "Rest", "Transit", "Special" };
            for (var i = 0; i < keys.Length; i++) t["type." + keys[i]] = values[i];
        }

        private static void AddStatuses(Dictionary<string, string> t, string[] values)
        {
            var keys = new[] { "Active", "Bankrupt", "Winner" };
            for (var i = 0; i < keys.Length; i++) t["status." + keys[i]] = values[i];
        }

        private static void AddDistricts(Dictionary<string, string> t, string[] values)
        {
            var keys = new[] { "market", "harbor", "industrial", "residential", "entertainment", "technology", "civic" };
            for (var i = 0; i < keys.Length; i++) t["district." + keys[i]] = values[i];
        }

        private static void AddErrors(Dictionary<string, string> t, string[] values)
        {
            var keys = new[] { "InvalidCommand", "GameFinished", "PlayerNotFound", "WrongTurn", "InvalidPhase", "PlayerEliminated", "PropertyUnavailable", "InsufficientFunds", "PropertyAlreadyMaxLevel" };
            for (var i = 0; i < keys.Length; i++) t["error." + keys[i]] = values[i];
        }

        private static void AddPhases(Dictionary<string, string> t, string[] values)
        {
            var keys = new[] { "AwaitingRoll", "AwaitingAction", "AwaitingEndTurn", "Finished" };
            for (var i = 0; i < keys.Length; i++) t["phase." + keys[i]] = values[i];
        }

        private static Dictionary<string, string>[] CreateTileTables()
        {
            var ids = new[] { "start", "lantern-market", "garden", "canal-row", "rooftop-park", "sky-tram", "copper-workshop", "city-maintenance", "sunset-apartments", "clock-tower", "pixel-arcade", "street-fair", "skyline-lab", "courtyard", "harbor-exchange", "ferry-terminal", "bamboo-court", "transit-fund", "mosaic-plaza", "observation-deck", "rainy-day-theater", "public-art-fund", "foundry-lane", "rooftop-garden", "solaris-heights", "monorail-loop", "tideglass-pier", "infrastructure-works", "northlight-studios", "quiet-alley", "crescent-bazaar", "festival-grant", "neon-garden", "riverside-rest", "aster-foundry", "summit-forum" };
            var en = new[] { "Central Station", "Lantern Market", "Community Garden", "Canal Row", "Rooftop Park", "Sky Tram", "Copper Workshop", "City Maintenance", "Sunset Apartments", "Old Clock Tower", "Pixel Arcade", "Street Fair", "Skyline Lab", "Quiet Courtyard", "Harbor Exchange", "Ferry Terminal", "Bamboo Court", "Transit Fund", "Mosaic Plaza", "Observation Deck", "Rainy Day Theater", "Public Art Fund", "Foundry Lane", "Rooftop Garden", "Solaris Heights", "Monorail Loop", "Tideglass Pier", "Infrastructure Works", "Northlight Studios", "Quiet Alley", "Crescent Bazaar", "Festival Grant", "Neon Garden", "Riverside Rest", "Aster Foundry", "Summit Forum" };
            var zh = new[] { "中央車站", "燈籠市集", "社區花園", "運河街", "屋頂公園", "空中電車", "銅工坊", "城市維護", "夕陽公寓", "古鐘塔", "像素遊樂場", "街頭市集", "天際實驗室", "寧靜庭院", "港灣交易所", "渡輪碼頭", "竹影庭院", "交通基金", "馬賽克廣場", "觀景台", "雨日劇院", "公共藝術基金", "鑄造巷", "屋頂花園", "索拉里斯高地", "單軌環線", "潮玻璃碼頭", "基礎建設工程", "北光工作室", "靜謐小巷", "新月市集", "節慶補助", "霓虹花園", "河畔休息區", "艾斯特鑄造所", "頂峰論壇" };
            var ja = new[] { "セントラル駅", "ランタン市場", "コミュニティガーデン", "キャナル通り", "屋上公園", "スカイトラム", "銅工房", "都市整備", "サンセット住宅", "古時計塔", "ピクセルアーケード", "ストリートフェア", "スカイライン研究所", "静かな中庭", "港湾取引所", "フェリーターミナル", "竹の庭", "交通基金", "モザイク広場", "展望デッキ", "雨の日劇場", "公共アート基金", "鋳造所通り", "屋上庭園", "ソラリス高台", "モノレール環状線", "タイドグラス埠頭", "インフラ工事", "ノースライトスタジオ", "静かな路地", "クレセントバザール", "フェスティバル助成", "ネオンガーデン", "川辺の休憩所", "アスター鋳造所", "サミットフォーラム" };
            var tables = new Dictionary<string, string>[3];
            var values = new[] { zh, en, ja };
            for (var language = 0; language < tables.Length; language++)
            {
                tables[language] = new Dictionary<string, string>();
                for (var i = 0; i < ids.Length; i++) tables[language][ids[i]] = values[language][i];
            }
            return tables;
        }
    }
}
