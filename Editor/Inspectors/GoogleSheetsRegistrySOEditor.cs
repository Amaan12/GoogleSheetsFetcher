using System.IO;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace GoogleSheetsFetcher.Editor
{
    [CustomEditor(typeof(GoogleSheetsRegistrySO))]
    public class GoogleSheetsRegistrySOEditor : UnityEditor.Editor
    {
        private SerializedProperty _outputFolderPathProp;
        private SerializedProperty _sheetsProp;
        private bool _isFetching;

        private void OnEnable()
        {
            _outputFolderPathProp = serializedObject.FindProperty("outputFolderPath");
            _sheetsProp = serializedObject.FindProperty("sheets");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var registry = (GoogleSheetsRegistrySO)target;

            DrawHeaderActionBar(registry);
            EditorGUILayout.Space(4f);
            DrawOutputFolderPicker();
            EditorGUILayout.Space(6f);

            EditorGUILayout.PropertyField(_sheetsProp, new GUIContent("Sheet Configs"), true);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeaderActionBar(GoogleSheetsRegistrySO registry)
        {
            GUI.enabled = !_isFetching && registry != null && registry.Sheets != null && registry.Sheets.Count > 0;
            string buttonText = _isFetching
                ? "Fetching All Sheets in Parallel..."
                : $"Fetch & Save All Sheets ({(registry.Sheets != null ? registry.Sheets.Count : 0)})";

            if (GUILayout.Button(buttonText, GUILayout.Height(28f)))
            {
                serializedObject.ApplyModifiedProperties();
                SyncEditorAsync(refreshAssetDatabase: true).Forget();
            }
            GUI.enabled = true;
        }

        private void DrawOutputFolderPicker()
        {
            EditorGUILayout.LabelField("Output Directory", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            if (string.IsNullOrWhiteSpace(_outputFolderPathProp.stringValue))
            {
                _outputFolderPathProp.stringValue = GoogleSheetsRegistrySO.DefaultOutputFolder;
            }

            EditorGUILayout.PropertyField(_outputFolderPathProp, GUIContent.none);

            if (GUILayout.Button("Browse...", GUILayout.Width(75f)))
            {
                string currentFolder = _outputFolderPathProp.stringValue;
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
                string startDir = Path.Combine(projectRoot, currentFolder).Replace('\\', '/');

                if (!Directory.Exists(startDir))
                {
                    startDir = Application.dataPath;
                }

                string selectedAbsPath = EditorUtility.OpenFolderPanel("Select Output Folder for Sheets", startDir, "");
                if (!string.IsNullOrEmpty(selectedAbsPath))
                {
                    selectedAbsPath = selectedAbsPath.Replace('\\', '/');
                    if (selectedAbsPath.StartsWith(projectRoot))
                    {
                        string relativePath = selectedAbsPath.Substring(projectRoot.Length).TrimStart('/');
                        _outputFolderPathProp.stringValue = string.IsNullOrEmpty(relativePath)
                            ? GoogleSheetsRegistrySO.DefaultOutputFolder
                            : relativePath;
                    }
                    else
                    {
                        _outputFolderPathProp.stringValue = selectedAbsPath;
                    }

                    serializedObject.ApplyModifiedProperties();
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button("Reset", GUILayout.Width(52f)))
            {
                _outputFolderPathProp.stringValue = GoogleSheetsRegistrySO.DefaultOutputFolder;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.EndHorizontal();
        }

        public async UniTask<bool> SyncEditorAsync(bool refreshAssetDatabase = true)
        {
            if (!(target is GoogleSheetsRegistrySO registry) || registry == null)
                return false;

            if (registry.Sheets == null || registry.Sheets.Count == 0)
            {
                Debug.LogWarning($"[GoogleSheetsFetcher] Registry '{registry.name}' has no sheets configured.", registry);
                return false;
            }

            _isFetching = true;
            Repaint();

            try
            {
                var savedFiles = await GoogleSheetsFetcher.FetchBatchAndSaveFilesAsync(
                    registry.Sheets,
                    registry.OutputFolderPath,
                    refreshAssetDatabase);

                return savedFiles.Count > 0;
            }
            finally
            {
                _isFetching = false;
                Repaint();
            }
        }
    }
}
