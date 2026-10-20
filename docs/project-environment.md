# 專案內環境紀錄

2026-10-20 使用者要求撤除本專案對自訂 `Toolchains` 目錄的依賴，並確認 Editor 也放進專案、登錄至 Unity Hub。此安排取代先前 C 槽活躍工具及輸出的配置。

## 目錄

| 相對於專案根目錄 | 用途 |
| --- | --- |
| `.python/cpython-3.10.12-windows-x86_64-none` | Python 3.10.12 執行檔及標準函式庫 |
| `.venv` | 依 `training/requirements-lock.txt` 重建的 42 個專案套件 |
| `.tools/unity/6000.0.85f1` | 本專案使用的 Unity Editor，已登錄至 Hub |
| `artifacts/runs` | 活躍訓練與往返紀錄，包含原 C 槽同名輸出 |
| `artifacts/cache/uv` | 原 D 槽套件快取 |
| `artifacts/downloads` | 原 D 槽 Unity 安裝檔 |
| `artifacts/migration` | 本次複製及驗證紀錄 |

以上目錄全部由 `.gitignore` 排除。原 `D:/TrainingHistory/game-ecosystem` 的獨立歷史封存繼續保留；既有實驗紀錄中的舊絕對路徑不改寫。

Python 標準函式庫仍是 `.venv` 的必要依賴，但不再存於外部共用位置；`pyvenv.cfg` 的 home 指向專案內 `.python`。套件透過鎖定檔重新安裝，未直接搬動帶有舊路徑的虛擬環境。

遷移前 C／D 槽分別剩約 70.1／616.5 GiB。原 Python 基底約 111 MiB、虛擬環境約 1.28 GiB，容量足夠。複製紀錄保留於 `artifacts/migration`，原始訓練輸出於清理前比對內容。

## 執行與重建

入口使用 `$PSScriptRoot` 定位專案，預設讀取專案內 Editor／Python 並寫入 `artifacts/runs`。可繼續使用 `-Editor`、`-Python`、`-OutputRoot` 覆寫。安裝命令及人工步驟見 [README](../README.md)。

`.venv` 不是可任意搬動的獨立 Python 安裝。若改變專案完整路徑，須重建 `.venv` 並重新登錄 Hub 的 Editor 路徑；Git clone 後也須重新準備環境。

## PowerShell 相容性

腳本保留 UTF-8 BOM，支援 Windows PowerShell 5.1 與 PowerShell 7。實際訓練驗證另發現並修正兩項差異：

- PowerShell 5.1 將原生程式 stderr 合併至管線時，套件警告會在 `ErrorActionPreference=Stop` 下終止腳本。現改由 stdout 保存訓練日誌，stderr 直接顯示，仍以程序退出碼判定成功。
- PowerShell 5.1 的 `Set-Content -Encoding utf8` 會加入 BOM，ML-Agents 因而將第一個 YAML 欄位讀成錯誤名稱。設定檔與日誌改以 .NET 明確寫入無 BOM 的 UTF-8，避免 PowerShell 版本影響檔案格式。

## 驗證結果

以下輸出均位於 `artifacts/runs`，並以 Windows PowerShell 5.1 執行：

| 驗證 | 證據 |
| --- | --- |
| 專案內 Editor 建置 | `build-20261020-150957-c3fd3ecb/build.log` 成功 |
| 可見往返 | `bridge-20261020-150448-28315f8d`，512 世界步、24 個不同 agent-id、18 個 terminal-id |
| 無畫面往返 | `bridge-20261020-151042-2f40ae88`，512 世界步、約 827 步／秒 |
| 專案內 `.venv` 從零訓練 | `train-20261020-150725-71a15070`，三物種 checkpoint 各為 2,112 步 |
| 遷移後的舊 checkpoint 續訓 | `train-20261020-151131-5c5c88e3`，三物種各從 2,112 到 4,160 步，Adam 計數從 84 到 168 |

續訓使用遷移前已保存的 `train-20261020-144209-d9309d5d`，三物種 Policy 參數皆繼續改變。`artifacts/migration/training-verification.json` 保留 checkpoint／optimizer 核對結果；新輸出的 JSON、YAML、日誌確認可直接以 UTF-8 讀取。

複製完整性：135 個原始輸出檔案的 SHA-256 逐一相同；46,785 個 Editor 檔案及 4,015 個 Python 檔案逐一核對存在與大小，結果見 `artifacts/migration/inventory.json`。Windows 長路徑以延伸路徑格式核對。

Python compileall、Pyright（0 errors／warnings）、42 個套件的相依檢查、PowerShell 5.1／7 語法解析通過。沒有專案既有測試套件，未新增測試檔。首次建置因與執行驗證重疊造成檔案鎖定失敗，停止執行後依序重跑建置通過。

人工核對：在一般 PowerShell 執行 `./scripts/world.ps1 -Mode bridge -Visible`，確認三色個體移動與增減，結果寫入 `artifacts/runs`；需要續訓時依 README 指定該目錄內的 trainer 路徑。

使用者已確認上述可見場景正常。舊位置退出後，另從 `D:/` 執行腳本並以相對 `-OutputRoot` 指向專案輸出，成功完成 128 世界步，證據為 `bridge-20261020-151633-a944506a`。

## 舊位置處理

遞迴刪除舊目錄被自動核准審查以 `blocked by policy` 拒絕，未提供更具體理由；改採可還原的封存，將原 `C:/Toolchains` 移入 `artifacts/migration/previous-toolchains`。此目錄僅供回復及人工清理，不是執行入口。

原 `D:/Toolchains/downloads` 的安裝檔與快取已遷入上表目錄，舊下載目錄僅剩空資料夾。先前存於 Codex AppData 的 Python 3.10.12 已透過 `uv python uninstall` 解除安裝；專案內 Python 及套件檢查仍通過。
