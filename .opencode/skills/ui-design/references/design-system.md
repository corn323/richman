# Design system

使用者要求 design system、tokens 或元件庫時使用；一般單頁與版面修正不建立設計系統。

先盤點既有品牌資產、CSS tokens 與重複元件，從真實頁面的不同內容角色提取共用規則，而不是先設計元件目錄再迫使頁面套用。資料工具與作品展示有不同的密度、比例與素材需求，系統要容納這些差異；一致性不是畫面等形。

- Tokens 使用語意角色，沿用專案格式；沒有既有格式時可用 CSS custom properties。
- 顏色、字體、間距與深度來自品牌、現況或此次設計目的；缺乏方向時做可逆提案。
- 元件只涵蓋真實重用需求，補齊適用的 default、hover、active、focus-visible、disabled、loading、error 狀態，附簡短用途與限制，不創造只有一頁會用的變體。
- 標題元件不強制 eyebrow、subtitle 或 description，也不自動補字；有與沒有補文時的內容與間距都要驗證（見 [ui-copy.md](ui-copy.md)）。

用真實頁面驗證 tokens 與元件能表達需求，不只展示一張孤立的元件牆。

源自 [Agency Agents UI Designer](https://github.com/msitarzewski/agency-agents/blob/main/design/design-ui-designer.md)（MIT，授權見 [LICENSE-third-party.md](../LICENSE-third-party.md)）；保留跨頁一致與開發交付用途，不採用其範例色票、字體與 design-system-first 作為一般網站預設。
