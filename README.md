# GoogleSheetsFetcher for Unity (`com.amaan.googlesheetsfetcher`)

A modular, UniTask-powered Google Sheets fetching and syncing toolkit for Unity Editor supporting Public URLs, Google Cloud API Keys, Apps Script Secret URLs, parallel batch downloading, a reusable generic `SheetSyncEditor<T>`, and a zero-dependency `[TableView]` inspector attribute.

## Installation

### Prerequisites
Make sure **UniTask** is installed in your project before adding this package. In Package Manager, click `+`, select **Add package from git URL...**, and enter:
```
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask
```

### Install GoogleSheetsFetcher
In Unity Package Manager (`Window > Package Manager`), click `+`, select **Add package from git URL...**, and enter:
```
https://github.com/Amaan12/GoogleSheetsFetcher.git
```

---

## Features

- **Auth Modes**: `PublicUrl` (Share link or ID), `ApiKey` (Google Cloud API Key), and `AppsScriptSecretUrl` (Web App secret link).
- **Formats**: `TSV`, `CSV`, `TXT_TSV`, `TXT_CSV`, `JSON`, `HTML`, `XLSX`, `PDF`.
- **0-Based Tab Index (`0, 1, 2...`)**: Automatically resolves tab position (`0` = first tab, `1` = second tab, etc.) across all auth modes.
- **Standalone Batch Registry (`GoogleSheetsRegistrySO`)**: Batch downloads multiple sheet configs in parallel into a configurable folder (defaults to `Assets/Samples/GoogleSheetsFetcher/v1.0.0/Sheets`) with an Inspector folder browser.
- **Generic Reusable Editor (`ISheetSyncable` + `SheetSyncEditor<T>`)**: Adds a compact `[↻ Sync Sheet] [⚙]` toolbar to any `ScriptableObject` or `MonoBehaviour` Inspector while keeping URLs hidden by default and stripped from builds.
- **`[TableView]` Attribute**: Renders any `List<T>` or `T[]` of serializable structs/classes as a multi-column spreadsheet table in the Inspector (`[TableView(readOnly: false, showIndex: true, maxHeight: 0f)]`).
- **Global Parallel Sync (`Tools/Amaan12/GoogleSheetsFetcher/SyncAllEditors`)**: Triggers every `SheetSyncEditor<T>` (SOs & MonoBehaviours) across the project in one parallel batch.

---

## 1. Using `ISheetSyncable` + `SheetSyncEditor<T>` on a Custom SO or MonoBehaviour

### Step A — Implement `ISheetSyncable` on your SO (or MonoBehaviour)
```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleSheetsFetcher;

[Serializable]
public struct ItemRow
{
    public string id;
    public string name;
}

[CreateAssetMenu(menuName = "Sample/ItemDatabase")]
public class ItemDatabaseSO : ScriptableObject, ISheetSyncable
{
#if UNITY_EDITOR
    [SerializeField] private SheetConfig sheetConfig = new SheetConfig();
    public SheetConfig SheetConfig => sheetConfig;
#endif

    [TableView]
    public List<ItemRow> items = new List<ItemRow>();
}
```

### Step B — Inherit from `SheetSyncEditor<T>` in an Editor script
```csharp
#if UNITY_EDITOR
using UnityEditor;
using GoogleSheetsFetcher.Editor;

[CustomEditor(typeof(ItemDatabaseSO))]
public class ItemDatabaseSOEditor : SheetSyncEditor<ItemDatabaseSO>
{
    protected override void OnSheetFetched(ItemDatabaseSO targetObject, string content)
    {
        // Custom parsing and assignment logic for this specific SO
    }
}
#endif
```

---

## 2. Standalone Batch Registry (`GoogleSheetsRegistrySO`)

1. Right-click in the Project window → **`Create > Google Sheets Fetcher > Sheet Registry`**.
2. Choose an output folder using **`Browse...`** (defaults to `Assets/Samples/GoogleSheetsFetcher/v1.0.0/Sheets`).
3. Add multiple `SheetConfig` entries and click **`Fetch & Save All Sheets`**.

---

## 3. Google Apps Script Snippet (For `AppsScriptSecretUrl` Mode)

To fetch **private** sheets without setting up Google Cloud API keys:
1. Open your Google Sheet → **Extensions > Apps Script**.
2. Paste the script below and click **Deploy > New deployment > Web app** (*Execute as: Me*, *Who has access: Anyone*).
3. Paste the Web App URL into `Apps Script URL` and `MY_SECRET_TOKEN` into `Secret Key / Token`.

```javascript
const SECRET_KEY = "MY_SECRET_TOKEN"; // Leave "" to disable secret check

function doGet(e) {
  const params = (e && e.parameter) ? e.parameter : {};
  if (SECRET_KEY && params.key !== SECRET_KEY && params.secret !== SECRET_KEY) {
    return ContentService.createTextOutput("Unauthorized").setMimeType(ContentService.MimeType.TEXT);
  }

  const tabIndex = parseInt(params.tabIndex || params.tab || "0", 10);
  const format = (params.format || "tsv").toLowerCase();

  const spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
  const sheets = spreadsheet.getSheets();
  if (tabIndex < 0 || tabIndex >= sheets.length) {
    return ContentService.createTextOutput("Tab index out of range").setMimeType(ContentService.MimeType.TEXT);
  }

  const values = sheets[tabIndex].getDataRange().getDisplayValues();
  if (format === "json") {
    const headers = values[0] || [];
    const rows = values.slice(1).map(r => {
      const obj = {};
      headers.forEach((h, i) => obj[h] = r[i] !== undefined ? r[i] : "");
      return obj;
    });
    return ContentService.createTextOutput(JSON.stringify(rows, null, 2))
      .setMimeType(ContentService.MimeType.JSON);
  }

  const delimiter = format === "csv" ? "," : "\t";
  const output = values.map(row => row.map(cell => {
    const str = String(cell ?? "");
    return (format === "csv" && (str.includes(",") || str.includes('"') || str.includes("\n")))
      ? '"' + str.replace(/"/g, '""') + '"'
      : str;
  }).join(delimiter)).join("\n");

  return ContentService.createTextOutput(output).setMimeType(ContentService.MimeType.TEXT);
}
```
