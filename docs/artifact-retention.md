# 本機產物清理與保留

2026-10-23 使用者確認本次清理方案：接受放棄舊實驗完整回放及續訓能力，保留結果、來源資訊與八個近期短回放。這是目前開發階段的保留決定，日後可重新調整；不設永久容量或天數門檻。

2026-10-30 新流程已實作，操作見[記錄與保留](recording.md)、[驗收及未完成項](recording-results.md)。首次整理已盤點384份舊世界／回放資料（24,032,917,856 bytes，383份建置未知），來源表頭保存於本機 `artifacts/cleanup-20261030/inventory.json`，分類摘要為 `summary.json`。只選取內容可辨識的世界／回放JSONL與殘檔；設定、結果、其他日誌、模型、環境、快取及本輪驗收證據排除。

自動核准審查拒絕依此清單刪除，只回報 `blocked by policy`，未提供具體原因；沒有執行任何刪除，尚未回收這24GB。07仍未完成，功能未歸檔。以下是2026-10-23歷史紀錄，保留當時結論；八份舊回放已列入本次待清理清單，不再是新流程相容性驗收依賴。

## 本次範圍

- 清理 `artifacts/downloads`、`artifacts/cache` 與 `artifacts/migration/previous-toolchains`；現行 `.tools`、`.python`、`.venv` 及 `unity/Builds` 保持原位。
- 淘汰歷史 JSONL、模型 `.pt`／`.onnx` 及未完成的 `.partial`，但保留 `artifacts/ecosystem-ticket07-probes-reviewed` 的八份完整回放及原有核對資料。
- 保留既有結果 JSON、設定、報告、日誌、截圖與少量輔助腳本。舊工具備份中的實驗設定另存本次清理目錄。
- 清理前逐份讀取 `world.jsonl`，保存首筆完整來源資料、紀錄種類計數、最大總步數、已完成輪次及最後記錄的統計；解析失敗如實記錄。這些摘要不替代既有科學結果，也不補推未知的歷史 Git 版本。

完整盤點、刪除清單、來源摘要及回放雜湊位於本機 `artifacts/cleanup-20261023/`，由 `.gitignore` 排除。`world-log-summaries.json` 保存世界紀錄摘要，`other-log-headers.json` 保存其他 JSONL 的首筆資料。Git 中已有的數值報告仍隨版本保存。

舊文件及任務內的 artifacts 路徑是當時驗證來源，不代表原始檔仍存在。已淘汰來源無法直接用 `ReloadRun`、`ResumeFrom` 或舊模型評估命令操作。歷史通過／未通過結果不因此改寫；未來要驗證新環境時須重新執行。

## 完成紀錄

2026-10-23 已完成清理：原有 116,041 個檔案、53.802 GiB，刪除 114,411 個檔案、53.659 GiB 的邏輯檔案量。加入來源摘要、稽核清單及驗證輸出後，artifacts 約 191.49 MiB。這是檔案大小加總；硬連結等因素使實際磁碟釋放量不同。

- 646 份世界紀錄全部完成摘要，沒有 JSON 解析失敗。
- 原有保留檔案及四個現行執行檔共 1,634 筆 SHA-256，清理前後完全一致；刪除清單中的檔案皆已不存在。
- 八份短回放皆通過既有封裝器的連續影格與完整性檢查。
- 既有 bridge 完成 512 步、2,886 次觀察、18 個 agent-id 與 12 個 terminal-id，三物種均移動。
- 現行 Player 讀取 roots 回放，依 0 → 500 → 1000 → 0 跳轉，四份完整狀態均與原始回放影格逐欄相等。
- 未修改執行程式、未新增測試檔；驗證輸出見本機清理目錄的 `checks/` 及 `verification-results.json`。

## 留下的代表回放

`artifacts/ecosystem-ticket07-probes-reviewed` 中的 `contest`、`costly`、`frost`、`hidden`、`retreat`、`roots`、`stale`、`switch` 各保留一份 `episode-1.replay.jsonl`，合計 112.17 MiB。播放只需封裝回放；不必留下重複的 `record-source.jsonl` 與 `episode-1.source.jsonl`。回放仍需要相容的 Player，不承諾未來所有規則版本均能播放。

## 後續清理

2026-10-23 原決定採明確盤點、報告清單與人工執行，不按時間或容量自動刪除；不把當時副檔名清單套用到未來資料。2026-10-30 已由新規格接續為程式管理日常淘汰、LLM 整理首次舊資料與調整規則，程式已完成驗收，首次舊資料實際刪除受阻，見本文開頭。

本次的 `prepare.py` 及 `execute.ps1` 保存在清理目錄，僅供稽核；後者預設只核對與預覽，明確加上 `-Apply` 才刪除已盤點的檔案，並拒絕跨出 artifacts、連結路徑、變動檔案及執行中的環境程序。完成後不應重跑本次清單。

「日常只保存輕量統計與來源資訊、明確要求才錄完整資料、驗證後移除冗餘來源」已於新規格定案，細節見[後續構想](future-ideas.md#產物記錄預設與保留規則)。記錄預設已修改並通過短程驗收，完整錄製須明確開啟。

## 人工核對

```powershell
$replay = Get-ChildItem 'artifacts/ecosystem-ticket07-probes-reviewed/roots/*/episode-1.replay.jsonl'
./scripts/world.ps1 -Mode replay -ReplayFrom $replay.FullName -Visible
```

前後拖動並核對定身期間仍可採食；也可改看 `contest`／`hidden` 比較感知。使用 `./scripts/world.ps1 -Mode bridge` 核對現行環境可正常啟動。這些命令會產生新的執行紀錄，不屬於本次舊資料清單。
