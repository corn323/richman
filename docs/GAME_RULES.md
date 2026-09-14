# M1 遊戲規則

## Classic Bankruptcy

M1 的正式 GameMode 是 4 人。所有玩家從 `Central Station` 出發，每人有 1500 資金；回合依玩家清單順序輪替。

每回合：

1. 當前玩家執行 `RollDiceCommand`。
2. 兩顆六面骰結果相加，位置以棋盤格索引移動。
3. 跨過起點獲得 200。
4. 落在未擁有地產可購買；落在自己的地產可升級；落在別人的地產支付租金。
5. 稅金與獎金自動結算；其他 Prototype 格目前沒有額外效果。
6. 玩家可略過可選動作，執行 `EndTurnCommand`。

資金不能變成負數。若玩家付不起租金，剩餘現金交給債權地主，地產全部移交給債權地主；若付不起稅金，地產回到銀行。破產玩家淘汰，最後仍為 Active 的玩家勝利。

## Prototype Board

棋盤是 36 格原創資料，地產名稱、區域、購買價格、租金階梯與升級成本都在 `PrototypeBoardFactory` 集中定義。規則核心只依賴 `BoardDefinition`，沒有依賴 Unity GameObject。

這一版刻意不加入事件卡、玩家交易、抵押、特殊移動、連鎖 Bonus 或完整經濟平衡。那些內容分別留給 M9 及之後的設計與測試。
