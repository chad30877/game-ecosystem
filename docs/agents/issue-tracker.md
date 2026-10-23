# 本機 Markdown 追蹤器

- 每項功能使用 `.scratch/<feature-slug>/`；規格為 `spec.md`，任務索引為 `README.md`。
- 每張任務各自存放在 `issues/<NN>-<slug>.md`，從 `01` 起依前置順序編號。
- 任務以 `What to build` 描述交付行為，`Blocked by` 列出前置編號與名稱，`Status` 記錄狀態，驗收條件使用核取清單。
- 正式發佈規格與可執行任務時使用 `ready-for-agent`；標籤定義見 [triage-labels.md](triage-labels.md)。
- `ready-for-agent` 表示內容已準備好；只有所有前置任務均為 `resolved` 時才能開始。開始工作改為 `claimed`，全部驗收完成才改為 `resolved`。
- 使用者決定延後時改為 `deferred`，保留原驗收與證據，附上原因及後續構想連結；暫不執行，也不視為完成。重新排入前依當時環境確認規格與預算，再改回 `ready-for-agent`。
- 發佈代表建立或更新本機檔案，不呼叫外部服務。讀取任務時同時讀取其規格、前置任務結果與既有留言。
- 後續留言附加在任務的「討論紀錄」區段；完成結果與驗證證據寫入「完成紀錄」。任務索引提供導航，逐張任務是狀態與依賴的維護來源。
