using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace GoogleSheetsFetcher.Editor
{
    public readonly struct SheetFetchResult
    {
        public readonly bool Success;
        public readonly string Text;
        public readonly byte[] RawBytes;
        public readonly string Error;
        public readonly SheetConfig Config;

        public SheetFetchResult(bool success, string text, byte[] rawBytes, string error, SheetConfig config)
        {
            Success = success;
            Text = text;
            RawBytes = rawBytes;
            Error = error;
            Config = config;
        }

        public static SheetFetchResult Fail(SheetConfig config, string error) =>
            new SheetFetchResult(false, null, null, error, config);

        public static SheetFetchResult Ok(SheetConfig config, string text, byte[] rawBytes) =>
            new SheetFetchResult(true, text, rawBytes, null, config);
    }

    public static class GoogleSheetsFetcher
    {
        private static readonly Regex SpreadsheetIdRegex =
            new Regex(@"/spreadsheets/d/([a-zA-Z0-9-_]+)", RegexOptions.Compiled);

        private static readonly Regex HtmlViewGidRegex =
            new Regex(@"items\.push\(\{name:\s*""([^""]*)"".*?gid:\s*""(\d+)""", RegexOptions.Compiled);

        private static readonly Regex ValuesArrayRegex =
            new Regex(@"""values""\s*:\s*\[(.*)\]\s*\}", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex RowArrayRegex =
            new Regex(@"\[([^\[\]]*)\]", RegexOptions.Compiled);

        private static readonly Regex CellStringRegex =
            new Regex(@"""((?:\\.|[^""\\])*)""", RegexOptions.Compiled);

        private static readonly Regex SheetPropertiesRegex =
            new Regex(@"""sheetId""\s*:\s*(\d+)\s*,\s*""title""\s*:\s*""((?:\\.|[^""\\])*)""\s*,\s*""index""\s*:\s*(\d+)", RegexOptions.Compiled);

        /// <summary>
        /// Fetches a single sheet asynchronously based on the provided <see cref="SheetConfig"/>.
        /// </summary>
        public static async UniTask<SheetFetchResult> FetchAsync(SheetConfig config)
        {
            if (config == null)
                return SheetFetchResult.Fail(null, "SheetConfig is null.");

            if (string.IsNullOrWhiteSpace(config.urlOrId))
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] URL or Spreadsheet ID is empty.");

            try
            {
                return config.authMethod switch
                {
                    SheetAuthMethod.PublicUrl => await FetchPublicAsync(config),
                    SheetAuthMethod.ApiKey => await FetchWithApiKeyAsync(config),
                    SheetAuthMethod.AppsScriptSecretUrl => await FetchWithAppsScriptAsync(config),
                    _ => SheetFetchResult.Fail(config, $"Unsupported auth method: {config.authMethod}")
                };
            }
            catch (Exception ex)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Exception while fetching sheet: {ex.Message}");
            }
        }

        /// <summary>
        /// Fetches multiple sheets concurrently in a single parallel batch.
        /// </summary>
        public static async UniTask<SheetFetchResult[]> FetchBatchAsync(IReadOnlyList<SheetConfig> configs)
        {
            if (configs == null || configs.Count == 0)
                return Array.Empty<SheetFetchResult>();

            var tasks = new UniTask<SheetFetchResult>[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                tasks[i] = FetchAsync(configs[i]);
            }

            return await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// Fetches a single sheet and saves it to <paramref name="outputFolderPath"/>.
        /// </summary>
        public static async UniTask<string> FetchAndSaveFileAsync(
            SheetConfig config,
            string outputFolderPath,
            bool refreshAssetDatabase = true)
        {
            var result = await FetchAsync(config);
            if (!result.Success)
            {
                Debug.LogError($"[GoogleSheetsFetcher] {result.Error}");
                return null;
            }

            string savedPath = SaveResultToFile(result, outputFolderPath);
            if (refreshAssetDatabase && !string.IsNullOrEmpty(savedPath))
            {
                AssetDatabase.Refresh();
            }

            return savedPath;
        }

        /// <summary>
        /// Fetches multiple sheets in parallel and writes them to <paramref name="outputFolderPath"/>,
        /// refreshing the AssetDatabase only once at the end.
        /// </summary>
        public static async UniTask<List<string>> FetchBatchAndSaveFilesAsync(
            IReadOnlyList<SheetConfig> configs,
            string outputFolderPath,
            bool refreshAssetDatabase = true)
        {
            var savedPaths = new List<string>();
            if (configs == null || configs.Count == 0)
                return savedPaths;

            SheetFetchResult[] results = await FetchBatchAsync(configs);

            for (int i = 0; i < results.Length; i++)
            {
                var result = results[i];
                if (!result.Success)
                {
                    Debug.LogError($"[GoogleSheetsFetcher] {result.Error}");
                    continue;
                }

                string path = SaveResultToFile(result, outputFolderPath);
                if (!string.IsNullOrEmpty(path))
                {
                    savedPaths.Add(path);
                }
            }

            if (refreshAssetDatabase && savedPaths.Count > 0)
            {
                AssetDatabase.Refresh();
            }

            return savedPaths;
        }

        private static string SaveResultToFile(SheetFetchResult result, string outputFolderPath)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath))
                outputFolderPath = GoogleSheetsRegistrySO.DefaultOutputFolder;

            if (!Directory.Exists(outputFolderPath))
            {
                Directory.CreateDirectory(outputFolderPath);
            }

            string baseName = string.IsNullOrWhiteSpace(result.Config.sheetName)
                ? "SheetData"
                : SanitizeFileName(result.Config.sheetName.Trim());

            string filePath = Path.Combine(outputFolderPath, baseName + result.Config.GetFileExtension())
                .Replace('\\', '/');

            if (result.Config.IsBinaryFormat() && result.RawBytes != null)
            {
                File.WriteAllBytes(filePath, result.RawBytes);
            }
            else
            {
                File.WriteAllText(filePath, result.Text ?? string.Empty, Encoding.UTF8);
            }

            Debug.Log($"[GoogleSheetsFetcher] Saved '{result.Config.sheetName}' (Tab {result.Config.tabIndex}) -> {filePath}");
            return filePath;
        }

        // =====================================================================
        // 1. PUBLIC URL MODE
        // =====================================================================
        private static async UniTask<SheetFetchResult> FetchPublicAsync(SheetConfig config)
        {
            string spreadsheetId = ExtractSpreadsheetId(config.urlOrId);
            if (string.IsNullOrEmpty(spreadsheetId))
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Could not extract Spreadsheet ID from '{config.urlOrId}'.");
            }

            string exportFormat = config.GetGoogleExportFormatParam();
            string exportUrl;

            if (config.tabIndex <= 0)
            {
                // In Google Sheets, omitting &gid= automatically exports the first tab (index 0)
                exportUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format={exportFormat}";
            }
            else
            {
                var gidResult = await ResolvePublicTabGidAsync(spreadsheetId, config.tabIndex, config.sheetName);
                if (!gidResult.success)
                {
                    return SheetFetchResult.Fail(config, gidResult.error);
                }

                exportUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format={exportFormat}&gid={gidResult.gid}";
            }

            var download = await SendGetRequestAsync(exportUrl);
            if (!download.success)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] HTTP Error fetching public sheet: {download.error}");
            }

            string textContent = download.text;
            if (config.format == SheetFormat.JSON)
            {
                textContent = ConvertDelimitedToJson(textContent, '\t');
            }

            return SheetFetchResult.Ok(config, textContent, download.bytes);
        }

        private static async UniTask<(bool success, string gid, string error)> ResolvePublicTabGidAsync(
            string spreadsheetId,
            int tabIndex,
            string sheetName)
        {
            string htmlViewUrl = $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/htmlview";
            var response = await SendGetRequestAsync(htmlViewUrl);
            if (!response.success)
            {
                return (false, null, $"[{sheetName}] Failed to resolve tab index {tabIndex} via htmlview: {response.error}");
            }

            MatchCollection matches = HtmlViewGidRegex.Matches(response.text);
            if (matches.Count == 0)
            {
                return (false, null, $"[{sheetName}] Could not parse tab list from public sheet. Ensure the sheet is shared as 'Anyone with the link can view'.");
            }

            if (tabIndex < 0 || tabIndex >= matches.Count)
            {
                return (false, null, $"[{sheetName}] Tab index {tabIndex} is out of range. Spreadsheet has {matches.Count} tab(s) (valid indices: 0 to {matches.Count - 1}).");
            }

            string gid = matches[tabIndex].Groups[2].Value;
            return (true, gid, null);
        }

        // =====================================================================
        // 2. GOOGLE API KEY MODE (Sheets API v4 / Drive API v3)
        // =====================================================================
        private static async UniTask<SheetFetchResult> FetchWithApiKeyAsync(SheetConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.apiKeyOrSecret))
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Google API Key is required when using ApiKey mode.");
            }

            string spreadsheetId = ExtractSpreadsheetId(config.urlOrId);
            string apiKey = config.apiKeyOrSecret.Trim();

            // Step 1: Query spreadsheet metadata to resolve 0-based tabIndex -> title and sheetId (gid)
            string metaUrl =
                $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}?fields=sheets.properties(sheetId,title,index)&key={Uri.EscapeDataString(apiKey)}";

            var metaResponse = await SendGetRequestAsync(metaUrl);
            if (!metaResponse.success)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Sheets API metadata request failed: {metaResponse.error}\n{metaResponse.text}");
            }

            var tabs = ParseSheetProperties(metaResponse.text);
            if (tabs.Count == 0)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] No sheet tabs found in API response.");
            }

            if (config.tabIndex < 0 || config.tabIndex >= tabs.Count)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Tab index {config.tabIndex} is out of range (0..{tabs.Count - 1}).");
            }

            var targetTab = tabs[config.tabIndex];

            // Binary or HTML formats: use export URL with resolved gid or Drive API v3 export
            if (config.IsBinaryFormat() || config.format == SheetFormat.HTML)
            {
                string exportUrl =
                    $"https://docs.google.com/spreadsheets/d/{spreadsheetId}/export?format={config.GetGoogleExportFormatParam()}&gid={targetTab.sheetId}";
                var binDownload = await SendGetRequestAsync(exportUrl);
                if (!binDownload.success)
                {
                    // Fallback to Drive API v3 export endpoint
                    string driveExportUrl =
                        $"https://www.googleapis.com/drive/v3/files/{spreadsheetId}/export?mimeType={Uri.EscapeDataString(config.GetDriveExportMimeType())}&key={Uri.EscapeDataString(apiKey)}";
                    binDownload = await SendGetRequestAsync(driveExportUrl);
                }

                if (!binDownload.success)
                {
                    return SheetFetchResult.Fail(config, $"[{config.sheetName}] Export failed: {binDownload.error}");
                }

                return SheetFetchResult.Ok(config, binDownload.text, binDownload.bytes);
            }

            // Text / Tabular formats (TSV, CSV, TXT_TSV, TXT_CSV, JSON): fetch exact values via Sheets API v4
            string valuesUrl =
                $"https://sheets.googleapis.com/v4/spreadsheets/{spreadsheetId}/values/{Uri.EscapeDataString(targetTab.title)}?key={Uri.EscapeDataString(apiKey)}";

            var valuesResponse = await SendGetRequestAsync(valuesUrl);
            if (!valuesResponse.success)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Failed to fetch values for tab '{targetTab.title}': {valuesResponse.error}");
            }

            List<List<string>> rows = ParseSheetsApiValuesJson(valuesResponse.text);
            string formattedText = FormatRows(rows, config.format);
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(formattedText);

            return SheetFetchResult.Ok(config, formattedText, utf8Bytes);
        }

        // =====================================================================
        // 3. APPS SCRIPT SECRET URL MODE
        // =====================================================================
        private static async UniTask<SheetFetchResult> FetchWithAppsScriptAsync(SheetConfig config)
        {
            string baseUrl = config.urlOrId.Trim();
            string separator = baseUrl.Contains("?") ? "&" : "?";
            string exportFormat = config.format == SheetFormat.JSON ? "json" : config.GetGoogleExportFormatParam();

            var urlBuilder = new StringBuilder(baseUrl);
            urlBuilder.Append(separator);
            urlBuilder.Append("tabIndex=").Append(config.tabIndex);
            urlBuilder.Append("&tab=").Append(config.tabIndex);
            urlBuilder.Append("&format=").Append(exportFormat);

            if (!string.IsNullOrWhiteSpace(config.apiKeyOrSecret))
            {
                string encodedSecret = Uri.EscapeDataString(config.apiKeyOrSecret.Trim());
                urlBuilder.Append("&key=").Append(encodedSecret);
                urlBuilder.Append("&secret=").Append(encodedSecret);
            }

            var response = await SendGetRequestAsync(urlBuilder.ToString());
            if (!response.success)
            {
                return SheetFetchResult.Fail(config, $"[{config.sheetName}] Apps Script request failed: {response.error}");
            }

            string textContent = response.text;
            if (config.format == SheetFormat.JSON && !string.IsNullOrWhiteSpace(textContent))
            {
                string trimmed = textContent.TrimStart();
                if (!trimmed.StartsWith("[") && !trimmed.StartsWith("{"))
                {
                    textContent = ConvertDelimitedToJson(textContent, '\t');
                }
            }

            return SheetFetchResult.Ok(config, textContent, response.bytes);
        }

        // =====================================================================
        // HELPERS & PARSERS
        // =====================================================================
        public static string ExtractSpreadsheetId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId))
                return string.Empty;

            string trimmed = urlOrId.Trim();
            Match match = SpreadsheetIdRegex.Match(trimmed);
            if (match.Success)
                return match.Groups[1].Value;

            // If the user pasted a raw Spreadsheet ID directly
            if (!trimmed.Contains("/") && !trimmed.Contains(":"))
                return trimmed;

            return string.Empty;
        }

        private static async UniTask<(bool success, string text, byte[] bytes, string error)> SendGetRequestAsync(string url)
        {
            using var req = UnityWebRequest.Get(url);
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                await UniTask.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                string errText = req.downloadHandler != null && req.downloadHandler.data != null
                    ? Encoding.UTF8.GetString(req.downloadHandler.data)
                    : string.Empty;
                return (false, errText, null, req.error);
            }

            byte[] data = req.downloadHandler.data;
            string text = data != null ? Encoding.UTF8.GetString(data) : string.Empty;
            return (true, text, data, null);
        }

        private static List<(string sheetId, string title, int index)> ParseSheetProperties(string json)
        {
            var result = new List<(string sheetId, string title, int index)>();
            MatchCollection matches = SheetPropertiesRegex.Matches(json);
            foreach (Match m in matches)
            {
                string sheetId = m.Groups[1].Value;
                string title = Regex.Unescape(m.Groups[2].Value);
                int index = int.TryParse(m.Groups[3].Value, out int parsedIdx) ? parsedIdx : result.Count;
                result.Add((sheetId, title, index));
            }

            result.Sort((a, b) => a.index.CompareTo(b.index));
            return result;
        }

        private static List<List<string>> ParseSheetsApiValuesJson(string json)
        {
            var rows = new List<List<string>>();
            Match valuesMatch = ValuesArrayRegex.Match(json);
            if (!valuesMatch.Success)
                return rows;

            string innerValues = valuesMatch.Groups[1].Value;
            MatchCollection rowMatches = RowArrayRegex.Matches(innerValues);
            foreach (Match rowMatch in rowMatches)
            {
                var row = new List<string>();
                MatchCollection cellMatches = CellStringRegex.Matches(rowMatch.Groups[1].Value);
                foreach (Match cellMatch in cellMatches)
                {
                    row.Add(Regex.Unescape(cellMatch.Groups[1].Value));
                }
                rows.Add(row);
            }

            return rows;
        }

        private static string FormatRows(List<List<string>> rows, SheetFormat format)
        {
            if (rows == null || rows.Count == 0)
                return format == SheetFormat.JSON ? "[]" : string.Empty;

            if (format == SheetFormat.JSON)
            {
                return ConvertRowsToJson(rows);
            }

            bool isCsv = format == SheetFormat.CSV || format == SheetFormat.TXT_CSV;
            var sb = new StringBuilder();

            for (int r = 0; r < rows.Count; r++)
            {
                if (r > 0)
                    sb.Append('\n');

                var row = rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    if (c > 0)
                        sb.Append(isCsv ? ',' : '\t');

                    string cell = row[c] ?? string.Empty;
                    if (isCsv && (cell.Contains(",") || cell.Contains("\"") || cell.Contains("\n") || cell.Contains("\r")))
                    {
                        sb.Append('"').Append(cell.Replace("\"", "\"\"")).Append('"');
                    }
                    else
                    {
                        sb.Append(cell);
                    }
                }
            }

            return sb.ToString();
        }

        private static string ConvertDelimitedToJson(string delimitedText, char delimiter)
        {
            if (string.IsNullOrWhiteSpace(delimitedText))
                return "[]";

            string[] lines = delimitedText.Replace("\r\n", "\n").Split('\n');
            var rows = new List<List<string>>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;
                rows.Add(new List<string>(lines[i].Split(delimiter)));
            }

            return ConvertRowsToJson(rows);
        }

        private static string ConvertRowsToJson(List<List<string>> rows)
        {
            if (rows == null || rows.Count < 2)
                return "[]";

            var headers = rows[0];
            var sb = new StringBuilder();
            sb.Append("[\n");

            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                sb.Append("  {");

                for (int c = 0; c < headers.Count; c++)
                {
                    if (c > 0)
                        sb.Append(", ");

                    string key = EscapeJsonString(headers[c]);
                    string val = c < row.Count ? EscapeJsonString(row[c]) : string.Empty;
                    sb.Append('"').Append(key).Append("\": \"").Append(val).Append('"');
                }

                sb.Append('}');
                if (r < rows.Count - 1)
                    sb.Append(',');
                sb.Append('\n');
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}
