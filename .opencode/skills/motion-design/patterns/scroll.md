# 滾動動效模式

只有內容或使用者需要時採用。沿用現有工具，原生 CSS、IntersectionObserver 或輕量 scroll listener 也可用；不要為禁用 listener 而新增 library。

- Sticky stack：容器固定、內部轉場；長內容與 keyboard 不可被遮住，reduced motion 改普通區塊。
- Horizontal pan：固定 wrapper，移動 track；距離是 max(0, trackWidth - viewportWidth)，resize 後重算。
- Reveal：進入 viewport 時顯示；JS 失敗與 reduced motion 時內容仍可見。

不要每幀把 scroll 值存 React state。先量測再寫 transform，清理 observer、listener、RAF 與 plugin instance。

GSAP 水平範例見 [horizontal.md](../../gsap-scrolltrigger/references/horizontal.md)。
