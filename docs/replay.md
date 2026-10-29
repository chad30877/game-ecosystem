# 指定場次記錄與歷史回放

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

現行煙幕感知、19 維自身／10 維 Entities、六分支動作與 `survival-v14` 相容性見[煙幕感知與觀察](smoke-awareness.md)；四資源見[四資源觀察與死亡診斷](survival-resources.md)。以下保留既有介面與驗證歷史。

`RecordEpisodes` 以逗號指定輪次，適用規則／指定動作展示及固定模型觀察。每輪須完整結束，才會封裝可播放的檔案；中途關閉保留原始記錄，並回報場次不完整。

```powershell
./scripts/world.ps1 -Mode forage -Visible -RecordEpisodes '1'
./scripts/world.ps1 -Mode observe -ResumeFrom './artifacts/runs/<已保存模型>' -Visible -RecordEpisodes '2,3'
./scripts/world.ps1 -Mode replay -ReplayFrom './artifacts/runs/<此次輸出>/episode-1.replay.jsonl' -Visible
```

可見 `forage` 自然結束後停留；要記下一輪，按「重建世界」。固定模型觀察會自動跨輪，持續到關閉或指定 `Steps`；觀察期間不更新策略。`Steps` 是世界步上限，不是輪次數；須足以涵蓋指定輪次。

回放操作列顯示「歷史回放・唯讀」、目前影格／總影格與 MB。拖動下方時間軸可前後移動；P 暫停／恢復、速度與縮放沿用即時觀察。「回到開頭」保留目前暫停狀態。選取個體可讀取該影格的健康、年齡、兩技能、冷卻、效果及局部視野。個體尚未出生或已死亡時不會保留成幽靈；往回拖可重新選取當時存在的個體。

回放只讀取記錄，不建立 ML-Agents agent、不執行世界步或模型。每個影格包含完整個體、食物、投射物、地面區域、效果、記憶及事件；不存在的個體與食物會同步移除。跳轉清除視覺拖尾與過期回饋，避免後來的效果污染較早影格。

## 格式與容量

`record-source.jsonl` 只擷取指定輪次的完整資料；一般 `world.jsonl` 改為精簡事件、族群採樣及生命週期紀錄，供分析使用。封裝產生各輪獨立的 `episode-N.replay.jsonl` 及容量／SHA-256 側錄 `.json`。中間來源與封裝檔皆保留，計算磁碟預算時須同時計入。

回放第一筆保存 schema、規則、完整世界配置、種子、來源執行與控制模式；固定模型觀察另保存實驗、checkpoint 雜湊與相容性資訊。影格從該輪第 0 步連續保存，末筆包含影格數及前面全部內容的 SHA-256。載入會拒絕缺幀、截斷、竄改、未知版本與不相容配置。Unity 只索引各影格的檔案位移，跳轉時才解析該影格，不將整場的狀態物件全部常駐記憶體。

既有完整 `world.jsonl` 也可明確封裝指定輪次：

```powershell
./.venv/Scripts/python.exe training/recording.py '<world.jsonl>' --output '<episode.replay.jsonl>' --episode 1
./.venv/Scripts/python.exe training/recording.py '<episode.replay.jsonl>'
./scripts/world.ps1 -Mode replay -ReplayFrom '<episode.replay.jsonl>' -SeekFrames '0,250,last,100,0'
```

省略 `Visible` 時只讀取並輸出指定影格的外部狀態，預設順序為 `0,last,0`，用於比對歷史資料。輸出寫入新的執行目錄，不改寫來源。這與 `ReloadRun` 重新執行同條件世界不同。

## 驗證紀錄

2026-10-22，證據在 `artifacts/replay/`：

- 指定只錄第 2 輪，實際完整來源只含 episode 2；封裝 1,001 影格，5,951,769 bytes。
- 依 `0 → 250 → 1000 → 100 → 0` 讀取快照，五筆完整世界狀態與原始影格逐欄相同；來源 SHA-256 未變。截斷、版本錯誤及竄改三類檔案均被拒絕。
- 可見回放實際拖到第 499／1,001 影格，以 8× 恢復後在第 695 影格暫停，再回到開頭。核對縮放及個體檢視；拖到第 30 影格顯示炎角獸定身剩餘 0.6 秒，與歷史效果一致。截圖在 `ui/seek-middle.png`、`ui/speed-paused.png`、`ui/selected-final.png`、`ui/effect-final.png`、`ui/zoom.png`。
- 原有 bridge 完成 512 世界步、2,886 次觀察、18 個 agent-id 與 12 個 terminal-id。未新增測試檔。

人工核對可依上述三個主要命令：先記一個完整場次，正常關閉後播放，來回拖動技能觸發前後、切換速度與選取個體，最後核對容量及來源雜湊。長場次的初次完整性掃描時間與檔案容量成正比。
