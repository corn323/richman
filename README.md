# Richman

Richman 是一款原創的 Low-Poly Toy City 多人地產桌遊。目前完成 M0/M1 核心，並加入只供本機驗證的四人熱座 Playtest 外殼。

## 開發狀態

- M0：Unity 6.3 LTS 專案骨架、Core asmdef、測試原始碼與基本文件已建立。
- M1：4 人 Classic Bankruptcy 的棋盤、玩家、回合、骰子、金錢、購買、升級、收租、破產與勝利規則已建立。
- Local Playtest：`Assets/Scenes/Playtest.unity` 提供 3D 低多邊形棋盤、四個人物棋子與四人熱座操作，可用來驗證完整回合流程。
- 尚未開始：Bot、Steam、Networking、交易、事件卡與大量模擬（M2+）。

## 開始方式

1. 使用 Unity 6.3 LTS 開啟本資料夾。
2. 若安裝 Unity Test Framework，可在 Test Runner 執行 `Richman.Core.Tests` 的 EditMode tests。
3. 開啟 `Assets/Scenes/Playtest.unity`，或執行 Windows Playtest build 進行四人熱座測試。
4. 也可以使用 .NET SDK 執行 `dotnet build Richman.Core.Standalone.csproj`，確認純 C# 核心不依賴 UnityEngine。

Playtest 外殼只負責輸入與 3D 呈現，M1 的遊戲流程仍透過 `GameSession.Execute(GameCommand)` 驅動；Steam、Networking 與 Bot 仍未加入。
