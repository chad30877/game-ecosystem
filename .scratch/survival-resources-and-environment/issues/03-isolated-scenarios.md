# 03：單體與生命週期隔離入口

**What to build:** 擴充既有世界執行入口，真正支援指定物種的單一成年個體，以及獨立停用繁殖與老化，供基本生存及成本校準使用。

**User stories:** 44–45。

**Blocked by:** [02：走跑、技能與繁殖成本拆分](02-activity-and-reproduction.md)

**Status:** resolved

[正式規格](../spec.md)為實作依據。開始前讀取前置完成紀錄與本任務討論；前置全部 resolved 才能開始，開始改 claimed，全部驗收通過才改 resolved。

## 驗收條件

- [x] 可只產生指定物種的一名成年個體，不暗中補入其他物種或個體。
- [x] 繁殖及老化有獨立、可記錄的顯式驗證設定；不是透過極大壽命、人口上限或反覆重建達成。
- [x] 未啟用隔離設定時保留正常生命週期；配置、種子、步長與特殊開關完整寫入執行產物。
- [x] 可從既有配置／指定動作入口設定四資源及食物位置、存量與供給，600秒觀察不被既有短輪次設定截斷。
- [x] 提供可重現的充足供給佈局及理由，透過正常食物生命週期與採食滿足需求，AI不取得特權情報。
- [x] 以短程隔離與正常世界對照核對入口及開關，保存產物及人工操作命令；不新增測試檔或私有入口。
- [x] 操作者可在可見世界確認個體數與生命週期行為，並從執行配置核對特殊開關；本切片交付可用情境，不只增加設定欄位。

## 人工核對

分別啟動三個物種的單體場景，確認僅有一名個體，年齡及出生事件符合停用設定；再啟用正常生命週期確認恢復。

## 討論紀錄

- 2026-10-23：沿用已確認的單一正式世界驗證邊界；四資源初值輸入由 01 提供，本切片增加隔離能力。依賴 02 是為核對啟用與停用後的新繁殖及生命週期行為，不依賴爭食決策。

## 完成紀錄

- 2026-10-23：新增 initialSpecies 指定唯一物種，搭配 individualsPerSpecies=1 真正初始化單體；空字串保留三物種世界。disableReproduction 與 disableAging 為各自獨立、預設 false 的正式配置，寫入 run header；停用老化固定生命年齡，不影響模擬時間及食物生命週期。
- 可用情境：training/scenarios/survival-isolation/{Flame,Frost,Moss}.json、layout.json 及 README.md。三配置僅物種不同，四資源初值一致，單輪30001步容納600秒；四個距起點2m天然來源，正常視野與採食、生長、壽命及補生。README記錄供給理由及可見操作命令。
- 建置成功；既有 bridge.py 128步、678觀察、三物種移動與加入/死亡檢查通過，git diff --check通過。未新增測試檔。
- 正式入口命令：./scripts/world.ps1 -Mode forage -WorldConfig training/scenarios/survival-isolation/Flame.json -Scenario training/scenarios/survival-isolation/layout.json -Controller rules -Steps 100 -Seed 101。Frost、Moss相同；Flame另跑30000步確認600秒單輪且僅一名成年。Frost另以 -Visible 100步成功，提供可見世界相同配置。
- 三物種短程均起終1名、age30、無出生；只停用繁殖時2秒後age32；只停用老化且初始飽食95時出生1名，親代仍age30、新生age0。正常兩開關false起始3名成年，2秒後6名、親代age32；縮短壽命31秒對照有old_age（死亡後入口正常重建，該場不當作600秒證據）。
- 證據：artifacts/survival-ticket03/isolation-validation/ 保存完整配置、結果、逐步紀錄、bridge與可見執行；build.log保留於同目錄。600秒僅證明正式入口不被短輪次截斷，完整30場基本生存矩陣仍由後續任務驗收。
- 人工核對：依情境 README 加 -Visible，點選個體看四資源、年齡與個體數；比較 reproduction.json、aging.json、normal.json，從world.jsonl第一筆核對特殊開關。繁體中文commit記錄本次功能及驗證。
