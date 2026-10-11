using UnityEditor;
using UnityEngine;

namespace GoogleSheetsFetcher.Editor
{
    [CustomPropertyDrawer(typeof(SheetConfig))]
    public class SheetConfigPropertyDrawer : PropertyDrawer
    {
        private const float VerticalSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            var authProp = property.FindPropertyRelative("authMethod");
            bool showSecretField = authProp != null &&
                                   (SheetAuthMethod)authProp.enumValueIndex != SheetAuthMethod.PublicUrl;

            // Foldout header + sheetName + authMethod + urlOrId + (optional apiKeyOrSecret) + format + tabIndex
            int lineCount = showSecretField ? 7 : 6;
            return lineCount * EditorGUIUtility.singleLineHeight + (lineCount - 1) * VerticalSpacing + 4f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var sheetNameProp = property.FindPropertyRelative("sheetName");
            var authProp = property.FindPropertyRelative("authMethod");
            var urlOrIdProp = property.FindPropertyRelative("urlOrId");
            var apiKeyOrSecretProp = property.FindPropertyRelative("apiKeyOrSecret");
            var formatProp = property.FindPropertyRelative("format");
            var tabIndexProp = property.FindPropertyRelative("tabIndex");

            string headerTitle = !string.IsNullOrWhiteSpace(sheetNameProp.stringValue)
                ? $"{sheetNameProp.stringValue}  (Tab {tabIndexProp.intValue} • {(SheetFormat)formatProp.enumValueIndex})"
                : label.text;

            Rect lineRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, headerTitle, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(lineRect, sheetNameProp, new GUIContent("Sheet / File Name"));

                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(lineRect, authProp, new GUIContent("Auth Method"));

                var authMethod = (SheetAuthMethod)authProp.enumValueIndex;
                string urlLabel = authMethod == SheetAuthMethod.AppsScriptSecretUrl
                    ? "Apps Script URL"
                    : "Sheet URL or ID";

                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(lineRect, urlOrIdProp, new GUIContent(urlLabel));

                if (authMethod != SheetAuthMethod.PublicUrl)
                {
                    string secretLabel = authMethod == SheetAuthMethod.ApiKey
                        ? "Google API Key"
                        : "Secret Key / Token";

                    lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                    EditorGUI.PropertyField(lineRect, apiKeyOrSecretProp, new GUIContent(secretLabel));
                }

                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(lineRect, formatProp, new GUIContent("Export Format"));

                lineRect.y += EditorGUIUtility.singleLineHeight + VerticalSpacing;
                EditorGUI.PropertyField(lineRect, tabIndexProp, new GUIContent("Tab Index (0 = First)"));

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }
    }
}
