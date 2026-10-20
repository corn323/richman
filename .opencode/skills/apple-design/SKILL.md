---
name: apple-design
description: 手勢、spring、drag/swipe/sheet、慣性與可中斷互動的實作與檢查。只有刻意採用 Apple 視覺時才套用材質與字體建議。
license: MIT
metadata:
  author: Emil Kowalski
  source: https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md
  version: "1.0.0"
---

# 流暢手勢互動

讓畫面從目前位置接續指標速度，能隨時停止、抓住與反向。

- pointer-down 立即給回饋，pointer-up 才提交可取消的操作；支援 pointercancel。
- 拖曳使用 Pointer Events 與 capture，保留抓取偏移；追蹤短期位置與時間以估計釋放速度。
- 中斷時從實際畫面值開始，接續速度；不要跳到上次目標值，也不鎖住輸入等動畫結束。
- 以投影落點選 snap target。速度單位需符合所用 spring API；歸一化速度時處理剩餘距離為零。
- 邊界給漸進阻力；reduced-motion 路徑移除 bounce 與大位移，仍保留操作。
- 鍵盤與非拖曳替代操作必須存在。手勢好看不等於功能可及。

需要投影與阻力公式時讀 [gestures.md](references/gestures.md)。刻意採用 Apple 材質、字體時才讀 [materials-type.md](references/materials-type.md)。

驗證快速反向、拖出邊界、取消、重複開關及 resize。沿用現有動畫工具；CSS/WAAPI 能滿足可中斷要求時可直接使用，不強制加 spring library。

沿革：Emil Kowalski apple-design 1.0.0，MIT；來源 [原 skill](https://github.com/emilkowalski/skills/blob/main/skills/apple-design/SKILL.md)。保留 fluid interaction 意圖，取消將視覺偏好列為通用 invariant。
