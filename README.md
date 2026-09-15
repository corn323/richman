# Richman

Richman 是一款原創的 Low-Poly Toy City 多人地產桌遊。目前完成 M0/M1 核心與 M1.5 可生成的 3D Vertical Slice 架構。

## 開發狀態

- M0：Unity 6.3 LTS 專案骨架、Core asmdef、測試原始碼與基本文件已建立。
- M1：4 人 Classic Bankruptcy 的棋盤、玩家、回合、骰子、金錢、購買、升級、收租、破產與勝利規則已建立。
- M1.5：`Assets/Scripts/Editor/BuildM15Presentation.cs` 會生成正式 Gameplay Scene，包含可在 Unity Editor 編輯的 Board Root、36 個 Tile Prefab instances、Property Prefab、Pawn Prefab、3D Dice、Miniature City、Camera Rig、Lighting 與 UI Toolkit HUD。
- Local Playtest：場景生成後的 Windows Build 可進行四人熱座，支援 Roll Dice、逐格棋子動畫、購買 Ownership、Upgrade 建築變化、收租、破產與 Winner。
- 尚未開始：Bot、Steam、Networking、交易、事件卡與大量模擬（M2+）。

## 開始方式

1. 使用 Unity 6.3 LTS 開啟本資料夾。
2. Unity Hub 需登入並啟用 Personal license；Windows build 只需要 Windows Build Support (IL2CPP)，UI Toolkit 不需要額外 package。
3. 先執行 `Richman > Generate M1.5 Gameplay Scene`，再執行 `Richman > Build Windows Playtest` 進行四人熱座測試。
4. 若安裝 Unity Test Framework，可在 Test Runner 執行 `Richman.Core.Tests` 的 EditMode tests。
5. 也可以使用 .NET SDK 執行 `dotnet build Richman.Core.Standalone.csproj`，確認純 C# 核心不依賴 UnityEngine。

Presentation 層只負責輸入、Prefab 視覺狀態與動畫，M1 的遊戲流程仍透過 `GameSession.Execute(GameCommand)` 驅動；Steam、Networking 與 Bot 仍未加入。正式美術資產的規格與生成提示見 `docs/ASSET_REQUESTS.md`。

# AI 美術與資產生成規範

開發者不具備專業 3D 建模與美術能力。

因此 Codex 不得假設開發者可以自行：

- 建模
- UV
- Retopology
- 製作 PBR 材質
- 繪製 Concept Art
- 製作 UI Icon
- Rigging
- Animation Cleanup

Codex 必須主動協助建立可以由 AI 與少量人工完成的美術 Pipeline。

---

## 核心原則

Placeholder 可以用於早期驗證。

但是：

> Placeholder 不是最終 Visual Direction。

不得因為開發者不會建模，而讓整款遊戲永久保持：

- Cube
- Capsule
- Sphere
- Debug Text
- OnGUI
- 純色方格

的工程測試風格。

當核心玩法已經能運作後，必須開始建立具有一致 Art Direction 的真正 Playable Vertical Slice。

---

# Asset Request System

任何功能如果需要目前 Repository 不存在的：

- 3D Model
- Texture
- Material
- UI Icon
- Illustration
- Character
- Animation
- Audio
- VFX

Codex 不得只寫：

「需要美術資源。」

必須建立完整 Asset Request。

格式：

## Asset Request

Asset ID:

Asset Type:

用途:

重要程度:

建議生成工具:

Art Style:

尺寸:

World Scale:

Triangle Budget:

Texture Resolution:

Material Requirements:

Animation Requirements:

Collider Requirements:

LOD Requirements:

Unity Import Format:

Pivot:

Generation Prompt:

Negative Prompt:

Post Processing:

Unity Import Instructions:

---

# Preferred Art Direction

專案主要視覺方向：

Stylized Low-Poly Miniature Toy City。

關鍵字：

- miniature
- toy city
- stylized
- low-poly
- chunky proportions
- rounded geometry
- clean silhouette
- colorful
- readable
- friendly
- casual board game
- soft lighting
- hand-painted / simplified PBR

避免：

- Photorealistic
- Extremely Detailed
- AAA realism
- High poly sculpt
- 8K texture
- gritty realism
- horror style

所有資產必須盡可能保持一致比例與視覺語言。

---

# Preferred Tool Pipeline

優先順序：

## 1. Unity Native / Procedural

適合：

- Board
- Road
- Tile
- Tile Highlight
- Movement Path
- Property Slot
- Selection Indicator
- Simple VFX

Gameplay-critical geometry 優先由 Unity 建立，而不是由 AI 生整塊 Mesh。

---

## 2. Unity AI

適合生成：

- 簡單 Props
- Environment Decoration
- Material
- Placeholder 3D Asset
- Cubemap
- Prototype Asset

如果目前 Unity 版本支援官方 Unity AI：

優先使用其 Project-aware Assistant / Agent / Generators。

---

## 3. Meshy

主要 AI 3D Generator。

適合：

- Building
- Vehicle Prop
- Decoration
- Character Prototype
- Landmark
- Furniture
- Interactive Props

優先：

Concept Image
→ Image-to-3D

而不是完全依賴：

