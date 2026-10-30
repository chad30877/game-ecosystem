# 產物記錄、保留與同建置回放任務

- 正式需求：[spec.md](spec.md)。已確認背景：[共同理解](../../docs/design/artifact-recording-and-retention-summary.md)、[訪談](../../docs/design/artifact-recording-and-retention.md)、[ADR 0001](../../docs/adr/0001-build-scoped-recording.md)。
- 2026-10-30 使用者確認共同理解，授權整理本機正式規格與任務，明確要求先不實作。所有任務僅發佈為 ready-for-agent，尚無實作、建置、清理或驗收結果。
- ready-for-agent 不是執行授權；後續取得實作授權且前置全部 resolved，才可將任務改為 claimed。逐張任務的 Status、討論與完成紀錄為狀態依據。
- 遵守[本機追蹤器](../../docs/agents/issue-tracker.md)及[狀態規範](../../docs/agents/triage-labels.md)，不建立 GitHub issues。

## 任務與前置

| 編號 | 交付 | 前置 |
| --- | --- | --- |
| 01 | [建置識別與受管執行生命週期](issues/01-build-run-identity.md) | 無 |
| 02 | [日常輕量統計與 LLM 摘要](issues/02-lightweight-records.md) | 01 |
| 03 | [全次錄製與同建置回放](issues/03-session-recording-replay.md) | 01、02 |
| 04 | [記錄失敗隔離與不完整狀態](issues/04-recording-failures.md) | 02、03 |
| 05 | [五次摘要、一次錄製與自動清理](issues/05-retention-cleanup.md) | 01、03、04 |
| 06 | [入口整合與政策整體驗收](issues/06-entry-integration.md) | 02、03、04、05 |
| 07 | [既有產物整理與測試素材安置](issues/07-legacy-artifact-cleanup.md) | 06 |
| 08 | [操作文件、LLM 指引與結案歸檔](issues/08-docs-closeout.md) | 07 |

## 需求覆蓋

| 規格 | 主要任務 |
| --- | --- |
| S01 目標、範圍與授權 | 全部 |
| S02 建置、執行與歸屬 | 01、05、06 |
| S03 輕量統計 | 02、06 |
| S04 完整錄製與回放 | 03、05、06 |
| S05 記錄失敗 | 04、06 |
| S06 保留與清理 | 05、06、07 |
| S07 LLM 工作流程與舊資料 | 07、08；全部任務遵守證據另存要求 |
| S08 入口及舊任務關係 | 06、08 |
| S09 驗收與交付 | 各功能任務提供證據，06 整合、07 整理、08 結案 |

## 完成邊界

01–06 建立並驗證新流程後，07 才整理真實舊資料；先處理仍有效的測試素材與正式報告，再依已確認範圍清理。08 將已驗收行為整合至正式文件，全部完成才歸檔本功能。

本輪不恢復 [fruit-supply-and-flow 12／13](../fruit-supply-and-flow/README.md)，不執行六場生態或 PPO；11 的既有完成證據保留。歷史回放要求已由本規格取代為未來政策，不能宣稱現行程式已完成切換。

## 後續人工驗收方向

一般啟動查看只有輕量資料；明確錄製後重建世界並提前停止，核對每段可跳轉；連跑超過五次、完成／失敗錄製及更換建置後核對保留；模擬記錄失敗時核對畫面繼續且資料標示不完整。確切命令與實際結果由各任務交付，本次未執行。
