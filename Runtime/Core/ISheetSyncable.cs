namespace GoogleSheetsFetcher
{
    /// <summary>
    /// Implement this interface on any ScriptableObject or MonoBehaviour to attach a SheetConfig
    /// and use <see cref="Editor.SheetSyncEditor{T}"/> for Inspector syncing and batch SyncAllEditors.
    /// </summary>
    public interface ISheetSyncable
    {
#if UNITY_EDITOR
        SheetConfig SheetConfig { get; }
#endif
    }
}
