using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Billiards.Editor
{
    public static class MeasureTableMeshBounds
    {
        [MenuItem("Tools/Game/Measure Table Mesh Bounds")]
        public static void Measure()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            var tableWrapper = Object.FindObjectOfType<Physics.TableWrapper>();
            if (tableWrapper == null)
            {
                Debug.LogError("TableWrapper not found in Gameplay.unity!");
                return;
            }

            Debug.Log("================ TABLE MESH & COLLIDER ANALYSIS ================");
            Debug.Log($"TableWrapper position: {tableWrapper.transform.position}");
            Debug.Log($"PlayableHalfLength: {tableWrapper.PlayableHalfLength}, PlayableHalfWidth: {tableWrapper.PlayableHalfWidth}, ClothSurfaceY: {tableWrapper.ClothSurfaceY}");

            // 1. Inspect all MeshFilters and Renderers in TableWrapper
            var filters = tableWrapper.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var rend = mf.GetComponent<Renderer>();
                var rBounds = rend != null ? rend.bounds : new Bounds();
                var mBounds = mesh.bounds;
                Debug.Log($"[MESH] '{mf.gameObject.name}':");
                Debug.Log($"       Local Bounds: center={mBounds.center}, extents={mBounds.extents}, min={mBounds.min}, max={mBounds.max}");
                Debug.Log($"       World Bounds: center={rBounds.center}, extents={rBounds.extents}, min={rBounds.min}, max={rBounds.max}");
            }

            // 2. Inspect all Colliders in TableWrapper
            var colliders = tableWrapper.GetComponentsInChildren<Collider>(true);
            Debug.Log($"Total Colliders under TableWrapper: {colliders.Length}");
            foreach (var col in colliders)
            {
                Debug.Log($"[COLLIDER] '{col.gameObject.name}' ({col.GetType().Name}): enabled={col.enabled}, isTrigger={col.isTrigger}, bounds.min={col.bounds.min}, bounds.max={col.bounds.max}, bounds.center={col.bounds.center}, bounds.size={col.bounds.size}");
            }
            Debug.Log("===============================================================");
        }
    }
}
