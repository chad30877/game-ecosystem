# 11 新規則記錄、回放與舊版相容

## Status

resolved

執行授權：2026-10-30 使用者已以 implement-spec 授權實作；全部前置 resolved 後才可 claimed，驗收通過後才可 resolved。

## What to build

為新規則保存完整配置、果實流轉、個體操作與當時資訊；回放依快照還原並支援診斷，同時保留 v9–v14 原有讀取語意。

需求依據：[正式規格](../spec.md) S06、S09.1、S10。同時遵守規格 S08 的範圍與變更邊界。

## Blocked by

- [02 合法生長空間、起始配置與天然補生](02-source-placement.md)
- [04 雙方交付、地面拾取與掉落](04-delivery-ground.md)
- [06 持續培育與合法機率播種](06-cultivate-sow.md)
- [07 果實感知與有限記憶](07-observation-memory.md)
- [08 採食、送果、接收與逃生減重 AI](08-food-care-ai.md)
- [09 局部供給、播種與培育選擇 AI](09-production-ai.md)
- [10 來源與果實外觀及選取資訊](10-visual-observation.md)

## 驗收條件

- [x] 新規則有不同於 survival-v14 的識別，規則版本與格式版本分開；配置、種子、動作及狀態契約對齊，舊格式不靜默誤讀。
- [x] 快照包含來源階段、果實位置／持有／保存狀態、暫占與預留、操作、培育及個體當時情報；診斷區分真值與推測。
- [x] 流量摘要涵蓋產果、吃掉、腐爛、交付、掉落、到期損失及人口／來源變化，能核對守恆與決策原因。
- [x] 新回放多次前後跳轉與原始快照一致，不重新感知或重跑世界；保留完整性與錯版拒絕檢查。
- [x] v9–v14 代表回放仍按歷史呈現，缺少新資訊標示未提供，不造果實、不把新規則套回歷史。
- [x] 更新必要入口／bridge／版本相容契約並驗證；不新增 PPO 或混合控制實驗，不默認舊模型支援新動作。

## 討論紀錄

- 2026-10-30：依已確認共同理解發佈本機任務；目前僅文件準備完成，尚未實作。
- 2026-10-30 後續政策：使用者回報相容性問題後選擇暫不修復，另確認[產物記錄與同建置回放規格](../../artifact-recording-and-retention/spec.md)，先不實作。其未來政策退役 v9–v14 專用相容分支；本票 resolved、原驗收勾選與完成證據保留為歷史結果，不宣稱本次已重現或修復回報問題。

## 完成紀錄

