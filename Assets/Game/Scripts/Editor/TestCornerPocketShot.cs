using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    public static class TestCornerPocketShot
    {
        [MenuItem("Tools/Game/Test Corner Pocket Shot")]
        public static void RunTest()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            var tableWrapper = Object.FindObjectOfType<TableWrapper>();
            if (tableWrapper == null)
            {
                Debug.LogError("No TableWrapper!");
                return;
            }

            tableWrapper.EnsureClothBedCollider();

            // Dump all active colliders on table
            StringBuilder colSb = new StringBuilder();
            colSb.AppendLine("=== TABLE ACTIVE COLLIDERS ===");
            foreach (var col in tableWrapper.GetComponentsInChildren<Collider>(true))
            {
                colSb.AppendLine($"{col.gameObject.name}: enabled={col.enabled}, isTrigger={col.isTrigger}, type={col.GetType().Name}, bounds={col.bounds}");
            }
            
            colSb.AppendLine("\n=== POCKET DETECTOR LOCATIONS ===");
            for (int i = 0; i < 6; i++)
            {
                var p = tableWrapper.GetPocket(i);
                if (p != null)
                {
                    var pSc = p.GetComponent<SphereCollider>();
                    colSb.AppendLine($"Pocket {i} ({p.name}): pos={p.transform.position}, radius={(pSc ? pSc.radius : 0)}");
                }
            }

            colSb.AppendLine("\n=== TABLE VISUAL CHILDREN ===");
            var visual = tableWrapper.transform.Find("TableVisual");
            if (visual != null)
            {
                foreach (Transform child in visual)
                {
                    var mf = child.GetComponent<MeshFilter>();
                    var mc = child.GetComponent<Collider>();
                    bool isEnabled = mc != null ? mc.enabled : false;
                    colSb.AppendLine($"Child: {child.name}, Mesh={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "null")}, Vertices={(mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0)}, Col_enabled={isEnabled}");
                }
            }

            File.WriteAllText("table_colliders_dump.txt", colSb.ToString());
            Debug.Log("[TestCornerPocketShot] table_colliders_dump.txt written");

            var allBalls = Object.FindObjectsOfType<BallController>(true);
            BallController ball = null;
            foreach (var b in allBalls)
            {
                if (b.BallNumber == 0 || b.name.Contains("_00") || b.name.EndsWith("_0"))
                {
                    ball = b;
                    break;
                }
            }
            if (ball == null && allBalls.Length > 0) ball = allBalls[0];

            var rb = ball.GetComponent<Rigidbody>();
            var sc = ball.GetComponent<SphereCollider>();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("================ TEST CORNER POCKET SHOT ================");

            // Check all 4 corner pockets:
            // Pocket 0: FootLeft (-1.11, 0.84, 0.55)
            // Pocket 1: FootRight (-1.11, 0.84, -0.55)
            // Pocket 4: HeadLeft (1.11, 0.84, 0.55)
            // Pocket 5: HeadRight (1.11, 0.84, -0.55)
            // Pocket 2: SideLeft (0, 0.84, 0.58)

            int[] testPockets = new int[] { 0, 1, 2, 3, 4, 5 };

            bool prevAuto = UnityEngine.Physics.autoSimulation;
            UnityEngine.Physics.autoSimulation = false;
            try
            {
                foreach (int pId in testPockets)
                {
                    var pocket = tableWrapper.GetPocket(pId);
                    Vector3 pocketPos = pocket.transform.position;
                    var pocketSc = pocket.GetComponent<SphereCollider>();
                    sb.AppendLine($"\n--- TESTING POCKET {pId} ({pocket.name}) ---");
                    sb.AppendLine($"Pocket position: {pocketPos}, Trigger radius={pocketSc.radius}, captureDist={(pocketSc != null ? 0.068f : 0f)}");

                    Vector3 mouthCenter;
                    Vector3 dirToPocket;
                    if (pId == 0) // FootLeft
                    {
                        mouthCenter = new Vector3((-1.0724f + -1.0140f) * 0.5f, pocketPos.y, (0.4467f + 0.5023f) * 0.5f);
                        dirToPocket = new Vector3(-1f, 0f, 1f).normalized;
                    }
                    else if (pId == 1) // FootRight
                    {
                        mouthCenter = new Vector3((-1.0724f + -1.0140f) * 0.5f, pocketPos.y, (-0.4467f + -0.5023f) * 0.5f);
                        dirToPocket = new Vector3(-1f, 0f, -1f).normalized;
                    }
                    else if (pId == 4) // HeadLeft
                    {
                        mouthCenter = new Vector3((1.0603f + 1.0020f) * 0.5f, pocketPos.y, (0.4467f + 0.5023f) * 0.5f);
                        dirToPocket = new Vector3(1f, 0f, 1f).normalized;
                    }
                    else if (pId == 5) // HeadRight
                    {
                        mouthCenter = new Vector3((1.0603f + 1.0020f) * 0.5f, pocketPos.y, (-0.4467f + -0.5023f) * 0.5f);
                        dirToPocket = new Vector3(1f, 0f, -1f).normalized;
                    }
                    else if (pId == 2) // SideLeft
                    {
                        mouthCenter = new Vector3(0f, pocketPos.y, 0.5023f);
                        dirToPocket = new Vector3(0f, 0f, 1f);
                    }
                    else // SideRight
                    {
                        mouthCenter = new Vector3(0f, pocketPos.y, -0.5023f);
                        dirToPocket = new Vector3(0f, 0f, -1f);
                    }

                    Vector3 startPos = mouthCenter - dirToPocket * 0.35f;
                    startPos.y = tableWrapper.ClothSurfaceY + sc.radius;
                    Vector3 shootVel = dirToPocket * 2.0f; // 2 m/s straight into pocket mouth centerline

                    sb.AppendLine($"StartPos: {startPos}, Velocity: {shootVel}");

                    ball.ResetTo(startPos, Quaternion.identity);
                    rb.isKinematic = false;
                    rb.position = startPos;
                    rb.velocity = shootVel;
                    rb.angularVelocity = Vector3.zero;
                    UnityEngine.Physics.SyncTransforms();

                    bool pocketed = false;
                    int pocketedStep = -1;
                    Vector3 minPos = startPos, maxPos = startPos;

                    for (int step = 0; step < 120; step++)
                    {
                        UnityEngine.Physics.Simulate(0.02f);
                        Vector3 p = rb.position;
                        Vector3 v = rb.velocity;
                        float distToPocket = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pocketPos.x, pocketPos.z));

                        sb.AppendLine($"  Step {step:D3}: Pos=({p.x:F3}, {p.y:F3}, {p.z:F3}), Vel=({v.x:F2}, {v.z:F2}), DistToPocket={distToPocket:F3}, BallPocketed={ball.IsPocketed}");

                        // In edit mode Physics.Simulate, MonoBehaviour.OnTriggerEnter is not dispatched by Unity.
                        // We check trigger condition matching PocketDetector:
                        if (!ball.IsPocketed && pocketSc != null)
                        {
                            float d3d = Vector3.Distance(p, pocketPos);
                            if (d3d <= (pocketSc.radius + sc.radius) && distToPocket <= 0.080f)
                            {
                                ball.MarkPocketed(pId);
                            }
                        }

                        if (ball.IsPocketed)
                        {
                            pocketed = true;
                            pocketedStep = step;
                            sb.AppendLine($"  >>> SUCCESS: POCKETED at step {step}! Final pos=({p.x:F3}, {p.z:F3}), dist={distToPocket:F3}");
                            break;
                        }

                        // Check if ball stopped or bounced back
                        if (v.magnitude < 0.05f || Vector3.Dot(v, shootVel) < -0.5f)
                        {
                            var hits = UnityEngine.Physics.OverlapSphere(p, sc.radius + 0.01f);
                            string hitNames = "";
                            foreach (var h in hits)
                            {
                                if (h.gameObject != ball.gameObject && !h.isTrigger) hitNames += h.name + " (" + h.GetType().Name + "), ";
                            }
                            sb.AppendLine($"  >>> BOUNCED or STOPPED at step {step}: Pos=({p.x:F3}, {p.z:F3}), Vel=({v.x:F2}, {v.z:F2}), DistToPocket={distToPocket:F3}. Overlapping: {hitNames}");
                            break;
                        }
                    }

                    sb.AppendLine($"Result for Pocket {pId}: Pocketed={pocketed} (step {pocketedStep})");
                }

                sb.AppendLine("\n================ TEST FACING COLLISION SOLIDITY ================");
                // Test direct shots at the 2 cushion edges (mép băng vát) of Pocket 0 to verify solidity & reflection
                var facingTests = new (string Name, Vector3 Start, Vector3 Target)[]
                {
                    ("Pocket0_FootFacing", new Vector3(-0.95f, 0.864f, 0.40f), new Vector3(-1.1009f, 0.864f, 0.47055f)),
                    ("Pocket0_SideFacing", new Vector3(-0.95f, 0.864f, 0.40f), new Vector3(-1.03875f, 0.864f, 0.53095f)),
                    ("Pocket1_FootFacing", new Vector3(-0.95f, 0.864f, -0.40f), new Vector3(-1.1009f, 0.864f, -0.47055f)),
                    ("Pocket1_SideFacing", new Vector3(-0.95f, 0.864f, -0.40f), new Vector3(-1.03875f, 0.864f, -0.53095f))
                };

                foreach (var ft in facingTests)
                {
                    sb.AppendLine($"\n--- TESTING FACING IMPACT: {ft.Name} ---");
                    Vector3 dir = (ft.Target - ft.Start).normalized;
                    Vector3 shootVel = dir * 3.0f; // 3 m/s hard strike into cushion edge

                    ball.ResetTo(ft.Start, Quaternion.identity);
                    rb.isKinematic = false;
                    rb.position = ft.Start;
                    rb.velocity = shootVel;
                    rb.angularVelocity = Vector3.zero;
                    UnityEngine.Physics.SyncTransforms();

                    bool collidedWithFacing = false;
                    bool penetrated = false;

                    for (int step = 0; step < 80; step++)
                    {
                        UnityEngine.Physics.Simulate(0.02f);
                        Vector3 p = rb.position;
                        Vector3 v = rb.velocity;

                        // Check reflection: velocity dot product with initial direction reverses
                        if (!collidedWithFacing && Vector3.Dot(v, dir) < 0f)
                        {
                            collidedWithFacing = true;
                            sb.AppendLine($"  >>> REFLECTED at step {step}: Pos=({p.x:F3}, {p.y:F3}, {p.z:F3}), ReflectedVel=({v.x:F2}, {v.z:F2})");
                        }

                        // Check if penetrated outside table outer bounds
                        if (Mathf.Abs(p.x) > 1.25f || Mathf.Abs(p.z) > 0.70f)
                        {
                            penetrated = true;
                            sb.AppendLine($"  >>> FAILED: Penetrated cushion at step {step}! Pos=({p.x:F3}, {p.z:F3})");
                            break;
                        }
                    }

                    sb.AppendLine($"Result for {ft.Name}: CollidedAndReflected={collidedWithFacing}, Penetrated={penetrated}");
                }
            }
            finally
            {
                UnityEngine.Physics.autoSimulation = prevAuto;
            }

            File.WriteAllText("test_pocket_shot_report.txt", sb.ToString());
            Debug.Log("[TestCornerPocketShot] Report written to test_pocket_shot_report.txt");
        }
    }
}
