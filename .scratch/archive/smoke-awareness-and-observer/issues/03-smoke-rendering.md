# 03：煙幕視野裁切與辨識

## What to build

改善目前觀看視角的煙幕填色及邊線，支援即時與回放，不改世界傷害判定。以[規格](../spec.md)為準。

## Blocked by

無

## Status

resolved

## 驗收條件

- [x] 全知完整顯示；個體視角只顯示視野交集，中心可見性不控制整片。
- [x] 邊線符合實際範圍，重疊煙幕內生物與食物仍可辨識。
- [x] 進出、建立、消失、暫停及前後跳轉沒有殘留錯誤。

## 討論紀錄

- 使用者已確認五項拆分與前置關係，正式發佈為 ready-for-agent；依使用者指示先不實作。

## 完成紀錄

全知及個體視角已實際檢視；edge 場景中心位於視野外，仍呈現扇形內煙幕填色與邊線，視野外無警告。兩片重疊煙幕中個體及食物可辨識；建立、消失、暫停及前後跳轉以 651 影格逐欄核對。畫面：artifacts/smoke-validation/ui-edge-960-selected.png、ui-replay-1280-selected.png。人工重現見 docs/smoke-awareness.md。
