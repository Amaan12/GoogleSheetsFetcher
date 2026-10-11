using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GoogleSheetsFetcher.Editor
{
    /// <summary>
    /// Generic base Editor for any <see cref="ScriptableObject"/> or <see cref="MonoBehaviour"/>
    /// that implements <see cref="ISheetSyncable"/>.
    /// Draws a [Fetch &amp; Sync Sheet] button and a [⚙] gear toggle that reveals/hides the SheetConfig.
    /// Inherit from this class and override <see cref="OnSheetFetched(T, string)"/> to parse &amp; assign data.
    /// </summary>
    public abstract class SheetSyncEditor<T> : UnityEditor.Editor, ISheetSyncEditor
        where T : Object, ISheetSyncable
    {
        private bool _showGearSettings;
        private bool _isFetching;

        private string ShowSettingsPrefKey =>
            $"GoogleSheetsFetcher.ShowGear.{ (target != null ? target.GetInstanceID() : 0) }";

        protected virtual string FetchButtonLabel => "Sync Sheet";

        protected virtual void OnEnable()
        {
            _showGearSettings = SessionState.GetBool(ShowSettingsPrefKey, false);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            SerializedProperty sheetConfigProp = FindSheetConfigProperty(serializedObject);

            DrawSyncToolbar(sheetConfigProp);

            if (_showGearSettings)
            {
                DrawGearSettingsBox(sheetConfigProp);
            }

            EditorGUILayout.Space(4f);

            // Draw all normal fields of the SO / MonoBehaviour EXCEPT the SheetConfig property
            DrawInspectorExcludingSheetConfig(sheetConfigProp != null ? sheetConfigProp.name : null);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawSyncToolbar(SerializedProperty sheetConfigProp)
        {
            const float buttonHeight = 24f;

            string label = _isFetching ? "Syncing..." : FetchButtonLabel;
            GUIContent syncContent = GetSyncButtonContent(label);
            GUIContent gearContent = GetGearIconContent();

            float syncWidth = GUI.skin.button.CalcSize(syncContent).x;
            float gearWidth = Mathf.Max(buttonHeight + 4f, GUI.skin.button.CalcSize(gearContent).x);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_isFetching && target != null;
            if (GUILayout.Button(syncContent, GUILayout.Width(syncWidth), GUILayout.Height(buttonHeight)))
            {
                serializedObject.ApplyModifiedProperties();
                SyncEditorAsync(refreshAssetDatabase: true).Forget();
            }
            GUI.enabled = true;

            Color prevColor = GUI.backgroundColor;
            if (_showGearSettings)
            {
                GUI.backgroundColor = new Color(0.65f, 0.85f, 1f, 1f);
            }

            if (GUILayout.Button(gearContent, GUILayout.Width(gearWidth), GUILayout.Height(buttonHeight)))
            {
                _showGearSettings = !_showGearSettings;
                SessionState.SetBool(ShowSettingsPrefKey, _showGearSettings);
            }

            GUI.backgroundColor = prevColor;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGearSettingsBox(SerializedProperty sheetConfigProp)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Google Sheet Config", EditorStyles.boldLabel);

            if (sheetConfigProp != null)
            {
                sheetConfigProp.isExpanded = true;
                EditorGUILayout.PropertyField(sheetConfigProp, GUIContent.none, true);
            }
            else if (target is T typedTarget && typedTarget.SheetConfig != null)
            {
                DrawFallbackSheetConfigFields(typedTarget, typedTarget.SheetConfig);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "No serialized SheetConfig field found on this target. Add '[SerializeField] private SheetConfig sheetConfig;' and return it from ISheetSyncable.SheetConfig.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawFallbackSheetConfigFields(T typedTarget, SheetConfig cfg)
        {
            EditorGUI.BeginChangeCheck();

            string newName = EditorGUILayout.TextField("Sheet / File Name", cfg.sheetName);
            SheetAuthMethod newAuth = (SheetAuthMethod)EditorGUILayout.EnumPopup("Auth Method", cfg.authMethod);
            string urlLabel = newAuth == SheetAuthMethod.AppsScriptSecretUrl ? "Apps Script URL" : "Sheet URL or ID";
            string newUrl = EditorGUILayout.TextField(urlLabel, cfg.urlOrId);
            string newSecret = cfg.apiKeyOrSecret;
            if (newAuth != SheetAuthMethod.PublicUrl)
            {
                string secretLabel = newAuth == SheetAuthMethod.ApiKey ? "Google API Key" : "Secret Key / Token";
                newSecret = EditorGUILayout.TextField(secretLabel, cfg.apiKeyOrSecret);
            }
            SheetFormat newFormat = (SheetFormat)EditorGUILayout.EnumPopup("Export Format", cfg.format);
            int newTab = Mathf.Max(0, EditorGUILayout.IntField("Tab Index (0 = First)", cfg.tabIndex));

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(typedTarget, "Modify Sheet Config");
                cfg.sheetName = newName;
                cfg.authMethod = newAuth;
                cfg.urlOrId = newUrl;
                cfg.apiKeyOrSecret = newSecret;
                cfg.format = newFormat;
                cfg.tabIndex = newTab;
                EditorUtility.SetDirty(typedTarget);
            }
        }

        private void DrawInspectorExcludingSheetConfig(string sheetConfigPropName)
        {
            SerializedProperty iterator = serializedObject.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                if (iterator.name == "m_Script")
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(sheetConfigPropName) && iterator.name == sheetConfigPropName)
                {
                    continue;
                }

                if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String)
                {
                    System.Reflection.FieldInfo fieldInfo = FindFieldOnTarget(target.GetType(), iterator.name);
                    if (fieldInfo != null)
                    {
                        var tableAttr = System.Attribute.GetCustomAttribute(fieldInfo, typeof(TableViewAttribute)) as TableViewAttribute;
                        if (tableAttr != null)
                        {
                            TableViewLayoutDrawer.DrawTable(iterator, tableAttr, fieldInfo);
                            continue;
                        }
                    }
                }

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private static System.Reflection.FieldInfo FindFieldOnTarget(System.Type type, string fieldName)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly;

            while (type != null && type != typeof(Object))
            {
                System.Reflection.FieldInfo field = type.GetField(fieldName, flags);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }

        private static SerializedProperty FindSheetConfigProperty(SerializedObject so)
        {
            if (so == null)
                return null;

            SerializedProperty iterator = so.GetIterator();
            bool enterChildren = true;
            while (iterator.Next(enterChildren))
            {
                enterChildren = false;
                if (iterator.type == nameof(SheetConfig))
                {
                    return so.FindProperty(iterator.name);
                }
            }

            return null;
        }

        private static GUIContent GetSyncButtonContent(string label)
        {
            GUIContent icon = EditorGUIUtility.IconContent("d_Refresh");
            if (icon != null && icon.image != null)
            {
                return new GUIContent($" {label}", icon.image, "Fetch and sync Google Sheet into this object");
            }
            return new GUIContent($"{label} ↻", "Fetch and sync Google Sheet into this object");
        }

        private static GUIContent GetGearIconContent()
        {
            GUIContent icon = EditorGUIUtility.IconContent("d_Settings");
            if (icon != null && icon.image != null)
            {
                return new GUIContent(icon.image, "Toggle Sheet Fetcher Config");
            }
            return new GUIContent("⚙", "Toggle Sheet Fetcher Config");
        }

        /// <summary>
        /// Triggers the sheet fetch, invokes <see cref="OnSheetFetched(T, string)"/>, and marks the target dirty.
        /// Called both by the Inspector button and by Tools/Amaan12/GoogleSheetsFetcher/SyncAllEditors.
        /// </summary>
        public async UniTask<bool> SyncEditorAsync(bool refreshAssetDatabase = true)
        {
            if (target == null || !(target is T typedTarget))
                return false;

            SheetConfig config = typedTarget.SheetConfig;
            if (config == null)
            {
                Debug.LogWarning($"[GoogleSheetsFetcher] '{typedTarget.name}' has a null SheetConfig.", typedTarget);
                return false;
            }

            _isFetching = true;
            Repaint();

            try
            {
                SheetFetchResult result = await GoogleSheetsFetcher.FetchAsync(config);
                if (!result.Success)
                {
                    Debug.LogError($"[GoogleSheetsFetcher] Failed to sync '{typedTarget.name}': {result.Error}", typedTarget);
                    return false;
                }

                Undo.RecordObject(typedTarget, $"Sync Sheet ({typedTarget.name})");
                OnSheetFetched(typedTarget, result.Text);
                OnSheetFetchedResult(typedTarget, result);
                EditorUtility.SetDirty(typedTarget);

                if (typedTarget is Component component && component.gameObject.scene.IsValid())
                {
                    EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }

                if (refreshAssetDatabase)
                {
                    AssetDatabase.SaveAssetIfDirty(typedTarget);
                }

                Debug.Log($"[GoogleSheetsFetcher] Synced '{typedTarget.name}' from sheet '{config.sheetName}' (Tab {config.tabIndex}).", typedTarget);
                return true;
            }
            finally
            {
                _isFetching = false;
                Repaint();
            }
        }

        /// <summary>
        /// Override this method in your custom Editor to parse the fetched text and populate <paramref name="targetObject"/>.
        /// </summary>
        protected abstract void OnSheetFetched(T targetObject, string content);

        /// <summary>
        /// Optional override if your custom Editor needs raw bytes (e.g. for XLSX/PDF) or full <see cref="SheetFetchResult"/> metadata.
        /// </summary>
        protected virtual void OnSheetFetchedResult(T targetObject, SheetFetchResult result) { }
    }
}
