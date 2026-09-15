# Godot 移植計畫

## 已決定

Richman 改以 Godot 4 .NET 作為跨平台 Presentation 與最終 Windows/Linux/macOS Build 入口。現有 `Richman.Core` 不重寫，仍是唯一 Gameplay Rules authority；Unity M1.5 專案先保留作為參考與可回復版本。

## 目標結構

```text
Richman.Core (純 C#)
        |
        +-- GodotProject/Scripts (Node3D presentation)
        +-- Tools/Simulation (headless bot tests)
        +-- future Richman.Networking / Richman.Steam
```

## 移植順序

1. Godot .NET project bootstrap、3D board 與 camera。
2. Tile / Property / Pawn / Dice procedural placeholder。
3. UGUI equivalent：Godot Control HUD、action commands 與 responsive layout。
4. Core command sequence、逐格 Pawn movement、dice presentation。
5. Windows build；再驗證 Linux 與 macOS export configuration。
6. 事件、拘留、Bot、Networking 與 Steam 分階段整合。

## 安裝需求

- Godot 4 `.NET` editor，不是 Standard editor。
- 與 Godot 版本相容的 .NET SDK；Godot 官方文件目前要求桌面 C# 專案使用 .NET SDK。
- Godot export templates。

Godot 4.7.2 .NET Portable 已完成啟動；目前 C# project build 與 headless scene smoke test 已通過。Windows/Linux/macOS 發行檔仍需安裝並確認對應的 Godot export templates 後驗證。
