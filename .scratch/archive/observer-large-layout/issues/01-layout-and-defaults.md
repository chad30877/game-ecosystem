# 01：固定世界區域與大型啟動尺寸

## What to build

依[規格](../spec.md)固定右側資料空間，所有入口預設 1280×720。

## Blocked by

無。

## Status

resolved

## 驗收條件

- [x] 全景、選取、面板開合時世界矩形及相機維持穩定，右側沒有殘影或世界標記。
- [x] 即時、回放與 Python 入口均預設大尺寸，仍可明確覆寫。
- [x] 建置及必要檢查通過，更新正式文件及人工核對方式，提交後結案歸檔。

## 完成紀錄

Unity 建置通過，三個 Python 檔 Pyright 為 0 errors／0 warnings，Python 語法檢查通過；bridge 20 步核對 200 筆煙幕觀察。實際即時與回放皆以未指定尺寸啟動，診斷確認 1280×720；全景、選取及面板開合的世界矩形固定為寬 896，相機中心及尺度不變，收合後右側乾淨。明確尺寸參數仍由啟動條件保留。證據位於 artifacts/observer-large-layout/。人工測試：執行 ./scripts/world.ps1 -Mode forage -Visible，選取個體、收合與展開資料、按全景，確認世界始終位於左側且右側空間不變。
