using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GoogleSheetsFetcher.Editor
{
    /// <summary>
    /// Renders a [TableView] attribute both as a full cohesive table inside <see cref="SheetSyncEditor{T}"/>
    /// and as a per-element PropertyDrawer fallback in standard Unity / VInspector / Odin inspectors.
    /// </summary>
    [CustomPropertyDrawer(typeof(TableViewAttribute))]
    public class TableViewPropertyDrawer : PropertyDrawer
    {
        private const float IndexColWidth = 32f;
        private const float CellPadding = 4f;
        private const float RowVerticalPadding = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int elementIndex = TryParseArrayIndex(property.propertyPath);
            float rowHeight = EditorGUIUtility.singleLineHeight + RowVerticalPadding * 2f;

            // Above element [0], allocate space for the column header bar
            if (elementIndex == 0)
            {
                return rowHeight * 2f + 2f;
            }

            return rowHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var tableAttr = attribute as TableViewAttribute ?? new TableViewAttribute();
            int elementIndex = TryParseArrayIndex(property.propertyPath);

            var columns = TableViewLayoutDrawer.GetChildProperties(property);
            float rowHeight = EditorGUIUtility.singleLineHeight + RowVerticalPadding * 2f;

            Rect currentRect = new Rect(position.x, position.y, position.width, rowHeight);

            if (elementIndex == 0)
            {
                var headerNames = new List<string>(columns.Count);
                for (int i = 0; i < columns.Count; i++)
                {
                    headerNames.Add(columns[i].displayName);
                }

                TableViewLayoutDrawer.DrawColumnHeaderRect(
                    currentRect,
                    headerNames,
                    tableAttr.ShowIndex,
                    reserveDeleteCol: false);

                currentRect.y += rowHeight + 2f;
            }

            TableViewLayoutDrawer.DrawRowRect(
                currentRect,
                property,
                columns,
                elementIndex >= 0 ? elementIndex : 0,
                tableAttr.ShowIndex,
                tableAttr.ReadOnly,
                allowDeleteButton: false,
                out _);
        }

        private static int TryParseArrayIndex(string propertyPath)
        {
            const string token = ".Array.data[";
            int idx = propertyPath.LastIndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
                return -1;

            int start = idx + token.Length;
            int end = propertyPath.IndexOf(']', start);
            if (end <= start)
                return -1;

            return int.TryParse(propertyPath.Substring(start, end - start), out int parsed) ? parsed : -1;
        }
    }

    public static class TableViewLayoutDrawer
    {
        private const float IndexColWidth = 34f;
        private const float DeleteColWidth = 22f;
        private const float CellPadding = 4f;
        private const float RowHeight = 22f;

        private static readonly Dictionary<string, Vector2> ScrollPositions = new Dictionary<string, Vector2>();

        public static void DrawTable(SerializedProperty listProperty, TableViewAttribute attr, FieldInfo fieldInfo = null)
        {
            if (listProperty == null || !listProperty.isArray)
            {
                if (listProperty != null)
                    EditorGUILayout.PropertyField(listProperty, true);
                return;
            }

            attr ??= new TableViewAttribute();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // 1. Top Toolbar Header (Foldout + Count + Add/Remove buttons)
            DrawTableTitleBar(listProperty, attr);

            if (listProperty.isExpanded)
            {
                int rowCount = listProperty.arraySize;
                List<string> columnHeaders = ResolveColumnHeaders(listProperty, fieldInfo);
                bool showDeleteCol = !attr.ReadOnly;

                // 2. Column Headers Bar
                Rect headerRect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
                DrawColumnHeaderRect(headerRect, columnHeaders, attr.ShowIndex, showDeleteCol);

                // 3. Rows Body (with optional scroll view)
                if (rowCount == 0)
                {
                    Rect emptyRect = GUILayoutUtility.GetRect(0f, RowHeight + 4f, GUILayout.ExpandWidth(true));
                    EditorGUI.LabelField(emptyRect, "  Table is empty (0 rows).", EditorStyles.centeredGreyMiniLabel);
                }
                else
                {
                    string scrollKey = $"{listProperty.serializedObject.targetObject.GetInstanceID()}_{listProperty.propertyPath}";
                    bool useScroll = attr.MaxHeight > 0f && (rowCount * RowHeight) > attr.MaxHeight;

                    if (useScroll)
                    {
                        ScrollPositions.TryGetValue(scrollKey, out Vector2 scrollPos);
                        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MaxHeight(attr.MaxHeight));
                        ScrollPositions[scrollKey] = scrollPos;
                    }

                    int deleteIndex = -1;
                    for (int r = 0; r < rowCount; r++)
                    {
                        SerializedProperty elementProp = listProperty.GetArrayElementAtIndex(r);
                        List<SerializedProperty> cellProps = GetChildProperties(elementProp);

                        Rect rowRect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
                        DrawRowRect(
                            rowRect,
                            elementProp,
                            cellProps,
                            r,
                            attr.ShowIndex,
                            attr.ReadOnly,
                            showDeleteCol,
                            out bool clickedDelete);

                        if (clickedDelete)
                        {
                            deleteIndex = r;
                        }
                    }

                    if (useScroll)
                    {
                        EditorGUILayout.EndScrollView();
                    }

                    if (deleteIndex >= 0)
                    {
                        listProperty.DeleteArrayElementAtIndex(deleteIndex);
                    }
                }
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawTableTitleBar(SerializedProperty listProperty, TableViewAttribute attr)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string title = $"{listProperty.displayName} ({listProperty.arraySize})";
            listProperty.isExpanded = EditorGUILayout.Foldout(listProperty.isExpanded, title, true);

            GUILayout.FlexibleSpace();

            if (!attr.ReadOnly)
            {
                if (GUILayout.Button(new GUIContent("+", "Add Row"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                {
                    listProperty.arraySize++;
                    listProperty.isExpanded = true;
                }

                using (new EditorGUI.DisabledScope(listProperty.arraySize == 0))
                {
                    if (GUILayout.Button(new GUIContent("-", "Remove Last Row"), EditorStyles.toolbarButton, GUILayout.Width(24f)))
                    {
                        if (listProperty.arraySize > 0)
                            listProperty.arraySize--;
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        internal static void DrawColumnHeaderRect(
            Rect rect,
            IReadOnlyList<string> headers,
            bool showIndex,
            bool reserveDeleteCol)
        {
            Color headerBg = EditorGUIUtility.isProSkin
                ? new Color(0.18f, 0.18f, 0.18f, 1f)
                : new Color(0.78f, 0.78f, 0.78f, 1f);
            EditorGUI.DrawRect(rect, headerBg);

            float x = rect.x;
            float remainingWidth = rect.width;

            if (showIndex)
            {
                Rect idxRect = new Rect(x + CellPadding, rect.y + 2f, IndexColWidth - CellPadding, rect.height - 4f);
                EditorGUI.LabelField(idxRect, "#", EditorStyles.miniBoldLabel);
                x += IndexColWidth;
                remainingWidth -= IndexColWidth;
            }

            if (reserveDeleteCol)
            {
                remainingWidth -= DeleteColWidth;
            }

            int colCount = Mathf.Max(1, headers != null ? headers.Count : 1);
            float colWidth = Mathf.Max(20f, remainingWidth / colCount);

            if (headers != null && headers.Count > 0)
            {
                for (int c = 0; c < headers.Count; c++)
                {
                    Rect colRect = new Rect(x + CellPadding, rect.y + 2f, colWidth - CellPadding * 2f, rect.height - 4f);
                    EditorGUI.LabelField(colRect, headers[c], EditorStyles.miniBoldLabel);

                    // Subtle vertical column divider
                    EditorGUI.DrawRect(
                        new Rect(x, rect.y, 1f, rect.height),
                        new Color(0.5f, 0.5f, 0.5f, 0.25f));

                    x += colWidth;
                }
            }
            else
            {
                Rect colRect = new Rect(x + CellPadding, rect.y + 2f, colWidth - CellPadding * 2f, rect.height - 4f);
                EditorGUI.LabelField(colRect, "Value", EditorStyles.miniBoldLabel);
            }

            // Bottom border line
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.4f));
        }

        internal static void DrawRowRect(
            Rect rect,
            SerializedProperty elementProp,
            IReadOnlyList<SerializedProperty> cellProps,
            int rowIndex,
            bool showIndex,
            bool readOnly,
            bool allowDeleteButton,
            out bool clickedDelete)
        {
            clickedDelete = false;

            // Alternating row background shading
            Color rowBg = (rowIndex % 2 == 0)
                ? (EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f, 0.6f) : new Color(0.92f, 0.92f, 0.92f, 0.6f))
                : (EditorGUIUtility.isProSkin ? new Color(0.25f, 0.25f, 0.25f, 0.6f) : new Color(0.86f, 0.86f, 0.86f, 0.6f));
            EditorGUI.DrawRect(rect, rowBg);

            float x = rect.x;
            float remainingWidth = rect.width;

            if (showIndex)
            {
                Rect idxRect = new Rect(x + CellPadding, rect.y + 2f, IndexColWidth - CellPadding, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(idxRect, rowIndex.ToString(), EditorStyles.centeredGreyMiniLabel);
                x += IndexColWidth;
                remainingWidth -= IndexColWidth;
            }

            if (allowDeleteButton)
            {
                remainingWidth -= DeleteColWidth;
            }

            int colCount = Mathf.Max(1, cellProps != null && cellProps.Count > 0 ? cellProps.Count : 1);
            float colWidth = Mathf.Max(20f, remainingWidth / colCount);

            using (new EditorGUI.DisabledScope(readOnly))
            {
                if (cellProps != null && cellProps.Count > 0)
                {
                    for (int c = 0; c < cellProps.Count; c++)
                    {
                        Rect cellRect = new Rect(
                            x + CellPadding,
                            rect.y + 2f,
                            colWidth - CellPadding * 2f,
                            EditorGUIUtility.singleLineHeight);

                        EditorGUI.PropertyField(cellRect, cellProps[c], GUIContent.none, false);

                        EditorGUI.DrawRect(
                            new Rect(x, rect.y, 1f, rect.height),
                            new Color(0.5f, 0.5f, 0.5f, 0.15f));

                        x += colWidth;
                    }
                }
                else
                {
                    // Primitive or single-value element (e.g. List<string>)
                    Rect cellRect = new Rect(
                        x + CellPadding,
                        rect.y + 2f,
                        colWidth - CellPadding * 2f,
                        EditorGUIUtility.singleLineHeight);

                    EditorGUI.PropertyField(cellRect, elementProp, GUIContent.none, false);
                    x += colWidth;
                }
            }

            if (allowDeleteButton)
            {
                Rect delRect = new Rect(rect.xMax - DeleteColWidth + 2f, rect.y + 2f, DeleteColWidth - 4f, EditorGUIUtility.singleLineHeight);
                if (GUI.Button(delRect, new GUIContent("×", "Delete Row"), EditorStyles.miniButton))
                {
                    clickedDelete = true;
                }
            }

            // Subtle horizontal row divider
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.yMax - 1f, rect.width, 1f),
                new Color(0.5f, 0.5f, 0.5f, 0.15f));
        }

        internal static List<SerializedProperty> GetChildProperties(SerializedProperty elementProp)
        {
            var children = new List<SerializedProperty>();
            if (elementProp == null || !elementProp.hasVisibleChildren || elementProp.propertyType == SerializedPropertyType.String)
                return children;

            SerializedProperty copy = elementProp.Copy();
            SerializedProperty end = copy.GetEndProperty();

            if (copy.NextVisible(true))
            {
                while (!SerializedProperty.EqualContents(copy, end))
                {
                    children.Add(copy.Copy());
                    if (!copy.NextVisible(false))
                        break;
                }
            }

            return children;
        }

        private static List<string> ResolveColumnHeaders(SerializedProperty listProperty, FieldInfo fieldInfo)
        {
            var headers = new List<string>();

            if (listProperty.arraySize > 0)
            {
                var firstElem = listProperty.GetArrayElementAtIndex(0);
                var childProps = GetChildProperties(firstElem);
                for (int i = 0; i < childProps.Count; i++)
                {
                    headers.Add(childProps[i].displayName);
                }

                if (headers.Count > 0)
                    return headers;
            }

            // Fallback when array is empty: inspect element Type via reflection
            Type elementType = GetElementType(fieldInfo);
            if (elementType != null && elementType != typeof(string) && !elementType.IsPrimitive)
            {
                FieldInfo[] fields = elementType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo f in fields)
                {
                    if (f.IsPublic || f.GetCustomAttribute<SerializeField>() != null)
                    {
                        headers.Add(ObjectNames.NicifyVariableName(f.Name));
                    }
                }
            }

            if (headers.Count == 0)
            {
                headers.Add("Value");
            }

            return headers;
        }

        private static Type GetElementType(FieldInfo fieldInfo)
        {
            if (fieldInfo == null)
                return null;

            Type type = fieldInfo.FieldType;
            if (type.IsArray)
                return type.GetElementType();

            if (type.IsGenericType)
                return type.GetGenericArguments()[0];

            return null;
        }
    }
}
