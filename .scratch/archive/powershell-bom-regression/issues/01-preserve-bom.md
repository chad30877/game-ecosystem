# 01：恢復 BOM 並強化實際解析檢查

## What to build

依[規格](../spec.md)恢復 PowerShell 編碼相容性並鎖住回歸。

## Blocked by

無。

## Status

resolved

## 驗收條件

- [x] 原錯誤可重現，僅補 BOM 即可讓相同內容通過 Windows PowerShell 5.1。
- [x] 預設檢查涵蓋全部腳本，缺少 BOM 明確失敗，5.1／7 均通過。
- [x] 5.1 實際啟動有界可見世界，正式文件更新並提交歸檔。

## 完成紀錄

已重現相同剖析錯誤：PowerShell 7 通過、5.1 失敗；只補 BOM 的副本在 5.1 通過。修復兩份腳本時逐位元組核對除 BOM 外不變。強化檢查先拒絕缺 BOM 腳本，再於修復後以 7／5.1 通過全部三份腳本，指定單檔及缺 BOM 副本失敗亦核對完成。5.1 實際執行 forage -Visible -Steps 2 成功並核對兩筆世界步。正式知識更新於 docs/toolchain.md，證據位於 artifacts/powershell-encoding/。人工測試：在原本視窗執行 ./scripts/world.ps1 -Mode forage -Visible，確認可開啟世界。
