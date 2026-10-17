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
- 在第 17 格可按 `B`，棋子會走過瞭望支線並接回第 18 格；主要路線維持原有 Board 索引閉環。
