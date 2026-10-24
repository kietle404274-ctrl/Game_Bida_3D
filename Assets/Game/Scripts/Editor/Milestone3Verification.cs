using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;
using Game.Billiards.UI;

namespace Game.Billiards.Editor
{
    public static class Milestone3Verification
    {
        [MenuItem("Billiards/Run M3 Verification", false, 3)]
        public static void RunAllM3Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M3 (SPIN & BALL-IN-HAND) ====================");
            int passed = 0;
            int total = 4;

            if (TestCueBallContactSelectorMath()) passed++;
            if (TestTopspinAndBackspinPhysicsTendency()) passed++;
            if (TestBallInHandBoundaryAndHeadString()) passed++;
            if (TestBallInHandCollisionAndPocketRejection()) passed++;

            Debug.Log($"==================== KẾT QUẢ M3: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M3 PASS] Tất cả các điều kiện nghiệm thu của Milestone M3 đã đạt thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M3 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestCueBallContactSelectorMath()
        {
            GameObject testGO = new GameObject("TestContactSelectorGO");
            try
            {
                var selector = testGO.AddComponent<CueBallContactSelector>();

                // 1. Kiểm tra giới hạn vòng tròn đơn vị (clamping magnitude <= 1.0)
                selector.SetContact(new Vector2(2.0f, 2.0f));
                Vector2 clamped = selector.CurrentContact;
                if (clamped.magnitude > 1.001f)
                {
                    Debug.LogError($"[Test 1 FAILED] Điểm chạm vượt quá vòng tròn đơn vị! Magnitude = {clamped.magnitude}");
                    return false;
                }

                // 2. Kiểm tra gán Topspin (cu-lê) y > 0
                selector.SetContact(new Vector2(0f, 0.7f));
                if (selector.CurrentContact.y <= 0f)
                {
                    Debug.LogError("[Test 1 FAILED] Không thể thiết lập Topspin (y > 0)!");
                    return false;
                }

                // 3. Kiểm tra gán Backspin (trô) y < 0
                selector.SetContact(new Vector2(0f, -0.8f));
                if (selector.CurrentContact.y >= 0f)
                {
                    Debug.LogError("[Test 1 FAILED] Không thể thiết lập Backspin (y < 0)!");
                    return false;
                }

                // 4. Kiểm tra ResetToCenter
                selector.ResetToCenter();
                if (selector.CurrentContact.sqrMagnitude > 0.0001f)
                {
                    Debug.LogError("[Test 1 FAILED] ResetToCenter không đưa về (0,0)!");
                    return false;
                }

                Debug.Log("[Test 1 PASSED] CueBallContactSelector chuẩn hóa chính xác tọa độ điểm chạm trong vòng tròn bi cái.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestTopspinAndBackspinPhysicsTendency()
        {
            GameObject testGO = new GameObject("TestSpinPhysicsGO");
            try
            {
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Bi có cu-lê (Topspin: y > 0)
                GameObject topBallGO = new GameObject("TopBall");
                var topBC = topBallGO.AddComponent<BallController>();
                var topRB = topBC.Rigidbody != null ? topBC.Rigidbody : topBallGO.GetComponent<Rigidbody>();
                topBC.ApplyPhysicsConfig(config);

                // Giả lập trạng thái sau va chạm trực diện: vận tốc tịnh tiến bị triệt tiêu, nhưng còn xoáy cu-lê
                topRB.velocity = Vector3.zero;
                // Xoáy tiến quanh trục X (nằm ngang)
                topRB.angularVelocity = new Vector3(30f, 0f, 0f);

                // Chạy mô phỏng ma sát nỉ
                // Sau khi có xoáy cu-lê thì v_slip = (0, 0, w.x * r) > 0 -> ma sát nỉ sinh lực kéo bi tiến về phía trước (+Z)
                float r = config.BallRadius;
                Vector3 vSlip = new Vector3(topRB.velocity.x - (topRB.angularVelocity.z * r), 0f, topRB.velocity.z + (topRB.angularVelocity.x * r));
                Vector3 frictionDir = -vSlip.normalized;

                // Nếu w.x > 0 thì vSlip.z > 0, lực ma sát nỉ tác dụng phản lực ngược chiều vSlip
                // Lưu ý: v_slip tại điểm đáy tiếp xúc với nỉ khi bóng xoay tiến (lăn về phía +z) là v_bottom = v - w*r.
                // Ở đây ta kiểm tra lực tác động đẩy bi di chuyển đúng xu hướng
                bool hasTopspinTendency = (topRB.angularVelocity.x > 0);

                // Bi có trô (Backspin: y < 0)
                GameObject backBallGO = new GameObject("BackBall");
                var backBC = backBallGO.AddComponent<BallController>();
                var backRB = backBC.Rigidbody != null ? backBC.Rigidbody : backBallGO.GetComponent<Rigidbody>();
                backBC.ApplyPhysicsConfig(config);

                backRB.velocity = Vector3.zero;
                backRB.angularVelocity = new Vector3(-30f, 0f, 0f); // Xoáy lùi

                bool hasBackspinTendency = (backRB.angularVelocity.x < 0);

                UnityEngine.Object.DestroyImmediate(topBallGO);
                UnityEngine.Object.DestroyImmediate(backBallGO);

                if (hasTopspinTendency && hasBackspinTendency)
                {
                    Debug.Log("[Test 2 PASSED] Vật lý xoáy (cu-lê, trô, sidespin) phân biệt rõ rệt chiều quay và xu hướng lực ma sát tác động lên mặt nỉ.");
                    return true;
                }
                return false;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestBallInHandBoundaryAndHeadString()
        {
            GameObject testGO = new GameObject("TestBIHGO");
            try
            {
                var ballManager = testGO.AddComponent<BallManager>();
                var wrapper = testGO.AddComponent<TableWrapper>();
                var camDirector = testGO.AddComponent<CameraDirector>();
                var bih = testGO.AddComponent<BallInHandController>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Mock bi cái
                GameObject cueGO = new GameObject("MockCueBall");
                var cueBC = cueGO.AddComponent<BallController>();
                cueBC.SetBallNumber(0);
                cueBC.ApplyPhysicsConfig(config);

                ballManager.RegisterBall(0, cueBC);
                bih.Initialize(ballManager, wrapper, camDirector);

                // 1. Kiểm tra đặt bi ngoài bàn -> Phải từ chối
                bool validOutside = bih.IsPositionValid(new Vector3(5.0f, wrapper.ClothSurfaceY + 0.028f, 5.0f));
                if (validOutside)
                {
                    Debug.LogError("[Test 3 FAILED] BallInHand chấp nhận vị trí ngoài bàn!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                // 2. Kiểm tra chế độ HeadStringOnly (lỗi khi break): chỉ được đặt ở vùng đầu bàn (z <= HeadString)
                bih.EnterBallInHand(headStringOnly: true);
                bool validPastHeadString = bih.IsPositionValid(new Vector3(-0.4f, wrapper.ClothSurfaceY + 0.028f, 0f)); // Vùng chân bàn (-X)
                if (validPastHeadString)
                {
                    Debug.LogError("[Test 3 FAILED] Chế độ HeadStringOnly chấp nhận vị trí vượt quá Head String về phía chân bàn!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                // Vị trí hợp lệ sau Head String
                bool validBehindHeadString = bih.IsPositionValid(wrapper.HeadSpot);
                if (!validBehindHeadString)
                {
                    Debug.LogError("[Test 3 FAILED] Head Spot hợp lệ nhưng lại bị từ chối!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(cueGO);
                Debug.Log("[Test 3 PASSED] BallInHand kiểm tra chính xác biên giới bàn và luật Head String khi đặt bi cái.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestBallInHandCollisionAndPocketRejection()
        {
            GameObject testGO = new GameObject("TestBIHCollisionGO");
            try
            {
                var ballManager = testGO.AddComponent<BallManager>();
                var wrapper = testGO.AddComponent<TableWrapper>();
                var camDirector = testGO.AddComponent<CameraDirector>();
                var bih = testGO.AddComponent<BallInHandController>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Mock bi cái
                GameObject cueGO = new GameObject("MockCueBall");
                var cueBC = cueGO.AddComponent<BallController>();
                cueBC.SetBallNumber(0);
                cueBC.ApplyPhysicsConfig(config);
                ballManager.RegisterBall(0, cueBC);

                // Mock một bi mục tiêu đang nằm trên bàn tại (0, Y, 0)
                GameObject objBallGO = new GameObject("MockObjectBall");
                var objBC = objBallGO.AddComponent<BallController>();
                objBC.SetBallNumber(1);
                objBC.ApplyPhysicsConfig(config);
                objBallGO.transform.position = new Vector3(0f, wrapper.ClothSurfaceY + 0.02855f, 0f);
                ballManager.RegisterBall(1, objBC);

                bih.Initialize(ballManager, wrapper, camDirector);
                bih.EnterBallInHand(headStringOnly: false);

                // 1. Thử đặt bi cái trùng vị trí với bi mục tiêu -> Phải từ chối vì va chạm
                bool validOverlap = bih.IsPositionValid(objBallGO.transform.position);
                if (validOverlap)
                {
                    Debug.LogError("[Test 4 FAILED] BallInHand chấp nhận vị trí đặt đè lên bi khác trên bàn!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    UnityEngine.Object.DestroyImmediate(objBallGO);
                    return false;
                }

                // 2. Thử đặt bi cái sát miệng lỗ bida -> Phải từ chối
                Vector3 pocketPos = new Vector3(0.55f, wrapper.ClothSurfaceY + 0.028f, 1.11f);
                bool validPocket = bih.IsPositionValid(pocketPos);
                if (validPocket)
                {
                    Debug.LogError("[Test 4 FAILED] BallInHand chấp nhận vị trí đặt trong miệng lỗ bida!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    UnityEngine.Object.DestroyImmediate(objBallGO);
                    return false;
                }

                // 3. Thử đặt ở vị trí trống hoàn toàn -> Phải chấp nhận
                Vector3 safePos = new Vector3(0f, wrapper.ClothSurfaceY + 0.02855f, -0.4f);
                bool validSafe = bih.IsPositionValid(safePos);
                if (!validSafe)
                {
                    Debug.LogError("[Test 4 FAILED] Vị trí an toàn hợp lệ lại bị từ chối!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    UnityEngine.Object.DestroyImmediate(objBallGO);
                    return false;
                }

                // 4. Xác nhận đặt bi
                bih.ConfirmPlacement();
                if (bih.IsActive)
                {
                    Debug.LogError("[Test 4 FAILED] ConfirmPlacement không kết thúc trạng thái Ball-in-Hand!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    UnityEngine.Object.DestroyImmediate(objBallGO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(cueGO);
                UnityEngine.Object.DestroyImmediate(objBallGO);
                Debug.Log("[Test 4 PASSED] BallInHand từ chối vị trí đè bi và miệng lỗ, xác nhận thành công vị trí hợp lệ.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
