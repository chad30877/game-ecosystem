# 05：四資源觀察、死亡前因與回放

> 2026-10-23 保存狀態更新：本文 artifacts 路徑保留為歷史驗證來源；大型原始紀錄與舊模型已淘汰，摘要及指定短回放的保存範圍見[產物清理紀錄](../../../../docs/artifact-retention.md)。歷史操作範例須改用仍存在或新產生的來源。

**What to build:** 使用者可從死亡摘要追溯此前的資源、決策與傷害，透過規則運行回放前後核對，分辨多種因素造成的生存失敗。

**User stories:** 38–43、52。

**Blocked by:** [04：可見採食與爭食、換點決策](04-perception-and-survival-ai.md)

**Status:** resolved

[正式規格](../spec.md)為實作依據。開始前讀取前置完成紀錄與本任務討論；前置全部 resolved 才能開始，開始改 claimed，全部驗收通過才改 resolved。

## 驗收條件

- [x] 沿用 01、02、04 已交付的簡潔個體檢視，將四資源、走跑、恢復與決策串接至死亡摘要及歷史檢視；即時與回放的相同時間點一致。
- [x] 資源帳分列基礎、活動、技能、繁殖、進食及兩條恢復；期初、收入、支出、期末可核對，不重複計算營養轉換。
- [x] 死亡紀錄能關聯死亡前資源變化、戰鬥／飢餓傷害、最近進食、繁殖與恢復，保留最後死因及混合成因證據。
- [x] 分開起始成年／新生、觀察存活秒數／生命年齡及未完成生命；不能把存活到截止者當作完整壽命。
- [x] 規則模式可產生可回放結果，前後拖動能重現資源、決策及死亡；不需PPO模型、不重新執行策略。
- [x] 全景與個體當時所知可區分，當次執行的完整配置、種子及版本可追溯；保留舊資料並明確處理版本不相容。
- [x] 同步適用的使用文件與人工操作，完成相關建置、回放與紀錄核對；不新增測試檔。

## 人工核對

選取個體觀察恢復及爭食理由，回放跨越一次繁殖與死亡並前後拖動；對照資源帳、最後死因與死亡前歷史。

## 討論紀錄

- 2026-10-23：本切片以「死亡前因可追溯」為完整使用者行為，不集中承擔所有早期畫面工作。03 自行記錄隔離開關，06 再整合隔離情境與本切片診斷，因此 05 不必依賴 03。

## 完成紀錄

2026-10-23：完成四資源收支帳、實際傷害分類、死亡前32筆每秒資源／走跑／恢復／決策樣本、最近戰鬥來源及進食／繁殖／營養恢復時間。即時與回放可選擇死亡摘要，歷史按鈕可跳回該時刻；完整逐步紀錄保存所有事件。摘要區分起始成年／本輪出生、生命年齡／觀測秒數、完整／截止生命；關閉時保留最後快照。版本升為 survival-v9，保留並拒絕不相容資料，執行與回放標頭保留規則物種及場景。

證據位於主工作區 `artifacts/survival-ticket05/`，`results.json` 與三份 `*-summary.json` 彙整核對，配置與完整錄製一併保留。建置紀錄 `build.log`；既有 bridge 128步、678 observations 通過（`bridge-20261023-185157-91703566/bridge.json`）。Python compileall、PowerShell語法解析、git diff --check 通過；未新增測試檔。

實際命令（Python指定主工作區既有環境）：

```powershell
./scripts/world.ps1 -Mode build -Editor D:/Projects/game-ecosystem/.tools/unity/6000.0.85f1/Editor/Unity.exe
./scripts/world.ps1 -Mode forage -WorldConfig artifacts/diagnostics-validation/life.json -Controller rules -Steps 250 -RecordEpisodes 1 -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe -OutputRoot artifacts/diagnostics-validation
./scripts/world.ps1 -Mode forage -WorldConfig artifacts/diagnostics-validation/mixed.json -Scenario artifacts/diagnostics-validation/mixed-scenario.json -Steps 2600 -RecordEpisodes 1 -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe -OutputRoot artifacts/diagnostics-validation
./scripts/world.ps1 -Mode forage -WorldConfig artifacts/diagnostics-validation/recovery.json -Scenario artifacts/diagnostics-validation/recovery-scenario.json -Steps 500 -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe -OutputRoot artifacts/diagnostics-validation
./scripts/world.ps1 -Mode replay -ReplayFrom artifacts/diagnostics-validation/forage-20261023-185210-d07bd3ba/episode-1.replay.jsonl -SeekFrames '0,1,100,last,50,0' -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe -OutputRoot artifacts/diagnostics-validation
./scripts/world.ps1 -Mode replay -ReplayFrom artifacts/diagnostics-validation/forage-20261023-185210-bbdafac1/episode-1.replay.jsonl -SeekFrames '0,10,2200,last,1500,0' -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe -OutputRoot artifacts/diagnostics-validation
./scripts/world.ps1 -Mode bridge -Steps 128 -Python D:/Projects/game-ecosystem/.venv/Scripts/python.exe
```

結果：規則模式202個frame包含3起始成年觀測3秒／年齡4秒、3新生觀測4秒，完整平均3.5秒；截止下一輪6個體均標未完成。混合案例最後死因飢餓，仍保留10戰鬥及110飢餓傷害、32筆歷史；停用老化時生命年齡30而觀測45.84秒。恢復案例活動支出6、技能10、進食2.16、自動及營養恢復三項均非零。全部資源帳最大殘差0.000872，兩組各6次前後seek完整state逐值相等。改成v8的副本拒絕，原始回放hash未改。

人工操作：依 `docs/survival-resources.md` 開啟可見規則世界／回放，點選個體核對收支與恢復；按「死亡」選取摘要，再點歷史樣本或拖曳回放，跨越出生、死亡後返回核對。此次生產入口與seek驗證為無畫面執行，未宣稱完成視覺人工操作。完整每步快照較大（混合2502 frame約30.5MB），保留全部事件及原始來源；死亡摘要最多32筆。06／07仍未啟動。


### 最終審查修正

2026-10-23：修正 `ReloadRun` 原先只為 scripted 還原場景而遺失 rules layout 的問題。現在只要執行標頭有非 null 場景就還原，scripted 缺場景仍拒絕。以03的 Flame 單體配置、layout、種子19，先重現初始狀態不同，再核對修正後初始與10步共13份完整 state 相等；證據主工作區 `artifacts/survival-review/` 的 `before-results.json`、`reload-results.json`、原始及重載紀錄。

另抽出 `SkillRules.IsStrenuous` 共用衝鋒、冷霧衝刺、炙熱猛擊的身體活動分類；保留施放開始時間及世界步末更新兩處時間語意。以三技能正式指定動作180步核對重構前後所有狀態。人工方式：以 `-Controller rules -Scenario training/scenarios/survival-isolation/layout.json` 啟動單體，再以 `-ReloadRun '<原始world.jsonl>'` 啟動，確認個體與四個食物位置及資源起始相同。

修正驗證結果：Unity build成功、PowerShell語法解析及git diff --check通過。三技能均實際施放，180步共185份完整state與重構前逐值相等，包含lastStrenuousAt；結果見physical-results.json。未新增測試檔。
