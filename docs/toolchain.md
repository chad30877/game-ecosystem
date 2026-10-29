# 工具鏈驗證紀錄

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

檢查日期：2026-10-20。任務 01 已完成建置、可見／無畫面往返與實際 PPO 更新驗證；這不代表已驗證學習品質改善。

## 版本與相容性

| 工具 | 選用版本 | 狀態 |
| --- | --- | --- |
| Unity Hub | 3.22.3 | 使用者已安裝至 `C:/Program Files/Unity Hub`，Editor 授權可用 |
| Unity Editor | 6000.0.85f1，ee7790f1426a | 已安裝；Windows Player 建置通過 |
| Unity ML-Agents | 4.0.0（Release 23） | C# 編譯、Python 連線通過，通訊版本 1.5.0 |
| Python | CPython 3.10.12 | 已安裝，獨立虛擬環境 |
| mlagents／mlagents-envs | 1.1.0 | 已完成實際 PPO 更新及模型匯出 |
| PyTorch | 2.5.1+cpu | 已安裝；CUDA false，未使用 GPU 訓練 |
| setuptools | 80.9.0 | 避免新版移除 `pkg_resources` 導致 ML-Agents 啟動失敗 |
| huggingface-hub | 0.27.1 | 固定相依版本，完整解析見 requirements-lock.txt |

