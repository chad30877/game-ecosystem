# 四資源觀察與死亡診斷

目前規則、模型及回放相容性版本為 `survival-v9`。正常世界使用健康、體力、魔力、飽食度；世界起始飽食60，新生30。走路不按距離扣營養，奔跑速度1.5倍、每秒10體力；普通近戰耗體力，冰箭及特殊技能耗魔力。完整數值見[正式規格](../.scratch/survival-resources-and-environment/spec.md)。

## 操作與紀錄

```powershell
./scripts/world.ps1 -Mode forage -Visible
./scripts/world.ps1 -Mode forage -Controller manual -Visible
./scripts/world.ps1 -Mode forage -Steps 30000 -RecordEpisodes '1'
./scripts/world.ps1 -Mode replay -ReplayFrom '<執行目錄>/episode-1.replay.jsonl' -Visible
```

手動方向鍵走路、LeftShift奔跑、空白採食、A普通攻擊、S/D技能、Q/E轉向。點選個體查看四資源、走跑、恢復、決策理由與資源帳；全景顯示世界真值，個體視野只呈現當時合法可見內容。規則與指定動作可直接錄影，不需模型；錄製須涵蓋完整一輪，步數不足時保留來源並拒絕封裝。

「死亡」按鈕可選擇死亡紀錄，查看起始成年／本輪出生、生命年齡、觀測存活秒數、最後死因及此前戰鬥與飢餓傷害；同時保存最近進食、繁殖與營養恢復時間。最後死因不代表唯一成因。死亡前最多32筆每秒資源樣本保留於紀錄；回放中點選樣本可跳至該時刻，完整逐步歷史仍在回放／原始紀錄。時間 -1 表示尚未發生。

回放前後拖動只讀取快照，保持資源、決策、局部感知及死亡歷史；往回跳至出生前或死亡後時不建立幽靈個體。死亡摘要保留至該輪結束，向前拖過死亡後可重新選擇。

## 資源帳與生命摘要

`ledger` 的 `opening`、`income`、`expense`、`closing`、`residual` 陣列順序固定為健康、體力、魔力、飽食度。每項均可核對「期初＋收入－支出＝期末」。殘差保留浮點累積差異，不以帳面值覆蓋真實資源。

分類欄位包括 `basalSpent`、`activityStaminaSpent`、`skillManaSpent`、`reproductionSpent`、`foodReceived`、`automaticRecovered`、`nutritionRecovered`、`nutritionSpent`，以及實際戰鬥、飢餓與老死健康損失。營養轉換分別列為飽食支出及其他資源收入，不重複算成食物收入。死亡紀錄保存最終資源帳及完整分類。

```powershell
./.venv/Scripts/python.exe training/summarize.py '<執行目錄>/world.jsonl' '<摘要.json>'
./.venv/Scripts/python.exe training/summarize.py '<執行目錄>/record-source.jsonl' '<完整摘要.json>'
```

摘要的 `lives` 分列資源帳、分類與死亡歷史；`censored=true` 是觀察截止仍活著者，排除於 `meanCompletedObservedSeconds`。生命年齡包含初始成年年齡，觀測存活秒數從本次初始化／出生計算；停用老化時觀測秒數仍增加。關閉時的 `observation_end` 保存最後狀態，未完成輪次也能核對當下資源。使用精簡紀錄分析每步事件時，請改讀完整 `record-source.jsonl`。

## 共同契約與重現

自身觀察19維：四向邊界、朝向、飽食、兩技能、健康、兩冷卻、體力、魔力、三項營養恢復啟動、三項暫停。Entities Buffer 為10維，原七維後追加實際採食旗標及來源相對座標；隱藏記憶對應三欄為0。規則只依可見或有限記憶食物選目標，不讀全域採食事件。

動作為四個連續值及五個離散分支 `[3,3,2,2,2]`，最後一項為奔跑；指定動作使用 `run`。執行標頭保存完整配置、種子、步長、規則版本、控制模式、規則物種與場景。`-RulesSpecies Flame` 可在固定指定動作場景只讓炎角使用規則，供環境驗證；此功能沒有加入PPO或混合策略實驗。

`-ReloadRun` 重新執行相同條件，包含規則模式的單體位置／食物 layout；scripted 控制仍須有場景。回放完全不執行策略。模型、重載及回放拒絕舊版語意，舊檔不覆寫；既有歷史文件與舊PPO證據不視為新制驗收。單體600秒驗收已由06完成，見[30場驗收報告](survival-validation.md)；生態觀察仍由07執行。
