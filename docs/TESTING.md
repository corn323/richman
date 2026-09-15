# M0/M1 測試

## Unity EditMode（可選）

若專案已安裝 Unity Test Framework，先在 Player Settings 的 Scripting Define Symbols 加入 `RICHMAN_ENABLE_UNITY_TESTS`，再開啟 Unity 6.3 LTS，在 `Window > General > Test Runner` 選擇 EditMode，執行 `Richman.Core.Tests`。測試不建立 Scene、不啟動 Steam，也不需要圖形或網路；未啟用此符號時不會阻塞一般遊戲建置。

目前測試涵蓋：

- 4 人初始化與資料驅動棋盤。
- 骰子移動與購買地產。
- 升級地產與提高後的租金。
- 資金不足時的租金支付、現金歸屬、地產移交與破產。
- 稅金破產與最後玩家勝利。
- 錯誤回合與錯誤命令階段拒絕。

## 純 C# 編譯檢查

在專案根目錄執行：

```text
dotnet build Richman.Core.Standalone.csproj
```

此專案只編譯 `Assets/Scripts/Core/Runtime`，用來確認核心沒有 Unity 依賴。Unity Test Framework 的 NUnit 執行仍以 Unity Test Runner 為準。

## Local Playtest

開啟 `Assets/Scenes/Playtest.unity`，或執行 `Builds/Windows/Richman.exe`。這是四人熱座 3D 驗證版：畫面會建立低多邊形棋盤、彩色人物棋子與升級建築；玩家依序操作 Roll Dice、Buy Property、Upgrade、End Turn，可看到棋子移動、資金、位置、產權與勝者。

## 尚未驗證的範圍

 Bot、Headless 1000 局模擬、Steam、Networking 與真實線上多人流程尚未建立。Windows 測試請整個複製 `Builds/Windows` 資料夾，不要只複製單一 `.exe`；本機 Player 已完成啟動煙霧測試。
