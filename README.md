# 生態世界

目前完成 [02：三物種共享策略與動態個體驗證](.scratch/first-world/issues/02-shared-policies.md)。最小場景包含炎角獸（橘）、霜羽獸（藍）、苔靈（綠），各自使用獨立共享策略；尚未包含完整生態。

## 執行

在專案根目錄使用 PowerShell：

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode manual
./scripts/world.ps1 -Mode bridge -Visible
./scripts/world.ps1 -Mode bridge
./scripts/world.ps1 -Mode train
./scripts/world.ps1 -Mode train -TrainSteps 4096 -ResumeFrom 'C:/Toolchains/game-ecosystem/runs/<首次輸出>/<trainerRunId>'
```

`manual` 使用方向鍵控制所有個體；`bridge` 讓 Python 分別送出動作，同種個體也可移往不同方向。`-Visible` 顯示場景，省略時無畫面執行。

`train` 預設以每物種 2,048 筆經驗為停止門檻，完成 rollout 批次後才停止，checkpoint 可能略超過門檻。`-TrainSteps` 設定累計門檻；續訓時須大於三物種既有 checkpoint 步數。`-ResumeFrom` 指向含 `Flame/checkpoint.pt`、`Frost/checkpoint.pt`、`Moss/checkpoint.pt` 的 trainer 目錄，會複製至新輸出目錄後恢復參數、optimizer 與累計步數。續訓從新世界開始，原始輸出保留；目前僅驗證相同模型結構與配置的工具能力。

入口預設 Editor 位於 `C:/Toolchains/Unity/6000.0.85f1/Editor/Unity.exe`，Python 位於 `C:/Toolchains/game-ecosystem/python/Scripts/python.exe`；可透過 `-Editor`、`-Python` 覆寫。每次產生獨立輸出目錄，預設為 `C:/Toolchains/game-ecosystem/runs`，可用 `-OutputRoot` 調整。

## 世界入口

`unity/Assets/Scripts/World.cs` 提供 `Reset`、`Step(actions)`、`AddIndividual`、`RemoveIndividual`、`Observe(individual)` 與 `State`。一個世界步同時推進所有個體；`WorldRunner` 負責畫面、ML-Agents 生命週期與 JSONL，`WorldAgent` 只提供個體觀察與動作。

`training/world.json` 設定邊界、速度、步長、局部觀察半徑、輪次長度及增減事件。預設每物種兩隻，第 64 步各移除一隻、第 96 步各加入一隻，每 256 步重建世界。可複製配置後修改，透過 `-WorldConfig <路徑>` 載入；須滿足每物種至少兩隻及 `0 < removeStep < addStep < episodeSteps`。這些是訓練介面驗證事件，正式出生死亡規則另由後續任務實作。

`-Seed` 固定起始隨機序列。策略只取得四個方向的局部邊界距離，超出半徑統一為 1；全圖位置只寫入外部紀錄。`Flame`、`Frost`、`Moss` 對應三個 behavior，同物種個體使用同一套參數與訓練經驗。

`world.jsonl` 記錄執行識別、種子、規則版本、裝置、配置，以及輪次、世界步數、時間、個體狀態、增減事件與結束原因。世界個體編號跨輪次遞增，須與 run-id 一起識別；`unityObject` 僅供 Unity 物件追蹤。`bridge` 的 `connection.jsonl` 另記 Python 收到的 behavior、decision／terminal agent-id；ML-Agents 識別與世界個體編號屬不同命名空間。

`bridge.json` 提供步數、吞吐量及識別檢查結果；訓練輸出包含 `training.json`、`trainer.yaml`、`trainer.log`，以及 trainer 子目錄內各物種的 TensorBoard、checkpoint、ONNX 和共用 `run_logs/timers.json`。

## 人工核對

1. 建置後開啟 `manual`，確認三色個體可依方向鍵移動，族群依序由六隻變三隻、再恢復六隻。
2. 執行 `bridge -Visible`，觀察同種個體可往不同方向移動、移除及新增；對照 `connection.jsonl` 的 terminal 與新識別。
3. 以相同 `-Seed` 執行無畫面 `bridge`，核對 `bridge.json`，比對兩次 `world.jsonl` 的狀態及事件（排除 run-id、裝置與 Unity 物件識別）。
4. 執行 `train`，確認三個 behavior 分別有更新步數、TensorBoard Policy／Value Loss 與 checkpoint；`training.json` 提供 `trainerRunId`。
5. 程序結束後依上述 `ResumeFrom` 範例續訓。確認 `trainer.log` 對三物種各輸出 `Resuming training from step ...`，且最終 checkpoint 步數高於原始值。

只使用微小存活／碰壁獎勵驗證更新機制，未驗證學習品質。實測證據與限制見 [共享策略驗證紀錄](docs/shared-policies.md)；環境安裝見 [工具鏈驗證紀錄](docs/toolchain.md)。

## 環境

需要已啟用授權的 Unity 6000.0.85f1（Windows Mono 建置）及 Python 3.10.12。Unity 套件由 `unity/Packages/manifest.json` 指定，Python 相依鎖定於 `training/requirements-lock.txt`。

```powershell
uv python install --install-dir C:/Toolchains/game-ecosystem 3.10.12
uv venv --python C:/Toolchains/game-ecosystem/cpython-3.10.12-windows-x86_64-none/python.exe C:/Toolchains/game-ecosystem/python
uv pip install --python C:/Toolchains/game-ecosystem/python/Scripts/python.exe -r training/requirements-lock.txt
```

基底 Python 與虛擬環境均放在 `C:/Toolchains`，避免 Codex 的 AppData 路徑重新導向使一般 PowerShell 找不到基底執行檔。

目前沒有專案既有測試套件，本次未新增測試檔；以 Unity 建置、Python 語法／型別檢查及實際世界入口驗證。
