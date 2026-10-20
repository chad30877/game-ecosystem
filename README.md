# 生態世界

目前完成 [04：繁殖、衰老與世界結束](.scratch/first-world/issues/04-life-cycle.md)。炎角獸（橘方塊）、霜羽獸（藍球）、苔靈（綠圓柱）以規則控制器覓食、繁殖並衰老，尚非學習結果；既有三物種共享策略驗證仍可執行。

## 執行

在專案根目錄使用 PowerShell：

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode forage -Visible
./scripts/world.ps1 -Mode forage
./scripts/world.ps1 -Mode manual
./scripts/world.ps1 -Mode bridge -Visible
./scripts/world.ps1 -Mode bridge
./scripts/world.ps1 -Mode train
./scripts/world.ps1 -Mode train -TrainSteps 4096 -ResumeFrom './artifacts/runs/<首次輸出>/<trainerRunId>'
```

`forage -Visible` 點選個體查看健康、能量、年齡與繁殖冷卻；成長期個體較小。每輪結束停留在統計畫面，按「重建世界」開始下一輪，關閉視窗結束。四個大型黃色區域是產地，小型黃色區域是零散食物，亮度與大小反映存量。無畫面 `forage` 預設推進 12,000 步，可用 `-Steps` 指定步數；無畫面及指定正數步數的可見執行均在每輪結束後自動重建，直到指定總步數。兩種模式共用規則與固定步長。

```powershell
./scripts/world.ps1 -Mode forage -Visible -Seed 7
./scripts/world.ps1 -Mode forage -Controller idle -Steps 8000
./scripts/world.ps1 -Mode forage -ReloadRun './artifacts/runs/<輸出>/world.jsonl'
```

`-Controller idle` 停止移動與採食，方便核對飢餓；`forage -Controller manual -Visible` 可用方向鍵移動、空白鍵採食。`-ReloadRun` 從 `life-cycle-v1` 首筆紀錄重載完整配置、種子與控制模式，重現起始世界；規則及靜止模式亦可重現執行序列，手動模式需再次輸入動作。此參數不能與 `-Seed`、`-Controller` 或 `-WorldConfig` 混用。舊 `foraging-v1` 紀錄因生命規則不同而拒絕重載。這不是歷史回放，也不承諾跨裝置逐影格一致。

`manual` 使用方向鍵控制所有個體；`bridge` 讓 Python 分別送出動作，同種個體也可移往不同方向。`-Visible` 顯示場景，省略時無畫面執行。

`train` 預設以每物種 2,048 筆經驗為停止門檻，完成 rollout 批次後才停止，checkpoint 可能略超過門檻。`-TrainSteps` 設定累計門檻；續訓時須大於三物種既有 checkpoint 步數。`-ResumeFrom` 指向含 `Flame/checkpoint.pt`、`Frost/checkpoint.pt`、`Moss/checkpoint.pt` 的 trainer 目錄，會複製至新輸出目錄後恢復參數、optimizer 與累計步數。續訓從新世界開始，原始輸出保留；目前僅驗證相同模型結構與配置的工具能力。

入口以專案根目錄定位 `.tools/unity/6000.0.85f1/Editor/Unity.exe`、`.venv/Scripts/python.exe` 及 `artifacts/runs`，可透過 `-Editor`、`-Python`、`-OutputRoot` 覆寫。每次執行產生獨立輸出目錄，不需要先啟用虛擬環境。

## 世界入口

`unity/Assets/Scripts/World.cs` 提供 `Reset`、`Step(actions)`（移動與採食選擇）、`AddIndividual`、`RemoveIndividual`、`Observe(individual)` 與 `State`。一個世界步同時推進所有個體；`WorldRunner` 負責畫面、ML-Agents 生命週期與 JSONL，`WorldAgent` 只提供個體觀察與動作。

`training/foraging.json` 為 40 × 40 地圖、每物種 10 隻、4 處產地與 12 處零散食物。採食距離 0.7、速率每秒 12 能量；產地容量 30、初值 20、每秒再生 4，零散食物為其四分之一。再生量於任務 04 從 0.4 調至 4，讓預設規則展示可觀察出生；這些參數尚未平衡。能量上限 100、初值 60；每秒基礎消耗 0.5，每移動單位另耗 1.2，耗盡後每秒損失 6 健康。`ForagingController` 只在感知半徑 6 內尋食，找不到時巡遊。`rules`、`idle` 與 `manual` 可執行覓食世界；此階段拒絕將舊 Python 模型接入覓食，以免誤用尚未擴充的觀察／動作介面。

起始個體年齡 30 秒，後代從 0 秒開始；成年門檻 30、壽命 240 模擬秒。能量達 80 且繁殖冷卻歸零時自動出生同種後代，親代扣 40 能量並冷卻 30 秒；後代能量初值 60。全世界上限 120，滿額時不扣能量或啟動冷卻。每步先結算飢餓及老死，再依個體順序繁殖；同時符合兩種死因時記為飢餓。單物種滅絕繼續運行，全體死亡（`all_dead`）或 600 秒（`time_limit`）結束。

`state.deaths` 保留本輪死亡個體、物種、死因、年齡及時間；`state.statistics` 分物種累計出生、飢餓、老死、完成生命年齡總和、人口上限觸發與 `incompleteLives`。後者為當前存活樣本數，其年齡在 `individuals` 中；結束時仍存活者不混入死亡壽命樣本。人口上限每隻符合繁殖條件但遭阻擋的個體、每步計一次。起始成年個體已有 30 秒年齡，分析壽命時須區分起始個體與本輪出生個體。`episode_end` 保存完整輪次結果，重建後另起統計且個體編號不重用。

`training/world.json` 設定邊界、速度、步長、局部觀察半徑、輪次長度及增減事件。預設每物種兩隻，第 64 步各移除一隻、第 96 步各加入一隻，每 256 步重建世界。可複製配置後修改，透過 `-WorldConfig <路徑>` 載入；須滿足每物種至少兩隻及 `0 < removeStep < addStep < episodeSteps`。這些是訓練介面驗證事件，正式生命循環只在 `foraging` 配置啟用。

`-Seed` 固定起始隨機序列。策略只取得四個方向的局部邊界距離，超出半徑統一為 1；全圖位置只寫入外部紀錄。`Flame`、`Frost`、`Moss` 對應三個 behavior，同物種個體使用同一套參數與訓練經驗。

`world.jsonl` 記錄執行識別、種子、規則版本、裝置、配置，以及輪次、世界步數、時間、個體狀態、增減事件與結束原因。世界個體編號跨輪次遞增，須與 run-id 一起識別；`unityObject` 僅供 Unity 物件追蹤。`bridge` 的 `connection.jsonl` 另記 Python 收到的 behavior、decision／terminal agent-id；ML-Agents 識別與世界個體編號屬不同命名空間。

`bridge.json` 提供步數、吞吐量及識別檢查結果；訓練輸出包含 `training.json`、`trainer.yaml`、`trainer.log`，以及 trainer 子目錄內各物種的 TensorBoard、checkpoint、ONNX 和共用 `run_logs/timers.json`。

## 人工核對

1. 建置後開啟 `forage -Visible`，確認三色個體接近黃色食物；點選個體觀察能量補充及飢餓時健康下降。執行 `forage -Controller idle -Steps 8000`，核對死亡事件與再生不超出容量。用 `-ReloadRun` 重載同一紀錄，比較狀態與事件。
2. 開啟 `manual`，確認三色個體可依方向鍵移動，族群依序由六隻變三隻、再恢復六隻。
3. 執行 `bridge -Visible`，觀察同種個體可往不同方向移動、移除及新增；對照 `connection.jsonl` 的 terminal 與新識別。
4. 以相同 `-Seed` 執行無畫面 `bridge`，核對 `bridge.json`，比對兩次 `world.jsonl` 的狀態及事件（排除 run-id、裝置與 Unity 物件識別）。
5. 執行 `train`，確認三個 behavior 分別有更新步數、TensorBoard Policy／Value Loss 與 checkpoint；`training.json` 提供 `trainerRunId`。
6. 程序結束後依上述 `ResumeFrom` 範例續訓。確認 `trainer.log` 對三物種各輸出 `Resuming training from step ...`，且最終 checkpoint 步數高於原始值。

只使用微小存活／碰壁獎勵驗證更新機制，未驗證學習品質。生命循環驗證及人工操作見 [生命循環驗證紀錄](docs/life-cycle.md)；覓食驗證見 [覓食驗證紀錄](docs/foraging.md)；共享策略實測證據與限制見 [共享策略驗證紀錄](docs/shared-policies.md)；環境安裝見 [工具鏈驗證紀錄](docs/toolchain.md)。

## 環境

需要已啟用授權的 Unity 6000.0.85f1（Windows Mono 建置）及 Python 3.10.12。Unity 套件由 `unity/Packages/manifest.json` 指定，Python 相依鎖定於 `training/requirements-lock.txt`。

```powershell
uv python install --install-dir .python --cache-dir artifacts/cache/uv 3.10.12
uv venv --python .python/cpython-3.10.12-windows-x86_64-none/python.exe .venv
uv pip install --python .venv/Scripts/python.exe --cache-dir artifacts/cache/uv -r training/requirements-lock.txt
```

Python 執行檔及標準函式庫位於 `.python`，專案套件位於 `.venv`；兩者都由專案管理，避免 AppData 重新導向。Editor 安裝於 `.tools/unity/6000.0.85f1` 並登錄至 Unity Hub；另有 Editor 時可用 `-Editor` 指定。上述環境與 `artifacts` 均不納入 Git。

虛擬環境包含絕對路徑；搬動整個專案後，須依上述命令重建 `.venv`，並更新 Hub 的 Editor 位置。

本機遷移結果與相容性驗證見 [專案內環境紀錄](docs/project-environment.md)。

目前沒有專案既有測試套件，本次未新增測試檔；以 Unity 建置、Python 語法／型別檢查及實際世界入口驗證。