Text-to-3D。

重要資產最好先建立一致 Concept。

---

# 4. Blender

Blender 是 AI 生成模型的 Cleanup 工具。

可使用：

Blender MCP / AI Agent

協助：

- Apply Transform
- Correct Scale
- Set Pivot
- Retopology
- Decimate
- UV Cleanup
- Material Cleanup
- Remove Hidden Geometry
- Merge Mesh
- Separate Mesh
- Generate LOD
- Collider Mesh
- Rig Cleanup
- Export FBX/GLB

不得假設 AI 生成的模型可以不經檢查直接放入正式遊戲。

---

# AI Asset Quality Gate

任何 AI Model 加入正式 Asset Library 前至少檢查：

- Scale 是否正確。
- Pivot 是否正確。
- 法線是否正常。
- 是否存在破面。
- 是否存在巨大不可見 Mesh。
- 是否有異常 Vertex 數。
- 是否有不需要的 Materials。
- Texture 是否過大。
- 是否存在文字亂碼。
- 是否出現多餘 Logo。
- 是否可能侵犯第三方 IP。
- Collider 是否合理。
- Static Asset 是否真的標示 Static。
- 是否適合建立 LOD。

---

# Target Budgets

這是一款低硬體需求桌遊。

一般小型 Prop：

300–2,000 triangles。

普通建築：

800–4,000 triangles。

重要 Landmark：

最高約 8,000 triangles。

一般 Pawn：

約 3,000–10,000 triangles。

這些不是絕對限制。

如果視覺品質不需要更多 Geometry：

不要增加。

一般 Texture：

512 或 1024。

重要角色或 Landmark：

最多通常 2048。

不要無理由使用：

4K / 8K Texture。

---

# Materials

盡量共用 Material。

適合時建立：

Shared Material

Material Variant

Texture Atlas。

不要讓每一棟簡單房子都有五個獨立 Material。

目標是：

低 Draw Call

低 VRAM

容易替換。

---

# Character Generation

MVP 不需要複雜寫實角色。

角色方向：

Stylized Toy / Chibi。

優先使用相同 Body Base。

角色差異透過：

- Color
- Hat
- Glasses
- Hair
- Accessory
- Face

建立。

避免每名玩家使用完全不同 Skeleton。

如果角色需要動畫：

優先建立統一 Humanoid Rig。

---

# Concept First

對重要資產：

不要直接盲目 Text-to-3D。

流程：

Art Brief

→ Concept Image

→ 開發者確認

→ Image-to-3D

→ Blender Cleanup

→ Unity Import

→ Prefab

→ Performance Check。

Codex 必須在需要 Concept 時，直接產生 Concept Image Prompt。

---

# Generated Asset Registry

建立：

docs/ASSET_REGISTRY.md

紀錄每個正式 Asset：

Asset ID

來源工具

生成日期

Prompt

Source Image

AI Service

License

Commercial Usage Status

Original File

Processed File

Triangle Count

Texture Size。

任何 Steam Release 中使用的 AI Asset 都應能追蹤來源。

---

# Commercial Release Safety

本遊戲預計正式上架 Steam。

所以禁止使用：

- 權利不明資產
- 網路隨意下載圖片
- 未知授權模型
- 明確仿造知名遊戲角色的生成內容
- 第三方商標
- 第三方 Logo
- Copyright Character

使用任何 AI Service 前：

確認目前方案允許 Commercial Use。

如果 Codex 無法確認：

將資產標記：

LICENSE_REVIEW_REQUIRED

不得將其當成 Release Asset。

---

# Visual Vertical Slice

目前專案已經具備基本 Gameplay Core。

下一個視覺目標不是繼續增加文字測試功能。

必須建立：

Playable 3D Vertical Slice。

最低要求：

- 真正的 3D Board Scene
- 有高度差與城市裝飾
- 3D Property Building
- 4 個清楚可辨識 Pawn
- 3D Dice
- Dice Animation
- Pawn Movement Animation
- Property Ownership Visual
- Upgrade Visual
- Camera Follow
- Camera Overview
- Camera Zoom
- Camera Rotate
- Modern Runtime UI
- Player HUD
- Action Panel
- Property Panel
- Turn Indicator
- Dice Button
- Event Popup

不得使用：

Unity OnGUI

作為正式遊戲 UI。

可以使用：

UGUI Canvas

或：

UI Toolkit。

---

# AI Agent Responsibility

如果 Codex 可以操作 Unity Editor：

應主動建立：

Scene

Prefab

Material

Lighting

Camera

UI Layout

而不是只產生 C# Script。

如果 Codex 無法產生某個美術 Asset：

它必須輸出完整 Asset Request 與生成 Prompt。

開發者取得模型後：

Codex 必須能繼續完成：

Import

Prefab

Material

Collider

LOD

Scene Placement

Gameplay Integration。

---

# Success Condition

最終要求不是：

「程式可以證明規則有運作。」

而是：

> 玩家第一次啟動 Build 時，應該立刻感覺自己正在玩一款 3D 地產桌遊，而不是在操作規則 Debugger。

即使所有正式美術尚未完成：

Prototype 也必須具有一致且可辨識的遊戲視覺體驗。
