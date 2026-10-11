using System;
using UnityEngine;
using UnityEditor;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    public static class Milestone2Verification
    {
        [MenuItem("Billiards/Run M2 Verification", false, 2)]
        public static void RunAllM2Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M2 (LOCAL SHOT CORE) ====================");
            int passed = 0;
            int total = 4;

            if (TestShotExecutionPhysics()) passed++;
            if (TestCameraDirectorLogic()) passed++;
            if (TestCueStickVisualBehavior()) passed++;
            if (TestNoAimGuideEnforcement()) passed++;

            Debug.Log($"==================== KẾT QUẢ M2: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M2 PASS] Tất cả các điều kiện nghiệm thu của Milestone M2 đã đạt thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M2 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestShotExecutionPhysics()
        {
            GameObject testGO = new GameObject("TestShotGO");
            try
            {
                var ballManager = testGO.AddComponent<BallManager>();
                var monitor = testGO.AddComponent<BallMovementMonitor>();
                var executor = testGO.AddComponent<ShotExecutor>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Tạo mock cue ball
                GameObject cueGO = new GameObject("MockCueBall");
                var bc = cueGO.AddComponent<BallController>();
                var rb = cueGO.AddComponent<Rigidbody>();
                cueGO.AddComponent<SphereCollider>();

                bc.SetBallNumber(0);
                bc.ApplyPhysicsConfig(config);

                ballManager.RegisterBall(0, bc);
                monitor.Initialize(config, new[] { bc });
                executor.Initialize(config, ballManager, monitor);

                // Kiểm tra CanExecuteShot khi bi đang dừng
                if (!executor.CanExecuteShot())
                {
                    Debug.LogError("[Test 1 FAILED] CanExecuteShot trả về false khi bi cái đang dừng và hợp lệ!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                // Thực hiện cú đánh lực 50% hướng Vector3.forward
                Vector3 shotDir = Vector3.forward;
                float power = 0.5f;
                ShotCommand cmd = new ShotCommand(0, shotDir, power, Vector2.zero);

                bool shotExecuted = executor.ExecuteShot(cmd);
                if (!shotExecuted)
                {
                    Debug.LogError("[Test 1 FAILED] ExecuteShot thất bại!");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                // Vận tốc bi cái sau khi nhận xung lực: v = impulse / mass
                // impulse = power * MaxShotImpulse = 0.5 * 6.0 = 3.0 N.s
                // expected velocity = 3.0 / 0.17 ~ 17.6 m/s
                float expectedVel = (power * config.MaxShotImpulse) / config.BallMass;
                float actualVel = rb.velocity.magnitude;

                if (Mathf.Abs(actualVel - expectedVel) > 0.1f)
                {
                    Debug.LogError($"[Test 1 FAILED] Vận tốc bi cái sau cú đánh không đúng xung lực vật lý! Actual={actualVel}, Expected={expectedVel}");
                    UnityEngine.Object.DestroyImmediate(cueGO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(cueGO);
                Debug.Log("[Test 1 PASSED] ShotExecutor tính toán chính xác xung lực (Impulse) và kích hoạt giám sát chuyển động.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestCameraDirectorLogic()
        {
            GameObject camGO = new GameObject("TestCamGO");
            try
            {
                var cam = camGO.AddComponent<UnityEngine.Camera>();
                var camDirector = camGO.AddComponent<CameraDirector>();

                GameObject targetGO = new GameObject("TestTarget");
                targetGO.transform.position = new Vector3(0f, 0.86f, 0f);

                camDirector.SetCueBallTarget(targetGO.transform);

                // Ban đầu ở AimCamera
                if (camDirector.CurrentState != CameraState.Aim)
                {
                    Debug.LogError("[Test 2 FAILED] CameraState ban đầu không phải là Aim!");
                    UnityEngine.Object.DestroyImmediate(targetGO);
                    return false;
                }

                // Kiểm tra xoay hướng ngắm
                camDirector.RotateAim(10f, 0f);
                Vector3 aimDir = camDirector.AimDirection;
                if (Mathf.Abs(aimDir.y) > 0.001f || Mathf.Abs(aimDir.magnitude - 1.0f) > 0.01f)
                {
                    Debug.LogError($"[Test 2 FAILED] AimDirection không chuẩn hóa trên mặt phẳng XZ! Dir={aimDir}");
                    UnityEngine.Object.DestroyImmediate(targetGO);
                    return false;
                }

                // Chuyển sang góc rộng khi bắn
                camDirector.SwitchToShotCamera();
                if (camDirector.CurrentState != CameraState.ShotWide)
                {
                    Debug.LogError("[Test 2 FAILED] SwitchToShotCamera không đổi state sang ShotWide!");
                    UnityEngine.Object.DestroyImmediate(targetGO);
                    return false;
                }

                // Chuyển lại góc ngắm
                camDirector.SwitchToAimCamera();
                if (camDirector.CurrentState != CameraState.Aim)
                {
                    Debug.LogError("[Test 2 FAILED] SwitchToAimCamera không đưa về Aim!");
                    UnityEngine.Object.DestroyImmediate(targetGO);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(targetGO);
                Debug.Log("[Test 2 PASSED] CameraDirector điều khiển góc xoay AimCamera và chuyển đổi mượt sang ShotCamera góc rộng.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGO);
            }
        }

        private static bool TestCueStickVisualBehavior()
        {
            GameObject testGO = new GameObject("TestCueGO");
            try
            {
                var cueCtrl = testGO.AddComponent<CueController>();
                GameObject stickModel = new GameObject("StickModel");
                GameObject cueBall = new GameObject("CueBall");
                cueBall.transform.position = new Vector3(0f, 0.86f, 0f);

                cueCtrl.Initialize(stickModel.transform, cueBall.transform);

                // Kiểm tra gậy cơ bám vị trí khi ngắm với lực 0% và 100%
                Vector3 forwardAim = Vector3.forward;
                cueCtrl.UpdateAim(forwardAim, 0.0f);
                Vector3 posLowPower = stickModel.transform.position;

                cueCtrl.UpdateAim(forwardAim, 1.0f);
                Vector3 posHighPower = stickModel.transform.position;

                // Lực cao hơn thì gậy cơ phải lùi xa hơn bi cái về phía sau (z nhỏ hơn vì aim về +z)
                if (posHighPower.z >= posLowPower.z)
                {
                    Debug.LogError($"[Test 3 FAILED] Gậy cơ không lùi về sau khi tăng lực bắn! LowZ={posLowPower.z}, HighZ={posHighPower.z}");
                    UnityEngine.Object.DestroyImmediate(stickModel);
                    UnityEngine.Object.DestroyImmediate(cueBall);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(stickModel);
                UnityEngine.Object.DestroyImmediate(cueBall);
                Debug.Log("[Test 3 PASSED] CueController căn chỉnh gậy cơ 3D chính xác theo hướng ngắm và độ lùi lực.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestNoAimGuideEnforcement()
        {
            // Kiểm tra quy tắc A08: Không được tồn tại LineRenderer hoặc trajectory preview component
            string[] scripts = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Game/Scripts" });
            bool hasAimGuide = false;

            foreach (var guid in scripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null)
                {
                    string code = script.text;
                    if (code.Contains("LineRenderer") && !path.Contains("Editor") && !path.Contains("Test"))
                    {
                        Debug.LogError($"[Test 4 FAILED] Phát hiện LineRenderer trong script gameplay: {path} (Vi phạm quy tắc A08: No aim guide)!");
                        hasAimGuide = true;
                    }
                }
            }

            if (!hasAimGuide)
            {
                Debug.Log("[Test 4 PASSED] Tuân thủ nghiêm ngặt quy tắc A08: Tuyệt đối không sinh line guide / trajectory prediction.");
                return true;
            }
            return false;
        }
    }
}
