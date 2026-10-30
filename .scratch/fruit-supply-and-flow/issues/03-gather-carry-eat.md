# 03 採集、攜帶與完整進食

## Status

resolved

執行授權：2026-10-30 使用者已以 implement-spec 授權實作；全部前置 resolved 後才可 claimed，驗收通過後才可 resolved。

## What to build

建立世界層果實持有與互斥操作，讓個體先花時間採入攜帶，再吃完整顆取得營養。提供可由規則或指定行動使用的合法操作，不在本任務加入完整規則 AI。

需求依據：[正式規格](../spec.md) S03、S09.1、S09.6–8、S09.10。同時遵守規格 S08 的範圍與變更邊界。

## Blocked by

- [01 逐顆果實與來源生命週期](01-source-cycle.md)

## 驗收條件

- [x] 成體三格、幼體一格、成年轉三格；正在吃的果實占格，滿載拒絕採集。
- [x] 來源旁也須先採集一秒再進食兩秒，距離 0.7；吃完整顆才加二十飽食、封頂一百且多餘不退。
- [x] 僅有效採集期間暫占一顆，同時合格競爭者公平可重現抽選，後到者不重抽；同來源其他果實仍可採。
- [x] 移動、攻擊、換互斥行動或目標失效中斷並清進度、放暫占；普通受傷／中毒不直接中斷，掃視仍需符合感知條件。
- [x] 腐爛與吃完同時先腐爛；中斷不給部分營養、不刷新保存；果實唯一持有與統計守恆。
- [x] 固定情境涵蓋多人爭果、容量滿、跨成年、腐爛／死亡／來源移除與操作完成相遇；不存在舊制連續採食旁路。

## 討論紀錄

- 2026-10-30：依已確認共同理解發佈本機任務；目前僅文件準備完成，尚未實作。

## 完成紀錄

- 2026-10-30：完成世界層 `FoodOperations`。`WorldAction` 保留既有建構參數並追加 `foodAction`、`fruit`、`recipient`；採集及進食使用 `FoodAction.Gather/Eat`。相容 `forage` 是空手採集、持果進食的操作便捷入口，沒有連續營養旁路；完整規則選擇由 08 接續。
- 成體三格、幼體一格，包含進食中的果實及接收預留容量。採集一秒後僅將原果實從來源移至持有者；進食兩秒完成才消耗整顆、補二十飽食並封頂一百。
- 暫占記於 `FruitState.claimedBy`，操作快照記於個體 `foodOperation/operationFruit/operationTarget/operationStartedAt/operationProgress`。競爭依果實及個體識別排序，再由獨立種子亂數流在合格者間等機會抽選，不依列表順序決定，不重抽有效暫占。
- 食物互斥判定集中於操作模組，移動、攻擊、換技能、失效、超距或失去視線會取消並清進度；普通受傷及實際中毒跳傷不取消，仍在視線內的掃視可持續。腐爛及來源到期在操作前結算，死亡不完成操作。舊直接轉移親代飽食路徑已移除，04 將實作雙方交付。
- TDD：`ValidateFruitOperations` 透過已確認的 `World.Reset/Step/State` 邊界先重現「採集一秒後只轉為攜帶」失敗，再完成採集並通過；接著先重現「完整吃一顆增加二十且只消耗一次」失敗，再完成進食並通過。
- 最終命令：專案內 Unity `Unity.exe -batchmode -quit -projectPath <工作目錄>/unity -executeMethod Ecosystem.Editor.ValidateFruitOperations.Run -logFile <輸出>`。`operations-and-cycle.log` 退出碼 0，同時重跑 01 的 `ValidateFruitCycle.Run`；`git diff --check` 通過。
- 固定情境涵蓋完整操作時間、成幼體與成年容量、進食占格、開始與完成容量重驗、換目標與中斷重計、超距與視線、掃視、普通受傷與實際中毒、到期同時、來源移除、死亡同時、四十種子雙方勝出與相同種子列表反轉一致、同來源不同顆同時暫占、轉移守恆。此抽樣不宣稱證明所有種子的統計分布。
- 證據保存在主專案 `artifacts/fruit-supply/03/`：`gather-red.log`、`gather-green.log`、`eat-red.log`、`eat-green.log`、`boundaries.log`、`operations-and-cycle.log`。新增中毒案例首次失敗是測試配置誤用同種施毒者（原有友軍傷害規則正確拒絕），已改為苔靈施毒者後通過，未改動友軍傷害規則。
- 人工核對：執行上述固定驗證入口，搜尋「果實操作驗收通過」及「果實生命週期驗收通過」；或以指定 `WorldAction` 在來源旁持續採集一秒，確認僅取得攜帶果，再進食兩秒後才增加二十飽食，中途移動後重試需重新計時。外觀與操作入口的完整呈現由 10、11 驗收。
- 後續接點：04 可沿用 `FoodOperations.Cancel/Carried/Capacity/Requested`，擴充已保留的 `Pickup/Deliver/Receive/Drop` 動作與接收預留；本票未實作其完成行為。負重、果實感知及 AI 分別由 05、07、08 接續。
