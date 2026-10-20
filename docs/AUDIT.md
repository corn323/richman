# Phase 0 架構審查報告（AUDIT）

> 日期：2026-10-20
> 範圍：Richman 專案全域唯讀審查（8 個領域 agent），branch `master`。
> 本文件為 Phase 1 重構的事實基準；所有結論均附 `file:line` 證據。

---

## 1. 現況架構

### 1.1 Core 原始碼位置與組件形態

Core 原始碼實際位置為 `Assets/Scripts/Core/Runtime/`，共 8 檔：

| 檔案 | 行數 | 內容 |
| --- | --- | --- |
| `Board.cs` | 437 | 棋盤定義、驗證、視覺座標、PrototypeBoardFactory |
| `GameSession.cs` | 663 | 規則引擎主體（god class，見 B 級風險） |
| `GameModel.cs` | 326 | GameState / PlayerState / TileState 等資料模型 |
| `GameCommands.cs` | 123 | Command 型別與 CommandResult |
| `Dice.cs` | — | IGameRandom / SeededRandom / 骰子 |
| `MapCard.cs` | — | 卡牌資料型別 |
| `Bots.cs` | — | IBotStrategy / SimpleBotStrategy |
| `BotSimulation.cs` | — | 模擬跑局器 |

**沒有獨立的 Richman.Core 組件**。三個入口以「原始碼直接編譯」方式共用同一批 .cs：

- `Richman.Core.Standalone.csproj`（netstandard2.1、零 PackageReference）→ 測試用；同時是隱性的「Core 無引擎依賴」防護——只要它能編譯，Core 就沒有引用 Unity/Godot API。
- `GodotProject/Richman.Godot.csproj`（Godot.NET.Sdk/4.7.2、net10.0）以
  `<Compile Include="..\Assets\Scripts\Core\Runtime\**\*.cs" LinkBase="Richman.Core">`
  直接吸收原始碼（Richman.Godot.csproj:8-10）。
- `Tools/Simulation/Richman.Simulation.csproj` 同樣以 Compile Include 直接吸收。

後果：Core 的 `internal` 成員對展示層全開，組件邊界只存在於約定，不存在於編譯器（詳見 B2）。

### 1.2 API 模式

現行 API 為 `GameSession.Execute(GameCommand) → CommandResult`（單一結果物件），外部另行輪詢 `session.State` 取得狀態。**不是 Command→Event 模式**——展示層看不到「發生了什麼事」的序列，只能比對狀態差異或讀 `LastEventMessage` 字串。

### 1.3 棋盤

- `PrototypeBoardFactory.Create()`（Board.cs:328-436）硬編碼 48 格：41 格主環＋3 條合流支線。
- `BoardDefinition` 已是半圖結構：`NextTileIds` 支援多出口、`RejoinsAtId` 支援合流、Airport 支援傳送。但驗證邏輯強制「單一封閉主環」且「PositionIndex == 陣列索引」（Board.cs:153,163-191），離通用圖結構尚有距離（估計地基約六成，詳見 ADR-0002）。
- `GodotProject/Data/archipelago_tiles.json` 是由 `Tools/Simulation --export-map` 匯出的**快照**，runtime 並不讀它。README 對此描述與實況不符（README.md:21,25）。

### 1.4 Godot 展示層

- `Main.tscn` 僅 1 個手拉節點，棋盤與 UI 全部由程式動態生成。
- **未發現展示層自行計算規則**：移動使用 Core 回傳的 `PathTileIndices`，Transform 僅跟隨狀態。這點健康，應在重構中保持。

### 1.5 Unity 版現況

- 棋盤佈局停留在舊 36/40 格方形外圈（`BoardLayout.cs`；`Gameplay.unity` 的 `tileViews` 只有 36 個），對現行 48 格 Core 執行會 `IndexOutOfRange`。
- 但本地化領先 Godot：`RichmanLocalization.cs` 有繁中/EN/日完整 key-table；Godot 端零本地化，約 60+ 處硬編碼繁中字串。
- 處置方向見 ADR-0001（Godot-only，Unity 凍結）。

