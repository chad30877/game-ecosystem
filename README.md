# 生態世界

目前實作 [01：工具鏈與最小訓練往返](.scratch/first-world/issues/01-toolchain.md)。這是單一個體的移動與訓練驗證，尚未包含完整生態。

## 執行

在專案根目錄使用 PowerShell：

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode manual
./scripts/world.ps1 -Mode bridge -Visible
./scripts/world.ps1 -Mode bridge
./scripts/world.ps1 -Mode train
```

`manual` 使用方向鍵；`bridge -Visible` 讓 Python 控制個體先向右再向上；省略 `-Visible` 使用相同場景無畫面執行。`train` 執行 2,048 步 CPU PPO 訓練，`-Visible` 可顯示訓練場景。

入口預設 Editor 位於 `C:/Toolchains/Unity/6000.0.85f1/Editor/Unity.exe`，Python 位於 `C:/Toolchains/game-ecosystem/python/Scripts/python.exe`；可透過 `-Editor`、`-Python` 覆寫。每次產生獨立輸出目錄，預設為 `C:/Toolchains/game-ecosystem/runs`，可用 `-OutputRoot` 調整。

## 環境

需要已啟用授權的 Unity 6000.0.85f1（Windows Mono 建置）及 Python 3.10.12。Unity 套件由 `unity/Packages/manifest.json` 指定；Python 直接相依在 `training/requirements.txt`，實際解析版本記錄於 `training/requirements-lock.txt`。

```powershell
uv python install 3.10.12
uv venv --python 3.10.12 C:/Toolchains/game-ecosystem/python
uv pip install --python C:/Toolchains/game-ecosystem/python/Scripts/python.exe -r training/requirements-lock.txt
```

實際安裝、儲存安排與驗證限制見 [工具鏈驗證紀錄](docs/toolchain.md)。

## 世界入口

`unity/Assets/Scripts/World.cs` 提供 `Reset`、`Step(Vector2)`、`Observe` 與 `State`。`WorldAgent` 將同一套規則接到 Unity 畫面、ML-Agents 及 JSONL 紀錄；`training/bridge.py` 從 Python 取得觀察並送出動作。

`training/world.json` 設定邊界、速度、固定模擬步長、局部觀察半徑與輪次長度。`-Seed` 固定起始隨機序列。觀察為四個方向的局部邊界距離，超出半徑統一為 1，不傳送全圖座標給策略。個體狀態的全圖位置只供外部觀察與驗證。

`world.jsonl` 第一筆包含執行識別、種子、規則版本、Unity 版本、繪圖裝置與配置；之後包含輪次、個體、物種占位名稱、累計步數、模擬時間、位置、邊界事件及 `time_limit` 結束原因。每 256 步重建世界，策略保留。`bridge.json` 提供實際步數、耗時與吞吐量；訓練目錄保存 ML-Agents 配置、TensorBoard、checkpoint 與 ONNX。

這個最小場景只用存活／碰壁的微小獎勵驗證更新機制，不代表正式生態獎勵或已證實學習改善。模型識別以訓練目錄的唯一 run-id 與 checkpoint 步數為準。

## 人工核對

1. 建置完成後開啟 `manual`，按方向鍵確認個體移動且不能越界。
2. 執行 `bridge -Visible`，確認 Python 使個體先向右再向上，輪次結束後重建。
3. 以相同 `-Seed` 執行無畫面 `bridge`，查看 `bridge.json` 的 `moved`、步數與吞吐量，並比對 `world.jsonl` 的狀態／事件序列。
4. 執行 `train`，確認 TensorBoard 出現實際 `Losses/Policy Loss`、`Losses/Value Loss` 及更新計時，而不只檢查匯入或 checkpoint 檔案存在。

目前沒有既有測試套件，本任務不新增測試檔。以 Unity 建置檢查 C# 編譯，Python 語法編譯及上述世界入口檢查外部行為。
