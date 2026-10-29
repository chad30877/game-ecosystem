# 煙幕感知與觀察體驗任務草案

[規格](spec.md) · [訪談](../../docs/design/observation-experience.md)

目前為 needs-info，等待整體規格與拆分確認；尚未正式發佈或啟動實作。逐張任務是狀態與依賴的維護來源。

| 編號 | 任務 | 前置 |
| --- | --- | --- |
| 01 | [煙幕察覺、記憶與共同觀察](issues/01-smoke-perception.md) | 無 |
| 02 | [規則煙幕避險](issues/02-smoke-avoidance.md) | 01：煙幕察覺、記憶與共同觀察 |
| 03 | [煙幕視野裁切與辨識](issues/03-smoke-rendering.md) | 無 |
| 04 | [工具列、面板與世界配置](issues/04-observer-layout.md) | 無 |
| 05 | [整合回放、驗收與效能量測](issues/05-integration-validation.md) | 01–04：察覺、避險、呈現與配置 |

正式確認後改為 ready-for-agent；所有前置 resolved 才能開始，開始改 claimed，驗收通過改 resolved。此依賴表不授權平行 agent。

人工核對：逐項對照規格、任務驗收與 Q1–Q20 訪談決定；確認未納入 PPO 訓練及決策頻率調整。

