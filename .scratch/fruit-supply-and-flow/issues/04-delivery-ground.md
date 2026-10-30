# 04 雙方交付、地面拾取與掉落

## Status

resolved

執行授權：2026-10-30 使用者已以 implement-spec 授權實作；全部前置 resolved 後才可 claimed，驗收通過後才可 resolved。

## What to build

完成同種逐顆交付與接收配合、容量預留、主動放下、死亡掉落及跨物種地面拾取；全部沿用原果實識別與保存時間。

需求依據：[正式規格](../spec.md) S03、S09.6–10。同時遵守規格 S08 的範圍與變更邊界。

## Blocked by

- [03 採集、攜帶與完整進食](03-gather-carry-eat.md)

## 驗收條件

- [x] 交付距離 0.7、花一秒；雙方自願停下，一次接一筆並預留一格，完成前果實仍屬送方。
- [x] 開始及完成核對容量，拒絕／逃跑／超距／失效／腐爛中止，果實留送方、釋放接收預留，雙方均不直接補營養。
- [x] 主動每顆放下 0.2 秒，完成才落地；死亡立即掉落全部未腐爛攜果，進食或交付中的果實不複製。
- [x] 拾取 0.5 秒、距離 0.7、暫占單顆，任何物種可拾；中斷留原地，不刷新期限、不長成來源或占生長空間。
- [x] 固定情境涵蓋多邀請、滿載與預留競爭、雙方死亡、中斷後重試、拾取競爭及到期同時；轉移前後守恆。

## 討論紀錄

- 2026-10-30：依已確認共同理解發佈本機任務；目前僅文件準備完成，尚未實作。

## 完成紀錄

- 2026-10-30：完成 `FoodOperations` 的雙方交付、地面拾取、主動放下與死亡掉落；沿用原 `FruitState` 識別及 `expiresAt`，沒有新增營養或重設保存時間。逐顆交付與掉落只增加對應流量計數，不計新產出，固定情境均核對守恆。
- 指定行動契約：送方使用 `FoodAction.Deliver`，`recipient` 指接收者、`fruit` 指持有果實；接收者使用 `FoodAction.Receive`，`recipient` 明確指送方，`fruit=0` 表示接受該送方本次果實。雙方同種、存活、距離不超過 0.7、互相可見且停止互斥動作時才開始；接收者選哪筆由輸入／後續 AI 自主決定，世界不以爭果抽選代替接收選擇。
- 配對後雙方 `operationTarget` 互指，接收者 `reservedCapacity=1`，果實在完整一秒前仍由送方持有；完成才轉移且釋放預留，須再進食才補營養。`Cancel` 會一併清除雙方有效配對、暫占及預留；未同意的邀請不打斷接收者的有效採集或拾取。容量、距離、視線、果實及雙方行動每步重驗。
- 觀察接點：送方 `deliveryInviteTo/deliveryInviteFruit` 僅代表當前有效、近距離、已看見接收者的交付動作，尚非接收同意或容量預留；移動、取消、目標死亡及果實到期會清除。07 須只向看得見送方的個體揭露這項互動，離視野記憶不得跟隨真值即時更新。08 再依需求、安全及親代優先規則選擇明確接收對象；本票不實作該 AI 決策。
- 主動放下 0.2 秒完成才轉地面；拾取 0.5 秒，三物種共用單顆公平暫占。`DropAll` 於死亡及公開 `RemoveIndividual` 立即處理攜果並解除配對，避免 bridge 移除後留下無持有者的孤立持果；只掉落尚未腐爛者，位置使用死亡／移除當下位置。進食、交付中的同一果實不重複。
- TDD：先分別重現缺少交付預留、0.2 秒放下、0.5 秒拾取及死亡掉落的失敗，再逐項實作並通過。新增可見邀請亦先紅再綠；續行交付以 `fruit=0` 時曾錯誤呈現另一顆較小識別果實，固定情境重現後修正為保留 `operationFruit`，完成轉移及可見邀請保持一致。
- 最終命令：專案 Unity `Unity.exe -batchmode -quit -projectPath <工作目錄>/unity -executeMethod Ecosystem.Editor.ValidateFruitTransfer.Run -logFile <證據>/final-integrated.log`。已合併當時整合分支 `d9c87c0` 後執行，退出碼 0；同入口亦重跑 `ValidateFruitOperations.Run`（包含 01 生命週期）及 `ValidateSourcePlacement.Run`，01–04 均通過。`git diff --check` 通過；本票未修改 PowerShell 腳本。
- 驗收情境：未接受／異種／容量滿／幼體一格、接收預留最後一格、明確多邀請選擇、有效拾取不被邀請打斷、移動／拒絕／超距／容量失效中斷及完整重試、普通受傷與掃視續行、任一方及雙方死亡、老死、腐爛與完成同時、公開移除、放下中斷、地面位置與期限、異種拾取、四十種子競爭與列表反轉重現、原有暫占不重抽、進食中死亡、全部未腐爛攜果掉落及守恆。種子樣本只核對重現及雙方勝出，不宣稱證明統計分布。
- 證據保存在主專案 `artifacts/fruit-supply/04/`：`transfer-red/green.log`、`drop-red/green.log`、`pickup-red/green.log`、`death-red/green.log`、`delivery-boundaries.log`、`transfer-and-regressions.log`、`invitation-red/green.log`、`invitation-consistency-red/green.log` 及 `final-integrated.log`；原始產物不進 Git。
- 人工核對：執行上述驗收入口，搜尋四項「驗收通過」；或在固定世界讓同種個體相距 0.5，送方指定 `Deliver`、收方指定 `Receive`，核對一秒前仍由送方持有、一秒後只有原果實換持有者且雙方飽食不變。再指定放下 0.2 秒、異種拾取 0.5 秒，核對原期限不變；交付中移動或移除任一方，確認預留立即解除且無果實複製。畫面與完整操作入口由 10、11 接續驗收。
