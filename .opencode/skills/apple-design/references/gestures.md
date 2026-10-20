# 投影、速度與邊界

以下是從既有 apple-design 整理的公式示意。實作優先用現有 gesture/spring 工具；參數與單位以所用 API 為準。

釋放速度以 px/s 計，指數衰減的投影距離：

```javascript
function project(velocity, rate = 0.998) {
  return velocity / 1000 * rate / (1 - rate);
}
```

rate 必須在 0 與 1 之間。將 current + projected distance 對應到允許的 snap points。0.998 只是起始值，依產品調整。某些 spring API 用 px/s，某些用 relative velocity；後者除以剩餘距離前處理零距離。

漸進邊界阻力：

```javascript
function rubberband(overshoot, dimension, strength = 0.55) {
  return overshoot * dimension * strength
    / (dimension + strength * Math.abs(overshoot));
}
```

dimension 必須大於零，resize 後更新。此式用於越界距離，不取代 bounds 驗證。

Pointer Events：capture 指標、保留 grab offset、記錄短期位置與時間；pointercancel/lostpointercapture 清理狀態。不要以單一最後兩點的零時間差相除。中斷時取 presentation value，取消舊 scheduler，避免兩條動畫搶寫。
