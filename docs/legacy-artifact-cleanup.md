# 非受管產物的盤點與手動清理

這是可重複使用的流程：LLM 盤點並產生清單，使用者先預覽、再手動刪除，LLM 最後核對結果及整理正式紀錄。`.simulation/` 維持[既有自動清理](recording.md#保留)，不納入這個腳本。

`cleanup` 目錄保存候選清單、正式清單及稽核，不搬入原檔。原檔留在原位，直到使用者手動套用。工具不自行掃描或按副檔名推定可刪資料。

## 使用者操作

在專案根目錄使用 PowerShell，先正常結束 World Player。LLM 每次提供實際清單路徑，將下例 `$plan` 換成該路徑；腳本必須明確指定 `-Plan`。

```powershell
$plan = './artifacts/cleanup/<批次>/plan.json'

# 預覽並保存這份清單的雜湊；未刪除檔案
$preview = ./scripts/cleanup-legacy-artifacts.ps1 -Plan $plan

# 確認後手動套用預覽過的清單
./scripts/cleanup-legacy-artifacts.ps1 -Plan $plan -PlanSha256 $preview.PlanSha256 -Apply

# 清完後驗證
./scripts/cleanup-legacy-artifacts.ps1 -Plan $plan -PlanSha256 $preview.PlanSha256 -Verify
```

套用與驗證必須提供預覽所得的 `PlanSha256`；清單變動就拒絕處理，須重新盤點及預覽。若關閉了 PowerShell，使用預覽時顯示的完整雜湊，或重新預覽；不要在套用當下重新計算雜湊來跳過檢查。`-Apply -WhatIf` 仍是預覽。

核對包括實際路徑、所有祖先的連結／junction、使用狀態、大小、修改時間及完整 SHA-256。先核對整批，再刪第一份；套用時逐檔再檢查。只處理明列的非受管 `artifacts/` 檔案，不刪目錄；`.simulation`、環境、模型、工具快取、下載／遷移備份及清理清單／稽核排除。已不存在的項目如實記為 `already_absent`，不宣稱為本次刪除。

可用同一清單重跑以處理中斷後的剩餘檔案；錯誤不回滾已刪項目，也不標整批成功。各次稽核保存於 `artifacts/cleanup/<清單雜湊前16碼>/manual-*.jsonl`，記錄完整清單雜湊、實際刪除量、原已不存在項目、錯誤與剩餘數。預覽回傳物件的 `AuditPath` 可直接找到紀錄。

## LLM 盤點與交付

1. 讀取[記錄契約](recording.md)，先區分自動受管資料、仍有效的模型／測試素材／環境，以及可清理的非受管產物。檢查程式及文件依賴，先保存仍需要的證據與正式結果；來源未知如實記錄。
2. 在 `artifacts/cleanup/<功能及日期>/candidates.json` 明列候選檔案與理由。只加入已分類、已停止使用的檔案，不把整個目錄列為目標。例如：

```json
{
  "purpose": "清理已完成分析的舊觀察原檔",
  "files": [
    {
      "path": "artifacts/old-observation/world.jsonl",
      "reason": "結論已整理至正式文件，測試不再依賴此原檔"
    }
  ]
}
```

3. 將範例替換為實際路徑與依據後，使用唯讀產生器保存大小、修改時間、完整SHA-256與總量；既存輸出不覆寫，檔案盤點途中變動便失敗：

```powershell
./.venv/Scripts/python.exe training/prepare_cleanup_plan.py artifacts/cleanup/<功能及日期>/candidates.json --output artifacts/cleanup/<功能及日期>/plan.json
```

4. 以 `cleanup-legacy-artifacts.ps1 -Plan <plan.json>` 預覽，將清單路徑、用途、數量／大小、排除項與手動命令交付使用者。LLM 不代替使用者執行 `-Apply`，也不透過其他工具刪除這批候選資料。
5. 使用者回報完成後，比對完整清單雜湊、稽核及實際路徑不存在狀態，重驗受素材影響的入口，將處置數量、保留項與限制整合至 `docs/artifact-retention.md`，再結案任務。尚有檔案或失敗時保持未完成。

清單格式為 `schema: 1`、`count`、`bytes`、`files`；每項含專案相對 `path`、`bytes`、`modifiedUnixMs`、`sha256`。新產生器另保存 `purpose`、`createdAt` 及各項 `reason`。清單不是執行程式碼；數量、總量、正規化後的重複路徑與界線由清理器核對。

原384份已由使用者手動清除，逐項稽核與驗證見[清理結果](artifact-cleanup-results.json)。歷史清單仍為 `scripts/legacy-artifacts-20261030.json`，可使用同一套工具驗證不存在狀態；新批次另產清單。fruit-supply-and-flow 12／13繼續deferred，歷史結論不改寫。

## 開發驗證

`./.venv/Scripts/python.exe training/legacy_cleanup_validation.py` 使用獨立清單核對預覽、來源變動、清單變更、越界／junction、受管資料保護、重複項、驗證、缺少預覽雜湊及產生器，均不刪除資料。PowerShell腳本保留UTF-8 BOM，使用 Windows PowerShell 5.1 執行 `scripts/check-world-script.ps1`。
