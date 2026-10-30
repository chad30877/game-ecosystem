# 繁殖、供給與爭食的生態觀察

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

本輪沿用 survival-v9、正式世界入口與規則 AI，恢復正常繁殖及老化；沒有執行 PPO。這是短程診斷，不是長期平衡驗收。06 的單體生存結果仍見[原報告](survival-validation.md)。

## 重現與產物

```powershell
./.venv/Scripts/python.exe training/ecosystem_observation.py --output artifacts/ecosystem-ticket07-rerun
./.venv/Scripts/python.exe training/ecosystem_observation.py --probes --output artifacts/ecosystem-ticket07-probes-rerun
```

輸出目錄須尚未存在，避免覆寫證據。工具依序執行繁殖、有限供給、戰鬥：前兩階段三物種各兩場，戰鬥兩場共14場。種子固定101、102，每場上限15000步×0.02秒＝300秒；提早滅絕採實際第一輪時長，不混入自動重建後的下一輪。每場配置、場景、原始多輪紀錄及第一輪回放均保留。`--refresh --output <既有目錄>` 僅重新彙整第一輪。

繁殖階段沿用06四個大型來源及佈局，起始單一成年、天然來源上限4；有限供給階段改為單一來源，補生間隔60秒、批次1；戰鬥階段沿用有限供給，三物種各一名成年置於(1,0)、(1.5,0)、(2,0)，共同競爭(2,0)來源。兩個單物種階段起點(0,0)，其餘成本、生長與生命週期參數沿用06；人口上限24、老化及繁殖皆開啟。繁殖階段的「供給較充足」是相對配置，不保證繁殖後仍足以支撐全部後代。

每場 `result.json` 與根目錄 `results.json` 保存出生時親代／子代飽食度、分因死亡、實際存活時間、未完成生命、技能配置、資源帳、採集量、期初／期末食物、來源峰值、來源更替事件、決策步數與轉換及代表步數。採集量包含生長後補給，不能把期末減期初直接當成生產量；自然補生、到期移除與培育須對照事件與回放。

## 指定動作核對

八場各20秒、種子101，對照個體使用規則，其他個體使用保存的固定動作；這是正式入口的環境核對，不是 PPO 或混合策略實驗。

- `contest`：炎角 HotStrike／IntimidatingRoar，在唯一已知來源遭採食時爭食，取得採食機會後轉 feed，來源耗盡後 food_lost→explore。
- `switch`：炎角健康20、有另一來源時 switch_food；`retreat` 同健康但無替代時撤退。
- `costly`：霜羽爭食後受到固定近戰與 HotStrike，重選為 retreat；與低初始健康案例分開。
- `frost`：FrostbindArrow／PiercingIceArrow 與基本冰箭；`roots`：苔靈 EntanglingRoots／SporeSmoke，沒有普通攻擊，且定身中的採食仍持續。
- `stale`：來源初始年齡239.9秒，接近途中到期，原目標失效後探索；`hidden`：與 contest 同位置但背向來源，未取得敵方採食情報。舊記憶導航另由有限供給場次的 rememberedTarget 與來源失效轉換核對，不能把來源到期直接宣稱為所有舊情報情境的因果。

舊情報撲空的明確證據另見 `switch`：第135步轉向記憶中的(2,0)，世界真值該來源已為0；第152步靠近後 food_lost，第153步 explore。這是舊情報未即時更新造成的可觀察落差；來源真值只用於觀察核對，沒有提供給規則策略。`roots` 有54步同時定身與採食；`costly` 由 contest 轉為100步 retreat；`hidden` 全20秒無採食感知，對照 `contest` 有91筆可見採食。

## 人工核對

```powershell
$replay = Get-ChildItem 'artifacts/ecosystem-ticket07/reproduction-Flame-101/*/episode-1.replay.jsonl'
./scripts/world.ps1 -Mode replay -ReplayFrom $replay.FullName -Visible
```

先看出生步數，核對親代扣40後仍高於30、子代30，以及後代自行移動採食；跳至210秒核對起始成年老死。再播放 limited 與 combat 同種子，依 `observation.examples` 的步數÷50定位，前後拖動核對 food_lost、爭食、換點及撤退前因。指定動作回放在 `artifacts/ecosystem-ticket07-probes-reviewed/<案例>/forage-*/`；roots 核對定身與採食共存，hidden 對照 contest 的個體感知。全景真值與策略所知分開核對。

原始大型產物由 .gitignore 排除並保留本機，重現工具與本報告隨Git保存。初版 probes 與 v2 的紀錄保留作配置調整證據：costly 初版尚未造成足夠損失，最後改為固定炎角攻擊霜羽，沒有修改世界成本或AI門檻。

