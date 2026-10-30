# 記錄與保留驗收結果

2026-10-30，依本機 artifact-recording-and-retention 規格驗收。Unity 6000.0.85f1、專案 Python 3.10.12、Windows PowerShell 5.1。未改世界規則／AI，未執行六場長期生態或 PPO 訓練；舊果實任務 12／13 仍為 deferred。

## 可重現檢查

```powershell
./scripts/check-recording.ps1 -Build
./.venv/Scripts/python.exe training/artifact_validation.py
./.venv/Scripts/python.exe training/artifact_contract_tests.py
./.venv/Scripts/python.exe training/fruit_recording_validation.py
./scripts/check-world-script.ps1
./.tools/typecheck/node_modules/.bin/pyright.cmd --pythonpath .venv/Scripts/python.exe training/recording.py training/fruit_recording_validation.py training/summarize.py training/bridge.py training/session.py training/evaluate.py training/artifact_validation.py training/artifact_contract_tests.py
```

`check-recording` 在 `artifacts/recording-validation` 建立隔離 junction fixture。加 Build 會成功採用新建置並依政策清理舊受管資料；跨建置比較前先另存證據。Python 整合測試會另建八次受管執行以驗證五次／一次，結論另外保存。

## 結果

| 邊界 | 結果 |
| --- | --- |
| 建置／生命週期 | 不同成功建置有不同內嵌識別；失敗 Editor 啟動未切換 current.json。活躍租約跨建置保留，釋放後才淘汰。重用既有輸出拒絕且保留原來源。 |
| 群體記錄 | C# 公開介面核對每秒、跨輪、提前停止；實際八場 3+2 步的輕量／錄製摘要相等，沒有日常完整個體狀態。 |
| 已知果實帳 | 正式 fruit-flow 場景 150 步：交付1、吃掉1；兩名苔靈飽食最小60／最大80／平均70，摘要與領域帳一致。 |
| 全次錄製 | 所有分段從0連續到摘要末步，核對輪次、結尾、影格數及校驗；實際每段0→last→0逐欄比對，錯建置、損壞、缺幀拒絕。 |
| 失敗 | 初始化拒絕；注入途中寫入失敗後世界仍達100步，資料不完整；失敗錄製保留來源、不替換上一份成功。Python拒絕不完整或缺分段資料。 |
| 保留 | C#隔離混合成功／失敗及實際八次受管執行：五次輕量加五次以外的一次成功錄製，共六次；其餘舊大檔淘汰。 |
| 路徑／並行保護 | 播放與清理共用根鎖；執行／播放租約受保護。junction、未知資料、模型檔、子目錄與外部自訂輸出不誤刪。 |
| bridge | 512步、2,886次觀察、18個agent-id、12個terminal-id，三物種均移動；預設未加完整快照。 |
| 評估入口 | 4項Python公開邊界測試含3步規則控制的詳細評估／完整錄製；沒有載入或訓練模型。分開世界停止與封裝完成，關閉後檢查資料。 |
| 可見操作 | P暫停、手動重建至第2輪、正常結束；run完整、user_stop、recordingComplete=true、兩段。證據已在下一次建置前另存。 |
| 工具檢查 | 上述列示核心Python檔Pyright零錯誤／警告；PowerShell 5.1解析所有腳本並核對UTF-8 BOM。完整C#果實／生產／照顧／感知／負重／煙幕回歸通過。 |

本機詳細證據在 `artifacts/recording-validation/`：`final-accepted.log`、`integration-*/results.json`／`managed-results.json`、`final-bridge`、`known-fruit`、`visible-accepted`；後者保存建置 acf0c9cdc54c4ba785d808bcbaae0370 的可見驗收來源。原始檔不隨 Git 保存，也不承諾未來建置可播放。

最後一次完整回歸／建置為 `check-b6aab708136342688392bd3dea543247.log`，退出碼0，採用建置73d9e602372c4824a245ae7fda19cf63。`final-contract/checks.json` 證明重用輸出拒絕後所有原檔SHA-256不變，以及無畫面預設0張／明確跳轉3張快照。

## 限制與未完成項

首次舊資料已辨識384份、24,032,917,856 bytes，其中383份沒有建置識別；保留表頭與分類清單，沒有猜補建置。自動核准審查以 `blocked by policy` 拒絕依清單刪除，未提供其他理由，故實際刪除尚未執行，不能宣稱已回收容量。本機清單為 `artifacts/cleanup-20261030/inventory.json`／`summary.json`。

07清理維持未完成，08尚不能結案歸檔；本功能留在 `.scratch/artifact-recording-and-retention`。正式程式操作文件已更新。歷史果實11的驗收結論不改寫，12／13不恢復、不歸檔。

後續已交付[手動清理腳本](legacy-artifact-cleanup.md)與固定384項完整SHA-256清單。Windows PowerShell 5.1已完成只讀預覽（刪除0），並以隔離資料核對變動檔、遭修改清單、junction、全數不存在及`-Apply -WhatIf`；測試不執行刪除。等待使用者手動清理後才續辦07／08。

注入故障驗收透過公開記錄介面的I/O邊界；未實際耗盡磁碟。固定模型觀察的整合契約、來源分離及完整性檢查已更新；本輪沒有可用的相容模型，也未為此新訓練模型。套件本身的既有deprecated警告仍存在。

既有 `ecosystem_observation.py`／`juvenile_ecology.py` 另有10項型別診斷；與基準618cf6a的原檔逐項相同（本機 `baseline-typecheck`），本輪只調整明確診斷旗標與讀取位置，未擴充成生態工具型別修復。

## Standards

兩次獨立複查確認，清理／播放競態與既有輸出覆寫已修正，沒有剩餘硬違規或重大風險。生命週期欄位可進一步收斂為轉移介面，保留為一項非阻擋的維護性判斷。07／08未結案狀態如實保留。

## Spec

原先固定模型完整性、評估舊結構、受管分離、預設跳轉快照與封裝末端檢查均已修正；最後複查沒有剩餘程式契約問題。首次舊資料實際清理及整體結案仍未完成。

審查合計：Standards 0項硬違規、1項非阻擋維護性建議；Spec 0項剩餘程式缺失。

## 人工測試

一般 `forage -Visible` 暫停及重建後正常關閉，先讀 `.simulation/<執行>/summary.json`，確認只有群體樣本。再以 `-Record` 重複，播放兩個分段並前後拖動。無畫面不指定 SeekFrames 時只驗證播放，不匯出完整快照；明確指定 SeekFrames 才匯出。完整流程與資料保留見[操作契約](recording.md)。
