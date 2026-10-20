# 02：三物種共享策略與動態個體驗證

**Status:** resolved

**What to build:** 在最小場景中讓三種策略各自控制多個同類個體，並在訓練期間加入、移除個體及保存恢復訓練。

**Blocked by:** 01：工具鏈與最小訓練往返。

## 驗收條件

- [x] 三物種策略分離，同種個體參數共享；記錄實際連線與更新證據。
- [x] 透過可操作的加入／移除事件驗證個體出生死亡的訓練介面；移除一隻不會重設同種策略，個體識別不混用。
- [x] 小規模實驗可保存後重新啟動並接續訓練；此處僅驗證工具能力，不代替完整產品續訓操作。
- [x] 驗證報告列出無畫面、動態個體及恢復支援的結果與限制；若未通過，後續正式學習實作保持被阻擋。

## 驗證方式

沿用規格提出的世界執行入口，核對可觀察結果並提供人工操作步驟；保留及執行屆時存在的有效測試。本任務不授權新增測試檔，若需要新增須先取得使用者同意。

## 完成紀錄

- 2026-10-20：前置 01 已 resolved，開始實作並完成本任務。三物種各兩隻起始，以 Flame／Frost／Moss 三個 behavior 共享各自策略。
- 建立共用世界時鐘、可設定的每物種移除／加入事件、跨輪次遞增的個體識別，以及 Python decision／terminal 連線證據。移除個體不重建 trainer。
- 可見與無畫面各完成 512 世界步，狀態與事件序列相同；三物種皆有移動。每次收到 24 個不同 agent-id、18 個 terminal-id，個體數符合 2→1→2。
- 三套 PPO 各從 2,112 步恢復至 4,160 步，Adam 計數各由 84 延續至 168，策略參數繼續改變。原始輸出保留；停止門檻低於 checkpoint 時拒絕啟動續訓。
- Unity 建置、Python 語法及型別、相依與 PowerShell 語法檢查通過；沒有專案既有測試套件，未新增測試檔。
- 證據與限制見 [共享策略驗證紀錄](../../../docs/shared-policies.md)，人工操作見 [README](../../../README.md)。任務 08 的本項前置阻擋解除，不代表其餘前置任務已完成；尚未驗證學習品質。
- 以 `4f8490c` 為固定比較點完成平行雙軸 code-review：Standards 及 Spec 均為 0 項可行動發現；另以每物種三隻的自訂配置驗證增減事件入口通過。

## 討論紀錄

- 2026-10-20：使用者回報 Windows PowerShell 執行 `bridge -Visible` 出現解析錯誤。已於 PowerShell 5.1 重現：無 BOM 的 UTF-8 腳本被 CP950 誤讀，產生 12 個解析錯誤；同內容明確以 UTF-8 解析為 0 個。為腳本加入 UTF-8 BOM，並以 `.editorconfig` 設定 PowerShell 檔案編碼。
- 修正後 PowerShell 5.1／7 語法解析皆為 0 個錯誤；透過 `powershell.exe -NoProfile -NonInteractive -File ./scripts/world.ps1 -Mode bridge -Visible` 實際完成 512 步、24 個不同 agent-id、18 個 terminal-id，輸出為 `C:/Toolchains/game-ecosystem/runs/bridge-20261020-144931-f67a1200`。人工可直接重跑使用者原本指令；本次未新增測試檔。
