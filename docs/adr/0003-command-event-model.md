# ADR-0003：Command→Event 模型與 Core 契約

- 狀態：Accepted
- 日期：2026-10-20

## 背景

現行 API 為 `GameSession.Execute(GameCommand) → CommandResult`（單一結果物件）＋外部輪詢 `session.State`。Phase 0 審查（docs/AUDIT.md）確認其直接後果：

- 展示層只能從狀態差異「猜」發生了什麼（RichmanMain.cs:125,139,144-153：用 `DiceResult==null` 猜住院、用路徑尾格猜傳送）。
- Core 產生約 20 處顯示字串 `LastEventMessage`，中英混雜（GameSession.cs:222,262,266,355,382,428,643 等），既污染 Core 又綁死測試斷言（Blocker B3）。
- 確定性破口：`random ?? new SeededRandom(Environment.TickCount)`（GameSession.cs:34）＋ Godot 端注入錯誤（RichmanMain.cs:26,56）。
- 組件邊界失效：`<Compile Include>` 吸收 Core 原始碼，internal 對展示層全開（Richman.Godot.csproj:8-10；Blocker B2）。
- 「現在能做什麼」三處重複（GodotGameplayHud.cs:155-181、Unity GameplayHUD.cs:153-165、Bots.cs:19-38）。
- 交易無對方同意（GameSession.cs:321-357），連線化後等同資產竊取。

## 決策

1. **Execute 回傳事件序列**：`GameSession.Execute(Command)` 改回傳 `IReadOnlyList<GameEvent>`——有序、可序列化、可重播。過渡期 `CommandResult` 保留為相容層；其最終去留於 Phase 4 完成後再議（**open question**）。
2. **Core 不再產生顯示字串**：事件攜帶結構化資料與本地化 key，顯示文字由 Presentation 層組合。`LastEventMessage` 全數退役。
3. **統一 PendingDecision**：單一型別（`kind` + `options` + `context`），同一時間僅一個未決，僅接受對應的 `ResolveDecision` 命令；取代現行 PendingAction 枚舉＋散落欄位。
4. **回合狀態機擴為 8 階段**：
   `TurnStart → PreRoll → Roll → Move（逐格、含 pass-through）→ Land → ResolveTile → PostAction → TurnEnd`。
   各階段邊界為 Effect/Modifier 的掛鉤點。
5. **IRng 強制注入**：移除 `Environment.TickCount` fallback（GameSession.cs:34）。seed 於 composition root 決定一次並記錄；骰子與卡牌等各亂數流的衍生方式須文件化。
6. **Core 升格獨立組件**：下游一律 `ProjectReference`，禁止 `<Compile Include>` 吸收 Core 原始碼，並加 CI 斷言防回歸；僅 Tests 透過 `InternalsVisibleTo` 取得 internal 存取。
7. **Core 提供 `GetLegalCommands(playerId)`**：HUD 與 Bot 共用同一合法命令來源，消除三處重複。
8. **交易改兩階段**：`ProposeTrade → RespondTrade`（僅 `TargetPlayerId` 可回應）。proposal 綁定 `turnNumber` 與資產快照，相關狀態一有變動即失效。

## 證據

- 事件猜測：RichmanMain.cs:125,139,144-153。
- 顯示字串入 Core：GameSession.cs:222,262,266,355,382,428,643。
- 確定性破口：GameSession.cs:34、RichmanMain.cs:26,56。
- 邊界失效與 internal 外洩：Richman.Godot.csproj:8-10、GodotBoardView.cs:159。
- 合法命令三處重複：GodotGameplayHud.cs:155-181、GameplayHUD.cs:153-165、Bots.cs:19-38。
- 單方交易：GameSession.cs:321-357（HUD 側確認在 GodotGameplayHud.cs:287，Core 無防護）。

## 後果

- 現有測試的字串斷言全面失效——必須先完成 docs/AUDIT.md §7.2 的 T1–T3（黃金重播快照、確定性測試、斷言去字串化）才能動工。
- 展示層改為消費事件序列，刪除狀態差異猜測邏輯；動畫驅動來源由「比對 State」改為「播放 Events」。
- Godot/模擬器改 `ProjectReference` 後，所有誤用 internal 之處（如 GodotBoardView.cs:159）會在編譯期浮現，需改用公開 API。
- Bot 介面同步改為以 `GetLegalCommands` 為輸入。

## 附錄：連線預留安全需求（列入契約）

以下需求自本 ADR 起為 Core 契約的一部分，Phase 1+ 實作與 review 皆以此為驗收項：

### 命令層

- **SEC-CMD-01** Command 只承載**意圖**，不承載結果。維持 `BuyPropertyCommand` 不帶 nodeId 的設計（位置由 Core 依權威狀態認定）並明文化為通則。
- **SEC-CMD-02** 涉及多方的命令（交易等）一律雙階段同意，Core 強制執行，不得只靠 UI 確認。
- **SEC-CMD-03** proposal 生命週期：`proposalId` + `turnNumber` + 資產快照；相關狀態變動即失效，過期 proposal 回絕不執行。
- **SEC-CMD-04** 每回合操作配額（升級次數、交易次數等）寫入 `GameModeConfig`，由 Core 計數強制。
- **SEC-CMD-05** `commandId` 冪等 ＋ `expectedStateVersion` 樂觀併發檢查，重送與亂序命令可安全拒絕。

### 權威層

- **SEC-AUTH-01** 亂數歸 Core 所有：client 不得提交骰值或抽牌結果，對外只暴露 seed（供重播/驗證）。
- **SEC-AUTH-02** Core 為獨立組件，internal 狀態 mutator 不對展示層開放（終結 Blocker B2）。
- **SEC-AUTH-03** Event 預留每玩家視野過濾：事件區分 Public/Private，為隱藏資訊（手牌等）與連線廣播預留。
- **SEC-AUTH-04** dispatcher 統一 try/catch，Core 對外不拋例外；內部錯誤轉為 `CommandErrorCode.InternalError` 事件/結果，避免一條壞命令讓對局或伺服器崩潰。

### I/O 層

- **SEC-IO-01** 反序列化規約：僅用 `System.Text.Json` 且顯式 options；`MaxDepth=32`；檔案大小上限；enum 一律字串白名單 `TryParse` + `IsDefined`；外部地圖資料必經 `BoardDefinition` 驗證後才可進入規則；禁用 `BinaryFormatter` 及一切多型反序列化。
- **SEC-IO-02** 路徑一律 `GetFullPath` 正規化並限制於 `user://` 範圍內；存檔格式含版本號與 checksum，載入時驗證。
