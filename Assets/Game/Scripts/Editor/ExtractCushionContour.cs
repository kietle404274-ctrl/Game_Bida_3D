using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Billiards.Editor
{
    public static class ExtractCushionContour
    {
        [MenuItem("Tools/Game/Extract Cushion Contour")]
        public static void Extract()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            var tableWrapper = Object.FindObjectOfType<Physics.TableWrapper>();
            if (tableWrapper == null) return;

            var visual = tableWrapper.transform.Find("TableVisual");
            var cushions = visual != null ? visual.Find("Cushions") : tableWrapper.transform.Find("Cushions");
            if (cushions == null)
            {
                Debug.LogError("Cushions not found!");
                return;
            }

            var mf = cushions.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Cushions Mesh: {verts.Length} verts, {tris.Length / 3} triangles");

            // Transform all vertices to TableWrapper local space (which is world space when table is at 0,0,0)
            Vector3[] wVerts = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                wVerts[i] = cushions.TransformPoint(verts[i]);
            }

            // Find all triangles that face INWARD (towards playfield) or find boundary edges
            // Specifically, let's look at the nose vertices: height Y between 0.855 and 0.865
            List<int> noseVertIndices = new List<int>();
            for (int i = 0; i < wVerts.Length; i++)
            {
                if (wVerts[i].y >= 0.855f && wVerts[i].y <= 0.865f)
                {
                    noseVertIndices.Add(i);
                }
            }

            sb.AppendLine($"\nVertices with Y in [0.855, 0.865] (Nose level): count = {noseVertIndices.Count}");
            foreach (int idx in noseVertIndices)
            {
                Vector3 v = wVerts[idx];
                sb.AppendLine($"  Vert {idx:D3}: X={v.x:F4}, Y={v.y:F4}, Z={v.z:F4}");
            }

            // Also find all triangles involving these vertices
            sb.AppendLine("\nEdges between nose vertices:");
            HashSet<string> edges = new HashSet<string>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t];
                int i1 = tris[t + 1];
                int i2 = tris[t + 2];

                CheckEdge(noseVertIndices, i0, i1, wVerts, edges, sb);
                CheckEdge(noseVertIndices, i1, i2, wVerts, edges, sb);
                CheckEdge(noseVertIndices, i2, i0, wVerts, edges, sb);
            }

            File.WriteAllText("cushion_contour_extracted.txt", sb.ToString());
            Debug.Log($"[ExtractCushionContour] Written to cushion_contour_extracted.txt");
        }

        private static void CheckEdge(List<int> noseVerts, int a, int b, Vector3[] wVerts, HashSet<string> edges, StringBuilder sb)
        {
            if (noseVerts.Contains(a) && noseVerts.Contains(b))
            {
                string key = a < b ? $"{a}_{b}" : $"{b}_{a}";
                if (!edges.Contains(key))
                {
                    edges.Add(key);
                    sb.AppendLine($"  Edge ({a} -> {b}): ({wVerts[a].x:F4}, {wVerts[a].z:F4}) -> ({wVerts[b].x:F4}, {wVerts[b].z:F4})");
                }
            }
        }
    }
}
