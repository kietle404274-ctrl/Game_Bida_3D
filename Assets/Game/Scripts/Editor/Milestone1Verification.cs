using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Billiards.Physics;
using Game.Billiards.Match;
using Game.Billiards.Core;

namespace Game.Billiards.Editor
{
    public static class Milestone1Verification
    {
        [MenuItem("Billiards/Run M1 Verification", false, 1)]
        public static void RunAllM1Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M1 ====================");
            int passed = 0;
            int total = 5;

            if (TestPhysicsConfig()) passed++;
            if (TestBallPrefabs()) passed++;
            if (TestTableWrapper()) passed++;
            if (TestWpaRackGeneration()) passed++;
            if (TestMovementMonitorLogic()) passed++;

            Debug.Log($"==================== KẾT QUẢ M1: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M1 PASS] Tất cả các điều kiện nghiệm thu của Milestone M1 đã đạt thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M1 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestPhysicsConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");
            if (config == null)
            {
                Debug.LogError("[Test 1 FAILED] Không tìm thấy DefaultPhysicsConfig.asset tại Assets/Game/Configs/");
                return false;
            }

            bool valid = true;
            if (Mathf.Abs(config.BallMass - 0.17f) > 0.01f) { Debug.LogError($"[Test 1] Sai ballMass: {config.BallMass}"); valid = false; }
            if (config.BallRadius <= 0.02f || config.BallRadius >= 0.04f) { Debug.LogError($"[Test 1] Bán kính bất thường: {config.BallRadius}"); valid = false; }
            if (config.MaxShotImpulse <= 0f) { Debug.LogError($"[Test 1] maxShotImpulse <= 0"); valid = false; }
            if (config.BallRestitution <= 0.5f || config.BallRestitution > 1f) { Debug.LogError($"[Test 1] ballRestitution không hợp lệ"); valid = false; }
            if (config.SettleDuration <= 0f) { Debug.LogError($"[Test 1] settleDuration <= 0"); valid = false; }

            if (valid)
            {
                Debug.Log("[Test 1 PASSED] Cấu hình vật lý PhysicsConfig hợp lệ và đúng chuẩn WPA.");
            }
            return valid;
        }

        private static bool TestBallPrefabs()
        {
            bool allValid = true;
            for (int i = 0; i <= 15; i++)
            {
                string path = $"Assets/Game/Prefabs/Balls/GameBall_{i:D2}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError($"[Test 2 FAILED] Không tìm thấy prefab: {path}");
                    allValid = false;
                    continue;
                }

                var ballCtrl = prefab.GetComponent<BallController>();
                var rb = prefab.GetComponent<Rigidbody>();
                var sphereCol = prefab.GetComponent<SphereCollider>();

                if (ballCtrl == null || rb == null || sphereCol == null)
                {
                    Debug.LogError($"[Test 2 FAILED] {prefab.name} thiếu component cần thiết (BallController/Rigidbody/SphereCollider)");
                    allValid = false;
                }
                else if (ballCtrl.BallNumber != i)
                {
                    Debug.LogError($"[Test 2 FAILED] {prefab.name} có số bi không khớp: {ballCtrl.BallNumber} != {i}");
                    allValid = false;
                }
            }