## 診斷與限制

- 已核對樣本中未發現已證實的規則違反：親代保留、新生營養、定身可採食、局部採食資訊、爭奪結束及資源帳均可追溯。這不是對所有場景的無錯誤保證。
- 觀察到的偏差：繁殖後仍可能缺糧；苔靈供給與後代數量較大，來源會到達64上限，仍有休養與到期更替；有限供給的苔靈101／102差異顯著。共同爭食101未發生攻擊、102出現根鬚與孢子；不能用兩個短程樣本宣稱穩定戰鬥或物種平衡。指定動作案例另核對可用攻擊與結束條件。
- 尚未確認的原因：技能配置、來源分布、掃視方向、繁殖後需求與情報過時各自的影響尚未分離。兩種子不能支持單一因果歸因；[後續構想](future-ideas.md)保留供給與配置對照。本輪沒有調整生存成本或AI門檻，也不宣稱解決全部早死。

## 實際檢查

Python compileall及既有bridge完整512步通過（2886 observations，三物種移動、18個體與12個生命結束），未新增測試檔。本輪只新增觀察工具與文件，未修改Unity程式。每份回放經既有封裝器檢查連續影格並保存SHA-256；costly回放依0→2→100→1000→500→0跳轉，6份完整快照與原影格逐值相等。未宣稱已人工播放全部大型回放。

### Standards

以 f14c57b2d888c52ad7f078a17c4fa6d1269e222b 為固定點，平行審查程式與書面規範，無確認違規；原先一項重複流程建議已由run_case共用函式處理，重審無新發現。

### Spec

平行審查未發現已確認缺陷；依建議補入培育實際產量、來源更替及技能差異證據。規則核對與生態偏差分開呈現，舊07維持deferred。最終數值紀錄另外核對出生營養、資訊邊界與收支殘差。

97次出生的親代最低飽食40.0079，子代皆30。各生命資源帳最大絕對殘差0.024514，保留單精度累積差異，未覆寫世界狀態；來源收支由逐步存量與採集推導，並非另一套獨立生產量測。培育事件實際增加食物：繁殖苔靈101／102為714.34／437.60，有限供給101／102為418.08／0；播種數、休養、成熟及到期事件見數值紀錄。

## 實際結果

| 階段／物種／種子 | 實際秒 | 出生 | 分因死亡 | 未完成生命 | 採集 | 來源峰值 |
| --- | ---: | ---: | --- | ---: | ---: | ---: |
| reproduction-Flame-101 | 300.00 | 4 | {'old_age': 3} | 2 | 515.64 | 4 |
| reproduction-Flame-102 | 278.88 | 2 | {'old_age': 3} | 0 | 450.68 | 4 |
| reproduction-Frost-101 | 300.00 | 4 | {'old_age': 2, 'starved': 1} | 2 | 501.06 | 4 |
| reproduction-Frost-102 | 300.00 | 4 | {'old_age': 2, 'starved': 2} | 1 | 470.76 | 4 |
| reproduction-Moss-101 | 300.00 | 27 | {'old_age': 4} | 24 | 4132.21 | 64 |
| reproduction-Moss-102 | 300.00 | 27 | {'old_age': 4} | 24 | 3093.25 | 64 |
| limited-Flame-101 | 196.26 | 1 | {'starved': 2} | 0 | 79.80 | 1 |
| limited-Flame-102 | 210.00 | 1 | {'old_age': 1, 'starved': 1} | 0 | 90.39 | 1 |
| limited-Frost-101 | 242.18 | 1 | {'starved': 1, 'old_age': 1} | 0 | 103.35 | 1 |
| limited-Frost-102 | 242.18 | 1 | {'starved': 1, 'old_age': 1} | 0 | 119.13 | 1 |
| limited-Moss-101 | 300.00 | 23 | {'old_age': 2} | 22 | 2530.10 | 64 |
| limited-Moss-102 | 210.00 | 1 | {'old_age': 1, 'starved': 1} | 0 | 109.65 | 1 |
| combat-all-101 | 210.00 | 1 | {'starved': 3, 'old_age': 1} | 0 | 106.01 | 1 |
| combat-all-102 | 210.00 | 0 | {'starved': 1, 'old_age': 2} | 0 | 97.80 | 1 |

逐生命資源帳、存活時間與技能配置隨 Git 保存於 [數值紀錄](ecosystem-observation-results.json)，完整回放與原始紀錄在本機 artifacts。
