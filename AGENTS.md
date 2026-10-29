# 專案規範

- 建立或安裝開發環境時，將執行環境、依賴套件與快取盡量放在專案目錄內；優先遵循專案及各工具的既有慣例，例如使用 `.venv/` 作為虛擬環境目錄。避免修改全域環境。若所需工具無法放在專案內，先說明原因與替代位置，再詢問使用者。
- 若使用 mattpocock skill 使用本機 Markdown 追蹤議題時存放在 `.scratch/<功能名稱>/`，不要發布 issues 到 Github
- 頁面文字應保持精簡；可由介面、操作流程或元件狀態直接理解的資訊，不要另外加入重複說明
- 若有非本輪未來規劃可放到 `docs/future-ideas.md` 中
- README、docs、註解及 Git commit message 使用繁體中文
- 修改 `.ps1` 須保留 UTF-8 BOM，並以 PowerShell 5.1 執行 `scripts/check-world-script.ps1` 驗證。
- 每次改動完成後建立 Git commit，並簡單說明人工測試方式。

## Agent skills

### Issue tracker

需求與任務以本機 Markdown 管理；操作前讀取 `docs/agents/issue-tracker.md`。

完成驗收與結案後，將功能資料夾整份移至 `.scratch/archive/`，更新相關引用，並將仍有效的知識整合至正式文件。日常搜尋排除歸檔，追查歷史時才讀取。

### Triage labels
分流標籤及任務狀態見 `docs/agents/triage-labels.md`。

### Domain docs
採 single-context；探索程式碼前讀取 `docs/agents/domain.md`。

### 設計訪談
使用 `grill-with-docs` 開始訪談時，先讀取 `docs/future-ideas.md`，提出與主題相關的未完成項目，由使用者決定是否納入；已延後的項目維持延後。訪談中產生延後構想，或既有構想被納入、完成或放棄時，依該文件的維護方式更新。
