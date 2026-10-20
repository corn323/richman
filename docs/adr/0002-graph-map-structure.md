# ADR-0002：地圖改為通用圖結構（nodes + edges）

- 狀態：Accepted
- 日期：2026-10-20

## 背景

現行 `BoardDefinition` 已是半圖結構：`NextTileIds` 支援多出口、`RejoinsAtId` 支援合流、Airport 支援傳送，並有 BFS 連通性驗證——地基約六成。但驗證邏輯仍強制：

- 「PositionIndex == 陣列索引」（Board.cs:153）；
- 「主環必佔前 N 個連續 index 且成單一封閉迴圈」（Board.cs:163-191）；
- 「支線每格僅一出口」（Board.cs:277）——這其實是**版面限制**，卻混在規則驗證裡。

另外 Core 內含視覺佈局計算：`AssignMapPositions`（橢圓＋Bezier，Board.cs:7-15,251-306），以及硬編碼的 48 格地圖工廠 `PrototypeBoardFactory.Create()`（Board.cs:328-436）。缺少條件邊與通用傳送機制（Airport 是特例硬編碼）。

這些約束使多地圖、分岔路網、事件開關道路等目標功能無法實作。

## 決策

1. **地圖改為通用圖結構（nodes + edges）**，以 node-id 為唯一 identity。廢除「PositionIndex == 陣列索引」與「主環必佔前 N 連續 index 且成單一封閉迴圈」約束（Board.cs:153,163-191）。
2. 圖結構支援：
   - 分岔（一節點多出邊）；
   - 合流（多入邊）；
   - 單向邊；
   - **條件邊**（可被事件開關）；
   - **傳送邊**（取代 Airport 特例，成為通用 Effect）；
   - **pass-through 與 landing 觸發**（經過 vs 停留分別掛鉤）。
3. `pos` / `rot` 僅為**佈局提示**，不影響規則。`AssignMapPositions` 的視覺佈局計算（Board.cs:251-306）移出 Core，歸展示層或佈局工具。
4. 地圖由 `Content/Maps/<id>/map.json` 定義，內容包含 `nodes` / `edges` / `regions` / `mapEvents` / `ruleOverrides`。
5. 載入時驗證：
   - node-id 唯一；
   - 圖連通；
   - 起點存在；
   - 無非預期死路；
   - property 節點具合法 `propertyId` 與 `region`；
   - 事件引用的節點存在。
6. 現有 48 格地圖遷移為第一張地圖，**行為保持一致**——以黃金重播快照（docs/AUDIT.md §7.2 T1）驗證遷移前後逐事件相同。

## 證據

- 半圖地基已存在：`NextTileIds` / `RejoinsAtId` / BFS 驗證（Board.cs）。
- 索引即位置的約束：Board.cs:153,163-191。
- 版面限制混入規則驗證：Board.cs:277。
- Core 內視覺座標計算：Board.cs:7-15,251-306。
- 硬編碼地圖工廠：Board.cs:328-436。

## 後果

- **API 破壞**：`PlayerState.CurrentTileIndex`、`CommandResult.PathTileIndices` 等 int 索引 API 需改為 node-id。所有下游（Godot 展示層、Bot、模擬器）同步修改。
- `GodotBoardView` 的 41/48 硬編碼斷言（GodotBoardView.cs:153-162）移除。
- 版面限制（如「支線單出口」）從規則驗證中拆出，改由佈局工具或地圖 lint 把關；規則驗證只留真正的規則（docs/AUDIT.md §7.2 T5 負責逐條分類）。
- Airport 特例刪除，改為通用傳送 Effect；現狀「機場傳送不觸發目的格」行為先以 T4 特徵測試鎖定，是否改變屬規則決策、另行審批。
- 需要建立 `Content/Maps/` 目錄、map.json schema 與 loader/validator（目前皆不存在，docs/AUDIT.md §4）。
