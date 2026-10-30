# 02 合法生長空間、起始配置與天然補生

## Status

resolved

執行授權：2026-10-30 使用者已以 implement-spec 授權實作；全部前置 resolved 後才可 claimed，驗收通過後才可 resolved。

## What to build

讓起始來源、天然補生與後續播種共用合法空間判定，完成正式世界十二小四大及天然七三補生；對施法呼叫端保留合法類型判定。

需求依據：[正式規格](../spec.md) S02、S09.4–5。同時遵守規格 S08 的範圍與變更邊界。

## Blocked by

- [01 逐顆果實與來源生命週期](01-source-cycle.md)

## 驗收條件

- [x] 半徑 4／5.5 的整圓在界內且不重疊，所有存續階段占地；生物通行不受來源圓阻擋。
- [x] 40×40 正式世界以可重現布局與合法備援確保十二小四大；抽年齡與階段、保留果實新鮮度，至少一處有可採果。
- [x] 後續新來源從零歲零果實開始，來源到期釋放空間；類型建立後不變。
- [x] 天然目標十六、每十秒最多兩處；所有存續天然來源均計數，播種來源只占共同空間及總數六十四上限。
- [x] 兩型都可放才按天然七三抽選，只可放小型就小型，無空間不補、不累積欠額、不擠掉其他來源。
- [x] 邊界、剛好相切、空間已滿、來源到期、自訂較小配置有明確驗證；不得宣稱小世界也能塞滿正式數量。

## 討論紀錄

- 2026-10-30：依已確認共同理解發佈本機任務；目前僅文件準備完成，尚未實作。

## 完成紀錄

- 2026-10-30：完成 `SourcePlacementRules` 共用整圓判定與來源建立；三種存續階段及等待期限都占地，地面果實與個體不參與占地。整圓可剛好相切，所有新來源以零歲零果實開始，建立後類型不變；來源到期後才釋放空間。來源總安全上限固定最多 64，亦尊重更低的配置上限。
- 正式 40×40 使用保證合法的十二小四大備援布局，依種子旋轉／鏡射；四大中心為 (±5.5, ±5.5)，十二小沿邊分布。較小或自訂半徑配置以合法候選逐處建立，放不滿時保存 `food_initial_shortfall` 診斷，不套用正式數量保證；半邊長 3 的世界驗證為零來源，恰容小型的世界驗證為一處。
- 起始年齡依各來源壽命前的均勻抽樣，用自然生命週期重建果實與原保存期限；僅全場無果時調整一處至合法有果年齡。重建歷史流量不計入新世界產出，`fruitLedger.initial` 只記起始存量。來源、初始位置、技能與採集競爭採獨立種子亂數流；混合 30 個體與全苔靈 30 個體的起點、來源及成熟安排已逐筆對齊。
- 天然補生目標含所有存續天然來源，播種不占名額但共用占地；每十秒最多兩處，失敗不欠額補發。候選點先檢查合法類型，兩型均可放才按七三抽選；只容小型時不抽大型，無空間不擠掉既有來源。
- 更新 `training/foraging.json`、`evaluation.json`、`scenarios/world.json` 及 `scenarios/survival-isolation/{Flame,Frost,Moss}.json` 的兩類果實參數，避免新欄位被巢狀舊 JSON 預設值覆蓋；不修改歷史回放。
- TDD 證據依序為整圓重疊、初始年齡、反覆補生重疊與舊 JSON 參數的預期失敗，再逐項實作通過。最終 `Unity.exe -batchmode -quit -projectPath <專案>/unity -executeMethod Ecosystem.Editor.ValidateSourcePlacement.RunRegression -logFile <輸出>` 退出碼 0；合併 03 後同一入口重跑 01／03／02，生命週期、採集進食與本票皆通過。`git diff --check` 通過。
- 固定驗收包括 50 個正式種子皆有十二小四大、整圓合法且有果；起始保存不刷新與守恆；天然零果新生、未成熟計數、播種名額、滿場、到期補生及無欠額。兩型同點均合法的 500 固定種子中大型 149 處，為權重實作的有限樣本檢查，不宣稱機率分布或生態穩定的普遍證明。
- 證據位於主專案 `artifacts/fruit-supply/02/`：`placement-red.log`、`placement-green.log`、`age-red.log`、`age-green.log`、`renew-red.log`、`renew-green.log`、`boundary-config-red.log`、`boundary-config-green.log`、`placement-final.log`、`regression-01-03.log`。產物不進 Git。
- 後續 06 可用 `SourcePlacementRules.CanPlace(config, state, point, type)` 查合法類型，並用 `TryCreate(config, state, random, point, natural, largeChance, time, out food)` 在先選定的同一點抽合法大小；必須傳入種子亂數且不藉失敗取得隱藏情報。`World.sourceRandom` 保持與 `foodRandom`（採集競爭）分開。
- 人工核對：重跑上述公開驗證入口並搜尋三項驗收通過訊息；核對正式種子 101 的十六個來源及混合／全苔靈起點。可見來源姿態、選取生長圓及果實外觀由 10 號任務驗收，本票不宣稱已完成畫面與生態驗收。
