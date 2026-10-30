# 實驗保存與續訓

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

`train` 每次執行使用獨立輸出目錄；省略操作選項等同 `-NewExperiment`。全新實驗從零建立三物種策略，保留既有結果。`-ResumeFrom` 則沿用同一實驗的策略、optimizer 與累計步數，從新世界開始。

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode train -NewExperiment -Visible -TrainSteps 4096
./scripts/world.ps1 -Mode train -ResumeFrom './artifacts/runs/<已保存執行>' -Visible -TrainSteps 8192
```

訓練視窗的「重建世界」只重設目前世界，Python trainer 保留策略；「保存並結束」或正常關閉視窗會通知 trainer 停止並保存。關閉後等待 PowerShell 顯示保存結果，再離開終端機。強制終止程序、停電或關閉終端機不保證完成本次保存。

全新實驗與續訓不可同時指定。續訓可指向含 `training.json` 的執行目錄，或其中含三物種 checkpoint 的 trainer 子目錄。省略 `-WorldConfig` 與 `-Seed` 會沿用原設定；可另指定種子從不同起點繼續，但世界配置必須相同。`-TrainSteps` 是累計停止門檻，須大於三物種已保存步數。

## 保存內容與相容性

`training.json` 記錄實驗識別、此次執行識別、來源、種子、世界配置、模型／規則版本、世界組件 SHA-256、Python 套件版本、trainer 配置、三物種起訖步數、checkpoint 大小與 SHA-256。`world-config.json` 保存實際使用的配置；trainer 子目錄保留模型、optimizer、TensorBoard 及 trainer 必要資料。`training-summary.json` 保存此次世界的輪次與技能配置統計。

續訓會先檢查三物種 checkpoint 可讀取、包含策略及 optimizer、檔案大小與 SHA-256 符合紀錄、配置與版本相容；失敗時不啟動 Unity。世界組件採保守的完整雜湊比對，包含畫面程式的重新建置若改變組件，也會拒絕續訓。任務 08 及更早的存檔沒有本次 manifest，會明確拒絕；請保留原資料，另開全新實驗。

同一實驗的多次執行共享 `experimentId`，各有不同 `runId`；模型由各物種 checkpoint 雜湊與步數識別。`completed` 表示達到門檻，`stopped` 表示提前正常關閉並完成保存，`failed` 表示訓練或模型保存失敗。這些保存不包含原世界影格或精確隨機狀態；續訓不是歷史回放。

## 輸出位置與容量

```powershell
./scripts/world.ps1 -Mode train -NewExperiment -TrainSteps 4096 `
  -OutputRoot 'D:/Training/active' `
  -BackupRoot 'D:/Training/backup' `
  -HistoryRoot 'D:/Training/history'
```

`-OutputRoot` 預設為專案內 `artifacts/runs`；備份與歷史目錄為選填，必須與活躍輸出及彼此分開。正常保存後會複製整個此次執行目錄，不搬動或覆寫來源；複製前檢查剩餘空間。`training.json` 的 `checkpointBytes` 可核對三物種模型容量，`storage.json` 記錄備份／歷史路徑及各份複製 bytes。每份備份或歷史目錄都可直接作為 `-ResumeFrom`。

## 人工核對

1. 開啟全新可見訓練，按「重建世界」，確認輪次及個體重建，訓練仍持續；按「保存並結束」，等待終端機顯示三物種步數與保存位置。
2. 以該執行目錄續訓，指定更高步數門檻。確認來源步數保留、世界由新一輪開始，保存後步數增加。
3. 再開全新實驗，確認 `experimentId` 不同、`startingSteps` 全為零，舊模型及紀錄仍存在。
4. 指定備份及歷史目錄，核對容量與 checkpoint 雜湊，再從備份續訓。使用缺少 checkpoint 或不同配置的副本，確認啟動前顯示明確錯誤。

## 驗證紀錄

2026-10-22，證據集中於 `artifacts/resume-training/final/`：

- Unity 建置、Python compileall／Pyright、相依檢查及 PowerShell 語法解析通過；既有 bridge 完成 512 世界步、2,886 次觀察、18 個 agent-id 與 12 個 terminal-id。沒有既有通用測試套件，未新增測試檔。
- 新實驗由零開始，Flame／Frost／Moss 分別保存 2232／2212／2232 步。三物種 checkpoint 合計 6,715,563 bytes；備份及歷史副本各 41,851,982 bytes，逐份核對檔案容量與三模型 SHA-256 一致。
- 從備份恢復可見訓練，沿用實驗識別、配置及種子 7，世界由第 1 輪第 0 步開始。正常關閉該次視窗後，三物種各保存 2790／2770／2790 步，狀態為 `stopped`。再從這份存檔重新啟動，完成 4464／4444／4464 步。彙整見 `verification.json`。
- Windows PowerShell 5.1 另開新實驗，識別不同、三物種起始步數全零，最終各 558 步；原實驗模型雜湊維持不變。新日誌轉接也修正了 CP950 無法輸出 trainer 特殊字元的中斷問題，UTF-8 日誌保留原文。
- 缺少 checkpoint、checkpoint 損毀、模型版本不同、世界配置不同、步數門檻過低、舊版 manifest 與缺少種子，共七類資料於啟動 Unity 前拒絕，未產生新的執行目錄。

本次以正常關閉視窗驗證保存路徑；「重建世界」與「保存並結束」按鈕的點擊操作仍依上述人工步驟核對。ML-Agents 在視窗關閉後曾輸出 worker 未回報 shutdown 的清理訊息，但 trainer 以成功狀態結束，三 checkpoint 保存及再次恢復均已通過。

## Standards

以 `dae2f30` 為固定比較點，審查本次改動及新增檔案，零項明文違規、零項需修正的程式異味。

## Spec

一項發現已修正並複核：原本手動重建會替已自然結束的輪次再記一筆 `manual_reset`，目前只在進行中補記結束，已結束者直接重建。剩餘零項可行動問題。

## 模擬資料保存

預設模型輸出仍在 artifacts/runs，模擬記錄另受管於 .simulation，輸出中的 simulation.json 提供入口。模型備份不會替受管記錄建立永久保留；需跨建置比較時先另存必要資料。明確自訂OutputRoot仍不自動淘汰，詳見[記錄契約](recording.md)。
