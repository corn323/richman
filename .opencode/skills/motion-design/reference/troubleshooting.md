# 排錯與驗證

依改動選會暴露錯誤的流程，不逐項套用全表。

| 症狀 | 先檢查 |
| --- | --- |
| 太慢 | 人工 delay、stagger 最後一項、spring settle |
| 反向跳動 | 是否從舊 target 而非當前畫面值開始 |
| 卡頓 | layout 讀寫交錯、大面積 paint、殘留 tween |
| 太干擾 | 位移幅度、循環、同時搶焦點的物件 |
| 出現兩次 | 重複 mount、未清理 listener、effect dependencies |
| 內容一直看不到 | 初始 hidden 後動畫沒啟動、JS 失敗或 reduced-motion 分支 |

代表性驗證：

- 快速重複、反向、取消，確認可操作且無跳動。
- 離開再回來，確認 timer/listener/tween 沒有累積。
- resize、圖片/資料載入後，確認位置仍正確。
- keyboard、focus、screen reader 仍能取得相同結果。
- reduced motion 移除大位移、zoom、parallax、bounce，保留文字、狀態或短淡入。
- 自動持續移動提供適當暫停/停止，離開頁面停止不必要工作。

touch 不依賴 hover；深色與透明材質檢查對比和閃爍。依實際裝置量測，不固定手機時長乘 0.8，也不宣稱 transform/opacity 有無限 GPU 配額。

先修症狀原因；「平淡」不一律代表缺 ambient、bounce 或粒子。沒有實際裝置或瀏覽器驗證時說明證據限制。
