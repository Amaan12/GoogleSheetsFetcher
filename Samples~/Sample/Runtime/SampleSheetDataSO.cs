using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoogleSheetsFetcher.Samples
{
    [Serializable]
    public struct SampleSheetRow
    {
        public string id;
        public string english;
        public string french;
    }

    [CreateAssetMenu(
        fileName = "SampleSheetDataSO",
        menuName = "Google Sheets Fetcher/Samples/Sample Sheet Data SO",
        order = 10)]
    public class SampleSheetDataSO : ScriptableObject, ISheetSyncable
    {
        public const string SamplePublicUrl =
            "https://docs.google.com/spreadsheets/d/1olneD8WjrGBj6i2xjKTpuxs317dc6wI4uONzNdOD94I/edit?gid=0#gid=0";

#if UNITY_EDITOR
        [SerializeField]
        private SheetConfig sheetConfig = new SheetConfig
        {
            sheetName = "SampleLocalizationSheet",
            authMethod = SheetAuthMethod.PublicUrl,
            urlOrId = SamplePublicUrl,
            format = SheetFormat.TSV,
            tabIndex = 0
        };

        public SheetConfig SheetConfig => sheetConfig;
#endif

        [Header("Synced Data (Populated via SheetSyncEditor)")]
        [TableView]
        public List<SampleSheetRow> rows = new List<SampleSheetRow>();
    }
}
