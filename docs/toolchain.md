# 工具鏈驗證紀錄

檢查日期：2026-10-19。任務 01 尚未完成；Unity 授權阻擋實際場景建置與訓練。

## 版本與相容性

| 工具 | 選用版本 | 狀態 |
| --- | --- | --- |
| Unity Editor | 6000.0.85f1，ee7790f1426a | 已安裝；批次建置因無授權失敗 |
| Unity ML-Agents | 4.0.0（Release 23） | manifest 已指定，尚未完成 Editor 套件解析與編譯 |
| Python | CPython 3.10.12 | 已安裝，獨立虛擬環境 |
| mlagents／mlagents-envs | 1.1.0 | 已安裝，訓練 CLI 可啟動 |
| PyTorch | 2.5.1+cpu | 已安裝；CUDA false，未使用 GPU 訓練 |
| setuptools | 80.9.0 | 避免新版移除 `pkg_resources` 導致 ML-Agents 啟動失敗 |
| huggingface-hub | 0.27.1 | 固定相依版本，完整解析見 requirements-lock.txt |

[Unity Release 23](https://github.com/Unity-Technologies/ml-agents/releases/tag/release_23_tag)對應 Unity 套件 4.0.0 與 Python 套件 1.1.0。[官方安裝說明](https://docs.unity.cn/Packages/com.unity.ml-agents%404.0/manual/Installation.html)要求 Unity 6000.0 以上；[Python 套件宣告](https://github.com/Unity-Technologies/ml-agents/blob/release_23/ml-agents/setup.py)要求 Python 3.10.1–3.10.12。這些是版本配對依據，不等於本機整合已通過。

Editor 安裝檔由 Unity 官方 release API 取得，Authenticode 簽章為 Valid、簽署者為 Unity Technologies SF；靜默安裝結束碼為 0。只安裝 Editor 內含的 Windows Standalone Mono 支援，沒有追加 Android、WebGL、IL2CPP 或 Visual Studio。

## 實際位置與磁碟

| 用途 | 路徑 |
| --- | --- |
| 唯一活躍來源與 Unity Library 快取 | `D:/Projects/game-ecosystem`、`unity/Library` |
| Editor | `C:/Toolchains/Unity/6000.0.85f1/Editor/Unity.exe` |
| Python 執行環境 | `C:/Toolchains/game-ecosystem/python` |
| Python 基底 | `C:/Users/User/AppData/Roaming/uv/python/cpython-3.10.12-windows-x86_64-none` |
| 活躍輸出 | `C:/Toolchains/game-ecosystem/runs` |
| 安裝下載與 uv 套件下載快取 | `D:/Toolchains/downloads` |
| 歷史輸出預留位置 | `D:/TrainingHistory/game-ecosystem`（目前為空） |

安裝前 C 槽剩餘 105,957,982,208 bytes（約 98.7 GiB），D 槽剩餘 668,849,696,768 bytes（約 622.9 GiB）。安裝後量測 C 槽約 89.8 GiB，仍高於原規劃 25–30 GB 餘裕。

`Get-Partition`、`Get-Disk` 與 `Get-PhysicalDisk` 實測 C 對應 Micron 2450、D 對應 ADATA LEGEND 900，兩者均為 NVMe SSD。因此保留 D 槽現有專案，沒有另複製一份活躍來源。下載與歷史輸出已與活躍環境分開，但本機未發現 HDD，不能宣稱已完成實體 HDD 配置；若另接 HDD，再指定歷史與下載目錄。此實測更正先前環境預檢中的使用者回報，不改寫歷史紀錄。

## 已執行檢查

- Python 語法編譯通過；Pyright 對 `training/bridge.py` 回報 0 errors、0 warnings。
- `uv pip check` 通過；`mlagents-learn --help` 可正常啟動。這些不代表訓練更新成功。
- PyTorch 回報 `2.5.1+cpu`、`cuda=False`、預設 12 個執行緒。Unity 繪圖 GPU 與正式場景的 CPU／GPU 使用尚未量測。
- `scripts/world.ps1 -Mode build` 實際呼叫 Editor，因授權失敗退出，沒有產生可執行場景。紀錄：`C:/Toolchains/game-ecosystem/runs/build-20261019-175224-d3c0fcae/build.log`。
- 主要授權錯誤為 `Access token is unavailable`、`Found 0 entitlement groups and 0 free entitlements`、`com.unity.editor.headless was not found`。
- 尚無既有測試套件，未新增測試檔。C# 編譯、可見移動、無畫面往返、實際 PPO 更新及吞吐量均待授權後驗證。

## 接續方式

手動安裝 [Unity Hub](https://unity.com/download)，登入並啟用適用授權，在 Installs 加入上述已安裝 Editor。此次自動下載及安裝 Hub 的操作被自動核准審查以 `blocked by policy` 拒絕，未執行；未嘗試繞過。

完成授權後依 [README](../README.md) 執行建置、可見與無畫面 bridge、PPO 訓練，檢查損失統計與更新計時，再記錄 CPU 吞吐量。首輪編譯及執行仍可能需要修正，不能將目前來源視為已測通。

歷史封存時只複製已結束的執行目錄至 `D:/TrainingHistory/game-ecosystem`；目前沒有成功訓練成果可封存。
