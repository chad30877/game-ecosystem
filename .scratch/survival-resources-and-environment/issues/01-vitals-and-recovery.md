# 01：四項資源與雙路徑恢復

**What to build:** 以四項資源取代共用能量，從既有世界入口觀察消耗、進食、恢復與飢餓傷害；所有世界控制方式使用同一套結算。

**User stories:** 01–15。

**Blocked by:** 無；承接舊功能已完成的 01–06，舊 07 學習驗收不是前置。

**Status:** resolved

[正式規格](../spec.md)為實作依據。開始前讀取前置完成紀錄與本任務討論；前置全部 resolved 才能開始，開始改 claimed，全部驗收通過才改 resolved。

## 驗收條件

- [x] 四資源上限、初始值及完整配置符合規格；無第五項重複能量，無負值或超上限。
- [x] 自動恢復與營養恢復可並行；脫戰／休息等待、30% 啟動／60% 停止、暫停及飽食度底線可從狀態核對。
- [x] 健康20%、體力40%且同時符合營養恢復時，以2：1分配營養支出；覆蓋某項暫停、達目標與營養不足，未轉換部分不扣費。
- [x] 飽食度歸零停止健康兩條恢復路徑並按時間扣健康；體力／魔力仍可自動恢復，死亡不被後續回血復活。
- [x] 用固定配置與指定動作核對基礎消耗、食物收入、兩類恢復與資源上下限，保存可重現的狀態／事件證據。
- [x] 本切片即可從既有配置設定四資源初值，於個體檢視看到四項數值、恢復狀態與基本收支；不用等待單體隔離或完整死亡診斷才能展示。
- [x] 共同資料與必要介面能建置，版本明確區分新舊語意；不執行PPO訓練，不新增測試檔。

## 人工核對

以既有世界入口設定低健康、體力與魔力，觀察兩條恢復、飽食度支出及底線；停用採食觀察飢餓停止回血。

## 討論紀錄

- 2026-10-23：依 to-tickets 補齊配置、世界結算、簡潔畫面與對外紀錄的完整交付路徑。必要的局部整理先於行為接入，在本切片完成；若既有動作需短期介面適配，須共用同一份資源資料、不產生第五項能量餘額，並在 02 完成成本遷移時移除過渡依賴。

## 完成紀錄

- 2026-10-23：四資源、可配置初值、雙路徑恢復、各資源啟停／暫停、基本收支與個體檢視已交付。結算順序記於 World.cs；共用觀察 19 維，版本 survival-v6 拒絕舊模型、回放與重載。
- 程式 commit：232a3c5；後續驗證與場景恢復狀態配置一併提交。
- 建置：`./scripts/world.ps1 -Mode build -Editor D:/Projects/game-ecosystem/.tools/unity/6000.0.85f1/Editor/Unity.exe`，三次成功，最後 build-20261023-181717-a31e0f21。
- 既有驗證：`training/bridge.py --env unity/Builds/World.exe --output artifacts/vitals-validation/bridge --config training/world.json --steps 128` 通過，678 次觀察，三物種位移、加入與 terminal 正常；Python compileall、git diff --check 通過。未新增測試檔、未訓練 PPO。
- 正式入口以 Seed 101 執行：recovery 1000／2000 步、starvation 2600 步、feeding 指定採食 100 步、reserve 100 步、ratio 2 步。命令為 `./scripts/world.ps1 -Mode forage -WorldConfig <配置> -Controller idle -Steps <步數> -Seed 101`；feeding／ratio 改用 `-Scenario <同名場景>`。
- 證據保留於 `artifacts/survival-ticket01/vitals-validation/`（完整配置、結果、bridge）及 `artifacts/survival-ticket01/runs/`（完整逐步狀態）。恢復樣本收支誤差小於 0.01；三物種零飽食健康無恢復、50–50.02 模擬秒死亡，體力與魔力仍恢復；指定採食取得正常收入。
- 精確健康20%、體力40%且兩項營養恢復已啟動，以正式 Scenario 配置、停用自動恢復核對，三物種營養比例均為 2:1（誤差小於 0.000001）；飽食30.02場景只扣可轉換部分，基礎消耗可低於30，營養恢復暫停；達60%後停止並可繼續自動恢復至上限。
- 人工操作：用 recovery.json 加 `-Visible`，點選個體核對四資源、營養恢復與收支；用 starvation.json 觀察餓死與停止回血。
- 02 承接：技能目前統一暫接魔力；普通近戰需改扣體力，活動需更新 lastStrenuousAt，繁殖條件與新生飽食度由02完成。此處沒有第五項能量餘額。
