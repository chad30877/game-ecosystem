# 單體隔離情境

三物種配置僅 `initialSpecies` 不同，均為一名成年個體，獨立停用繁殖及老化。每步0.02秒、單輪30001步容納完整600秒，不重建世界。健康、體力、魔力滿值，初始飽食60。

從專案根目錄執行（先建置）：

```powershell
./scripts/world.ps1 -Mode forage -WorldConfig training/scenarios/survival-isolation/Flame.json -Scenario training/scenarios/survival-isolation/layout.json -Controller rules -Steps 30000 -Seed 101 -Visible
```

換成 Frost.json 或 Moss.json 觀察其他物種。移除 `-Visible` 可無畫面執行；移除 `-Controller rules` 則使用場景指定動作（無命令時待機）。執行配置及場景寫入 world.jsonl 第一筆；點選個體核對四資源及年齡。

個體起點(0,0)，四個天然大型來源位於四方向2公尺，距離在6公尺視野半徑內，仍受朝向及正常採食距離限制。每來源容量30、初始20.1、每秒生長1.5；四來源總生長6高於單體每秒基礎消耗0.5。保留240秒來源壽命、枯竭休養、10秒自然補生及數量4上限；後續位置由種子及一般補生規則決定，不瞬間補滿、不賦予策略全圖情報。此為供給能力配置，完整規則AI生存矩陣由後續驗收記錄。

正常對照可複製配置後移除 `initialSpecies` 並將兩個 disable 開關設為 false；恢復三物種、成長繁殖與240秒老死。兩開關可分別啟用：停用老化將年齡固定於初始化成年年齡，不停用觀察時間或食物生命週期；停用繁殖只阻止出生及親代扣費。