---

## 2. 測試與模擬基準（Phase 1 重構後比對用）

重構後必須重跑並與以下數字比對：

### 2.1 單元測試

- `dotnet test`（.NET SDK 10.0.401）：**32/32 全綠，62ms**。

### 2.2 自測

- `Tools/Simulation --self-test`：**18 項斷言全過**（輸出 `CORE_VERIFICATION_OK`）。

### 2.3 四 Bot 1000 局模擬（seed 928371–929370）

| 指標 | 數值 |
| --- | --- |
| 完成局數 | 1000/1000 |
| 卡死 | 0 |
| 負數金錢 | 0 |
| 孤兒地產 | 0 |
| 回合數 min/max/avg | 85 / 240 / 238.97 |
| 勝率 | P1 27.8%、P2 26.3%、P3 24.7%、P4 21.2% |

**重要警示**：`TurnLimit=240` 實際只有 60 輪（`TurnNumber` 每換人 +1，見 GameSession.cs:562,584）。平均 238.97 幾乎全數撞上限 → 絕大多數對局是靠淨值平手裁定結束，而非破產分勝負；且先手優勢明顯（P1 27.8% vs P4 21.2%）。**此基準的平衡統計意義有限**，僅作回歸比對用，已記錄在案。

### 2.4 確定性

顯式注入 RNG 的路徑下，同 seed 兩次完整對局日誌**逐字元相同**（476,204 字元，IDENTICAL）。注意：這只覆蓋「有注入 RNG」的路徑；未注入時的破口見 B1。

---

## 3. 違規與風險清單（依嚴重度）

### 3.1 Blocker

- **B1 確定性破口**：GameSession.cs:34
  `_random = random ?? new SeededRandom(Environment.TickCount)`。
  Godot 端 RichmanMain.cs:26,56 未傳 random → 卡牌/法院亂數每次啟動都不同；同時骰子固定 seed 928371 → 每局骰序相同。兩個錯誤方向疊加：該固定的不固定、不該固定的固定了。
- **B2 組件邊界失效**：Richman.Godot.csproj:8-10 的原始碼連結使 Core 的 `internal` mutator（`PlayerState.AddMoney`/`Spend`、`GameState.Phase` setter 等）對展示層全開。已有實際使用：GodotBoardView.cs:159 呼叫 internal 的 `ContainsOrdinal`。目前 grep 未發現狀態 mutator 遭濫用，但**無編譯器防護**，只靠紀律。
- **B3 無確定性/重播測試**：現有測試大量斷言 `LastEventMessage` 文字。Phase 1 一旦改掉訊息字串（本就該改，見 Major），保護網會整批失效，屆時規則若被悄悄改變將無任何測試能攔截。

### 3.2 Major

- **交易無對方同意**：GameSession.cs:321-357。Core 允許當前玩家單方面以 $0 拿走對手資產；「雙方確認」只存在於 HUD（GodotGameplayHud.cs:287）。一旦連線化等同資產竊取。修法見 ADR-0003 的兩階段交易與 SEC-CMD-02/03。
- **「現在能做什麼」判斷三處重複**：GodotGameplayHud.cs:155-181、Unity GameplayHUD.cs:153-165、Bots.cs:19-38。需要 Core 提供 `GetLegalCommands`（ADR-0003）。
- **展示層從狀態差異「猜」事件**：RichmanMain.cs:125,139,144-153——用 `DiceResult==null` 猜住院、用路徑尾格猜傳送。是缺 Event 模型的直接症狀。
- **Core 產生顯示字串**：約 20 處 `LastEventMessage`，中英混雜（GameSession.cs:222,262,266,355,382,428,643 等）。Core 不應含顯示文字，應改為結構化事件＋本地化 key（ADR-0003）。
- **Core 計算視覺座標**：Board.cs:7-15,251-306（`AssignMapPositions` 橢圓＋Bezier）。另 Board.cs:277「支線每格僅一出口」是**版面限制混入規則驗證**，兩者需拆分（ADR-0002）。
- **破產收歸銀行未重設 UpgradeLevel**：GameSession.cs:530-536。下一位買家直接繼承建築等級，疑似 bug，列入 T4 特徵測試記錄現狀。
- **Godot 建築節點 QueueFree 未先 RemoveChild**：GodotBoardView.cs:209-225，造成同名節點累積。
- **硬編碼斷言與色票缺口**：GodotBoardView.cs:153-162 硬編碼 41/48；TileColor 色票缺 6 個 district，靜默 fallback 成灰色（GodotBoardView.cs:241-249）。
- **地圖/卡牌/規則數值硬編碼**：地圖 Board.cs:328-436；卡牌 GameSession.cs:8-22；保釋金/罰金/法院門檻散落於 GameSession.cs:157-158,399,407。無 Content 管線（見 §4）。
- **async void 無 try/finally**：RichmanMain.cs:119-159，動畫途中拋例外會讓 `_busy` 永久鎖死，整場遊戲凍結。
- **GameSession 663 行 god class**：規則、卡牌、法院、交易、破產、平手裁定全在一檔。

