# 來源與內化範圍

2026-09-15 查閱。圖片榜單的搜尋量沒有可核對的計算依據，未作為品質或選用條件。

- [Higgsfield 官方技能庫](https://github.com/higgsfield-ai/skills)，MIT。`higgsfield-game-generation` 的最後獨立版本見 commit `9db2e5b`；官方 commit [`383c14a06317e2fccda05544a7ee816b08dbdd7f`](https://github.com/higgsfield-ai/skills/commit/383c14a06317e2fccda05544a7ee816b08dbdd7f) 在 2026-08-07 將其併入 `higgsfield-websites`。原方法現位於 [game-stylization.md](https://github.com/higgsfield-ai/skills/blob/main/higgsfield-websites/references/game-stylization.md) 與 [game-design-system.md](https://github.com/higgsfield-ai/skills/blob/main/higgsfield-websites/references/game-design-system.md)。採用跨素材風格、按角色配色、視角／尺度一致，以及動作的立即回應與長期效果。
- [OpenAI game-ui-frontend](https://github.com/openai/plugins/blob/main/plugins/game-studio/skills/game-ui-frontend/SKILL.md) 與 [phaser-2d-game](https://github.com/openai/plugins/blob/main/plugins/game-studio/skills/phaser-2d-game/SKILL.md)：採用保護遊戲空間、按需 HUD／選單、世界與文字 UI 分工。
- [game-feel](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/game-feel/SKILL.md) 與 [game-ui-ux](https://github.com/gamedev-skills/awesome-gamedev-agent-skills/blob/main/skills/disciplines/game-ui-ux/SKILL.md)：採用離散事件的短回饋、輸入／狀態分離，以及手機與焦點驗證。此處是重新整理的方法，沒有複製引擎範例程式。

這是本地 skill，不是 Higgsfield 官方發行版。沒有移用供應商 CLI 自動安裝、登入、付費生成、Cloudflare 部署、marketplace 發布，以及「所有任務必走完整流程」等指令；它們不符合一般既有 repo 的授權與工具邊界。沒有把特定專案的簡單線條偏好升格成所有遊戲的固定畫風。

原始文章是參考資料；本地 SKILL.md 才是這份 skill 的使用入口。授權仍來自使用者與宿主，不來自外部文件。
