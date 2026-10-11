using System;
using UnityEngine;

namespace GoogleSheetsFetcher
{
    /// <summary>
    /// Renders a <see cref="System.Collections.Generic.List{T}"/> or <c>T[]</c> of serializable structs/classes
    /// as a spreadsheet-style multi-column table in the Unity Inspector.
    /// Zero external dependencies; compatible alongside Default Inspector, SheetSyncEditor, VInspector, and Odin.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class TableViewAttribute : PropertyAttribute
    {
        /// <summary>
        /// When true, cells are read-only in the Inspector and row add/remove buttons are hidden.
        /// </summary>
        public bool ReadOnly { get; set; }

        /// <summary>
        /// When true, displays a 0-based row index column (#) on the left of the table.
        /// </summary>
        public bool ShowIndex { get; set; }

        /// <summary>
        /// Optional maximum height (in pixels) for the table body before scrolling. 0 = auto-expand.
        /// </summary>
        public float MaxHeight { get; set; }

        public TableViewAttribute(bool readOnly = false, bool showIndex = true, float maxHeight = 0f)
        {
            ReadOnly = readOnly;
            ShowIndex = showIndex;
            MaxHeight = maxHeight;
        }
    }
}
