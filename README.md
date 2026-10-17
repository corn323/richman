# Richman

Richman 是一款原創的 3D Low-Poly Toy City 地產桌遊。玩家在一座放在桌上的微縮城市中擲骰、移動、購買地產、升級建築並向其他玩家收取租金，最後成為唯一仍在場上的玩家。

遊戲以繁體中文使用者為優先，正式遊戲 HUD 支援繁體中文、English 與日本語，語言可以在遊戲內直接切換並保存。

## 目前版本

目前完成 M0、M1、M1.5 Unity 參考版本，並開始 Godot 4.7.2 .NET 跨平台移植：

- 純 C# Richman.Core：棋盤、玩家、回合、骰子、金錢、地產、租金、升級、破產與勝利。
- Godot 旅遊棋盤有 41 格閉環主路線、3 條合流支線與 48 個總地格；巴黎蒙馬特保留為可購買地產，法院另設為正式遊戲格。
- 可編輯的 3D Gameplay Scene、Tile、Property、Pawn、Dice、Miniature City、Camera Rig 與 UI Toolkit HUD。
- 四人同機熱座遊玩，包含逐格移動、骰子動畫、所有權標記、建築等級變化、收租與勝利結果。
- 遊戲內語言切換：繁中、English、日本語。
- `GodotProject` 已有可編譯的 Godot C# 3D Board、雙骰子、Pawn、Camera 與 HUD vertical slice。

目前已加入純 C# `IBotStrategy`、`SimpleBotStrategy` 與 headless simulation，可在不啟動 Unity、Scene 或 Steam 的情況下執行四 Bot 對局。Steam、Networking、交易與事件卡仍屬後續工作。

## 開始遊玩

### Godot 版本（目前移植入口）

使用 Godot 4.7.2 `.NET / Mono` 開啟 `GodotProject`，按 Play 執行 `Scenes/Main.tscn`。開發建置需要 .NET 10 SDK 與 Godot Export Templates。

### Unity 參考版本

使用 Unity 6.3 LTS 6000.3.24f1 開啟專案後：

1. 開啟 Assets/Scenes/Gameplay.unity。
2. 按 Unity 的 Play。
3. 使用 HUD 的 擲骰子、購買、升級 與 結束回合。
4. 使用 HUD 下方的語言按鈕切換繁中、English 或日本語。

Windows 測試版輸出在：

Builds/Windows-M15/Richman.exe

完整遊玩流程與操作細節見 Introduction.md。

## 專案架構

Assets/Scripts/Core/             純 C# 規則核心，不依賴 Scene、Steam 或網路
Assets/Scripts/Presentation/     3D 視覺、動畫、鏡頭、HUD 與本地化
Assets/Scripts/Playtest/         本機遊戲流程與 Core/Presentation 組裝
Assets/Scripts/Editor/           Scene、Prefab 與 Windows Build 工具
Assets/Scenes/Gameplay.unity     正式 M1.5 遊戲場景
Assets/Prefabs/                  可替換的棋盤、角色、地產、骰子與環境 Prefab
Assets/UI/                       UXML、USS 與 Panel Settings
docs/                            架構、規則、測試與資產需求文件

Richman.Core 是 authoritative game state 的唯一規則來源。Presentation 只把 TileIndex、地產狀態與指令結果呈現成模型、動畫與 UI；不以 Transform 位置取代遊戲狀態。

## 測試與建置

Core 測試位於 `Assets/Scripts/Core/Tests/GameSessionTests.cs`，可直接使用獨立 NUnit 專案執行：

```text
dotnet test Assets/Scripts/Core/Tests/Richman.Core.Tests.csproj --configuration Release
```

GitHub Actions 工作流程 `.github/workflows/ci-release.yml` 會在對 master 的 Pull Request、推送 master 及 `v*` 標籤時執行 Core NUnit 測試、Core/Simulation/Godot Release 建置和模擬自測。master 推送與標籤推送也會在 Windows runner 匯出、冒煙測試並上傳 `Richman-Windows-x64.zip`。

推送到 master 後，可在 GitHub Actions 的該次工作執行頁下載 `Richman-Windows-x64` artifact。建立並推送 `v*` 標籤（例如 `v1.0.0`）會在所有驗證與打包成功後建立 GitHub Release，附上相同 ZIP 與自動產生的 Release Notes。PR 只執行驗證，不需要 Release 寫入權限。

Windows 測試版包含 `PLAYTEST_README.txt` 和 `BUILD_INFO.txt`。Godot 官方 .NET 匯出模板會提供已編譯遊戲所需的 .NET runtime 元件，玩家不需要安裝 Godot Editor 或 .NET SDK。

在 Unity Editor 選單執行：

- Richman > Generate M1.5 Gameplay Scene
- Richman > Build Windows Playtest

Windows Build Support (IL2CPP) 可用於建立 Windows x64 測試版。Library、Temp、Logs 與 Build cache 不應提交到 Git。

執行四 Bot 模擬（不需要 Unity）：

```text
dotnet run --project Tools/Simulation/Richman.Simulation.csproj -- 1000
```

模擬器會輸出 `SimulationResults.csv`，並在所有對局完成時以 exit code 0 結束。

## 美術方向

M1.5 使用風格一致的 Procedural Low-Poly Placeholder，方便未來替換正式模型。資產規格、三角面數、貼圖尺寸、Pivot、生成提示與 Unity 匯入要求見 docs/ASSET_REQUESTS.md。

## License

本專案的程式碼與資產授權方式尚未定稿。加入外部資產前，請確認其授權允許商業 Steam 遊戲使用。
