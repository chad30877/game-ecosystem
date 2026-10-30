# 01 修復腳本編碼

## What to build
恢復 world.ps1 的 UTF-8 BOM，加入實際解析腳本的回歸檢查。

## Blocked by
無。

## Status
resolved

## 驗收條件
- [x] Windows PowerShell 5.1 重現使用者相同的解析錯誤。
- [x] 僅補回 BOM 後，5.1 與 7 解析成功。
- [x] 可見 forage 短程執行成功。

## 完成紀錄
修正前 5.1（碼頁 950）產生 31 個解析錯誤。scripts/check-world-script.ps1 修正前失敗、修正後通過；PowerShell 7.6.5 亦通過。未更動模擬邏輯。

Windows PowerShell 5.1 執行 world.ps1 -Mode forage -Visible -Steps 2，退出碼 0；輸出 artifacts/runs/forage-20261029-111706-986ccef7。正式維護說明已整合至 docs/toolchain.md。
