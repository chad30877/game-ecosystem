# 煙幕感知與觀察

煙幕圓形與 120° 視野扇形相交即察覺，包含相切及圓心位於個體的位置。個體取得完整範圍與對自身的有害性；最後觀察後保留 5 秒模擬時間，重見原範圍且確認消失時提早清除。視野外消失不會直接刪除記憶。

規則控制器使用局部方向候選繞避已知有害煙幕，包含前往食物、照顧及保護子女的移動。煙幕內優先離開，暫停主動採食、餵養、攻擊與施放技能；世界仍照原規則處理定身、已發動的技能與移動偏向。既有求生邏輯判定緊急撤退後，優先安全前進或側移；無安全方向才穿越。無毒煙幕不避開，不承諾最短路徑或永不中毒。

煙幕填色及邊線直接依實際圓形範圍繪製；個體視角以著色器裁至扇形交集，不顯示視野外煙幕或把記憶畫成實體。世界固定在工具列下方的左側，右側始終保留資料空間；面板可收合，換選仍保留手動偏好，收合及返回全景均不擴張世界區域。全景容納整張地圖，個體視角開合面板時維持中心與縮放。遊戲、回放及 Python／訓練入口統一預設 1280×720，仍可明確指定尺寸或調整視窗。

`IndividualState.smokeKnowledge` 保存圓心、半徑、可見狀態、最後觀察時間、有害性及內部區域識別。沒有施放者或失效時間；規則與學習介面都使用這份情報。最多 64 筆，依可見、有害、距離近、觀察時間新及內部識別排序後截斷。

`survival-v14` 的 Entities 新增 10 維煙幕列：`[-5, dx / 地圖寬, dy / 地圖寬, 半徑 / 地圖寬, 有害, 可見, 情報年齡 / 5, 0, 0, 0]`。相對位置限制於 −1～1，年齡限制於 0～1；煙幕列優先輸出，張量另預留 64 列。自身 19 維與動作規格維持原格式。語義及張量容量已變更，舊模型不可直接續用，模型與規則版本檢查會拒絕舊版本。本輪沒有訓練模型。

回放仍採 schema 1，接受 survival-v9～v14；舊回放可觀看實體煙幕，但個體面板顯示「煙幕情報未記錄」，不推算當年的情報。v14 逐影格保存情報及決策原因，跳轉直接讀取該影格，不更新感知。

## 重現

```powershell
./scripts/world.ps1 -Mode build
./.venv/Scripts/python.exe training/smoke_validation.py artifacts/smoke-validation/check
./.venv/Scripts/python.exe training/smoke_validation.py artifacts/smoke-validation/rules-check --rules
./.venv/Scripts/python.exe training/smoke_validation.py artifacts/smoke-validation/edge-check --edge
./.venv/Scripts/python.exe training/bridge.py --env unity/Builds/World.exe --config artifacts/smoke-validation/check/config.json --smoke-scenario artifacts/smoke-validation/check/scenario.json --output artifacts/smoke-validation/bridge-check --steps 20 --seed 7
./scripts/check-smoke-baseline.ps1 -Fixture artifacts/smoke-validation/check
```

`check` 及 `bridge-check` 須使用尚不存在的輸出資料夾。固定場景種子 7、6 個體、2 片煙幕、650 步（13 秒）；回放依 0、1、150、650、1、0 逐欄比對。個體轉頭後保留記憶，煙幕在視野外消失，情報不提前刪除。

人工核對：使用產生的 config.json、scenario.json 開啟 `forage -Visible -Controller scripted`；選取左方炎角獸，捲動資料面板查看煙幕情報。以輸出的 episode-1.replay.jsonl 開啟 `replay -Visible`，暫停後前後拖動，核對可見／記憶／到期。使用預設 1280×720，收合面板、換選個體、重新展開、返回全景並縮放；資料面板與工具列應不遮住世界，點擊 UI 不應換選。

2026-10-29 後續調整：依使用者要求以 1280×720 作為日常預設，不再以小尺寸啟動；世界固定左側，右側資料空間持續保留。下方兩尺寸紀錄是前次驗收歷史；目前人工核對以預設大尺寸檢查面板開合、選取及全景都不移動世界。

