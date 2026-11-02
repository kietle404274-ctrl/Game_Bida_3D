using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Billiards.Editor
{
    /// <summary>
    /// Dọn 2 cây cơ trang trí thừa trên mặt bàn ("Cue Stick (1)", "Cue Stick (2)") trong scene Lobby
    /// mà không chạm tới các cây cơ nằm trong giá treo trên tường ("Pool Cue Rack 1", "Pool Cue Rack 2").
    /// Menu: Tools/Game/Fix Lobby Cues (chạy lại nhiều lần vẫn an toàn).
    /// </summary>
    public static class FixLobbyCues
    {
        private const string ScenePath = "Assets/Game/Scenes/Lobby.unity";

        [MenuItem("Tools/Game/Fix Lobby Cues", false, 41)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            int removed = 0;
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
            {
                if (t == null || t.gameObject == null) continue;
                if (t.name != "Cue Stick (1)" && t.name != "Cue Stick (2)") continue;
                if (IsInsideWallCueRack(t)) continue;

                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[FixLobbyCues] Đã ẩn {removed} cây cơ trang trí thừa trên mặt bàn Lobby (giữ nguyên giá treo cơ trên tường).");
        }

        private static bool IsInsideWallCueRack(Transform t)
        {
            Transform cur = t != null ? t.parent : null;
            while (cur != null)
            {
                string pName = cur.name;
                if (!string.IsNullOrEmpty(pName) &&
                    (pName.IndexOf("Cue Rack", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     pName.IndexOf("Cue Sticks", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     pName.IndexOf("CueStickWrapper", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
                cur = cur.parent;
            }
            return false;
        }
    }
}
