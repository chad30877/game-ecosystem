# 戰術學習設定與輸出

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

`training/foraging.json` 是 `train` 的預設完整生態配置。每物種使用一個 ML-Agents behavior／PPO 策略；初始個體依六種技能配置輪替，讓起始族群涵蓋各配置。後代仍依正式規格獨立隨機配置。個體觀察含自身狀態、視野內生物，以及觀察半徑內食物的位置、成熟狀態與剩餘比例；策略仍只收到自身局部觀察。連續瞄準輸出是相對個體的偏移，按世界範圍縮放，與全圖座標無關。

訓練獎勵集中設定於 `training/foraging.json`：存活每世界步 `+0.001`、成功繁殖時親代 `+1.0`、死亡 `-1.0`。不對傷害、擊殺、採食本身直接給分；這些初始權重是可追溯的實驗起點，並非已證明平衡或有效。探索由 `training/toolchain.yaml` 的 PPO `beta: 0.005` 控制。

執行 `scripts/world.ps1 -Mode train` 時，Unity 在輪次結束／全滅後重建世界，停用舊個體以送出 terminal，Python trainer 保留各 behavior 權重並繼續更新。輸出目錄的 `training.json` 記錄種子、配置與步數目標，`world.jsonl` 保存逐步狀態、事件及輪末三物種統計，`training-summary.json` 彙整已完成輪次、步數、結束原因與物種統計。`-Visible` 使用正常畫面及 1 倍時間；預設無畫面並加速。

`training-summary.json` 另依物種及排序後技能組合彙整個體數、觀察存活秒數、採食能量、出生數、造成／承受傷害、死亡及輪末未完結生命。存活秒數從該次執行觀察到的年齡差計算；初始年齡不算本次存活時間。傷害來源以原個體 ID 關聯，因此也會計入來源死亡後才結算的持續傷害。未完結生命包含輪末仍活著及執行結束時尚未死亡的個體。

驗證紀錄（2026-10-21）：Unity build 通過；預設完整生態與 512 trainer steps 可連線、產生三物種 checkpoint。短週期設定下，無畫面訓練完成 1 次 time_limit 重建；可見續訓由三物種各 558 步續至 2232 步，完成 3 次 time_limit 重建，策略權重均有更新；all_dead 設定完成 3 次全滅重建。既有 512 步橋接驗證確認三物種觀察／移動及 terminal。這些是管線與統計驗證，不代表策略品質已提升。

目前此設定只證明訓練路徑與指標可追溯，不證明策略優於規則基準；學習效果需由任務 12 的固定種子評估確認。

## 接續驗證

2026-10-22：重新建置目前版本，預設完整生態以 512 trainer steps 門檻執行，三物種各保存 640 步 checkpoint，輸出涵蓋十八種技能配置，各物種六種起始配置的個體數相差不超過一。既有 bridge 再次完成 512 世界步、2,886 次觀察、18 個 agent-id 與 12 個 terminal-id，三物種皆有移動。

短週期無畫面訓練完成一次 `time_limit` 重建，三策略各保存 558 步；可見續訓完成一次重建，三策略各增至 1116 步。此短程執行尚未更新權重，因此再以 2048 步門檻從 558 步可見續訓：完成三次重建，Flame／Frost／Moss 最終各為 2232／2180／2232 步，各有 29 個策略張量改變且保存 optimizer 狀態。全滅配置另完成三次 `all_dead` 自動重建。證據位於 `artifacts/tactical-learning/verification-20261022/`。Python compileall、指定專案 `.venv` 的 Pyright、相依檢查及 PowerShell 語法解析通過；沒有既有通用測試套件，未新增測試檔。

## Standards

以 `6e87f37` 為固定比較點，審查本次未提交實作及新增檔案。零項明文違規或需修正的程式異味；另外修正 README 仍稱 Python 尚不能控制完整生態的過期說明。

## Spec

一項發現已修正並複核：可見 Python 控制原本顯示手動操作提示，現顯示「Python 策略控制」。其餘需求未發現可行動問題。Standards 與 Spec 均無未處理發現。

## 人工核對

執行 `./scripts/world.ps1 -Mode train -Visible`，確認畫面標示 Python 策略控制，點選個體查看技能配置與冷卻；結束後核對 `training-summary.json` 的物種／配置統計及三物種 checkpoint。以 `-WorldConfig` 指定較短 `episodeSteps` 的配置及較高 `-TrainSteps`，觀察跨輪重建；用同配置與 `-ResumeFrom` 續訓，確認三物種步數增加。完整命令與續訓路徑格式見 README。
