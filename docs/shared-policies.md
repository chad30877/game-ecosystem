# 三物種共享策略與動態個體驗證

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

驗證日期：2026-10-20。任務 02 通過三物種共享策略、動態個體、無畫面與關閉後續訓的工具能力驗證；後續任務 08 的此項前置阻擋解除，其餘依賴仍依任務追蹤器判定。

## 連線與共享

- Unity 6000.0.85f1、ML-Agents 4.0.0、Python 套件 1.1.0、PyTorch 2.5.1+cpu；沿用[任務 01 的工具配置](toolchain.md)。訓練使用 CPU，可見場景使用 RTX 3070 Laptop GPU 繪圖。
- 每物種兩隻起始，`Flame?team=0`、`Frost?team=0`、`Moss?team=0` 各自只有一個 behavior／trainer，每個 behavior 同時接收多隻個體的觀察與動作。同種共用一份 checkpoint，跨物種有三份不同的 Policy 參數；相同 YAML 超參數不代表共享模型參數。
- `trainer.log` 每次只出現三次 `Connected new brain`；訓練期間持續增減個體，沒有因移除或新增個體重新建立 trainer。三物種 TensorBoard 步數持續累加，並有實際 Policy／Value Loss。

## 動態個體與無畫面

每輪第 64 步各移除一隻，第 96 步各新增一隻，第 256 步重建。事件可由 `-WorldConfig` 指定的 JSON 調整。移除透過停用 Agent 傳送最後觀察與 terminal，再刪除物件；不呼叫個體 `EndEpisode` 立即復活。

Python 實際核對每個 behavior 的存活數為 2→1→2、消失的 decision-id 都有 terminal、terminal-id 不再出現、不同物種不混用識別。世界個體編號跨輪次不重用；完整識別為 run-id 加個體編號，ML-Agents agent-id 另記在 `connection.jsonl`。

| 執行 | 結果 |
| --- | --- |
| 無畫面 bridge | 512 世界步，0.8255 秒，約 620.23 步／秒，Null Device |
| 可見 bridge | 512 世界步，10.2674 秒，約 49.87 步／秒，RTX 3070 Laptop GPU |
| 每次連線檢查 | 2,886 筆個體觀察、24 個不同 agent-id、18 個 terminal-id；包含最後一步重建後的觀察 |
| 一致性 | 排除 run-id、裝置及 Unity 物件識別後，兩次世界狀態與事件逐筆相同 |

Python 向同種不同個體傳送不同方向的動作，三物種皆有實際位置變化。一個世界步同時推進所有個體，吞吐量沒有用個體經驗筆數冒充世界步數。量測包含 Python 通訊與 JSONL 寫入，不含程序啟動／退出，不代表完整生態效能。

另透過 `-WorldConfig` 改為每物種三隻、第 16／32 步移除／加入、第 128 步重建，256 世界步驗證通過：族群符合 3→2→3，收到 33 個不同 agent-id、24 個 terminal-id，證實事件時機及起始族群可由入口調整。輸出為 `bridge-20261020-144550-2613c48d`。

## 更新與恢復

首次訓練設定每物種累計 2,048 筆經驗；關閉後以原始 trainer 目錄及 `-TrainSteps 4096 -ResumeFrom ...` 啟動新程序。入口複製原始 trainer 資料至新輸出，原 checkpoint 保留。

| 物種 | 首次 checkpoint → 續訓 checkpoint | Adam 計數 | 續訓變更的 Policy tensor 數 | 首次 step 2048 Policy／Value Loss |
| --- | --- | --- | --- | --- |
| Flame（炎角獸） | 2112 → 4160 | 84 → 168 | 7 | 0.103663／0.000303 |
| Frost（霜羽獸） | 2112 → 4160 | 84 → 168 | 7 | 0.091127／0.000299 |
| Moss（苔靈） | 2112 → 4160 | 84 → 168 | 7 | 0.095687／0.000673 |

- 續訓日誌三次明確記錄 `Resuming training from step 2112.`，沒有缺失參數或載入失敗警告。checkpoint 內的 `global_step`、optimizer state 及 Policy tensor 均已直接核對。
- 首次及續訓各有 18 次策略更新、252 次 optimizer 更新（各物種 Adam 每次訓練增加 84）；證據來自 `run_logs/timers.json`。三物種 Policy 逐組比較均不同。
- 首次訓練記錄 1,167 世界步、45 次加入、15 次移除；續訓記錄 1,101 世界步、42 次加入、15 次移除。最後關閉前的世界步數可能受程序關閉時機影響，與 per-species 經驗門檻不同。
- 續訓是新 run-id、新世界第一輪；僅模型參數、optimizer 與累計訓練步數恢復，不承諾精確恢復原世界、rollout buffer 或隨機數序列。
- `TrainSteps` 小於既有 checkpoint 步數時已實測拒絕，避免把未更新就退出視為續訓成功。

## 證據位置與重現

任務 02 當時輸出位於 `C:/Toolchains/game-ecosystem/runs`；任務 13 已將下列同名目錄遷至專案內 `artifacts/runs`，原始紀錄中的歷史路徑保留：

- `bridge-20261020-144125-edfdfda8`：無畫面世界、連線序列與摘要。
- `bridge-20261020-144137-26e1f863`：可見世界、連線序列與摘要。
- `train-20261020-144209-d9309d5d`：首次三物種 PPO，trainer 子目錄與外層同名。
- `train-20261020-144236-ced3483d`：關閉後續訓，trainer 子目錄沿用 `train-20261020-144209-d9309d5d`。
- `shared-policies-evidence.json`：checkpoint、Adam、tensor 比較、更新計時及畫面一致性的彙整。

加入續訓門檻檢查後，再次使用相同來源成功恢復至 4,160 步，輸出為 `train-20261020-144343-ed828e59`。上述輸出與彙整已複製至 `D:/TrainingHistory/game-ecosystem/shared-policies-20261020`；封存前 C／D 槽分別約剩 70.3／616.5 GiB。

重現命令與人工核對步驟見 [README](../README.md)。使用相同配置與種子，先執行可見及無畫面 `bridge`，再執行 `train`、等程序結束後使用它的 trainer 目錄續訓；核對三套模型的恢復步數及後續更新。

## 檢查與限制

Unity 建置通過；Python compileall、Pyright（0 errors、0 warnings）、套件相依與 PowerShell 語法檢查通過。沒有專案既有測試套件，未新增測試檔；以實際入口檢查外部行為。

以使用者確認的 `4f8490c` 為固定比較點完成平行 code-review：Standards 0 項、Spec 0 項可行動發現。

本次只驗證小型移動場景、局部邊界感知、可設定的增減事件與相同模型結構的恢復。不包含食物、繁殖死亡規則、技能、正式續訓介面或學習品質評估；未驗證物種完全滅絕後重返、長時間穩定性、大人口效能及跨裝置逐影格一致性。

ML-Agents 仍顯示 `pkg_resources` 棄用及 PyTorch `torch.load` 未來預設變更警告；沿用鎖定版本能完成本次往返、更新與恢復，尚未進行工具升級驗證。
