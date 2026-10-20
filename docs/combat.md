# 三物種技能驗證

本輪實作任務 05、06、07，沿用同一套 `World.Step`；規則控制及指定動作均不是學習結果。沒有新增測試檔。

## 炎角獸

近戰射線長 1.1、命中半徑 0.55、傷害 12、耗能 2、冷卻 1 秒。衝鋒距離最多 4、速度 10、傷害 24、耗能 12、冷卻 6 秒；固定起手瞄準方向，掃掠第一個異種後停下，不穿透、不卡同種；邊界也會停止衝鋒。一般移動另套用既有活動耗能。

能量至少 50，且距最後交戰至少 5 秒，每秒恢復 2 健康，不能超過物種健康上限。戰鬥死亡另以 `combat` 死因及分物種計數保留。

`artifacts/combat/flame/forage-20261020-232402-460889e5` 完成 1,000 步指定動作，驗證衝鋒及近戰命中、冷卻與霜羽戰死；`recovery/forage-20261020-232404-7c718b17` 完成 500 步，確認受傷後至少 5 秒才開始恢復且不超過最大健康。

人工操作：

```powershell
./scripts/world.ps1 -Mode build
./scripts/world.ps1 -Mode forage -WorldConfig training/scenarios/world.json -Scenario training/scenarios/flame.json -Visible
```

觀察炎角向右衝鋒、霜羽受擊閃白、健康下降並死亡；點選個體核對健康、能量、攻擊及技能冷卻。

## 指定動作入口

`-Scenario <JSON>` 啟用 `scripted` 控制。`placements` 指定每輪起始個體位置；`slot` 為起始順序，先全部炎角、再霜羽、再苔靈，從 1 起算。`commands` 以 `fromStep`／`toStep` 指定包含端點的世界步數區間，提供 `moveX/Y`、`aimX/Y`、`forage`、`attack`、`skill`；同一個體區間不可重疊。未指定的動作為靜止，後代亦靜止；重建重新套用初始位置與動作時序。只覆寫位置，不改健康、能量或生命規則。

`skill` 使用 `None`、`Charge`、`Cultivate` 或 `Shield`。世界 API 使用相同的 `WorldAction`，瞄準與移動分開，技能不依賴控制器種類。場景、配置、種子及控制模式都記錄於首筆 JSONL；`-ReloadRun` 會重載指定動作。這是重新執行世界，並非歷史回放。
