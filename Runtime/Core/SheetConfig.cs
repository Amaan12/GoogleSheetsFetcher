using System;
using UnityEngine;

namespace GoogleSheetsFetcher
{
    public enum SheetAuthMethod
    {
        [InspectorName("Public URL / ID")]
        PublicUrl = 0,

        [InspectorName("Google API Key")]
        ApiKey = 1,

        [InspectorName("Apps Script Secret URL")]
        AppsScriptSecretUrl = 2
    }

    public enum SheetFormat
    {
        [InspectorName("TSV (.tsv)")]
        TSV = 0,

        [InspectorName("CSV (.csv)")]
        CSV = 1,

        [InspectorName("TXT - Tab Separated (.txt)")]
        TXT_TSV = 2,

        [InspectorName("TXT - Comma Separated (.txt)")]
        TXT_CSV = 3,

        [InspectorName("JSON (.json)")]
        JSON = 4,

        [InspectorName("HTML (.html)")]
        HTML = 5,

        [InspectorName("Excel (.xlsx)")]
        XLSX = 6,

        [InspectorName("PDF (.pdf)")]
        PDF = 7
    }

    [Serializable]
    public class SheetConfig
    {
        [Tooltip("File name (without extension) used when saving this sheet to disk.")]
        public string sheetName = "SheetData";

        [Tooltip("How to authenticate and fetch the Google Sheet.")]
        public SheetAuthMethod authMethod = SheetAuthMethod.PublicUrl;

        [Tooltip("Full Google Sheet share/edit URL, Apps Script Web App URL, or raw Spreadsheet ID.")]
        public string urlOrId = "";

        [Tooltip("Google Cloud API Key (for ApiKey mode) or Secret Token (for AppsScriptSecretUrl mode).")]
        public string apiKeyOrSecret = "";

        [Tooltip("Output format of the fetched spreadsheet.")]
        public SheetFormat format = SheetFormat.TXT_TSV;

        [Min(0)]
        [Tooltip("0-based tab index in the spreadsheet (0 = first tab, 1 = second tab, etc.).")]
        public int tabIndex = 0;

        public string GetFileExtension()
        {
            return format switch
            {
                SheetFormat.TSV => ".tsv",
                SheetFormat.CSV => ".csv",
                SheetFormat.TXT_TSV => ".txt",
                SheetFormat.TXT_CSV => ".txt",
                SheetFormat.JSON => ".json",
                SheetFormat.HTML => ".html",
                SheetFormat.XLSX => ".xlsx",
                SheetFormat.PDF => ".pdf",
                _ => ".txt"
            };
        }

        public string GetGoogleExportFormatParam()
        {
            return format switch
            {
                SheetFormat.TSV => "tsv",
                SheetFormat.TXT_TSV => "tsv",
                SheetFormat.CSV => "csv",
                SheetFormat.TXT_CSV => "csv",
                SheetFormat.JSON => "tsv", // Fetched as TSV/values and serialized into clean JSON array
                SheetFormat.HTML => "html",
                SheetFormat.XLSX => "xlsx",
                SheetFormat.PDF => "pdf",
                _ => "tsv"
            };
        }

        public string GetDriveExportMimeType()
        {
            return format switch
            {
                SheetFormat.TSV => "text/tab-separated-values",
                SheetFormat.TXT_TSV => "text/tab-separated-values",
                SheetFormat.CSV => "text/csv",
                SheetFormat.TXT_CSV => "text/csv",
                SheetFormat.JSON => "text/tab-separated-values",
                SheetFormat.HTML => "text/html",
                SheetFormat.XLSX => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                SheetFormat.PDF => "application/pdf",
                _ => "text/tab-separated-values"
            };
        }

        public bool IsBinaryFormat()
        {
            return format == SheetFormat.XLSX || format == SheetFormat.PDF;
        }
    }
}
