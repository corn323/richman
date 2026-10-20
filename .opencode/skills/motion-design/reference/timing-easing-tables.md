# 時長與 easing 起點

數值是初次試作的範圍，依實際距離、頻率與可及性調整，不作固定門檻。

| 場景 | 可試時長 |
| --- | --- |
| 按下、hover、短狀態回饋 | 80–180ms |
| icon / toggle 切換 | 120–250ms |
| 區塊進退場 | 180–350ms |
| dialog / 視圖轉場 | 200–450ms |
| 特意安排的品牌揭露 | 依內容節奏 |

回饋的開始時間與整段 duration 不同；操作應立即有回應，不能先等 delay。

- 進場：ease-out；退場：ease-in；畫面內兩端停留：ease-in-out。
- 等速 spinner 或 scroll scrub：linear；不要額外 easing 破壞位置對應。
- stagger 計算最後一項何時可用，不只看間隔。必要時同時顯示其餘內容。
- spring 的 stiffness、damping 與 damping ratio 不同；參數依 library 的單位查文件，不能把 damping=1 當所有 library 的臨界阻尼。

[GSAP 使用者](../../gsap-core/SKILL.md) 的 duration 單位是秒；CSS 可用 ms 或 s。
