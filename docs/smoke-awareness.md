# 煙幕感知與觀察

煙幕圓形與 120° 視野扇形相交即察覺，包含相切及圓心位於個體的位置。個體取得完整範圍與對自身的有害性；最後觀察後保留 5 秒模擬時間，重見原範圍且確認消失時提早清除。視野外消失不會直接刪除記憶。

`IndividualState.smokeKnowledge` 保存圓心、半徑、可見狀態、最後觀察時間、有害性及內部區域識別。沒有施放者或失效時間；規則與學習介面都使用這份情報。最多 64 筆，依可見、有害、距離近、觀察時間新及內部識別排序後截斷。

`survival-v14` 的 Entities 新增 10 維煙幕列：`[-5, dx / 地圖寬, dy / 地圖寬, 半徑 / 地圖寬, 有害, 可見, 情報年齡 / 5, 0, 0, 0]`。相對位置限制於 −1～1，年齡限制於 0～1；煙幕列優先輸出，張量另預留 64 列。自身 19 維與動作規格維持原格式。語義及張量容量已變更，舊模型不可直接續用，模型與規則版本檢查會拒絕舊版本。本輪沒有訓練模型。

回放仍採 schema 1，接受 survival-v9～v14；舊回放可觀看實體煙幕，但個體面板顯示「煙幕情報未記錄」，不推算當年的情報。v14 逐影格保存情報及決策原因，跳轉直接讀取該影格，不更新感知。

## 重現

```powershell
./scripts/world.ps1 -Mode build
./.venv/Scripts/python.exe training/smoke_validation.py artifacts/smoke-validation/check
./.venv/Scripts/python.exe training/bridge.py --env unity/Builds/World.exe --config artifacts/smoke-validation/check/config.json --smoke-scenario artifacts/smoke-validation/check/scenario.json --output artifacts/smoke-validation/bridge-check --steps 20 --seed 7
```

`check` 及 `bridge-check` 須使用尚不存在的輸出資料夾。固定場景種子 7、6 個體、2 片煙幕、650 步（13 秒）；回放依 0、1、150、650、1、0 逐欄比對。個體轉頭後保留記憶，煙幕在視野外消失，情報不提前刪除。

人工核對：使用產生的 config.json、scenario.json 開啟 `forage -Visible -Controller scripted`；選取左方炎角獸，捲動資料面板查看煙幕情報。以輸出的 episode-1.replay.jsonl 開啟 `replay -Visible`，暫停後前後拖動，核對可見／記憶／到期。分別使用 960×540 與 1280×720，收合面板、換選個體、重新展開、返回全景並縮放；資料面板與工具列應不遮住世界，點擊 UI 不應換選。
