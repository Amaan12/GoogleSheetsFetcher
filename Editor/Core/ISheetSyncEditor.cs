using Cysharp.Threading.Tasks;

namespace GoogleSheetsFetcher.Editor
{
    /// <summary>
    /// Implemented by custom Editors (such as <see cref="SheetSyncEditor{T}"/> and <see cref="GoogleSheetsRegistrySOEditor"/>)
    /// so that Tools/Amaan12/GoogleSheetsFetcher/SyncAllEditors can trigger every editor's sync action in parallel.
    /// </summary>
    public interface ISheetSyncEditor
    {
        UniTask<bool> SyncEditorAsync(bool refreshAssetDatabase = true);
    }
}
