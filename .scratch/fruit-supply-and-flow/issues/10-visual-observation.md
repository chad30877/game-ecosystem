# 10 來源與果實外觀及選取資訊

## Status

resolved

執行授權：2026-10-30 使用者已以 implement-spec 授權實作；全部前置 resolved 後才可 claimed，驗收通過後才可 resolved。

## What to build

把來源四階段、逐顆果實、持果與操作動作呈現在即時觀察中；選取後提供簡短資訊及按需生長圓，保持常態畫面精簡。

需求依據：[正式規格](../spec.md) S06、S09.15。同時遵守規格 S08 的範圍與變更邊界。

## Blocked by

- [05 攜帶負重與移動效果整合](05-load-movement.md)
- [07 果實感知與有限記憶](07-observation-memory.md)
- [08 採食、送果、接收與逃生減重 AI](08-food-care-ai.md)
- [09 局部供給、播種與培育選擇 AI](09-production-ai.md)

## 驗收條件

- [x] 四種來源階段以姿態及色調區分；成熟果實逐顆可見，花苞數不洩漏未來結果額度。
- [x] 新鮮／將腐爛兼用色調與表面差異，持果數與負重可由外觀或選取資訊辨識。
- [x] 採集、拾取、吃、交付、放下與培育有對應動作，完成／中斷／死亡／腐爛後顯示一致。
- [x] 常態不掛滿文字或全場生長圓，選取才顯示短階段、現有果數及所需細節；維持既有選取、縮放、暫停使用流程。
- [x] 診斷資訊與個體感知分開；以固定場景人工核對低果數、滿載、地面果實及多人互動，記錄可見檢查證據。

## 討論紀錄

- 2026-10-30：依已確認共同理解發佈本機任務；目前僅文件準備完成，尚未實作。

## 完成紀錄

- 2026-10-30：完成 `FruitWorldView`，來源以發育幼株、結果展葉、等待垂葉、休養短樁的姿態及色調區分；只從當前果實清單畫出逐顆果實，不以花苞或分枝數揭露未來額度。新鮮果有亮色與光點，將腐爛果呈褐色及斑點；地面、來源與攜帶共用外觀，持果隨持有者移動。
- 個體增加採集伸手、拾取下探、進食口部、交付伸手、接收張手、放下及培育光芒等短動作。依快照中的操作與時間呈現，完成／取消後隱藏，死亡後由既有掉落真值呈現。共用果實材質，物件移除時清理；無畫面模式略過新增純視覺物件及動作，不改世界狀態。
- 來源可點選，正常面板只列類型、階段、果數與粗略新鮮度，生長圓只畫選取的一處。個體面板增加攜果、容量、負重速度與食物操作，保留暫停、縮放、全景及資料收合；生產理由翻譯為繁體中文。診斷模式另列「世界真值」，個體記憶另以「最後觀察／推測」區分。衝鋒／遁行依未負重距離預算換算剩餘秒數。
- `WorldState.fruitInformationAvailable` 預設為 false，`World.Reset` 明確設 true。缺少此欄的歷史快照仍走原本食物圓片繪製，個體面板標示「果實資訊未提供」；沒有補造果實、交付或四階段。11 須將此可用性與正式規則版本相互核對。
- TDD 視覺紅／綠：先建置並以真實 Player 視窗重現舊圓片無逐顆果實與植株姿態，保存 `visual-red.png`；完成後在同入口核對新外觀，保存 `forage-green.png`、`source-selected.png`、`source-zoom.png`。全程以 computer-use 技能的 `@oai/sky` 選取實際回傳視窗、操作與截圖。
- 固定診斷情境由 `ValidateFruitVisuals.WriteFixture` 透過公開世界狀態產生，供外觀與操作姿態核對。它保存三張獨立診斷快照（來源三果、二果、採空），不作為生態或操作時間驗收證據。回放標頭從當次實際執行紀錄取得，後續規則改版時必須重新產生，不固定成 v14。打包腳本使用 UTF-8＋LF；首次打包因 Windows 換行造成校驗不符，改為明確 LF 後成功。
- 回放視覺一致性：來源果實位置只由當時快照的果實識別排序決定，採走時容許重新排列，不保存繪製歷史。實際拖動回放滑桿完成 0→1、1→2→1；`seek-forward.png` 與 `seek-back.png` 的世界視窗內部區域 `(16,145)-(595,500)` 像素完全一致，檢查記於 `seek-pixel-check.txt`，排除桌面視窗外框及游標。
- 最後建置及回歸命令：專案 Unity `Unity.exe -batchmode -quit -projectPath <工作目錄>/unity -executeMethod Ecosystem.Editor.ValidateFruitVisuals.BuildAndWrite --visual-output <證據目錄> -logFile <證據目錄>/final-build-regression.log`，退出碼 0，包含 Windows Player 建置與 01–09／煙幕固定情境。診斷快照修正後另執行 `WriteFixture`，`showcase-fixture.log` 同為退出碼 0。`git diff --check` 通過；未修改 `.ps1`。
- 可見核對：`showcase-overview.png`、`showcase-details.png`、`showcase-four-phases.png` 顯示四階段、低果數、地面兩類新鮮度與多人操作；`full-carrier.png` 核對三顆、容量 3/3、走速 80%、新鮮二／將腐爛一。真實舊 v14 代表回放顯示原黃色圓片及「果實資訊未提供」，保存 `legacy-v14.png`，Player 日誌無例外。來源選取、暫停／恢復、滾輪縮放、收合／展開及來源／個體切換均以實際 UI 操作核對；收合畫面保存 `panel-collapsed.png`。
- 證據保存於主專案 `artifacts/fruit-supply/10/`，不依賴 worktree；歷史代表來源為 `artifacts/smoke-validation/current-perception/forage-20261029-175107-072f3812/episode-1.replay.jsonl`。原始截圖與記錄不進 Git。
- 人工測試：建置後執行 `scripts/world.ps1 -Mode forage -Controller rules -Visible -Seed 101`；點植株看單一生長圓與短階段，點個體看持果與視野，按 P 暫停、滾輪縮放及收合／展開。固定診斷情境可重跑上述 `WriteFixture`，以證據目錄的 `pack-showcase.py` 從當次新規則 run 標頭打包，然後 `-Mode replay` 開啟 `showcase.replay.jsonl`，拖至採空再回前一張確認位置一致。版本／格式與全部歷史回放驗收由 11 接續。
