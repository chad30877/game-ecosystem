# 分流標籤與執行狀態

| 標籤 | 用途 |
| --- | --- |
| needs-triage | 尚待判定需求與處理方式 |
| needs-info | 缺少必要資訊 |
| ready-for-agent | 規格已明確，可在前置完成後由 agent 執行 |
| ready-for-human | 需要人工處理 |
| wontfix | 已決定不處理 |

本機任務開始後使用 `claimed`，全部驗收完成後使用 `resolved`。尚有未通過的驗收時保持未完成並記錄原因；不得只因程式可啟動就標為完成。
