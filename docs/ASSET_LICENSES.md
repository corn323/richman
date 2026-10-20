# 資產授權登記（ASSET_LICENSES）

> 本檔為商業 Steam 發行的授權舉證依據。任何外部/生成資產加入專案前，必須先在此登記來源與授權狀態。
> （`docs/ASSET_REQUESTS.md` 內提及的 `ASSET_REGISTRY.md` 即為本檔，統一以 `ASSET_LICENSES.md` 為正式名稱。）

## 登記表

| 資產 | 路徑 | 來源 | 授權狀態 | 商業使用 | 備註 |
| --- | --- | --- | --- | --- | --- |
| 角色概念圖 ×8 | `character/*.jpg` | AI 生圖（專案擁有者自行生成，2026-10-20 口頭確認） | 待補：生成工具名稱與其商業使用條款 | 待確認 | 使用者確認為本人生成；需補登生成工具（Midjourney/DALL·E/SD 等）與該服務之商用授權條款連結 |
| 職業角色模型 ×8 | `Assets/models/*.glb` | 本專案 `Tools/roles/*.py` 於 Blender 程序化建模（依據上列概念圖） | 原創（衍生自上列概念圖） | 可（隨概念圖授權確認） | 目前為孤兒資產，未被任何場景引用 |
| 角色預覽圖 | `Assets/models/*_preview.png` | `Tools/render_preview.py` 自行渲染 | 原創 | 可 | — |
| 世界巡遊棋盤地景 | `GodotProject/Assets/archipelago_map.blend` / `.glb` | `Tools/build_archipelago.py` 程序化生成 | 原創 | 可 | — |
| 早期地圖參考圖 | `Assets/map/gamemap.png` | **來源未知** | **未登記（高風險）** | 未知 | 無程式碼引用；建議確認來源或移除 |

## 待辦

- [ ] 補登 `character/*.jpg` 的生成工具名稱與商業使用條款（Phase 5 量產前必須完成）。
- [ ] 確認或移除 `Assets/map/gamemap.png`。
- [ ] 任何 Poly Haven / Sketchfab / Poly Pizza 匯入資產，匯入當下即登記（含作者、授權證別、CC-BY 需署名方式）。
