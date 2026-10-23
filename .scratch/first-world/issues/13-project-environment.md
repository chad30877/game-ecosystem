# 13：專案內環境與輸出遷移

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](../../../docs/artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

**Status:** resolved

**What to build:** 依 2026-10-20 使用者要求，將 Python 執行環境、虛擬環境及活躍輸出納入專案目錄管理，移除本專案對自訂 Toolchains 目錄的依賴，並處理本次安裝的 Unity Editor 位置。

**Blocked by:** 無；01 與 02 均已完成。

## 驗收條件

- [x] Python 位於 `.python/`，專案套件位於 `.venv/`，執行輸出位於 `artifacts/runs/`，均不納入 Git。
- [x] 更新預設入口與重建環境文件，保留 Windows PowerShell 5.1 的 UTF-8 BOM 相容性。
- [x] 遷移既有輸出及本次安裝的 Editor，驗證新位置可執行後清理舊 Toolchains 內容。
- [x] 實際驗證建置、可見／無畫面往返、三物種訓練及 checkpoint 續訓；保留原始模型與驗證證據。

## 驗證方式

使用既有世界入口、Python 套件相依檢查與 PowerShell 5.1／7 語法檢查，不新增測試檔。人工重跑 `./scripts/world.ps1 -Mode bridge -Visible`，核對場景與執行結果。

## 完成紀錄

2026-10-20：環境與輸出遷入專案，Editor 已登錄 Hub；既有 135 個輸出檔案逐一核對 SHA-256 相同。建置、可見／無畫面往返、三物種從零訓練及舊 checkpoint 續訓通過，另修正 PowerShell 5.1 的 stderr 管線與 YAML BOM 問題。完整證據見 [專案內環境紀錄](../../../docs/project-environment.md)。

使用者已在一般 PowerShell 確認可見場景正常。舊 `C:/Toolchains` 已退出使用；遞迴刪除遭自動核准審查拒絕後，改移入專案 `artifacts/migration/previous-toolchains` 保留回復能力。舊 AppData Python 已解除安裝，D 槽下載目錄僅剩空資料夾。舊位置退出後，再從 `D:/` 執行入口並指定相對輸出路徑，128 世界步成功。
