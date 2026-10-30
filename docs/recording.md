# 記錄、錄製與保留

一般模擬只保存 `source.json`、`world.jsonl`、`summary.json` 與 `run.json`。先讀摘要，再按輪次及時間讀時間序列。`source.json` 保存建置、執行、配置、種子、控制方式、場景與版本；相同種子不保證跨建置結果一致。

## 操作

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode forage -Visible
./scripts/world.ps1 -Mode forage -Visible -Record
./scripts/world.ps1 -Mode forage -Steps 150 -Record
./scripts/world.ps1 -Mode forage -ReloadRun '.simulation/<執行>/source.json'
./scripts/world.ps1 -Mode replay -ReplayFrom '.simulation/<執行>/episode-1.replay.jsonl' -Visible
```

`Record` 在啟動時錄製所有輪次；P 暫停、重建世界分段，正常提前結束仍可播放。舊 `RecordEpisodes` 參數僅作明確錄製的別名，數字不再篩選輪次。直接 Player 使用 `--record true`；詳細逐步診斷使用 `--diagnostic-log true` 或腳本 `DiagnosticLog`，個體 metrics 使用 `MetricsLog`。`SummaryLog` 已由輕量預設取代。

群體樣本每模擬秒一筆，另有輪次起訖及最後狀態；暫停不產生樣本。各物種保存人口、出生／死亡、低飽食（≤45）、飽食最小／最大／平均；世界保存來源數、來源／攜帶／地面果實，以及領域累計帳的產果、進食、腐爛、交付、掉落與來源到期損失。摘要保留各輪起訖、人口／飽食／果實範圍與停止理由，不把不同世界串成同一段歷史。

## 資料狀態

`run.json` 的 `status=complete` 才表示資料正常結束；`recordingComplete=true` 表示所有分段及必要來源已完成封裝。`summary.json` 亦須為 complete。`stopReason` 區分 `user_stop`、`step_limit`、`world_end`；每輪另保存 `manual_reset`、`time_limit`、`all_dead` 等實際原因。

初始化無法建立紀錄會拒絕啟動；途中失敗停止記錄，畫面顯示「資料記錄失敗（不完整）」，世界繼續。錯誤另送 Unity 日誌／stderr；腳本、bridge 與模型觀察檢查完整性，不能只看世界是否繼續。崩潰後未完成的來源保持不完整，清理發現無租約時記錄發現時刻及 unknown，不推測實際停止原因或時間。

## 保留

受管根目錄為專案 `.simulation/`。預設腳本與直接 Player 使用此處；訓練／固定模型入口的模型仍放 `artifacts/runs`，模擬資料分開保存，`simulation.json` 提供入口。明確自訂 `OutputRoot` 或外部 `--world-log` 不自動淘汰。

當前建置保留最近五次已停止的輕量資料（包含失敗），另保留最近一次成功完整錄製的全部分段及摘要／來源；失敗錄製不替換。其餘錄製的大檔不因仍在五次內而持續保留。新建置成功採用後淘汰舊建置資料；失敗建置不切換。建置採用與執行結束觸發清理，沒有常駐清理服務。

建置識別由 BuildWorld 產生並嵌入 Player，同步寫入 `unity/Builds/build.json`；不以 Git commit 或規則名稱冒充建置。根鎖與執行／播放租約使用作業系統檔案鎖，活躍資料延後淘汰。清理只處理有來源標記的直屬執行目錄；連結、越界、未知目錄、未知檔案及子目錄均保留。發生清理錯誤會回報，保留歸屬以供重試。環境、模型、工具快取及 Git 文件不納入管理。

## LLM 比對流程

1. 先讀 `summary.json`，再讀需要的輪次／秒數及 `source.json`；不將不完整結果補成完整結論。
2. 跨建置或超過五次的比較，在下一次建置／執行前將必要證據複製到專案 `artifacts/<任務>/`，包含來源、摘要及需要的分段；複本不提供跨建置播放保證。
3. 分析完成後將有效結論寫入正式 Git 文件，清除已用完的臨時大檔；不建立永久釘選。

短程驗收與限制見[驗收報告](recording-results.md)，首次舊資料處置見[清理紀錄](artifact-retention.md)。
