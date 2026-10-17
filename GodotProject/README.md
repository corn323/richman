# Richman Godot Project

這是 Unity 專案的平行 Godot .NET 移植入口。Unity 專案暫時保留作為可回復參考；Godot 4.7.2 .NET Portable 已可開啟此專案。

## 開啟

使用 Godot 4 的 `.NET` 版本匯入此資料夾。需要另外安裝 .NET SDK；Godot 會使用 `project.godot` 與 `Scenes/Main.tscn` 作為入口。

目前 `Richman.Core` 位於上層 Unity 專案，後續會在 Godot C# project file 內以 linked source 共用，不複製或重寫規則核心。

## 移植邊界

- `Richman.Core`：純 C#、權威規則狀態與測試，保持不依賴 Godot。
- `GodotProject/Scripts`：Godot Node3D、UI、Camera 與動畫呈現。
- 未來 Steam / Networking：獨立於 Core 與 Godot 場景組裝。

## 3D 群島棋盤

- Blender 原始場景：`Assets/archipelago_map.blend`（開啟後選 `ArchipelagoMap` scene）。
- Godot glTF 2.0 模型：`Assets/archipelago_map.glb`。
- 格子、Y-up 座標、閉環與支線：`Data/archipelago_tiles.json`。
- Godot 由 `GodotBoardView` 呼叫 `GltfDocument` 載入 GLB，格子中心由 JSON 的 `x/y/z` 讀取；位置不依賴 mesh 順序。
- 開啟此資料夾的 `project.godot`，使用 Godot 4.7 .NET 版。可沿用 `Richman.Godot.csproj` 建置。
- 棋盤主路線為 41 格閉環、總計 48 格，含三條可返回主路線的支線。路線分支會在 HUD 顯示選項，Core 的實際移動路徑驅動棋子動畫。
- 巴黎蒙馬特維持可購買地產；巴黎國際法院是新增的獨立主路線地格。格子資料由 Core 匯出，Blender 生成器與 Godot 都讀同一份資料。

## Windows Playtest 匯出

- `export_presets.cfg` 提供 `Windows Desktop` x86_64 預設，CLI 匯出命令為 `godot --headless --path GodotProject --export-release "Windows Desktop" build/Richman-Windows-x64/Richman.exe`。
- `.github/workflows/ci-release.yml` 在 master PR 執行測試與建置，在 master push 和 `v*` tag push 時額外匯出 Windows ZIP 並執行 headless 啟動冒煙測試。
- master push 的 ZIP 可從 GitHub Actions artifact 下載；推送 `v*` tag 會在工作流程成功後建立附 ZIP 和自動 Release Notes 的 GitHub Release。
- 匯出的 Windows 遊戲包含官方 Godot .NET runtime 元件，玩家不需要 Godot Editor、.NET SDK 或另外安裝 .NET Runtime。
