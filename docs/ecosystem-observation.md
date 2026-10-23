# 繁殖、供給與爭食的生態觀察

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

## 人工核對

```powershell
$replay = Get-ChildItem 'artifacts/ecosystem-ticket07/reproduction-Flame-101/*/episode-1.replay.jsonl'
./scripts/world.ps1 -Mode replay -ReplayFrom $replay.FullName -Visible
```

先看出生步數，核對親代扣40後仍高於30、子代30，以及後代自行移動採食；跳至210秒核對起始成年老死。再播放 limited 與 combat 同種子，依 `observation.examples` 的步數÷50定位，前後拖動核對 food_lost、爭食、換點及撤退前因。指定動作回放在 `artifacts/ecosystem-ticket07-probes-final/<案例>/forage-*/`；roots 核對定身與採食共存，hidden 對照 contest 的個體感知。全景真值與策略所知分開核對。

原始大型產物由 .gitignore 排除並保留本機，重現工具與本報告隨Git保存。初版 probes 與 v2 的紀錄保留作配置調整證據：costly 初版尚未造成足夠損失，最後改為固定炎角攻擊霜羽，沒有修改世界成本或AI門檻。
