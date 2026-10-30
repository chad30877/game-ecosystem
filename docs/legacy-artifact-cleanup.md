# 384 份舊產物的手動清理

在專案根目錄使用 PowerShell；先正常結束所有 World Player。

```powershell
# 預覽：核對完整 SHA-256，不刪除
./scripts/cleanup-legacy-artifacts.ps1

# 確認預覽後，由使用者手動執行
./scripts/cleanup-legacy-artifacts.ps1 -Apply

# 確認清單內全部檔案已不存在
./scripts/cleanup-legacy-artifacts.ps1 -Verify
```

腳本與固定清單均在 Git：`scripts/cleanup-legacy-artifacts.ps1`、`scripts/legacy-artifacts-20261030.json`。範圍為原盤點的 384 份、24,032,917,856 bytes；不掃描新增產物、不刪目錄。清單本身的 SHA-256 亦固定核對。

預覽與套用前均核對專案內實際路徑、所有祖先的連結／junction、檔案使用狀態、大小、修改時間及完整 SHA-256。先核對整批，才開始刪第一份；套用時逐檔再檢查。發現變動或仍有 Player 使用便停止，不重新接受變動後的檔案。

`-Apply -WhatIf` 仍是預覽。已不存在的清單項目會如實列為 `already_absent`；可重跑以處理上次中斷後的剩餘資料。中途出錯不回滾已刪項目，也不標記整批成功。

每次執行留下 `artifacts/cleanup-20261030/manual-<模式>-<時間及識別>.jsonl`，記錄清單雜湊、候選、實際刪除與位元組數、原已不存在的項目、錯誤及最後剩餘數。`-Verify` 只核對不存在，不會把缺少檔案宣稱為本次刪除或推算本次釋放容量。

當 `-Verify` 顯示「384 份清單檔案均不存在」後，回覆 Codex「清理完成，繼續 07、08」。後續會核對稽核紀錄及實際檔案、重驗受影響入口與素材，更新正式結果，再完成 07、08 並歸檔功能。fruit-supply-and-flow 12／13 繼續 deferred。

本輪由 agent 執行的驗證僅含預覽及隔離測試，沒有執行實際刪除。隔離檢查可用 `./.venv/Scripts/python.exe training/legacy_cleanup_validation.py`，所有呼叫均不刪除資料。腳本以 UTF-8 BOM 保存並通過 Windows PowerShell 5.1 的 `scripts/check-world-script.ps1`。
