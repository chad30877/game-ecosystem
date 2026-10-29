# PowerShell 腳本 BOM 回歸修復

狀態：resolved。

使用者在 Windows PowerShell 執行 forage -Visible 時發生中文亂碼與連鎖剖析錯誤。根目錄 .editorconfig 已要求所有 .ps1 使用 UTF-8 BOM；先前程式寫回移除了 world.ps1 與新建基準腳本的 BOM，而僅在 PowerShell 7 驗證，漏掉 5.1 的讀取差異。

交付：保留三份腳本的 UTF-8 BOM；檢查入口預設檢查所有專案腳本的 BOM 與語法，在 PowerShell 7 下也自動執行已安裝的 Windows PowerShell 5.1 驗證。維持指定單一 Path 的用法。

驗收：先以 Windows PowerShell ParseFile 重現，再只對副本補 BOM 驗證；強化檢查須先拒絕當前缺 BOM 腳本，修復後兩種 PowerShell 通過，並透過 5.1 執行有界可見 forage。
