using System.Collections.Generic;
using UnityEngine;

namespace GoogleSheetsFetcher
{
    [CreateAssetMenu(
        fileName = "GoogleSheetsRegistry",
        menuName = "Google Sheets Fetcher/Sheet Registry",
        order = 0)]
    public class GoogleSheetsRegistrySO : ScriptableObject
    {
        public const string DefaultOutputFolder = "Assets/Samples/GoogleSheetsFetcher/v1.0.0/Sheets";

#if UNITY_EDITOR
        [Tooltip("Project-relative folder path where all sheets in this registry will be saved.")]
        [SerializeField] private string outputFolderPath = DefaultOutputFolder;

        [Tooltip("List of Google Sheet configurations to batch download.")]
        [SerializeField] private List<SheetConfig> sheets = new List<SheetConfig>();

        public string OutputFolderPath
        {
            get => string.IsNullOrWhiteSpace(outputFolderPath) ? DefaultOutputFolder : outputFolderPath;
            set => outputFolderPath = value;
        }

        public List<SheetConfig> Sheets => sheets;
#endif
    }
}
