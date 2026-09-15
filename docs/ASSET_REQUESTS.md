# M1.5 Asset Requests

目前 repository 沒有正式商業美術資產。M1.5 先使用 Unity Primitive 與一致的 low-poly placeholder，所有 placeholder 都透過 Prefab 邊界接入，之後可直接替換，不需要修改 `Richman.Core` 或回合流程。

以下請求只描述未來可替換的正式資產；在收到資產前不阻塞開發。

## Asset Request: CHAR-Pawn-01

- Asset ID: `CHAR-Pawn-01`
- 用途: 四名玩家共用的 stylized toy character body；以顏色、帽子與小配件區分玩家。
- 建議 AI Tool: Unity AI 或 Meshy；Concept Image → Image-to-3D → Blender Cleanup → Unity。
- Triangle Budget: 3,000–6,000 triangles per character。
- Texture Size: 512×512，必要時 1024×1024。
- World Scale: 高度約 1.5 Unity units。
- Pivot: 腳底中心。
- Format: GLB 或 FBX；材質另存為 Unity Material。
- Generation Prompt: `stylized low-poly miniature toy city board-game character, chunky chibi proportions, rounded head, simple body, clean silhouette, friendly neutral face, small replaceable hat, colorful matte painted plastic, centered full-body orthographic concept, original design, no logos, no text`
- Negative Prompt: `photorealistic, realistic human anatomy, horror, weapons, famous game character, trademark, logo, text, watermark, complex skeleton, loose accessories, high-poly sculpt, 4k texture`
- Unity Import Requirement: 使用統一 Humanoid 或簡單 generic rig；材質採 URP/Lit 或專案當期標準材質；關閉不必要的 Read/Write；建立 `Pawn.prefab` 並保留 `PawnView` root。

## Asset Request: PROP-BuildingSet-01

- Asset ID: `PROP-BuildingSet-01`
- 用途: 地產升級視覺，取代 Property Prefab 的 Lv1、Lv2、Lv3 placeholder。
- 建議 AI Tool: Unity AI 或 Meshy；先生成同一建築家族的 concept，再 image-to-3D。
- Triangle Budget: Lv1 800、Lv2 1,500、Lv3 2,500 triangles 以內。
- Texture Size: 512×512，共用 atlas 優先。
- World Scale: 基地 0.85×0.85 units；Lv1 高 0.7、Lv2 高 1.1、Lv3 高 1.6 units。
- Pivot: 建築基地中心、底面 y=0。
- Format: GLB 或 FBX；每個等級獨立 mesh，避免 runtime 拆 mesh。
- Generation Prompt: `stylized low-poly miniature toy city property building upgrade set, one coherent architectural family, level 1 small shop, level 2 medium colorful building, level 3 large landmark building, chunky bevels, clean readable silhouette, simplified hand-painted PBR, original board game asset, no logos`
- Negative Prompt: `photorealistic, skyscraper realism, destroyed building, text, signage, logo, watermark, gothic horror, excessive tiny details, high-poly, 4k, floating geometry`
- Unity Import Requirement: 匯入後建立 `Property.prefab` 的 `BuildingSlot_Lv1`、`BuildingSlot_Lv2`、`BuildingSlot_Lv3`；每級只啟用對應 root，MeshCollider 不需要，使用簡單 BoxCollider 或無碰撞。

## Asset Request: ENV-MiniatureCity-01

- Asset ID: `ENV-MiniatureCity-01`
- 用途: 棋盤中央的 Low-Poly Miniature City landmark 與背景裝飾。
- 建議 AI Tool: Unity AI；若需要較完整 landmark，使用 Meshy 後 Blender Cleanup。
- Triangle Budget: 5,000–8,000 triangles total。
- Texture Size: 1024×1024 atlas。
- World Scale: 約 7.6×7.6 units，最高建築約 3.0 units。
- Pivot: 城市基座中心底部。
- Format: GLB 或 FBX，城市 landmark 與小 props 可分離成 Prefab child。
- Generation Prompt: `stylized low-poly miniature toy city centerpiece for a tabletop property board game, tiny colorful shops, civic tower, park trees, canals and roads, chunky proportions, soft friendly shapes, clean readable silhouettes, hand-painted simplified PBR, original world, no real-world logos`
- Negative Prompt: `real city replica, famous landmark, brand signage, text, logo, watermark, photorealistic, gritty, dark horror, dense traffic, high-poly, 8k texture`
- Unity Import Requirement: 尺寸以 1 Unity unit = 1 meter；標記 Static；建立 LODGroup（可選）；共用材質不超過 4 個；放入 `MiniatureCity.prefab`。

## Asset Request: UI-IconSet-01

- Asset ID: `UI-IconSet-01`
- 用途: Dice、Money、Property、Upgrade、Turn、Winner 與玩家狀態 icon，避免 UI 只依賴顏色。
- 建議 AI Tool: Unity AI 或 ImageGen；產生向量感或透明 PNG icon，再由 Unity UI 使用。
- Triangle Budget: 不適用。
- Texture Size: 256×256 per icon，或 1024×1024 atlas。
- World Scale: 不適用；UI reference canvas 1280×800。
- Pivot: 圖示中心；方向性圖示依視覺中心對齊。
- Format: SVG 優先，否則透明 PNG。
- Generation Prompt: `original friendly low-poly toy city board game UI icon set, clean bold silhouette, consistent rounded geometry, flat readable colors, dice, coins, property, building upgrade, turn arrow, winner crown, bankrupt status, transparent background, no text, no logos`
- Negative Prompt: `emoji clone, trademark icon, text, watermark, photorealistic, noisy gradients, tiny unreadable details, 3d render with background`
- Unity Import Requirement: SVG 使用 UI Sprite；PNG 設為 Sprite (2D and UI)、sRGB、無 mipmap；所有 icon 使用同一 atlas 與一致 padding。

## Asset Request: VFX-BoardFeedback-01

- Asset ID: `VFX-BoardFeedback-01`
- 用途: 購買、升級、骰子結果與玩家回合轉換的簡單非 gameplay VFX。
- 建議 AI Tool: Unity AI；優先使用 Particle System 與簡單材質，不需要外部模型。
- Triangle Budget: 每個 VFX emitter 不超過 200 particles；mesh 不超過 300 triangles。
- Texture Size: 256×256 particle atlas。
- World Scale: property feedback 半徑約 1 Unity unit。
- Pivot: 事件發生位置中心。
- Format: Unity Prefab + Material + Sprite/Texture。
- Generation Prompt: `soft colorful toy-like board game feedback burst, small stars and coins, clean low-poly style, readable at tabletop camera distance, transparent background, no text`
- Negative Prompt: `fire, smoke, explosion, horror, photorealistic, screen filling, logo, text, watermark, excessive bloom`
- Unity Import Requirement: VFX 不改變 Core 結果；建立可由 Presentation 呼叫的 Prefab，提供 Low Quality fallback，避免大量即時光源與長生命週期粒子。

## Asset Pipeline Checklist

收到正式資產後，依序處理：

1. 確認生成服務方案允許 Steam 商業使用，並將來源與授權狀態寫入 `docs/ASSET_REGISTRY.md`。
2. 在 Blender 或等效工具檢查 scale、pivot、法線、破面、隱藏 geometry、材質數、triangle count 與 collider。
3. 匯入 Unity 後建立或替換對應 Prefab；保留 `BoardTileView`、`PropertyView`、`PawnView` 或 `BuildingSlot` root contract。
4. 以 Windows Build 驗證替換資產不改變 TileIndex、Command 或 GameState 行為。
