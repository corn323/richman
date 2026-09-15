# M0/M1 架構

## 邊界

M0/M1 只處理可由純 C# 執行的遊戲規則。`Richman.Core.asmdef` 設定 `noEngineReferences`，因此核心不引用 `UnityEngine`，也不需要 Scene、Prefab、Steamworks 或 Transport。

```text
GameCommand
    |
    v
GameSession  ---->  GameState
    |                  |
    +--> BoardDefinition / PropertyState / PlayerState
    +--> IDiceRoller
```

Presentation、Steam 與 Networking 只能在後續階段接到 `GameSession` 的命令入口，不應把平台 API 放入核心規則。

## 核心責任

- `BoardDefinition`：以資料描述棋盤格與地產數值；目前是 36 格原創 Prototype Board。
- `GameModeConfig`：描述玩家數量與經濟常數；M1 的正式設定是固定 4 人。
- `GameState`：保存玩家、地產、回合、骰子結果、待處理動作與勝利結果。
- `GameSession`：驗證命令、移動玩家、處理購買／升級／收租／稅金／獎金、破產與勝利。
- `IDiceRoller`：隔離骰子來源；正式核心使用可重現的 `SeededDiceRoller`，測試使用 scripted roller。
- `GameCommand`：M1 只提供 Roll、Buy、Upgrade、EndTurn；不包含 Steam 或網路欄位。

## M1 的簡化規則

- 玩家由 `TileIndex` 表示位置，未使用 Transform。
- 跨過起點時獲得 `StartBonus`。
- 未擁有地產可以購買；自己的地產可以選擇升級；降落在他人地產自動支付目前租金。
- 現金不足支付租金時，玩家剩餘現金交給地產持有人，所有地產移交給該持有人。
- 現金不足支付稅金時，玩家破產，地產回到銀行（OwnerId 為空）。
- 最後一名 `Active` 玩家成為 `Winner`，Session 進入 `Finished`。
- 買不到或不想買、不能或不想升級時，可以用 `EndTurnCommand` 結束回合。

這些是 M1 的最小可測規則，不是完整產品規則；事件、交易、抵押、斷線同步與 Host-authority 留在後續 milestone。

## M1.5 Presentation

M1.5 將 Unity 呈現與 `Richman.Core` 分開。`GameSession` 仍是唯一規則入口；Scene、Prefab、UI Toolkit、Camera 與動畫只讀取 `GameState` 並送出 `GameCommand`。

```text
Gameplay.unity
  GameController (PlaytestGame)
      -> GameSession (Richman.Core)
      -> BoardView -> BoardTileView -> PropertyView / BuildingSlot
      -> PawnView / DiceView
      -> GameplayHUD (UI Toolkit)
      -> CameraRig
```

`Assets/Prefabs` 內的 BoardTile、Property、Pawn、Dice、MiniatureCity、Ground、CameraRig 與 GameplayHUD 是可替換的 asset boundary。Scene 直接保存 36 個 Tile instance、Property instance、4 個 Pawn instance 與 Dice instance；執行時只綁定它們，不以 `PlaytestGame.cs` 動態生成整張地圖。

Pawn 的 authoritative 位置仍是 Core 的 `PlayerState.CurrentTileIndex`。`BoardView` 只將 TileIndex 轉成 Scene 中 Tile instance 的位置並播放逐格動畫。Dice 的結果來自 Core，`DiceView` 只負責旋轉、彈跳與結果呈現。

正式 Gameplay UI 使用 UI Toolkit，不使用 `OnGUI()`。`GameplayHUD` 的按鈕依 `GameState.Phase`、`PendingAction` 與 `Outcome` 更新 enabled 狀態；Property Panel 只呈現目前 Tile 的資料。

## 目錄

```text
Assets/Scripts/Core/Runtime/  純 C# 核心與 Richman.Core.asmdef
Assets/Scripts/Core/Tests/    Unity EditMode/NUnit tests
Assets/Scripts/Presentation/Runtime/  Scene-bound 3D views、Camera 與 UI Toolkit HUD
Assets/Scripts/Playtest/      M1.5 controller；只組合 Core 與 Presentation
Assets/Prefabs/               可替換的棋盤、地產、角色、骰子、城市與 UI Prefab
Packages/                     Unity package manifest
ProjectSettings/              Unity project metadata
docs/                         開發文件
```
