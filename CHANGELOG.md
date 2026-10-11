# Changelog

All notable changes to this package will be documented in this file.

## [1.0.0] - 2026-10-11
### Added
- Initial package release of GoogleSheetsFetcher (`com.amaan.googlesheetsfetcher`).
- Support for Public URLs, Google Cloud API Keys, and Google Apps Script Secret URLs across `TSV`, `CSV`, `TXT_TSV`, `TXT_CSV`, `JSON`, `HTML`, `XLSX`, and `PDF` formats with 0-based tab index (`0, 1, 2...`) resolution.
- Standalone `GoogleSheetsRegistrySO` batch downloader with folder picker.
- Generic `ISheetSyncable` and `SheetSyncEditor<T>` with compact `Sync Sheet` + `⚙` gear toggle toolbar and `Tools/Amaan12/GoogleSheetsFetcher/SyncAllEditors`.
- Zero-dependency `[TableView]` attribute and multi-column spreadsheet table drawer.
