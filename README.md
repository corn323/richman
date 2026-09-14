# Richman

Richman 是一款原創的 Low-Poly Toy City 多人地產桌遊。目前只建立 M0/M1：Unity 專案基礎與不依賴 Unity Scene、Steam 或網路的純 C# 遊戲核心。

## 開發狀態

- M0：Unity 6.3 LTS 專案骨架、Core asmdef、EditMode 測試與基本文件已建立。
- M1：4 人 Classic Bankruptcy 的棋盤、玩家、回合、骰子、金錢、購買、升級、收租、破產與勝利規則已建立。
- 尚未開始：Bot、3D、Scene、UI、Steam、Networking、交易、事件卡與大量模擬（M2+）。

## 開始方式

1. 使用 Unity 6.3 LTS 開啟本資料夾。
2. 在 Test Runner 執行 `Richman.Core.Tests` 的 EditMode tests。
3. 也可以使用 .NET SDK 執行 `dotnet build Richman.Core.Standalone.csproj`，確認純 C# 核心不依賴 UnityEngine。

目前沒有可玩的 Scene；這是刻意保留給 M3 的範圍。M1 的遊戲流程透過 `GameSession.Execute(GameCommand)` 驅動。
