using UnityEngine;
using UnityEditor;

namespace Game.Billiards.Editor
{
    public static class PrintCushionProfile
    {
        [MenuItem("Tools/Game/Print Cushion Profile")]
        public static void PrintProfile()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
            var tableWrapper = Object.FindObjectOfType<Physics.TableWrapper>();
            if (tableWrapper == null) return;

            var cushions = tableWrapper.transform.Find("TableVisual/Cushions");
            if (cushions == null) cushions = tableWrapper.transform.Find("Cushions");
            if (cushions == null)
            {
                Debug.LogError("Cushions not found!");
                return;
            }

            var mf = cushions.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError("No mesh on cushions!");
                return;
            }

            var mesh = mf.sharedMesh;
            var vertices = mesh.vertices;
            Debug.Log($"Cushions mesh has {vertices.Length} vertices.");

            // Find min/max for the inner faces:
            // For side rails (along Z): what are the Z values of the nose facing Y ~ 0.86m?
            float minZ = float.MaxValue, maxZ = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;

            // Let's find vertices close to ball height (y approx 0.8635 in world or local)
            foreach (var v in vertices)
            {
                Vector3 worldV = cushions.transform.TransformPoint(v);
                if (worldV.z > 0 && worldV.z < maxZ_val(worldV))
                {
                    // keep track
                }
                minX = Mathf.Min(minX, worldV.x);
                maxX = Mathf.Max(maxX, worldV.x);
                minZ = Mathf.Min(minZ, worldV.z);
                maxZ = Mathf.Max(maxZ, worldV.z);
            }

            Debug.Log($"Overall Cushion World Bounds: X=[{minX:F3}, {maxX:F3}], Z=[{minZ:F3}, {maxZ:F3}]");

            // Specifically find the inner-most vertices facing the table cloth!
            // Inner-most positive Z (closest to Z=0):
            float innerPosZ = float.MaxValue;
            float innerNegZ = float.MinValue;
            float innerPosX = float.MaxValue;
            float innerNegX = float.MinValue;

            foreach (var v in vertices)
            {
                Vector3 w = cushions.transform.TransformPoint(v);
                // vertices on the nose (height around 0.84 to 0.87)
                if (w.y >= 0.835f && w.y <= 0.875f)
                {
                    // Positive Z rail (Z > 0.2m): inner-most is minimum Z!
                    if (w.z > 0.2f && Mathf.Abs(w.x) < 0.9f)
                    {
                        if (w.z < innerPosZ) innerPosZ = w.z;
                    }
                    // Negative Z rail (Z < -0.2m): inner-most is maximum Z!
                    if (w.z < -0.2f && Mathf.Abs(w.x) < 0.9f)
                    {
                        if (w.z > innerNegZ) innerNegZ = w.z;
                    }
                }
            }

            // Find inner-most X for Head and Foot rail:
            foreach (var v in vertices)
            {
                Vector3 w = cushions.transform.TransformPoint(v);
                // Head Rail (positive X): inner-most is minimum X among vertices in the central span |z| < 0.35m
                if (w.x > 0.7f && Mathf.Abs(w.z) < 0.35f)
                {
                    if (w.x < innerPosX) innerPosX = w.x;
                }
                // Foot Rail (negative X): inner-most is maximum X among vertices in the central span |z| < 0.35m
                if (w.x < -0.7f && Mathf.Abs(w.z) < 0.35f)
                {
                    if (w.x > innerNegX) innerNegX = w.x;
                }
            }

            string result = $"Head Rail (+X): Inner nose X = {innerPosX:F4}\n" +
                            $"Foot Rail (-X): Inner nose X = {innerNegX:F4}\n" +
                            $"Left Side Rail (+Z): Inner nose Z = {innerPosZ:F4}\n" +
                            $"Right Side Rail (-Z): Inner nose Z = {innerNegZ:F4}\n" +
                            $"Overall Cushion Bounds: X=[{minX:F3}, {maxX:F3}], Z=[{minZ:F3}, {maxZ:F3}]\n";

            Debug.Log($"*** EXACT CUSHION NOSE COORDINATES (WHERE BALL SHOULD BOUNCE): ***\n{result}");
            System.IO.File.WriteAllText("cushion_measurements.txt", result);
        }

        private static float maxZ_val(Vector3 v) => 10f;
    }
}
