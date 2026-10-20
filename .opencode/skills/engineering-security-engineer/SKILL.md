---
name: engineering-security-engineer
description: 威脅建模、授權邊界、安全 code review、加固或事件修復。一般功能修改不自動擴成全系統安全稽核。
---

# 安全檢查與修復

先確認範圍、資產、資料流、攻擊者能力與信任邊界。只測授權範圍內的系統。

沿實際資料流選有關檢查：

| 邊界 | 常見失敗 |
| --- | --- |
| 身分與 session | 簽章、expiry、issuer、audience 未驗；登出未失效 |
| 資源與角色 | IDOR、跨帳號存取、mass assignment、只在 UI 隱藏操作 |
| 外部輸入 | SQL/command/template injection、XSS、SSRF、path traversal |
| 業務流程 | TOCTOU、重複提交、負數金額、跳步或繞過限制 |
| 設定與依賴 | 過寬 IAM/CORS、公開儲存、已知漏洞、敏感錯誤資訊 |

修復放在真正信任邊界：使用參數化查詢、成熟密碼學 API、server-side 授權與最小權限。不要藉停用安全控制掩蓋 bug。

每項 finding 說明可重現條件、受影響範圍、證據與可行修正。嚴重度依實際 exploitability 和影響判斷；「少一個 header」不自動等於 Medium。無法證明的風險標為待驗證。

用最小安全重現或回歸測試驗證修復；避免為每項 finding 強制新增 CI/掃描平台。涉及真實資料、破壞性測試或正式環境時保留人類授權邊界。