本次即時與回放皆核對未指定尺寸時啟動為 1280×720，世界矩形寬度固定為 896，面板開合及選取不改變相機；右側留白沒有殘影。Unity 建置、Python 型別／語法及 20 步 bridge 通過，證據位於 `artifacts/observer-large-layout/`，[版面調整任務](../.scratch/archive/observer-large-layout/README.md)已結案。

需要覆寫尺寸時，可直接對 `unity/Builds/World.exe` 指定 `-screen-width 1600 -screen-height 900`，其餘世界／回放參數沿用入口產生的配置。追加 `--observer-diagnostics true` 可在 Player 紀錄中取得 `observer_layout`，包含 UI／世界矩形、所選個體、相機中心及尺度；預設關閉，不增加畫面文字。

## 驗收結果（2026-10-29）

- Unity 建置、Python 語法及四個變更 Python 檔的 Pyright、兩個 PowerShell 入口語法檢查通過。未啟動訓練。
- `ValidateSmoke.RunAvoidance` 經正式世界驗證相切、圓心、邊緣、未知背後、記憶到期／重見消失、同種／異種、繞避、緊急穿越、幼體照顧／保護、重疊與定身。審查另找到緩衝帶及邊界撤退兩項問題，均補上失敗案例、修正並複查通過。
- 指定動作、規則與中心不可見的邊緣場景，各產生 651 影格並核對 0→1→150→650→1→0；煙幕情報及決策原因與來源逐欄相同。舊 survival-v9 回放仍可讀取，沒有補入歷史煙幕情報。
- 固定動作與起始提交 `fa55e38` 的 650 步完整世界狀態一致（比較排除新增情報欄位）。原始碼由 Git 讀入暫存 Editor 命名空間，執行後移至 artifacts，沒有另一套世界模型。
- 原有 bridge 512 世界步、2,886 次觀察、18 個識別與 12 個 terminal 通過；煙幕 bridge 20 步逐值核對 200 筆共同煙幕列。
- 即時及回放已實際檢查 960×540、1280×720 的控制、面板、點選、暫停及視野裁切；收合後換選仍收合。診斷紀錄確認面板開合及視窗調整保留選取、中心 `(0,0)` 與尺度 `11`，世界與 UI 矩形不重疊。額外測過最大化；小於驗收尺寸的 512 像素寬不保證工具列全部可達。

證據位於 `artifacts/smoke-validation/`，包含 `baseline-compare.log`、`boundary-red.log`／`boundary-green.log`、`review-rules/`、`edge/`、bridge、視窗診斷與畫面。量測摘要另保存於 [結果 JSON](smoke-awareness-results.json)。

五項任務已驗收結案，原規格與逐張紀錄見[歸檔任務](../.scratch/archive/smoke-awareness-and-observer/README.md)。

## 成本與限制

基準為本輪起始提交；種子 7、120 個體、32 片集中煙幕，模擬時間固定為 0 的狀態。相同 Editor 程序各暖機 5 次、量測 50 次，以下為邊界修正後的單次平均毫秒：

| 項目 | 基準 | 修改後 | 差異 |
| --- | ---: | ---: | ---: |
| 全體感知 | 4.681 | 6.454 | +1.773 |
| 全體規則決策 | 0.453 | 3.092 | +2.639 |
| 960×540 全景渲染呼叫 | 0.336 | 0.234 | −0.102 |
| 1280×720 全景渲染呼叫 | 0.218 | 0.196 | −0.022 |

個體裁切渲染呼叫分別為 0.210／0.203 ms。渲染使用 120 個簡化個體與 32 片煙幕，離屏相機只量測 `Camera.Render` 的 CPU 呼叫時間，沒有等待 GPU 完成；不代表完整可見執行的 FPS、GPU 成本或整體加速。

首次量測的感知為 10.885→19.096 ms、規則為 0.989→6.899 ms；兩次記錄皆保留。Editor 暖機、同機工作負載與修正版本不同，單次平均有明顯變異，不以兩次差異宣稱優化。密集煙幕增加感知與候選方向成本，尚未加入空間索引；這是固定密集狀態的成本摘要，不能推論長期生態表現。
