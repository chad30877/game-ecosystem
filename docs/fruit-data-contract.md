# 果實規則資料契約

目前規則為 `survival-v15`，動作版本為 `2`，回放格式為 `2`。三者分開記錄；v9–v14 的格式 1 回放仍可讀取，不能用來重載新世界或控制新動作。舊模型相容性簽章會明確拒絕，未新增 PPO 實驗。

## 動作入口

`WorldAction` 保留 `forage=true` 的採集／進食便利操作，明確操作使用 `foodAction`、`fruit`、`recipient`；培育可用 `productionSource`。交付的 `recipient` 是接收者，接收的 `recipient` 是送方。`feedChild` 舊直接餵養入口會拒絕。

ML-Agents 使用四個連續值及七個離散分支，分支大小為 `3,3,2,2,2,7,8`。依序為轉向、技能欄、攻擊、採食便利操作、奔跑、食物操作、果實選擇。食物操作依序為 None／Gather／Eat／Pickup／Deliver／Receive／Drop；果實選擇 0 為自動，1–7 選取觀察清單候選。目標依瞄準位置從可見觀察選取，不能用此介面查詢隱藏真值。自身觀察仍十九維，實體列仍十維；新果實列型別見 [07 觀察契約](../.scratch/fruit-supply-and-flow/issues/07-observation-memory.md)。

手動 Space 採集／進食、G 拾取、C 進食、X 放下。按住 F 時，持果者送出、空手者配合可見邀請接收；角色在本次按住期間固定，放開 F 再按才重新判斷，避免接收後立刻送回。其餘移動與技能鍵沿用。

## 指定情境

場景使用 `actionVersion: 2`。舊 `setAmount` 與 `feedChild` 會明確拒絕。`setElapsedAge` 會重新推算來源週期及現有果實，沒有重新保鮮。來源搬移仍須符合完整生長圓合法空間。

`fruits` 是診斷情境的逐顆初始覆寫：可指定 `fruit` 識別、`source` 或 `holderSlot`，兩者皆零則使用地面 `x/y`；`ageSeconds` 是已經過的保存時間，必須小於四十秒。指定來源的果實清單覆寫該來源現存果實，作為已成熟、等待期限的一輪，不再追加該輪額度。攜帶容量及來源容量仍受限；這類診斷覆寫不代表正式世界會出生帶果。

指令使用 `foodAction`、`fruit`、`recipientSlot` 及 `productionSource`。`recipientSlot` 會轉成該次世界真正的個體識別，不能假定 slot 等於個體編號。範例為 `training/scenarios/fruit-flow-world.json` 與 `fruit-flow.json`。

```powershell
./scripts/world.ps1 -Mode forage -WorldConfig training/scenarios/fruit-flow-world.json -Scenario training/scenarios/fruit-flow.json -Steps 150 -SingleEpisode -RecordEpisodes 1
```

此診斷場景核對一秒交付、兩秒進食；送方／接收者起始各六十，交付不加飽食，吃完後接收者八十。來源與繁殖均隔離，不作生態平衡證據。

## 記錄與回放

完整快照原樣保存來源週期、果實期限／位置／持有者、暫占、容量預留、操作、培育及當時的粗略觀察。回放只讀快照，不重新感知或決策。世界真值與個體最後觀察／推測分區呈現。

`fruitInformationAvailable=true` 是新規則快照的必要標記。舊回放強制為 false，保留原食物外觀，新資訊顯示未提供，不補造果實。v14 的煙幕情報保留，新規則也保留煙幕情報。

`SingleEpisode` 在第一輪終止時結束；`SummaryLog` 保存事件、果實帳及存量／人口摘要，`MetricsLog` 另存每步個體營養、決策及培育樣本。`lastProductionStopReason` 是最近一次停止理由的樣本，不可逐步當作新停止事件累加；實際費用使用事件及累計 `skillManaSpent`，培育階段可由 `cultivatingFood/cultivateStartedAt` 核對。原始 run 保存步數、單場與記錄旗標，ReloadRun 沿用未明確覆寫的旗標。


固定外觀診斷快照可用 `ValidateFruitVisuals.WriteFixture` 匯出，再以 `training/fruit_recording_validation.py --showcase-folder <快照目錄> --run-log <當次 v15 執行紀錄> --output <新回放> --seed 101` 封裝。此工具透過正式封裝器產生格式 2，明確標記 `diagnostic=true`，不能作為生態試跑或自然結束證據；10 號階段的過渡 v14 標頭診斷檔不是歷史代表回放。

回放診斷執行的 `rules/actionVersion` 跟隨原始回放；另以 `runtimeRules/runtimeActionVersion` 記錄檢視器版本，`replayRules/replaySchemaVersion/replayActionVersion` 保留來源識別。歷史回放未提供的動作版本為 0，避免把 v9 快照誤認成 v15 模擬結果。

驗收摘要見 [資料契約結果](fruit-data-contract-results.json)，原始證據保存在 `artifacts/fruit-supply/11/`。