- 2026-10-30：完成 `survival-v15` 規則、動作版本 2、回放格式 2；`WorldRules` 提供 C# 識別，run、封裝器、回放、模型簽章、指定場景與入口同步。舊 v9–v14 格式 1 保留讀取，舊模型明確拒絕。規則與格式各自驗證，錯版與內容校驗失敗仍拒絕。
- 新回放原樣保存完整 `WorldState`：來源週期、果實唯一識別／位置／持有／保存、暫占、預留、操作、培育、帳與當時觀察均不重算。新規則快照須有 `fruitInformationAvailable=true`；歷史讀取強制 false 並清除新果實資訊，保留原來源存量、個體資源及 v14 煙幕。回放診斷包裝以原始規則／動作版本標記資料，另存檢視器 `runtimeRules/runtimeActionVersion` 與來源 `replayRules/replaySchemaVersion/replayActionVersion`，歷史未提供的動作版本為零。
- ML-Agents 契約改為四個連續值、七分支 `3,3,2,2,2,7,8`，明確食物操作及相對果實選擇由已觀察 DTO 選目標；舊六分支明確拒絕，沒有讀精確保存真值選果。手動 F 在一次按住期間固定送／收角色，避免所有個體共用輸入時無法配對或接收後立即回送；G 拾取、C 進食、X 放下，Space 保留採集／進食便利操作。
- 舊 `feedChild`、`setAmount` 明確拒絕。場景的 `setElapsedAge` 重建週期及果實保存，來源搬移檢查生長圓；新增逐顆 `fruits`、`foodAction/fruit/recipientSlot/productionSource` 契約。來源果實初始覆寫仍受容量限制，轉為已成熟等待，不再追加該輪果數；這是診斷覆寫，不改正式世界或新生無果規則。
- `SingleEpisode/SummaryLog/MetricsLog` 暴露既有執行選項；run 保存步數及旗標，ReloadRun 沿用未明確覆寫項。摘要包含實際果實帳與來源／攜帶／地面存量、人口及低飽食數；每步 metrics 保存決策樣本、培育階段及實際累計費用。`lastProductionStopReason` 是最後停止理由樣本，不逐步當新停止事件累加。
- TDD 紅／綠：新格式封裝、新規則原樣跳轉、舊直接餵養拒絕、舊連續存量拒絕、來源年齡重建、逐顆場景放下／拾取／吃完、七分支輸入、來源覆寫容量與搬移合法空間，均先重現失敗再修正。公開驗收入口為 `Ecosystem.Editor.ValidateFruitReplay`，沒有測試私有視圖或模擬內部合作物件。
- 建置與回歸：`Unity.exe -batchmode -nographics -quit -projectPath <專案>/unity -executeMethod Ecosystem.Editor.ValidateFruitReplay.BuildAndValidate --legacy-list <清單> -logFile <證據>/build-contracts.log` 退出碼 0，包含 01–09／煙幕、資料契約及六版歷史讀取。回放包裝來源版本補強後另以 `BuildWorld.Build` 建置，`metadata-build.log` 退出碼 0，再核對新舊 Player；沒有重跑無關機制測試。
- 實際必要 bridge：`training/bridge.py --env unity/Builds/World.exe --config training/world.json --steps 512 --seed 101 --output <新目錄>` 完成 512 世界步、2886 筆觀察、18 個個體與 12 次 terminal；三物種皆移動，七分支與十九／十維觀察契約通過。未執行 PPO。
- 實際指定動作使用 `training/scenarios/fruit-flow-world.json`／`fruit-flow.json`、150 步、SingleEpisode、SummaryLog、MetricsLog、RecordEpisodes 1：交付後雙方飽食仍 60／60，吃完為 60／80；交付一、吃掉一、守恆零，151 張原始快照。ReloadRun 的 151 張狀態完全一致；新回放 0→1→50→150→50→1→0 七張讀回快照逐張與原始狀態一致。四筆摘要及 150 筆 metrics 的帳與標籤核對通過。
- 歷史相容：Python 與 C# 均核對實際 v9–v14 代表回放，影格數為 1001／51／81／51／36／651；實際 Player 逐版前後跳轉共十九張，原來源存量、階段、位置與個體健康／營養等原值保留，新果實標記 false，v14 含煙幕的影格原樣保留。來源清單與結果保存 `legacy-validation.json`、`legacy-player-validation.json`，不拿 10 號過渡標頭診斷檔作歷史代表。
- 10 號外觀快照已用當次 v15 實際標頭與正式封裝器重新產生 `showcase-v15.replay.jsonl`（格式 2、`diagnostic=true`）；0→1→2→1→0 的五張 Player 快照與原診斷狀態完全一致。版本化打包入口為 `training/fruit_recording_validation.py --showcase-folder ... --run-log ... --output ... --seed 101`。
- `.ps1` 保留 UTF-8 BOM；`powershell.exe -NoProfile -File scripts/check-world-script.ps1` 實際 5.1 解析通過。Python 語法編譯、`fruit_recording_validation.py` 的封裝／校驗／錯版檢查及 `git diff --check` 通過。
- 正式契約與摘要見 [資料契約](../../../docs/fruit-data-contract.md)、[結果](../../../docs/fruit-data-contract-results.json)。原始紅綠、建置、bridge、指定動作、重載、快照及歷史證據完整複製至主專案 `artifacts/fruit-supply/11/`；不依賴清理後的 worktree。六場生態及跨模組最終整合仍由 12 執行。
- 人工核對：先建置，再依資料契約的 `fruit-flow` 命令跑指定情境，查看一秒轉果不補營養、兩秒吃完才加二十；以 `-Mode replay -ReplayFrom <episode-1.replay.jsonl> -Visible` 前後拖動，並開啟任一歷史代表核對「果實資訊未提供」。主世界 `-Controller manual -Visible` 可按住 F 配對送／收，放開重按重選角色，核對 G／C／X 與 Space。