### 3.3 安全

負面發現：

- CI 下載 Godot 編輯器/匯出範本**無 checksum 驗證**（.github/workflows/ci-release.yml:65-66）。
- Release 無 Authenticode 簽署。
- GitHub Actions 只 pin major tag（非 commit SHA）。
- 無 NuGet lock file。
- 出貨版保留 `--test-pawn-move` 除錯開關（RichmanMain.cs:37-38）。

正面發現（重構中應保持）：

- Command 驗證骨架健康：WrongTurn / InvalidPhase / InsufficientFunds / 路線白名單皆由 Core 把關。
- `BuyPropertyCommand` 不帶 nodeId 是正確的零信任設計（client 只表達意圖，位置由 Core 認定）——已明文化為 ADR-0003 SEC-CMD-01。
- CI 權限最小化正確；無不安全反序列化；Core 零 NuGet 依賴。

### 3.4 Minor（擇要）

- 死碼：GameModel.cs:114-115,155-161,198-205 的 Detain 系列欄位；Board.cs:108 `VisualId`。
- `SeededRandom.NextInt` 有 modulo bias（Dice.cs:44）；介面名 `IGameRandom` 與規範用語 `IRng` 不一致。
- 監獄格 `EffectAmount=1` 被 `Imprison(3)` 忽略（Board.cs:419、GameSession.cs:392）——資料與程式各說各話。
- 停在自己地產可升級「任意」自有地產（GameSession.cs:282），是否刻意待產品確認。
- `TurnNumber` 每換人 +1 而非每輪 +1，`TurnLimit=240` 實為 60 輪，命名易誤讀（GameSession.cs:562,584）。

---

## 4. 內容與本地化現況

- **無 `Content/` 目錄、無 schema、無 loader/validator**。所有遊戲內容（地圖、卡牌、規則數值）硬編碼在 Core 內。
- **本地化兩套並存**：
  - Unity `RichmanLocalization.cs`：繁中/EN/日 三語完整 key-table，但 key-space 停在舊 Toy City 地圖——48 格新地圖、新 district、卡牌全無 key。
  - Godot：零本地化，約 60+ 處硬編碼繁中字串。
- 8 張機會/命運卡硬編碼於 GameSession.cs:8-22（英文）。

---

## 5. 美術資產現況

- `docs/ASSET_REQUESTS.md` 存在（為 Unity 撰寫，單位需換算成 Godot 公尺）。
- `docs/ART_PALETTE.md`、`docs/ASSET_LICENSES.md`、`docs/ASSET_REGISTRY.md` 皆**不存在**。
- `Assets/models/` 下 8 個角色 .glb（adventure / artist / builder / coach / engineer / photographer / richman / shopkeeper，由 `Tools/roles/*.py` 程序化建出）是**孤兒資產**：
  - 無任何場景/腳本引用；
  - 與 ASSET_REQUESTS 的「單一共用 body」規格互斥；
  - 依據 `character/*.jpg` 建模，而該批圖片**來源與授權未記錄** → 授權治理阻塞項，出貨前必須釐清或重製。
