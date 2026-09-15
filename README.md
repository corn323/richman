# Richman

Richman 是一款原創的 3D Low-Poly Toy City 地產桌遊。玩家在一座放在桌上的微縮城市中擲骰、移動、購買地產、升級建築並向其他玩家收取租金，最後成為唯一仍在場上的玩家。

遊戲以繁體中文使用者為優先，正式遊戲 HUD 支援繁體中文、English 與日本語，語言可以在遊戲內直接切換並保存。

## 目前版本

目前完成 M0、M1 與 M1.5 Playable 3D Vertical Slice：

- 純 C# Richman.Core：棋盤、玩家、回合、骰子、金錢、地產、租金、升級、破產與勝利。
- 36 格原創棋盤，資料由 PrototypeBoardFactory 建立，規則不依賴 Scene。
- 可編輯的 3D Gameplay Scene、Tile、Property、Pawn、Dice、Miniature City、Camera Rig 與 UI Toolkit HUD。
- 四人同機熱座遊玩，包含逐格移動、骰子動畫、所有權標記、建築等級變化、收租與勝利結果。
- 遊戲內語言切換：繁中、English、日本語。

尚未開始的內容：Bot、Steam、Networking、交易、事件卡與大量模擬。這些屬於後續 Milestone。

## 開始遊玩

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

Core 測試位於 Assets/Scripts/Core/Tests，可在 Unity Test Runner 執行 EditMode tests。

在 Unity Editor 選單執行：

- Richman > Generate M1.5 Gameplay Scene
- Richman > Build Windows Playtest

Windows Build Support (IL2CPP) 可用於建立 Windows x64 測試版。Library、Temp、Logs 與 Build cache 不應提交到 Git。

## 美術方向

M1.5 使用風格一致的 Procedural Low-Poly Placeholder，方便未來替換正式模型。資產規格、三角面數、貼圖尺寸、Pivot、生成提示與 Unity 匯入要求見 docs/ASSET_REQUESTS.md。

## License

本專案的程式碼與資產授權方式尚未定稿。加入外部資產前，請確認其授權允許商業 Steam 遊戲使用。
