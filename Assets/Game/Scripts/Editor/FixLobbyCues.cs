using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    /// <summary>
    /// Dọn cây cơ trong scene Lobby: xóa cây cơ đen thò ra ngoài ("Cue Stick (1)", vật liệu Black)
    /// và dịch cây cơ đỏ ("Cue Stick (2)") vào phía bàn để phần đít không còn thò ra ngoài thành bàn.
    /// Menu: Tools/Game/Fix Lobby Cues (chạy lại nhiều lần vẫn an toàn).
    /// </summary>
    public static class FixLobbyCues
    {
        private const string ScenePath = "Assets/Game/Scenes/Lobby.unity";
        private const float RailThickness = 0.15f;   // bề dày thành bàn ngoài mép băng
        private const float RedCueExtraInset = 0.05f; // dịch thêm vào trong cho chắc

        [MenuItem("Tools/Game/Fix Lobby Cues", false, 41)]
        public static void Run()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject black = GameObject.Find("Cue Stick (1)");
            if (black != null)
            {
                if (UsesMaterial(black, "Black"))
                {
                    Undo.DestroyObjectImmediate(black);
                    Debug.Log("[FixLobbyCues] Đã xóa cây cơ đen 'Cue Stick (1)'.");
                }
                else
                {
                    Debug.LogWarning("[FixLobbyCues] 'Cue Stick (1)' không dùng vật liệu Black nên KHÔNG xóa.");
                }
            }

            GameObject red = GameObject.Find("Cue Stick (2)");
            if (red == null)
            {
                Debug.LogWarning("[FixLobbyCues] Không tìm thấy 'Cue Stick (2)'.");
            }
            else
            {
                MoveRedCueIntoTable(red);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static bool UsesMaterial(GameObject go, string materialName)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null && m.name == materialName) return true;
                }
            }
            return false;
        }

        private static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool has = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return has;
        }

        private static void MoveRedCueIntoTable(GameObject red)
        {
            TableWrapper table = Object.FindObjectOfType<TableWrapper>();
            if (table == null || !TryGetBounds(red, out Bounds cue))
            {
                Debug.LogWarning("[FixLobbyCues] Thiếu TableWrapper hoặc renderer của cây đỏ; không dịch được.");
                return;
            }

            Vector3 c = table.transform.position;
            float minX = c.x + TableWrapper.FootCushionX - RailThickness;
            float maxX = c.x + TableWrapper.HeadCushionX + RailThickness;
            float minZ = c.z - TableWrapper.CushionHalfWidth - RailThickness;
            float maxZ = c.z + TableWrapper.CushionHalfWidth + RailThickness;

            Vector3 shift = Vector3.zero;
            if (cue.max.z > maxZ) shift.z = -(cue.max.z - maxZ + RedCueExtraInset);
            else if (cue.min.z < minZ) shift.z = (minZ - cue.min.z) + RedCueExtraInset;
            if (cue.max.x > maxX) shift.x = -(cue.max.x - maxX + RedCueExtraInset);
            else if (cue.min.x < minX) shift.x = (minX - cue.min.x) + RedCueExtraInset;

            if (shift == Vector3.zero)
            {
                Debug.Log("[FixLobbyCues] Cây cơ đỏ đã nằm trong phạm vi bàn, không cần dịch.");
                return;
            }

            Undo.RecordObject(red.transform, "Move Red Cue Into Table");
            Vector3 before = red.transform.position;
            red.transform.position += shift;
            Debug.Log("[FixLobbyCues] Dịch cây cơ đỏ " + before + " -> " + red.transform.position
                + " (bounds cơ " + cue.min + " .. " + cue.max + ", bàn x[" + minX + "," + maxX + "] z[" + minZ + "," + maxZ + "]).");
        }
    }
}
