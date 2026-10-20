---
name: motion-design
description: 動畫的時序、easing、注意力與狀態回饋設計。適用於 transition、stagger 或品牌動效；單純沿用既有 hover 值不需全面設計。
license: MIT
metadata:
  author: LottieFiles
  version: "1.0.0"
---

# 動態設計

先判斷動畫要回饋操作、說明空間，還是表達品牌。只加入能完成該目的的動作。

- 操作回饋立即開始；不要為等完整動畫而鎖住輸入。
- 進場常用 ease-out、退場常用 ease-in；scroll scrub 與等速循環可用 linear。時間由距離、頻率與情境調整。
- 功能 UI 通常一層動作即可。品牌場景才按需增加 secondary/ambient；沒有情緒命名或角色設定也能實作。
- reduced motion 保留狀態意義，以淡入、顏色或直接切換代替大幅位移。opacity-only 是有效選擇。
- 避免大量同步移動，優先 transform/opacity；清理動畫與事件來源。手勢須追隨指標且可中斷。

按需要深入：

| 問題 | 參考 |
| --- | --- |
| 時長、easing、spring 單位 | [timing-easing-tables.md](reference/timing-easing-tables.md) |
| 屬性、狀態、進退場、多元素與循環 | [multi-element.md](patterns/multi-element.md) |
| sticky、水平捲動、進場顯示等滾動動效 | [scroll.md](patterns/scroll.md) |
| 品牌、情緒與敘事技巧 | [motion-personality.md](director/motion-personality.md) |
| 平台、可及性、排錯與驗證 | [troubleshooting.md](reference/troubleshooting.md) |

驗證實際中斷、快速重複操作、離開再進入與 reduced motion。表格值是起點，不是驗收公式。

沿革：LottieFiles motion-design 1.0.0，MIT；移除情緒三支柱、固定 1/3 與八步 checklist 的強制要求。
