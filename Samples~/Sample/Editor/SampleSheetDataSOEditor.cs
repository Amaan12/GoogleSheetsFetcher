using System;
using GoogleSheetsFetcher.Editor;
using UnityEditor;

namespace GoogleSheetsFetcher.Samples.Editor
{
    [CustomEditor(typeof(SampleSheetDataSO))]
    public class SampleSheetDataSOEditor : SheetSyncEditor<SampleSheetDataSO>
    {
        protected override void OnSheetFetched(SampleSheetDataSO targetObject, string content)
        {
            targetObject.rows.Clear();

            if (string.IsNullOrWhiteSpace(content))
                return;

            string[] lines = content.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Row 0 is header: ID | English | French
            for (int i = 1; i < lines.Length; i++)
            {
                string[] cells = lines[i].Split('\t');
                if (cells.Length == 0 || string.IsNullOrWhiteSpace(cells[0]))
                    continue;

                targetObject.rows.Add(new SampleSheetRow
                {
                    id = cells[0].Trim(),
                    english = cells.Length > 1 ? cells[1].Trim() : string.Empty,
                    french = cells.Length > 2 ? cells[2].Trim() : string.Empty
                });
            }
        }
    }
}
