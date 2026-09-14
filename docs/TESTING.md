# M0/M1 測試

## Unity EditMode

開啟 Unity 6.3 LTS 後，在 `Window > General > Test Runner` 選擇 EditMode，執行 `Richman.Core.Tests`。測試不建立 Scene、不啟動 Steam，也不需要圖形或網路。

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

## 尚未驗證的範圍

M2 之後的 Bot、Headless 1000 局模擬、Unity Build、3D、Steam、Networking 與真實多人流程尚未建立，因此不在 M0/M1 測試結果內。
