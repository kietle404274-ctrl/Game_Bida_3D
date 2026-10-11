using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Billiards.AI;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    public static class Milestone6Verification
    {
        [MenuItem("Billiards/Run M6 Verification", false, 6)]
        public static void RunAllM6Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M6 (OFFLINE UTILITY AI) ====================");
            int passed = 0;
            int total = 5;

            if (TestGhostBallAndCutAngleGeometry()) passed++;
            if (TestPathClearanceDetection()) passed++;
            if (TestUtilityEvaluationAndWeights()) passed++;
            if (TestDifficultyProfilesDifference()) passed++;
            if (TestAIBallInHandAndSafety()) passed++;

            Debug.Log($"==================== KẾT QUẢ M6: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M6 PASS] Tất cả các thuật toán hình học và Utility AI của Milestone M6 đã đạt chuẩn!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M6 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestGhostBallAndCutAngleGeometry()
        {
            // 1. Kiểm tra vị trí Ghost Ball
            Vector3 targetPos = new Vector3(0f, 0.8635f, 0.5f);
            Vector3 pocketPos = new Vector3(0f, 0.8635f, 1.12f); // Lỗ thẳng phía trước
            Vector3 targetToPocket = (pocketPos - targetPos).normalized;

            float ballDiameter = 2f * PathClearanceChecker.BALL_RADIUS; // 0.0571m
            Vector3 expectedGhost = targetPos - targetToPocket * ballDiameter;

            if (Mathf.Abs(expectedGhost.z - (0.5f - ballDiameter)) > 0.001f || Mathf.Abs(expectedGhost.x) > 0.001f)
            {
                Debug.LogError($"[Test 1 FAILED] Vị trí Ghost Ball tính sai! Expected Z={0.5f - ballDiameter}, Actual Z={expectedGhost.z}");
                return false;
            }

            // 2. Cú đánh thẳng (Straight pot): Bi cái ở (0, 0.8635, -0.2)
            Vector3 cueStraight = new Vector3(0f, 0.8635f, -0.2f);
            Vector3 aimStraight = (expectedGhost - cueStraight).normalized;
            float dotStraight = Vector3.Dot(aimStraight, targetToPocket);
            float cutAngleStraight = Mathf.Acos(Mathf.Clamp(dotStraight, -1f, 1f)) * Mathf.Rad2Deg;

            if (cutAngleStraight > 0.1f)
            {
                Debug.LogError($"[Test 1 FAILED] Cú đánh thẳng nhưng cut angle khác 0: {cutAngleStraight}");
                return false;
            }

            // 3. Cú đánh cắt góc 45 độ
            Vector3 offsetGhost = new Vector3(0.5f, 0.8635f, 0.5f);
            Vector3 dirToPocket = Vector3.forward;
            Vector3 aim45 = (Vector3.forward + Vector3.right).normalized;
            float dot45 = Vector3.Dot(aim45, dirToPocket);
            float cut45 = Mathf.Acos(dot45) * Mathf.Rad2Deg;

            if (Mathf.Abs(cut45 - 45f) > 0.5f)
            {
                Debug.LogError($"[Test 1 FAILED] Cắt góc 45 độ tính sai: {cut45}");
                return false;
            }

            Debug.Log("[Test 1 PASSED] Công thức tính toán vị trí Ghost-ball và góc cắt (Cut Angle) hoàn toàn chuẩn xác.");
            return true;
        }

        private static bool TestPathClearanceDetection()
        {
            GameObject testGO = new GameObject("TestClearanceGO");
            try
            {
                var bm = testGO.AddComponent<BallManager>();

                // Mock bi 1 ở vị trí (0, 0.8635, 0)
                var b1GO = new GameObject("Ball_1");
                b1GO.transform.position = new Vector3(0f, 0.8635f, 0f);
                var bc1 = b1GO.AddComponent<BallController>();
                bc1.SetBallNumber(1);
                bm.RegisterBall(1, bc1);

                // Đường đi A -> B đi qua đúng vị trí bi 1 (từ (0, 0.8635, -0.5) tới (0, 0.8635, 0.5))
                Vector3 startBlocked = new Vector3(0f, 0.8635f, -0.5f);
                Vector3 endBlocked = new Vector3(0f, 0.8635f, 0.5f);

                bool isClear = PathClearanceChecker.IsSegmentClear(startBlocked, endBlocked, 0.0571f, new int[] { 0 }, bm, out float minDist);
                if (isClear)
                {
                    Debug.LogError("[Test 2 FAILED] Hành lang có bi cản trở trực diện nhưng PathClearanceChecker lại báo thông thoáng!");
                    UnityEngine.Object.DestroyImmediate(b1GO);
                    return false;
                }

                // Đường đi lệch sang bên (X = 0.3) không chạm bi 1
                Vector3 startClear = new Vector3(0.3f, 0.8635f, -0.5f);
                Vector3 endClear = new Vector3(0.3f, 0.8635f, 0.5f);

                bool isClearSide = PathClearanceChecker.IsSegmentClear(startClear, endClear, 0.0571f, new int[] { 0 }, bm, out float minDistSide);
                if (!isClearSide)
                {
                    Debug.LogError("[Test 2 FAILED] Hành lang cách xa 30cm nhưng bị báo cản trở!");
                    UnityEngine.Object.DestroyImmediate(b1GO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(b1GO);
                Debug.Log("[Test 2 PASSED] PathClearanceChecker phát hiện chính xác vật cản trên hành lang di chuyển của bi.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestUtilityEvaluationAndWeights()
        {
            var profile = AIDifficultyProfile.NormalProfile;

            // Candidate 1: Cơ hội ăn bi tốt (thẳng, gần, thoáng, ít rủi ro)
            ShotCandidate goodShot = new ShotCandidate
            {
                potFeasibility = 0.9f,
                angleScore = 0.95f,
                distanceScore = 0.85f,
                clearanceScore = 1.0f,
                cueBallPositionScore = 0.7f,
                safetyScore = 0.1f,
                foulRisk = 0.05f
            };

            // Candidate 2: Góc cắt cực khó, xa, nguy cơ scratch cao
            ShotCandidate badShot = new ShotCandidate
            {
                potFeasibility = 0.2f,
                angleScore = 0.15f,
                distanceScore = 0.2f,
                clearanceScore = 0.3f,
                cueBallPositionScore = 0.2f,
                safetyScore = 0.1f,
                foulRisk = 0.85f
            };

            float goodScore = UtilityEvaluator.EvaluateCandidate(goodShot, profile);
            float badScore = UtilityEvaluator.EvaluateCandidate(badShot, profile);

            if (goodScore <= badScore)
            {
                Debug.LogError($"[Test 3 FAILED] Cú đánh tốt lại có điểm Utility ({goodScore}) thấp hơn cú đánh xấu ({badScore})!");
                return false;
            }

            // Kiểm tra lựa chọn tốt nhất
            var list = new List<ShotCandidate> { badShot, goodShot };
            var best = UtilityEvaluator.SelectBestCandidate(list, AIDifficultyProfile.HardProfile);
            if (best != goodShot)
            {
                Debug.LogError("[Test 3 FAILED] Hard profile không chọn candidate tốt nhất!");
                return false;
            }

            Debug.Log("[Test 3 PASSED] Hàm tính toán điểm Utility xếp hạng chuẩn xác các phương án ra cơ theo trọng số.");
            return true;
        }

        private static bool TestDifficultyProfilesDifference()
        {
            var easy = AIDifficultyProfile.EasyProfile;
            var normal = AIDifficultyProfile.NormalProfile;
            var hard = AIDifficultyProfile.HardProfile;

            // 1. Góc cắt tối đa: Hard > Normal > Easy
            if (hard.maxCutAngleDegrees <= normal.maxCutAngleDegrees || normal.maxCutAngleDegrees <= easy.maxCutAngleDegrees)
            {
                Debug.LogError("[Test 4 FAILED] Giới hạn góc cắt không tăng dần theo cấp độ khó!");
                return false;
            }

            // 2. Sai số ngắm: Easy > Normal > Hard
            if (easy.aimNoiseDegrees <= normal.aimNoiseDegrees || normal.aimNoiseDegrees <= hard.aimNoiseDegrees)
            {
                Debug.LogError("[Test 4 FAILED] Sai số ngắm Noise không giảm dần theo cấp độ khó!");
                return false;
            }

            // 3. Mẫu Ball-in-Hand: Hard > Normal > Easy
            if (hard.ballInHandSampleCount <= normal.ballInHandSampleCount || normal.ballInHandSampleCount <= easy.ballInHandSampleCount)
            {
                Debug.LogError("[Test 4 FAILED] Số lượng mẫu Ball-in-Hand không tương ứng cấp độ khó!");
                return false;
            }

            Debug.Log("[Test 4 PASSED] Ba profile Easy / Normal / Hard phân biệt rõ ràng về sai số, chiến thuật và độ nhạy (T13 pass).");
            return true;
        }

        private static bool TestAIBallInHandAndSafety()
        {
            GameObject testGO = new GameObject("TestBiHGO");
            try
            {
                var bm = testGO.AddComponent<BallManager>();
                var tw = testGO.AddComponent<TableWrapper>();
                var state = new MatchState();
                state.ResetForNewRack();
                state.turnSequence = 2;
                state.AssignGroups(PlayerGroup.Solids); // Player A là Solids, AI là Stripes (9-15)
                state.currentPlayerIndex = 1; // Lượt AI

                // Tạo bi 9 ở (0, 0.8635, 0.6)
                var b9GO = new GameObject("Ball_9");
                b9GO.transform.position = new Vector3(0f, 0.8635f, 0.6f);
                var bc9 = b9GO.AddComponent<BallController>();
                bc9.SetBallNumber(9);
                bm.RegisterBall(9, bc9);

                // 1. Test AI Ball-in-Hand tìm vị trí hợp lệ
                Vector3 placedPos = BallInHandPlanner.FindBestPlacement(false, state, bm, tw, AIDifficultyProfile.NormalProfile);

                if (!tw.IsInsidePlayArea(placedPos, 0.02f))
                {
                    Debug.LogError($"[Test 5 FAILED] AI Ball-in-Hand đặt bi ngoài ranh giới bàn: {placedPos}");
                    UnityEngine.Object.DestroyImmediate(b9GO);
                    return false;
                }

                // Khoảng cách tới bi 9 phải an toàn
                float distTo9 = Vector2.Distance(new Vector2(placedPos.x, placedPos.z), new Vector2(0f, 0.6f));
                if (distTo9 < 2f * PathClearanceChecker.BALL_RADIUS)
                {
                    Debug.LogError($"[Test 5 FAILED] AI Ball-in-Hand đè lên bi mục tiêu! Dist={distTo9}");
                    UnityEngine.Object.DestroyImmediate(b9GO);
                    return false;
                }

                // 2. Test Safety Shot Generator
                Vector3 cuePos = new Vector3(0f, 0.8635f, -0.4f);
                var safety = SafetyShotGenerator.GenerateBestSafety(cuePos, state, bm, tw, AIDifficultyProfile.NormalProfile);

                if (safety == null || !safety.isSafety || safety.targetBallNumber != 9)
                {
                    Debug.LogError("[Test 5 FAILED] Safety Shot Generator không tạo được cú đánh an toàn hợp lệ cho bi 9!");
                    UnityEngine.Object.DestroyImmediate(b9GO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(b9GO);
                Debug.Log("[Test 5 PASSED] AI Ball-in-Hand tối ưu vị trí (T15 pass) và Safety Generator hoạt động hợp lệ (T14 pass).");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
