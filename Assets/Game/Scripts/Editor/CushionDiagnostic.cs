using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    public static class CushionDiagnostic
    {
        [MenuItem("Tools/Game/Run Cushion Diagnostic")]
        public static void RunDiagnostic()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================ CUSHION & GAMEPLAY COLLISION DIAGNOSTIC ================");

            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            sb.AppendLine($"Scene loaded: {scene.path}");

            var tableWrapper = Object.FindObjectOfType<TableWrapper>();
            if (tableWrapper == null)
            {
                sb.AppendLine("ERROR: TableWrapper NOT FOUND in Gameplay.unity!");
                File.WriteAllText("cushion_diagnostic_report.txt", sb.ToString());
                return;
            }

            sb.AppendLine($"TableWrapper position={tableWrapper.transform.position}, rotation={tableWrapper.transform.eulerAngles}");
            sb.AppendLine($"ClothSurfaceY={tableWrapper.ClothSurfaceY}, HalfLength={tableWrapper.PlayableHalfLength}, HalfWidth={tableWrapper.PlayableHalfWidth}");

            var visual = tableWrapper.transform.Find("TableVisual");
            if (visual != null)
            {
                sb.AppendLine("\n--- Visual Mesh Renderers Bounds ---");
                foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
                {
                    sb.AppendLine($"  Renderer '{mr.name}': bounds.min={mr.bounds.min}, bounds.max={mr.bounds.max}, size={mr.bounds.size}, center={mr.bounds.center}");
                }

                var cushionsMf = visual.Find("Cushions")?.GetComponent<MeshFilter>();
                if (cushionsMf != null && cushionsMf.sharedMesh != null)
                {
                    var mesh = cushionsMf.sharedMesh;
                    var verts = mesh.vertices;
                    Transform cTrans = cushionsMf.transform;
                    sb.AppendLine($"\n--- Cushions Mesh Vertex Analysis ({verts.Length} vertices) ---");
                    sb.AppendLine($"Cushions transform: localPos={cTrans.localPosition}, localScale={cTrans.localScale}, rot={cTrans.localEulerAngles}");

                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minZ = float.MaxValue, maxZ = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;

                    // Also find vertices near center of Foot rail (Z near 0, X < -0.8)
                    List<Vector3> footVerts = new List<Vector3>();
                    List<Vector3> sideVerts = new List<Vector3>();

                    for (int vIdx = 0; vIdx < verts.Length; vIdx++)
                    {
                        Vector3 wPos = cTrans.TransformPoint(verts[vIdx]);
                        if (wPos.x < minX) minX = wPos.x;
                        if (wPos.x > maxX) maxX = wPos.x;
                        if (wPos.y < minY) minY = wPos.y;
                        if (wPos.y > maxY) maxY = wPos.y;
                        if (wPos.z < minZ) minZ = wPos.z;
                        if (wPos.z > maxZ) maxZ = wPos.z;

                        if (wPos.x < -0.95f) footVerts.Add(wPos);
                    }

                    sb.AppendLine($"World bounds of vertices: X=[{minX:F4}, {maxX:F4}], Y=[{minY:F4}, {maxY:F4}], Z=[{minZ:F4}, {maxZ:F4}]");
                    sb.AppendLine($"Foot rail (X < -0.95m) vertex samples count: {footVerts.Count}");
                    // Sort footVerts by X descending (closest to table center)
                    footVerts.Sort((a, b) => b.x.CompareTo(a.x));
                    int printCount = Mathf.Min(20, footVerts.Count);
                    for (int i = 0; i < printCount; i++)
                    {
                        var v = footVerts[i];
                        sb.AppendLine($"  Foot vert [{i}]: X={v.x:F4}, Y={v.y:F4}, Z={v.z:F4}");
                    }
                }
            }

            sb.AppendLine("\n--- Pockets Positions ---");
            for (int i = 0; i < tableWrapper.Pockets.Length; i++)
            {
                var p = tableWrapper.Pockets[i];
                if (p != null)
                {
                    sb.AppendLine($"  Pocket {i} ({p.name}): pos={p.transform.position}, localPos={p.transform.localPosition}");
                }
            }

            // Call EnsureClothBedCollider to simulate runtime Awake
            tableWrapper.EnsureClothBedCollider();

            // Check CushionsColliders
            var cushionsCollidersRoot = tableWrapper.transform.Find("CushionsColliders");
            if (cushionsCollidersRoot == null)
            {
                sb.AppendLine("ERROR: CushionsColliders child NOT FOUND after EnsureClothBedCollider!");
            }
            else
            {
                sb.AppendLine($"CushionsColliders child count: {cushionsCollidersRoot.childCount}");
                for (int i = 0; i < cushionsCollidersRoot.childCount; i++)
                {
                    var child = cushionsCollidersRoot.GetChild(i);
                    var box = child.GetComponent<BoxCollider>();
                    if (box == null)
                    {
                        sb.AppendLine($"  [{i}] {child.name}: NO BoxCollider!");
                    }
                    else
                    {
                        sb.AppendLine($"  [{i}] {child.name}: enabled={box.enabled}, isTrigger={box.isTrigger}, layer={child.gameObject.layer} ({LayerMask.LayerToName(child.gameObject.layer)}), localPos={child.localPosition}, size={box.size}, worldBounds.min={box.bounds.min}, worldBounds.max={box.bounds.max}");
                    }
                }
            }

            // Inspect all other colliders in TableWrapper
            sb.AppendLine("\n--- All other Colliders under TableWrapper ---");
            var allCols = tableWrapper.GetComponentsInChildren<Collider>(true);
            foreach (var col in allCols)
            {
                if (col.transform.parent == cushionsCollidersRoot) continue;
                sb.AppendLine($"  Collider '{col.gameObject.name}' ({col.GetType().Name}): enabled={col.enabled}, isTrigger={col.isTrigger}, layer={col.gameObject.layer}, bounds.min={col.bounds.min}, bounds.max={col.bounds.max}");
            }

            // Find Cue Ball (GameBall_00 or Ball_0)
            var allBalls = Object.FindObjectsOfType<BallController>(true);
            sb.AppendLine($"\nTotal BallControllers found in scene: {allBalls.Length}");
            BallController cueBall = null;
            foreach (var b in allBalls)
            {
                if (b.BallNumber == 0 || b.name.Contains("_00") || b.name.EndsWith("_0"))
                {
                    cueBall = b;
                    break;
                }
            }

            if (cueBall == null && allBalls.Length > 0) cueBall = allBalls[0];

            if (cueBall == null)
            {
                sb.AppendLine("ERROR: No BallController found in scene!");
                File.WriteAllText("cushion_diagnostic_report.txt", sb.ToString());
                return;
            }

            var rb = cueBall.GetComponent<Rigidbody>();
            var sc = cueBall.GetComponent<SphereCollider>();
            sb.AppendLine($"Testing with ball '{cueBall.name}': layer={cueBall.gameObject.layer} ({LayerMask.LayerToName(cueBall.gameObject.layer)}), radius={sc.radius}, mass={rb.mass}, freezePosY={rb.constraints.HasFlag(RigidbodyConstraints.FreezePositionY)}, isKinematic={rb.isKinematic}");

            bool prevAuto = UnityEngine.Physics.autoSimulation;
            UnityEngine.Physics.autoSimulation = false;
            try
            {
                // TEST 1: Shoot toward +Z (Left Side Rail) at X = 0.5 (middle of head-side rail)
                sb.AppendLine("\n--- TEST 1: Shoot Ball toward +Z (Left Side Rail at X=0.5m, v=3m/s) ---");
                TestShot(sb, rb, cueBall, new Vector3(0.5f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(0f, 0f, 3f), 80, "+Z");

                // TEST 2: Shoot toward -Z (Right Side Rail at X=0.5m, v=3m/s)
                sb.AppendLine("\n--- TEST 2: Shoot Ball toward -Z (Right Side Rail at X=0.5m, v=3m/s) ---");
                TestShot(sb, rb, cueBall, new Vector3(0.5f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(0f, 0f, -3f), 80, "-Z");

                // TEST 3: Shoot toward +X (Head Rail at Z=0m, v=3m/s)
                sb.AppendLine("\n--- TEST 3: Shoot Ball toward +X (Head Rail at Z=0m, v=3m/s) ---");
                TestShot(sb, rb, cueBall, new Vector3(0f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(3f, 0f, 0f), 80, "+X");

                // TEST 4: Shoot toward -X (Foot Rail at Z=0m, v=3m/s)
                sb.AppendLine("\n--- TEST 4: Shoot Ball toward -X (Foot Rail at Z=0m, v=3m/s) ---");
                TestShot(sb, rb, cueBall, new Vector3(0f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(-3f, 0f, 0f), 80, "-X");

                // TEST 5: Maximum Impulse Shot (v = 10.6 m/s) toward +Z rail to test high-speed tunneling
                sb.AppendLine("\n--- TEST 5: Maximum Impulse Shot (v=10.6m/s, max break speed) toward +Z rail ---");
                TestShot(sb, rb, cueBall, new Vector3(0.5f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(0f, 0f, 10.6f), 80, "MaxPowerSide");

                // TEST 6: Shoot toward Corner Pocket 4 (HeadLeft, X=+1.115, Z=+0.555)
                sb.AppendLine("\n--- TEST 6: Shoot toward Corner Pocket 4 (HeadLeft) ---");
                Vector3 toCorner4 = (new Vector3(1.115f, 0f, 0.555f) - new Vector3(0.5f, 0f, 0f)).normalized * 3.5f;
                TestShot(sb, rb, cueBall, new Vector3(0.5f, tableWrapper.ClothSurfaceY + sc.radius, 0f), toCorner4, 100, "CornerPocket4");

                // TEST 7: Shoot toward Side Pocket 2 (SideLeft, X=0, Z=+0.585)
                sb.AppendLine("\n--- TEST 7: Shoot toward Side Pocket 2 (SideLeft) ---");
                TestShot(sb, rb, cueBall, new Vector3(0f, tableWrapper.ClothSurfaceY + sc.radius, 0f), new Vector3(0f, 0f, 3.5f), 100, "SidePocket2");

                // TEST 8: Angled Shot into Pocket Jaw (Hitting pocket throat facing)
                sb.AppendLine("\n--- TEST 8: Angled Shot into Corner Jaw (X=0.92, Z=0.48) ---");
                Vector3 toJaw = (new Vector3(0.92f, 0f, 0.48f) - new Vector3(0.4f, 0f, 0f)).normalized * 3.5f;
                TestShot(sb, rb, cueBall, new Vector3(0.4f, tableWrapper.ClothSurfaceY + sc.radius, 0f), toJaw, 100, "CornerJaw");
            }
            finally
            {
                UnityEngine.Physics.autoSimulation = prevAuto;
            }

            File.WriteAllText("cushion_diagnostic_report.txt", sb.ToString());
            Debug.Log("[CushionDiagnostic] Diagnostic report generated at cushion_diagnostic_report.txt");
        }

        private static void TestShot(StringBuilder sb, Rigidbody rb, BallController ball, Vector3 startPos, Vector3 velocity, int steps, string testLabel)
        {
            ball.ResetTo(startPos, Quaternion.identity);
            rb.isKinematic = false;
            rb.position = startPos;
            rb.transform.position = startPos;
            rb.velocity = velocity;
            rb.angularVelocity = Vector3.zero;
            UnityEngine.Physics.SyncTransforms();

            Vector3 minPos = startPos;
            Vector3 maxPos = startPos;
            bool bounced = false;
            Vector3 lastVel = velocity;
            bool wasPocketed = false;

            for (int s = 0; s < steps; s++)
            {
                UnityEngine.Physics.Simulate(0.02f);
                Vector3 p = rb.position;
                Vector3 v = rb.velocity;

                if (p.x < minPos.x) minPos.x = p.x;
                if (p.x > maxPos.x) maxPos.x = p.x;
                if (p.z < minPos.z) minPos.z = p.z;
                if (p.z > maxPos.z) maxPos.z = p.z;

                if (Vector3.Dot(v, lastVel) < -0.1f)
                {
                    bounced = true;
                }

                if (ball.IsPocketed)
                {
                    wasPocketed = true;
                    break;
                }
                lastVel = v;
            }

            bool containedInsideOuterBounds = maxPos.x < 1.30f && minPos.x > -1.30f && maxPos.z < 0.80f && minPos.z > -0.80f;
            sb.AppendLine($"[{testLabel}] Bounced={bounced}, Pocketed={wasPocketed}, Contained={containedInsideOuterBounds}, PosX Range=[{minPos.x:F3}, {maxPos.x:F3}], PosZ Range=[{minPos.z:F3}, {maxPos.z:F3}], FinalVel={lastVel}");
        }
    }
}