            if (allValid)
            {
                Debug.Log("[Test 2 PASSED] 16 Prefab bi (GameBall_00 đến GameBall_15) đều đầy đủ thành phần Rigidbody, SphereCollider và BallController.");
            }
            return allValid;
        }

        private static bool TestTableWrapper()
        {
            string path = "Assets/Game/Prefabs/Table/TableWrapper.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"[Test 3 FAILED] Không tìm thấy {path}");
                return false;
            }

            var wrapper = prefab.GetComponent<TableWrapper>();
            if (wrapper == null)
            {
                Debug.LogError("[Test 3 FAILED] TableWrapper component bị thiếu trên root prefab!");
                return false;
            }

            if (wrapper.Pockets == null || wrapper.Pockets.Length != 6)
            {
                Debug.LogError("[Test 3 FAILED] TableWrapper không có đủ 6 lỗ (pockets)!");
                return false;
            }

            // Kiểm tra bounds logic
            bool insideCenter = wrapper.IsInsidePlayArea(Vector3.zero);
            bool outsideBounds = wrapper.IsInsidePlayArea(new Vector3(10f, 0f, 10f));
            bool headStringCheck = wrapper.IsBehindHeadString(new Vector3(0.8f, 0f, 0f));
            bool pastHeadStringCheck = !wrapper.IsBehindHeadString(new Vector3(-0.2f, 0f, 0f));

            if (!insideCenter || outsideBounds || !headStringCheck || !pastHeadStringCheck)
            {
                Debug.LogError("[Test 3 FAILED] Tính toán hình học phạm vi bàn và Head String không chính xác!");
                return false;
            }

            Debug.Log("[Test 3 PASSED] TableWrapper với 6 PocketDetector và logic kiểm tra vị trí trên bàn hoạt động chính xác.");
            return true;
        }

        private static bool TestWpaRackGeneration()
        {
            GameObject testGO = new GameObject("TestRackGO");
            try
            {
                var ballManager = testGO.AddComponent<BallManager>();
                var wrapper = testGO.AddComponent<TableWrapper>();
                var monitor = testGO.AddComponent<BallMovementMonitor>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Tạo mock 16 balls
                List<GameObject> tempBallObjects = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    GameObject bGO = new GameObject($"MockBall_{i}");
                    tempBallObjects.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    ballManager.RegisterBall(i, bc);
                }

                // Inject private fields via SerializedObject / reflection
                var so = new SerializedObject(ballManager);
                so.FindProperty("physicsConfig").objectReferenceValue = config;
                so.FindProperty("tableWrapper").objectReferenceValue = wrapper;
                so.FindProperty("movementMonitor").objectReferenceValue = monitor;
                so.ApplyModifiedProperties();

                ballManager.InitializeRegistry();
                ballManager.ResetRack();

                // 1. Kiểm tra bi cái ở Head Spot
                var cueBall = ballManager.CueBall;
                if (Vector3.Distance(cueBall.transform.position, wrapper.HeadSpot) > 0.01f)
                {
                    Debug.LogError($"[Test 4 FAILED] Bi cái không ở Head Spot: {cueBall.transform.position} != {wrapper.HeadSpot}");
                    return false;
                }

                // 2. Kiểm tra bi 8 nằm ở tâm dàn bi (khoảng cách tương đối với Apex)
                var eightBall = ballManager.EightBall;
                float d = config.BallRadius * 2f;
                float expectedEightBallX = wrapper.FootSpot.x - (2 * d * 0.8660254f);
                if (Mathf.Abs(eightBall.transform.position.x - expectedEightBallX) > 0.015f || Mathf.Abs(eightBall.transform.position.z) > 0.01f)
                {
                    Debug.LogError($"[Test 4 FAILED] Bi 8 không nằm ở tâm hàng thứ 3 của dàn bi! Pos={eightBall.transform.position}");
                    return false;
                }

                // 3. Kiểm tra không có 2 bi nào đè lên nhau (khoảng cách tối thiểu giữa 2 tâm bi >= 2*r - 0.005)
                var activeBalls = ballManager.GetActiveObjectBalls();
                for (int i = 0; i < activeBalls.Count; i++)
                {
                    for (int j = i + 1; j < activeBalls.Count; j++)
                    {
                        float dist = Vector3.Distance(activeBalls[i].transform.position, activeBalls[j].transform.position);
                        if (dist < (d - 0.005f))
                        {
                            Debug.LogError($"[Test 4 FAILED] Phát hiện bi {activeBalls[i].BallNumber} và bi {activeBalls[j].BallNumber} va chạm đè lên nhau! Khoảng cách = {dist}");
                            return false;
                        }
                    }
                }

                // Dọn dẹp mock objects
                foreach (var go in tempBallObjects)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }

                Debug.Log("[Test 4 PASSED] Quy trình xếp bi 15 quả WPA 8-ball hoàn toàn chính xác (Apex tại Foot Spot, Bi 8 ở tâm, không đè bi).");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestMovementMonitorLogic()
        {
            GameObject testGO = new GameObject("TestMonitorGO");
            try
            {
                var monitor = testGO.AddComponent<BallMovementMonitor>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                GameObject bGO = new GameObject("TestBall");
                var bc = bGO.AddComponent<BallController>();
                var rb = bc.Rigidbody != null ? bc.Rigidbody : bGO.GetComponent<Rigidbody>();

                bc.ApplyPhysicsConfig(config);
                monitor.Initialize(config, new[] { bc });

                // Ban đầu bi chưa chuyển động
                if (bc.IsMoving(config.RestLinearThreshold, config.RestAngularThreshold))
                {
                    Debug.LogError("[Test 5 FAILED] Bi đứng yên nhưng IsMoving trả về true!");
                    UnityEngine.Object.DestroyImmediate(bGO);
                    return false;
                }

                // Cho bi vận tốc
                rb.velocity = new Vector3(0.5f, 0f, 0f);
                if (!bc.IsMoving(config.RestLinearThreshold, config.RestAngularThreshold))
                {
                    Debug.LogError("[Test 5 FAILED] Bi có vận tốc 0.5m/s nhưng IsMoving trả về false!");
                    UnityEngine.Object.DestroyImmediate(bGO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(bGO);
                Debug.Log("[Test 5 PASSED] Thuật toán phát hiện dừng/chuyển động của bi hoạt động chính xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
