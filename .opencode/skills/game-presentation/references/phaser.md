# Phaser 呈現與直接驗證

先沿用既有 Phaser 版本與 bridge。規則／權益在 core 或 server，scene 把狀態轉成 game objects、鏡頭、音效與輸入；不能為視覺重構重新切掉已成立的 persistence。

- 先決定相機是固定、跟隨或可平移，以及不同長寬比如何保留可玩區域。DOM HUD 依畫面邊緣與內容排版；world hit areas 跟隨 game objects 或相機轉換。
- Pointer／touch 沒反應時先觀察 DOM 座標 → Phaser pointer → camera/world transform → hit area → 事件 → command。座標正確但事件沒來，不要盲目加大按鈕。
- Resize、重建 Container 與 child depth 可能影響輸入排序；檢查所用 Phaser 版本的 display list / render list 行為。不可只確認畫面物件位置正確。
- Scene shutdown / React unmount 清除輸入、resize 與 bridge 訂閱。可視物件生命週期不應改變永久狀態。
- 建造／命中等離散事件驅動短回饋：縮放回彈、局部亮起、浮動數值、少量粒子或輕微鏡頭反應。避免每次普通動作都全屏閃白；reduce motion 時維持可讀的靜態訊號。
- Agent hooks 回報必要狀態與可操作座標，不暴露整個 engine instance。測試時間只控制授權的時鐘，不把 renderer advanceTime 當 server 時間。
- 局部驗證涵蓋實際改到的輸入路徑、動畫結束後狀態與 resize。大改 UI 後先用快速視覺 fixture 排錯，再重跑受影響的真實 gameplay 流程；明確區分 seed fixture 和正常取得的進展。
