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

先在 Unity Hub 登入並啟用 Personal license，接著在 Unity Editor 執行 `Richman > Generate M1.5 Gameplay Scene` 生成 `Assets/Scenes/Gameplay.unity`；再執行 `Richman > Build Windows Playtest` 產生 `Builds/Windows/Richman.exe`。這是 M1.5 四人熱座 3D Vertical Slice：Scene 直接包含 36 格棋盤、Miniature City、Pawn、Dice、Lighting、Camera Rig 與 UI Toolkit HUD。

驗收流程：

1. 啟動後第一眼應看到有高度差的低多邊形城市棋盤，而非文字棋盤。
2. 按 `ROLL DICE`，先看 3D 骰子動畫，再看目前玩家棋子逐格移動。
3. 停在空地產時按 `BUY`，確認 Tile 顏色、Ownership Indicator 與 Owned Properties 更新。
4. 再次停在自己的地產時按 `UPGRADE`，確認 Empty Lot、Small、Medium、Large Building 會依 Lv0～Lv3 切換。
5. 讓其他玩家停到該地產，確認 Rent 扣款；持續測試資金不足時的破產與 Winner。
6. 按住滑鼠右鍵拖曳旋轉，使用滑鼠滾輪縮放；回合行動期間 Camera 會聚焦 Pawn、Dice 與 Property。

Unity Editor 中可直接編輯 `Gameplay.unity` 的 `BoardRoot`、36 個 Tile instance、Property instance、4 個 Pawn、Dice、`CameraRig`、`Directional Light` 與 `GameplayHUD` UI Toolkit Document。正式美術尚未加入時，使用的是可被替換的 low-poly procedural placeholder Prefab。

## 尚未驗證的範圍

Bot、Headless 1000 局模擬、Steam、Networking 與真實線上多人流程尚未建立。Windows 測試請整個複製 `Builds/Windows` 資料夾，不要只複製單一 `.exe`；M1.5 的 Unity 編譯、啟動與主要流程仍需在使用者本機實際驗收。
