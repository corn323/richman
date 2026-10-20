# ADR-0001：Godot 為唯一前進展示層，Unity 凍結為參考實作

- 狀態：Accepted
- 日期：2026-10-20

## 背景

專案目前同時存在兩套展示層：

- Unity 6.3（`Assets/Scripts/Presentation` 等）：擁有最完整的本地化（`RichmanLocalization.cs`，繁中/EN/日 key-table），但棋盤佈局停留在舊 36/40 格方形外圈，與現行 48 格 Core 已不相容。
- Godot 4.7.2 .NET（`GodotProject/`）：棋盤與 UI 對應現行 48 格 Core，具交易 UI 與選角畫面，且是 CI 唯一覆蓋的展示層；但零本地化，約 60+ 處硬編碼繁中字串。

雙展示層同時維護的成本（兩套色票、兩套 HUD 邏輯、兩套佈局）已在 Phase 0 審查中確認為重複與分歧的主要來源（見 docs/AUDIT.md §3.2「三處重複」、§5「兩套色票」）。

## 決策

1. **Godot 4.7.2 .NET 為唯一前進展示層**。所有新功能、新 UI、本地化補齊皆只做在 Godot。
2. **Unity 6.3 凍結為參考實作**：不再新增功能，僅保留作為本地化 key-table 與既有行為的參照。
3. Godot 達到功能對等——**必要條件是補回三語本地化**——之後，Unity 目錄移往 `legacy/` 或獨立 branch。
4. **Core 與 Content 獨立於兩引擎**：Unity 與 Godot 皆僅為 adapter，不得持有規則或內容。

## 證據

1. `docs/GODOT_MIGRATION.md` 已有既存書面決策，本 ADR 將其正式化。
2. Unity 棋盤佈局與 48 格 Core 不相容，執行會 `IndexOutOfRange`（BoardView.cs:116、BoardLayout.cs；`Gameplay.unity` 的 `tileViews` 僅 36 個）。
3. CI 僅覆蓋 Godot 匯出與 smoke test；Unity 無任何 pipeline（.github/workflows/ci-release.yml）。
4. Godot 已有交易 UI 與選角畫面，功能領先 Unity。

## 後果

- Godot 必須補回本地化才算達到對等：移植 Unity `RichmanLocalization.cs` 的 key-table 模式（三語），並為 48 格新地圖、新 district、卡牌補齊缺少的 key（docs/AUDIT.md §4）。
- `docs/TESTING.md` 等以 Unity 為敘述主體的文件需要更新——列入後續任務，**不在本 ADR 內執行**。
- Unity 端已知的不相容（IndexOutOfRange）不予修復；凍結即為處置。
- 風險：在 Unity 移往 legacy 前，貢獻者可能誤把功能做進 Unity 目錄；以 code review 與本 ADR 作為擋板。
