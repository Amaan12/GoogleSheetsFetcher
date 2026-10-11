using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GoogleSheetsFetcher.Editor
{
    public static class SheetSyncAllEditorsMenu
    {
        [MenuItem("Tools/Amaan12/GoogleSheetsFetcher/SyncAllEditors")]
        public static void SyncAllEditors()
        {
            SyncAllEditorsAsync().Forget();
        }

        public static async UniTask SyncAllEditorsAsync()
        {
            var targets = CollectAllSyncableTargets();
            if (targets.Count == 0)
            {
                Debug.Log("[GoogleSheetsFetcher] No ISheetSyncable targets or GoogleSheetsRegistrySO assets found to sync.");
                return;
            }

            var createdEditors = new List<UnityEditor.Editor>(targets.Count);
            var syncTasks = new List<UniTask<bool>>(targets.Count);

            var stopwatch = Stopwatch.StartNew();

            try
            {
                foreach (Object targetObj in targets)
                {
                    if (targetObj == null)
                        continue;

                    UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(targetObj);
                    if (editor == null)
                        continue;

                    createdEditors.Add(editor);

                    if (editor is ISheetSyncEditor syncEditor)
                    {
                        syncTasks.Add(syncEditor.SyncEditorAsync(refreshAssetDatabase: false));
                    }
                }

                if (syncTasks.Count == 0)
                {
                    Debug.Log("[GoogleSheetsFetcher] Found targets, but none had a custom Editor implementing ISheetSyncEditor.");
                    return;
                }

                bool[] results = await UniTask.WhenAll(syncTasks);

                int successCount = 0;
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i])
                        successCount++;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                stopwatch.Stop();
                Debug.Log(
                    $"[GoogleSheetsFetcher] SyncAllEditors finished: {successCount}/{syncTasks.Count} editor(s) synced in {stopwatch.Elapsed.TotalSeconds:F2}s.");
            }
            finally
            {
                for (int i = 0; i < createdEditors.Count; i++)
                {
                    if (createdEditors[i] != null)
                    {
                        Object.DestroyImmediate(createdEditors[i]);
                    }
                }
            }
        }

        private static List<Object> CollectAllSyncableTargets()
        {
            var uniqueTargets = new HashSet<Object>();
            var targetList = new List<Object>();

            void AddTarget(Object obj)
            {
                if (obj != null && uniqueTargets.Add(obj))
                {
                    targetList.Add(obj);
                }
            }

            // 1. All ScriptableObject assets in project that implement ISheetSyncable
            string[] soGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            for (int i = 0; i < soGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(soGuids[i]);
                if (!path.StartsWith("Assets/"))
                    continue;

                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so is ISheetSyncable)
                {
                    AddTarget(so);
                }
            }

            // 2. All MonoBehaviours in currently loaded scenes that implement ISheetSyncable
            MonoBehaviour[] sceneMonos = Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sceneMonos.Length; i++)
            {
                if (sceneMonos[i] is ISheetSyncable)
                {
                    AddTarget(sceneMonos[i]);
                }
            }

            // 3. All Prefab assets in project with MonoBehaviours implementing ISheetSyncable
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (!path.StartsWith("Assets/"))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                MonoBehaviour[] prefabMonos = prefab.GetComponentsInChildren<MonoBehaviour>(true);
                for (int j = 0; j < prefabMonos.Length; j++)
                {
                    if (prefabMonos[j] is ISheetSyncable)
                    {
                        AddTarget(prefabMonos[j]);
                    }
                }
            }

            return targetList;
        }
    }
}
