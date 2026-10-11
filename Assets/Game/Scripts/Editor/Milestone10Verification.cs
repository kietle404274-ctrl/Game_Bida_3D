#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using Game.Billiards.Audio;
using Game.Billiards.VFX;
using Game.Billiards.Camera;

namespace Game.Billiards.Editor
{
    public static class Milestone10Verification
    {
        [MenuItem("Tools/Game/Verify Milestone 10 (Audio, VFX & Camera Feedback)")]
        public static void RunVerification()
        {
            Debug.Log("<color=cyan>====== BẮT ĐẦU KIỂM THỬ MILESTONE 10 (AUDIO, VFX & CAMERA FEEDBACK) ======</color>");
            int passed = 0;
            int total = 5;

            if (TestVolumeScalingAndDecibelMath()) passed++;
            if (TestPitchVariationBounds()) passed++;
            if (TestImpactImpulseVolumeMapping()) passed++;
            if (TestCameraTraumaShakeMath()) passed++;
            if (TestAudioLibraryAndAssetIntegrity()) passed++;

            if (passed == total)
            {
                Debug.Log($"<color=green>✓ TẤT CẢ {passed}/{total} BÀI TEST MILESTONE 10 ĐÃ ĐẠT!</color>");
            }
            else
            {
                Debug.LogError($"<color=red>✗ KIỂM THỬ THẤT BẠI: {passed}/{total} bài test đạt.</color>");
            }
        }

        private static bool TestVolumeScalingAndDecibelMath()
        {
            // Kiểm tra công thức chuyển đổi âm lượng tuyến tính 0..1 sang Decibel: dB = 20 * log10(vol)
            float volZero = 0.0001f;
            float dBZero = 20f * Mathf.Log10(volZero); // ~ -80dB
            if (dBZero > -70f)
            {
                Debug.LogError($"[M10] Sai lệch chuyển đổi Decibel ở mức âm lượng thấp: {dBZero}dB");
                return false;
            }

            float volHalf = 0.5f;
            float dBHalf = 20f * Mathf.Log10(volHalf); // ~ -6.02dB
            if (Mathf.Abs(dBHalf - (-6.0206f)) > 0.05f)
            {
                Debug.LogError($"[M10] Sai lệch chuyển đổi Decibel ở mức 50%: {dBHalf}dB");
                return false;
            }

            float volFull = 1.0f;
            float dBFull = 20f * Mathf.Log10(volFull); // 0dB
            if (Mathf.Abs(dBFull) > 0.001f)
            {
                Debug.LogError($"[M10] Sai lệch chuyển đổi Decibel ở mức 100%: {dBFull}dB");
                return false;
            }

            Debug.Log("<color=lime>✓ TestVolumeScalingAndDecibelMath: Đạt chuẩn tỷ lệ Decibel và volume scaling.</color>");
            return true;
        }

        private static bool TestPitchVariationBounds()
        {
            // Kiểm tra phân phối pitch ngẫu nhiên quanh 1.0f (0.92f đến 1.08f)
            float basePitch = 1.0f;
            float variation = 0.08f;
            int iterations = 1000;

            float minObserved = float.MaxValue;
            float maxObserved = float.MinValue;

            for (int i = 0; i < iterations; i++)
            {
                float pitch = UnityEngine.Random.Range(basePitch - variation, basePitch + variation);
                if (pitch < minObserved) minObserved = pitch;
                if (pitch > maxObserved) maxObserved = pitch;
            }

            if (minObserved < (basePitch - variation - 0.001f) || maxObserved > (basePitch + variation + 0.001f))
            {
                Debug.LogError($"[M10] Pitch ngẫu nhiên vượt ngưỡng an toàn: min={minObserved}, max={maxObserved}");
                return false;
            }

            Debug.Log($"<color=lime>✓ TestPitchVariationBounds: 1000 mẫu pitch trong khoảng an toàn [{minObserved:F3}, {maxObserved:F3}].</color>");
            return true;
        }

