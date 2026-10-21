# 專案規範
- 頁面文字應保持精簡；可由介面、操作流程或元件狀態直接理解的資訊，不要另外加入重複說明。
- 預設不新增測試；只有能指出具體回歸風險，且現有測試無法涵蓋時，才擴充既有測試。新增測試檔須先經我同意。
- 不因一般 UI 調整、重構或提高覆蓋率擴寫測試；保留並執行既有有效測試，不為減少數量而刪除。
- README、專案文件、註解及 Git commit message 使用繁體中文；程式識別字、第三方 API / SDK 名稱及既有專有名詞保留原始名稱。
- 每次改動完成後建立 Git commit，並簡單說明人工測試方式。

## Agent skills

### Issue tracker
需求與任務以本機 Markdown 管理；操作前讀取 `docs/agents/issue-tracker.md`。

### Domain docs
採 single-context；探索程式碼前讀取 `docs/agents/domain.md`。