[Unity Release 23](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_23_tag)對應 Unity 套件 4.0.0 與 Python 套件 1.1.0。[官方安裝說明](https://docs.unity.cn/Packages/com.unity.ml-agents%404.0/manual/Installation.html)要求 Unity 6000.0 以上；[Python 套件宣告](https://github.com/Unity-Technologies/ml-agents/blob/release_23/ml-agents/setup.py)要求 Python 3.10.1–3.10.12。這些是版本配對依據，不等於本機整合已通過。

Editor 安裝檔由 Unity 官方 release API 取得，Authenticode 簽章為 Valid、簽署者為 Unity Technologies SF；靜默安裝結束碼為 0。只安裝 Editor 內含的 Windows Standalone Mono 支援，沒有追加 Android、WebGL、IL2CPP 或 Visual Studio。

## 實際位置與磁碟

| 用途 | 路徑 |
| --- | --- |
| 唯一活躍來源與 Unity Library 快取 | `D:/Projects/game-ecosystem`、`unity/Library` |
| Editor | 專案內 `.tools/unity/6000.0.85f1/Editor/Unity.exe`，已登錄 Unity Hub |
| Python 執行環境 | 專案內 `.venv` |
| Python 基底 | 專案內 `.python/cpython-3.10.12-windows-x86_64-none` |
| 活躍輸出 | 專案內 `artifacts/runs` |
| 安裝下載與 uv 套件下載快取 | 專案內 `artifacts/downloads`、`artifacts/cache/uv` |
| 歷史輸出 | `D:/TrainingHistory/game-ecosystem/toolchain-20261020`（已封存兩次 bridge 與一次 PPO 訓練） |

上表為任務 13 遷移後的位置；以下任務 01／02 的實測紀錄保留當時路徑，同名執行目錄已遷至 `artifacts/runs`。環境遷移與人工驗證見 [專案內環境紀錄](project-environment.md)。

安裝前 C 槽剩餘 105,957,982,208 bytes（約 98.7 GiB），D 槽剩餘 668,849,696,768 bytes（約 622.9 GiB）。安裝後量測 C 槽約 89.8 GiB，仍高於原規劃 25–30 GB 餘裕。

`Get-Partition`、`Get-Disk` 與 `Get-PhysicalDisk` 實測 C 對應 Micron 2450、D 對應 ADATA LEGEND 900，兩者均為 NVMe SSD。因此保留 D 槽現有專案，沒有另複製一份活躍來源。2026-10-20 使用者確認採用此雙 SSD 配置，取代原 HDD 要求。當日建置期間 C 槽剩餘約 70.4 GiB、D 槽約 616.7 GiB；下載與歷史輸出以獨立目錄管理。

## 已執行檢查

- Python 語法編譯通過；Pyright 對 `training/bridge.py` 回報 0 errors、0 warnings。
- `uv pip check` 通過；PowerShell 語法解析通過。
- `scripts/world.ps1 -Mode build` 通過，最終紀錄：`C:/Toolchains/game-ecosystem/runs/build-20261020-101040-89493687/build.log`。
- 移除不存在的 `com.unity.modules.inputlegacy` 套件宣告，提交 Unity 解析所得的 `packages-lock.json`、場景及材質。
- 往返驗證限制實際世界步數，避免 Unity 收到關閉訊號後、退出前額外執行的步數灌入吞吐量。512 步要求與實際步數一致。
- 透過視窗截圖確認可見場景有邊界與個體；Python 動作造成位置變化，並有碰界、輪次結束與重建紀錄。鍵盤模式可開啟，短按自動化未捕捉到移動，方向鍵持續按壓仍保留人工核對步驟。
- 可見與無畫面使用相同種子 1，512 步的狀態、事件、輪次與結束原因逐筆相同（排除各次 run-id 與裝置資訊）。
- 尚無專案既有測試套件，未新增測試檔；以上透過實際世界入口驗證。

## 實測結果

| 執行 | 結果 |
| --- | --- |
| 無畫面 bridge | 512 步／0.5481 秒，約 934.15 步／秒，Null Device |
| 可見 bridge | 512 步／10.2627 秒，約 49.89 步／秒，RTX 3070 Laptop GPU 繪圖 |
| CPU PPO | 6 次策略更新、90 次 optimizer 更新，更新計時共 0.4633 秒 |
| PPO 損失證據 | step 512：Policy Loss 0.110485、Value Loss 0.001885；step 2048：0.072833、0.000504 |
| PPO 初步吞吐量 | summary step 256→2048 用時 5.300 秒，約 338.1 步／秒；不含啟動及最後匯出 |
| PPO 完整 trainer 計時 | 約 25.149 秒；2,048 是停止門檻，最後 checkpoint 為 2,112 步，來自 rollout 批次邊界 |

Python 訓練使用 `torch-device=cpu`，PyTorch 為 `2.5.1+cpu`、CUDA false；GPU 僅用於可見 Unity 繪圖。未量測 CPU／GPU 百分比使用率。bridge 吞吐量含 Python 通訊及 JSONL 寫入，不含程序啟動／退出；只有單次小場景量測，不能推估完整生態效能。

證據目錄位於 `C:/Toolchains/game-ecosystem/runs`，相同名稱已複製至歷史目錄：

- `bridge-20261020-101112-4ced4c4f`：無畫面 `bridge.json`、`world.jsonl`。
- `bridge-20261020-101121-cfde1b46`：可見 `bridge.json`、`world.jsonl`。
- `train-20261020-101139-7ab522ae`：世界紀錄；同名子目錄內含 TensorBoard、`run_logs/timers.json`、checkpoint 與 ONNX。`_update_policy.count=6`、`TorchPPOOptimizer.update.count=90`，並非僅以模型檔存在推定成功。

2026-10-19 的首次建置曾因無授權失敗，Hub 自動安裝曾被拒絕；2026-10-20 使用者安裝 Hub 後授權阻擋解除。重現步驟見 [README](../README.md)。

## 一般 PowerShell 的 Python 路徑修復

2026-10-20 使用者回報虛擬環境出現 `No Python at ...`。原 `pyvenv.cfg` 的 home 指向 `C:/Users/User/AppData/Roaming/uv/python/cpython-3.10.12-windows-x86_64-none`；Codex 執行環境能讀取，但一般 PowerShell 的 `Test-Path` 為 False。

透過 Windows `GetFinalPathNameByHandleW` 確認，該檔案實際位於 `C:/Users/User/AppData/Local/Packages/OpenAI.Codex_2p2nqsd0c76g0/LocalCache/Roaming/uv/python/cpython-3.10.12-windows-x86_64-none/python.exe`。問題是應用程式的 AppData 重新導向，與帳號名稱或 Python 套件缺失無關。

當時將完整基底複製到共用 `C:/Toolchains/game-ecosystem/cpython-3.10.12-windows-x86_64-none`，更新既有虛擬環境的 home，保留套件；原設定備份為 `C:/Toolchains/game-ecosystem/runs/pyvenv-before-shared-runtime.cfg`。複製前 C 槽剩餘約 70.3 GiB，基底大小約 111.1 MiB。新執行檔的 Windows 實際路徑確認仍為 `C:/Toolchains`，Python 3.10.12、PyTorch 2.5.1+cpu 匯入及 42 個套件的相依檢查通過。後續再依使用者要求遷入專案，現行配置見上表。

修復後透過 Windows PowerShell 5.1 完成可見 bridge 512 步，輸出為 `C:/Toolchains/game-ecosystem/runs/bridge-20261020-145451-63788c10`；使用者亦在原本出錯的 PowerShell 視窗重跑 `./scripts/world.ps1 -Mode bridge -Visible`，確認可以正常執行。

## PowerShell 腳本編碼

含繁體中文的 `.ps1` 檔案須保存為 UTF-8 BOM，遵循根目錄 `.editorconfig`。無 BOM 時，Windows PowerShell 5.1 可能以碼頁 950 讀取，導致中文亂碼及連鎖解析錯誤；此時尚未啟動 Unity，與虛擬環境無關。

修改啟動腳本後執行 `powershell.exe -NoProfile -File scripts/check-world-script.ps1`，以 Windows PowerShell 實際解析檔案；亦可在 PowerShell 7 執行 `./scripts/check-world-script.ps1`。人工確認方式：在原本的 PowerShell 視窗執行 `./scripts/world.ps1 -Mode forage -Visible`，確認世界視窗開啟後關閉。