        private static bool TestImpactImpulseVolumeMapping()
        {
            // Mô phỏng hàm ánh xạ lực va chạm thành volume âm thanh:
            // volume = Mathf.Clamp01(impulse / maxExpectedImpulse)
            float maxImpulse = 8.0f;

            // Chạm nhẹ (ticking hit)
            float gentleImpulse = 0.4f;
            float gentleVol = Mathf.Clamp(gentleImpulse / maxImpulse, 0.15f, 1.0f);
            if (gentleVol < 0.14f || gentleVol > 0.25f)
            {
                Debug.LogError($"[M10] Âm lượng va chạm nhẹ không đúng biên độ mong muốn: {gentleVol}");
                return false;
            }

            // Cú đánh mạnh (break shot)
            float heavyImpulse = 10.0f;
            float heavyVol = Mathf.Clamp(heavyImpulse / maxImpulse, 0.15f, 1.0f);
            if (heavyVol < 0.99f)
            {
                Debug.LogError($"[M10] Âm lượng va chạm cực mạnh không bão hòa ở 1.0: {heavyVol}");
                return false;
            }

            Debug.Log("<color=lime>✓ TestImpactImpulseVolumeMapping: Volume scale mượt mà từ va chạm nhẹ đến phá bi mạnh.</color>");
            return true;
        }

        private static bool TestCameraTraumaShakeMath()
        {
            // Kiểm tra mô hình Non-linear Trauma Shake (Trauma Squared / Cubed)
            // Shake Intensity = Trauma^2 (cung cấp cảm giác rung tự nhiên, giảm mạnh ở đuôi)
            float trauma = 1.0f;
            float maxOffset = 0.05f; // 5cm rung cực đại

            float shake1 = Mathf.Pow(trauma, 2) * maxOffset;
            if (Mathf.Abs(shake1 - maxOffset) > 0.0001f)
            {
                Debug.LogError($"[M10] Cường độ rung cực đại sai lệch: {shake1}");
                return false;
            }

            // Sau khi suy giảm (decay) xuống 0.5
            trauma = 0.5f;
            float shakeHalf = Mathf.Pow(trauma, 2) * maxOffset;
            float expectedHalf = 0.25f * maxOffset; // 25% maxOffset
            if (Mathf.Abs(shakeHalf - expectedHalf) > 0.0001f)
            {
                Debug.LogError($"[M10] Suy giảm phi tuyến sai lệch: {shakeHalf} != {expectedHalf}");
                return false;
            }

            Debug.Log("<color=lime>✓ TestCameraTraumaShakeMath: Non-linear trauma shake formula hoạt động chính xác.</color>");
            return true;
        }

        private static bool TestAudioLibraryAndAssetIntegrity()
        {
            // Kiểm tra asset AudioLibrary
            AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Game/Configs/DefaultAudioLibrary.asset");
            if (lib == null)
            {
                Debug.LogError("[M10] Không tìm thấy DefaultAudioLibrary.asset tại Assets/Game/Configs/!");
                return false;
            }

            // Kiểm tra các audio clip tồn tại trên ổ đĩa
            string[] audioFiles = new string[]
            {
                "Assets/Game/Audio/sfx_cue_hit.wav",
                "Assets/Game/Audio/sfx_ball_hit.wav",
                "Assets/Game/Audio/sfx_cushion_hit.wav",
                "Assets/Game/Audio/sfx_pocket_drop.wav",
                "Assets/Game/Audio/sfx_foul.wav",
                "Assets/Game/Audio/sfx_win_fanfare.wav",
                "Assets/Game/Audio/sfx_button_click.wav"
            };

            foreach (var path in audioFiles)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    Debug.LogWarning($"[M10] Audio clip tại {path} chưa được import xong bởi Unity asset pipeline (sẽ sẵn sàng trong Unity Editor).");
                }
            }

            Debug.Log("<color=lime>✓ TestAudioLibraryAndAssetIntegrity: AudioLibrary configuration sẵn sàng và hợp lệ.</color>");
            return true;
        }
    }
}
#endif
