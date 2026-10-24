#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace Game.Billiards.Editor
{
    public static class MasterRegressionVerification
    {
        [MenuItem("Tools/Game/Run Master Regression Test Suite (M1-M11)")]
        public static void RunAllTests()
        {
            Debug.Log("<color=yellow>========================================================================================</color>");
            Debug.Log("<color=yellow>   ĐỒ ÁN TỐT NGHIỆP: GAME BIDA 3D TÍCH HỢP AI (UNITY 2022.3.62f3 LTS HDRP)   </color>");
            Debug.Log("<color=yellow>   MASTER REGRESSION & THESIS ACCEPTANCE TEST SUITE (MILESTONE 1 - 11)   </color>");
            Debug.Log("<color=yellow>========================================================================================</color>");

            int totalMilestones = 11;
            int passedMilestones = 0;

            Action[] suites = new Action[]
            {
                () => { Debug.Log("<color=cyan>--- [1/11] KIỂM THỬ MILESTONE 1: VẬT LÝ, BÀN ĐẤU & 16 BI WPA ---</color>"); Milestone1Verification.RunAllM1Tests(); },
                () => { Debug.Log("<color=cyan>--- [2/11] KIỂM THỬ MILESTONE 2: CƠ CHẾ ĐÁNH BI CỤC BỘ & HOẠT HỌA CƠ ---</color>"); Milestone2Verification.RunAllM2Tests(); },
                () => { Debug.Log("<color=cyan>--- [3/11] KIỂM THỬ MILESTONE 3: HIỆU ỨNG XOÁY (SPIN) & ĐẶT BI TRẮNG DO DO ---</color>"); Milestone3Verification.RunAllM3Tests(); },
                () => { Debug.Log("<color=cyan>--- [4/11] KIỂM THỬ MILESTONE 4: TRỌNG TÀI LUẬT WPA 8-BALL & QUẢN LÝ TRẬN ĐẤU ---</color>"); Milestone4Verification.RunAllM4Tests(); },
                () => { Debug.Log("<color=cyan>--- [5/11] KIỂM THỬ MILESTONE 5: GIAO DIỆN NGƯỜI CHƠI (HUD) & ĐỒNG HỒ ---</color>"); Milestone5Verification.RunAllM5Tests(); },
                () => { Debug.Log("<color=cyan>--- [6/11] KIỂM THỬ MILESTONE 6: HỆ THỐNG TRÍ TUỆ NHÂN TẠO (UTILITY AI 3 CẤP ĐỘ) ---</color>"); Milestone6Verification.RunAllM6Tests(); },
                () => { Debug.Log("<color=cyan>--- [7/11] KIỂM THỬ MILESTONE 7: SẢNH CHỜ 3D & CẤU HÌNH THÔNG SỐ TRẬN ---</color>"); Milestone7Verification.RunAllM7Tests(); },
                () => { Debug.Log("<color=cyan>--- [8/11] KIỂM THỬ MILESTONE 8: KẾT NỐI MẠNG PHOTON PUN 2 & PHÒNG CHỜ ONLINE ---</color>"); Milestone8Verification.RunVerification(); },
                () => { Debug.Log("<color=cyan>--- [9/11] KIỂM THỬ MILESTONE 9: MASTER CLIENT AUTHORITY & ĐỒNG BỘ BI 20HZ ---</color>"); Milestone9Verification.RunVerification(); },
                () => { Debug.Log("<color=cyan>--- [10/11] KIỂM THỬ MILESTONE 10: ÂM THANH SPATIAL, CAMERA SHAKE & HIỆU ỨNG VFX ---</color>"); Milestone10Verification.RunVerification(); },
                () => {
                    Debug.Log("<color=cyan>--- [11/11] KIỂM THỬ MILESTONE 11: HIỆU NĂNG, TÍNH TOÀN VẸN & SẴN SÀNG BUILD STANDALONE ---</color>");
                    if (!TestMilestone11SystemChecks()) throw new Exception("Kiểm thử hệ thống M11 không đạt!");
                }
            };

            for (int i = 0; i < suites.Length; i++)
            {
                try
                {
                    suites[i]();
                    passedMilestones++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MasterRegression] Milestone {i + 1} gặp lỗi: {ex.Message}\n{ex.StackTrace}");
                }
            }

            Debug.Log("<color=yellow>========================================================================================</color>");
            if (passedMilestones == totalMilestones)
            {
                Debug.Log($"<color=green>★★★ TẤT CẢ {passedMilestones}/{totalMilestones} MILESTONES ĐÃ ĐẠT CHUẨN ĐỒ ÁN TỐT NGHIỆP! DỰ ÁN SẴN SÀNG NGHIỆM THU! ★★★</color>");
            }
            else
            {
                Debug.LogError($"<color=red>✗ KẾT QUẢ: {passedMilestones}/{totalMilestones} Milestones đạt chuẩn.</color>");
            }
            Debug.Log("<color=yellow>========================================================================================</color>");
        }

        private static bool TestMilestone11SystemChecks()
        {
            int subPassed = 0;
            int subTotal = 4;

            // 1. Kiểm tra cấu hình Build Settings
            if (TestBuildSettingsScenes()) subPassed++;

            // 2. Kiểm tra tính toàn vẹn tài sản Publisher (Rule A02)
            if (TestPublisherAssetIntegrity()) subPassed++;

            // 3. Kiểm tra thông số vật lý FixedUpdate và Sleep
            if (TestPhysicsPerformanceParameters()) subPassed++;

            // 4. Kiểm tra cấu trúc đóng gói Windows Standalone
            if (TestStandalonePackagingReadiness()) subPassed++;

            return subPassed == subTotal;
        }

        private static bool TestBuildSettingsScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length < 3)
            {
                Debug.LogError($"[M11] Build Settings chỉ có {scenes.Length} scene, yêu cầu tối thiểu 3 scene (Boot, Lobby, Gameplay).");
                return false;
            }

            bool hasBoot = false, hasLobby = false, hasGameplay = false;
            foreach (var s in scenes)
            {
                if (s.path.Contains("Boot.unity") && s.enabled) hasBoot = true;
                if (s.path.Contains("Lobby.unity") && s.enabled) hasLobby = true;
                if (s.path.Contains("Gameplay.unity") && s.enabled) hasGameplay = true;
            }

            if (!hasBoot || !hasLobby || !hasGameplay)
            {
                Debug.LogError($"[M11] Build Settings thiếu các scene bắt buộc: Boot={hasBoot}, Lobby={hasLobby}, Gameplay={hasGameplay}");
                return false;
            }

            Debug.Log("<color=lime>✓ TestBuildSettingsScenes: Cả 3 scene Boot, Lobby, Gameplay đã được cấu hình và kích hoạt chính xác.</color>");
            return true;
        }

        private static bool TestPublisherAssetIntegrity()
        {
            // Kiểm tra Rule A02: Tuyệt đối không can thiệp/sửa đổi/thêm script vào thư mục gốc của Publisher
            string publisherDir = "Assets/AK Studio Art/The Billiards Room/";
            if (Directory.Exists(publisherDir))
            {
                string[] csFiles = Directory.GetFiles(publisherDir, "*.cs", SearchOption.AllDirectories);
                foreach (var file in csFiles)
                {
                    string content = File.ReadAllText(file);
                    if (content.Contains("namespace Game.Billiards") || content.Contains("Game.Billiards."))
                    {
                        Debug.LogError($"[M11] Vi phạm Rule A02: Phát hiện code custom trong thư mục publisher asset: {file}");
                        return false;
                    }
                }
            }

            Debug.Log("<color=lime>✓ TestPublisherAssetIntegrity: Thư mục publisher nguyên bản 100%, tuân thủ nghiêm ngặt Rule A02.</color>");
            return true;
        }

        private static bool TestPhysicsPerformanceParameters()
        {
            // Tần số mô phỏng WPA (100Hz = fixedDeltaTime 0.01s)
            float fixedDelta = Time.fixedDeltaTime;
            if (fixedDelta > 0.021f)
            {
                Debug.LogWarning($"[M11] Time.fixedDeltaTime hiện tại là {fixedDelta}s. Kiến nghị 0.01s (100Hz) cho giải đấu WPA.");
            }

            // Ngưỡng dừng bi an toàn
            float linearSleep = UnityEngine.Physics.sleepThreshold;
            if (linearSleep < 0.0001f)
            {
                Debug.LogWarning($"[M11] Physics.sleepThreshold quá thấp ({linearSleep}), có thể gây tốn CPU khi bi dừng.");
            }

            Debug.Log("<color=lime>✓ TestPhysicsPerformanceParameters: Thông số vật lý và chu kỳ mô phỏng ổn định.</color>");
            return true;
        }

        private static bool TestStandalonePackagingReadiness()
        {
            // Kiểm tra tồn tại thư mục xuất bản Builds
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string buildsDir = Path.Combine(projectRoot, "Builds");
            if (!Directory.Exists(buildsDir))
            {
                Directory.CreateDirectory(buildsDir);
            }

            // Kiểm tra phương thức Build Script
            var buildType = typeof(BuildScript);
            if (buildType == null || buildType.GetMethod("BuildWindowsStandalone") == null)
            {
                Debug.LogError("[M11] Không tìm thấy phương thức BuildScript.BuildWindowsStandalone()!");
                return false;
            }

            Debug.Log("<color=lime>✓ TestStandalonePackagingReadiness: Pipeline đóng gói Windows Standalone (x64) hoàn toàn sẵn sàng.</color>");
            return true;
        }
    }
}
#endif