- Unity `ToyCityPalette.cs` 與 Godot `GodotBoardView.cs` 是兩套不一致色票。
- 無 `tile_models.json` 之類的格子→模型對照表機制。

---

## 6. 與目標架構差距總表

| 目標項目 | 現況 | 差距 |
| --- | --- | --- |
| Command→Event 模型 | Execute→CommandResult＋輪詢 State，無事件序列 | **高** |
| PendingDecision 統一型別 | PendingAction 枚舉＋散落欄位 | 中 |
| 回合狀態機（8 階段） | Phase 枚舉粗粒度，無 pass-through 掛鉤點 | 中高 |
| 圖結構地圖 | NextTileIds/RejoinsAtId/BFS 已存在，但強制單主環＋索引即位置 | 中（地基約六成） |
| IRng 強制注入 | 介面已有，但有 TickCount fallback 與 Godot 端注入錯誤 | 低（但有實際 bug，見 B1） |
| Effect 系統 | 無；Airport/監獄皆特例硬編碼 | **高** |
| Modifier 管線 | 無 | **高** |
| Content 管線（schema/loader/validator） | 無 Content/ 目錄 | **高** |
| Godot 本地化 | 零，60+ 處硬編碼繁中 | **高** |
| 多地圖支援 | 單一硬編碼工廠 | **高** |

---

## 7. 風險清單與 Phase 1 保護措施

### 7.1 最大風險

**規則在重構中悄悄改變而無任何測試失敗。** 成因疊加：

1. 無重播測試（B3）；
2. 既有斷言依賴顯示字串，而顯示字串本身是重構對象；
3. 版面限制與規則驗證纏結（Board.cs:277），拆分時容易誤刪規則；
4. internal 邊界失效（B2），展示層理論上可繞過規則改狀態。

### 7.2 保護措施（Phase 1 動工前必須完成）

| 編號 | 措施 | 說明 |
| --- | --- | --- |
| T1 | 黃金重播快照測試 | 固定多組 seed 跑完整對局，對全局狀態序列取雜湊存為快照；重構後雜湊必須不變 |
| T2 | 確定性測試 | 同 seed 兩次對局逐事件比對；覆蓋骰子、卡牌、法院所有亂數流 |
| T3 | 斷言去字串化 | 現有測試改斷言結構化狀態/事件，不再比對 LastEventMessage 文字 |
| T4 | 邊界特徵測試 | 把「現狀」（含已知問題）鎖進測試：破產地產等級未重設、單方交易、機場傳送不觸發目的格、TurnLimit 平手判定。重構若刻意修正，測試更新必須是顯式動作 |
| T5 | 地圖約束逐條測試 | BoardDefinition 每條驗證一條測試，並標注該條屬「規則」或「版面限制」，為 ADR-0002 拆分鋪路 |

結構性前置（同樣在動工前完成）：

- Godot 改 `ProjectReference` 引用獨立 Core 組件（終結 B2，ADR-0003 SEC-AUTH-02）；
- Unity 目錄凍結，不再新增功能（ADR-0001）；
- Core 提供 `GetLegalCommands`，HUD 與 Bot 改為共用（消除三處重複）。

---

## 8. 決策記錄（2026-10-20，使用者確認）

| 決策 | 內容 |
| --- | --- |
| 進入 Phase 1 | 已確認。順序：T1–T5 保護測試 → Command/Event → 圖結構 |
| 已知行為 bug 修復授權 | 全部授權於 Phase 1 修復並記錄為刻意行為變更：(1) 確定性破口（固定 seed 928371＋TickCount fallback）、(2) 交易無對方同意、(3) 破產收歸銀行地產未重設升級等級 |
| character/*.jpg 來源 | 使用者確認為本人 AI 生圖；已登記於 docs/ASSET_LICENSES.md，生成工具與商用條款待補 |
| ADR-0001/0002/0003 | 均為 Accepted |
