# 同建置快照回放

操作與保留以[記錄契約](recording.md)為準。啟動前加 `Record`，所有世界輪次各產生 `episode-N.replay.jsonl`，正常提前停止也可封裝。完整成功後移除 `record-source.jsonl`；失敗時保留已寫來源，不產生整次成功狀態。

回放標頭保存實際建置識別、配置、種子、控制、場景、規則／動作／格式與輪次停止原因。影格從 0 連續保存，結尾含影格數與 SHA-256；全批核對各輪末步、結束記錄、分段數及來源後才標成功。只接受同建置，未知來源、v9–v14、毀損、缺漏及未完成整次錄製明確拒絕，沒有歷史轉換或殘檔修復。

```powershell
./scripts/world.ps1 -Mode forage -Visible -Record
./scripts/world.ps1 -Mode replay -ReplayFrom '.simulation/<執行>/episode-1.replay.jsonl' -Visible
./scripts/world.ps1 -Mode replay -ReplayFrom '.simulation/<執行>/episode-1.replay.jsonl' -SeekFrames '0,last,0'
```

一般播放不另存逐步快照；明確 `SeekFrames` 是診斷匯出，輸出所選影格以便驗證。播放器索引檔案位移，跳轉才讀取該張快照，不重跑世界、模型或感知。保留世界真值與個體當時情報的區別。播放租約與清理共用管理鎖，播放結束後依現有政策處理。

`ReloadRun` 從 `source.json` 重跑配置，與讀快照不同；不承諾跨建置重現結果。

下列是歷史驗收紀錄，原始檔可能已依清理政策淘汰；不代表目前仍支援舊格式。

## 驗證紀錄

2026-10-22，證據在 `artifacts/replay/`：

- 指定只錄第 2 輪，實際完整來源只含 episode 2；封裝 1,001 影格，5,951,769 bytes。
- 依 `0 → 250 → 1000 → 100 → 0` 讀取快照，五筆完整世界狀態與原始影格逐欄相同；來源 SHA-256 未變。截斷、版本錯誤及竄改三類檔案均被拒絕。
- 可見回放實際拖到第 499／1,001 影格，以 8× 恢復後在第 695 影格暫停，再回到開頭。核對縮放及個體檢視；拖到第 30 影格顯示炎角獸定身剩餘 0.6 秒，與歷史效果一致。截圖在 `ui/seek-middle.png`、`ui/speed-paused.png`、`ui/selected-final.png`、`ui/effect-final.png`、`ui/zoom.png`。
- 原有 bridge 完成 512 世界步、2,886 次觀察、18 個 agent-id 與 12 個 terminal-id。未新增測試檔。

人工核對可依上述三個主要命令：先記一個完整場次，正常關閉後播放，來回拖動技能觸發前後、切換速度與選取個體，最後核對容量及來源雜湊。長場次的初次完整性掃描時間與檔案容量成正比。
