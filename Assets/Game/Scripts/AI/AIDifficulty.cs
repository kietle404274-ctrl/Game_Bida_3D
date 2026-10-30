using System;
using UnityEngine;

namespace Game.Billiards.AI
{
    public enum AIDifficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    [Serializable]
    public class AIDifficultyProfile
    {
        [Header("Profile Info")]
        public AIDifficulty difficulty = AIDifficulty.Normal;
        public string displayName = "Bình Thường (Normal)";

        [Header("Utility Formula Weights")]
        [Tooltip("Trọng số khả thi vào lỗ (Pot Feasibility)")]
        public float wPot = 1.0f;

        [Tooltip("Trọng số góc cắt (Angle Score - góc càng thẳng điểm càng cao)")]
        public float wAngle = 0.8f;

        [Tooltip("Trọng số khoảng cách (Distance Score - bi càng gần lỗ và gần bi cái điểm càng cao)")]
        public float wDistance = 0.5f;

        [Tooltip("Trọng số độ thông thoáng hành lang (Clearance Score)")]
        public float wClearance = 0.7f;

        [Tooltip("Trọng số vị trí bi cái sau cú đánh (Cue Ball Position)")]
        public float wPosition = 0.4f;

        [Tooltip("Trọng số chiến thuật an toàn (Safety Score)")]
        public float wSafety = 0.6f;

        [Tooltip("Trọng số nguy cơ phạm lỗi hoặc rơi bi cái (Foul Risk)")]
        public float wRisk = 1.2f;

        [Header("Geometric Search & Limits")]
        [Tooltip("Góc cắt tối đa cho phép xem xét (Cut Angle limit in degrees)")]
        public float maxCutAngleDegrees = 65f;

        [Tooltip("Ngưỡng điểm khả thi tối thiểu để thực hiện cú pot")]
        public float minPotFeasibility = 0.15f;

        [Tooltip("Ngưỡng điểm để chuyển sang chiến thuật Safety nếu không có cú pot tốt")]
        public float safetyPreferenceThreshold = 0.45f;

        [Header("Execution Noise")]
        [Tooltip("Độ lệch ngắm ngẫu nhiên (Gaussian Noise in degrees)")]
        public float aimNoiseDegrees = 0.75f;

        [Tooltip("Độ sai số lực ngẫu nhiên (Tỷ lệ sai lệch từ 0 đến 1)")]
        public float powerNoiseFraction = 0.05f;

        [Header("Ball-in-Hand Planning")]
        [Tooltip("Số lượng mẫu vị trí thử nghiệm khi AI có quyền Ball-in-Hand")]
        public int ballInHandSampleCount = 30;

        [Header("Advanced Tactics & Capabilities")]
        [Tooltip("Cho phép AI đánh mượn 1 băng chạm bi khi bị giấu bi (Kick Shot)")]
        public bool allowKickShots = true;

        [Tooltip("Cho phép AI đánh mượn bi (Combination Shot)")]
        public bool allowComboShots = true;

        [Tooltip("Cho phép AI đánh bi mục tiêu bật băng vào lỗ (Bank Shot)")]
        public bool allowBankShots = false;

        [Tooltip("Bật thuật toán điều bi (Position Play) tính thế bi dọn bàn tiếp theo")]
        public bool usePositionPlay = false;

        [Tooltip("Tự động áp dụng Trô/Cù-lê né lỗ hoặc hãm bi (Smart Spin)")]
        public bool allowSmartSpin = true;

        public static AIDifficultyProfile EasyProfile => new AIDifficultyProfile
        {
            difficulty = AIDifficulty.Easy,
            displayName = "Dễ (Easy)",
            wPot = 1.2f,
            wAngle = 0.6f,
            wDistance = 0.8f,
            wClearance = 0.5f,
            wPosition = 0.2f,
            wSafety = 0.0f, // Full công, không đánh safety
            wRisk = 0.6f,
            maxCutAngleDegrees = 55f, // Chỉ xét các góc dễ
            minPotFeasibility = 0.20f,
            safetyPreferenceThreshold = 0.0f,
            aimNoiseDegrees = 0.20f, // Sai số nhẹ cho người mới
            powerNoiseFraction = 0.02f,
            ballInHandSampleCount = 15,
            allowKickShots = false,
            allowComboShots = false,
            allowBankShots = false,
            usePositionPlay = false,
            allowSmartSpin = false
        };

        public static AIDifficultyProfile NormalProfile => new AIDifficultyProfile
        {
            difficulty = AIDifficulty.Normal,
            displayName = "Bình Thường (Normal)",
            wPot = 1.5f,
            wAngle = 0.7f,
            wDistance = 1.1f, // Ưu tiên cực mạnh bi gần lỗ
            wClearance = 0.7f,
            wPosition = 0.9f, // Bật điều bi cho Normal
            wSafety = 0.0f, // Full công, không đánh safety
            wRisk = 1.0f,
            maxCutAngleDegrees = 65f,
            minPotFeasibility = 0.12f,
            safetyPreferenceThreshold = 0.0f,
            aimNoiseDegrees = 0.0f, // Ngắm chuẩn xác không bị lệch
            powerNoiseFraction = 0.0f,
            ballInHandSampleCount = 35,
            allowKickShots = true,
            allowComboShots = true,
            allowBankShots = true,
            usePositionPlay = true, // Kích hoạt điều bi cho Normal
            allowSmartSpin = true
        };

        public static AIDifficultyProfile HardProfile => new AIDifficultyProfile
        {
            difficulty = AIDifficulty.Hard,
            displayName = "Khó (Hard)",
            wPot = 1.6f,
            wAngle = 0.8f,
            wDistance = 1.2f, // Ưu tiên cực mạnh bi gần lỗ
            wClearance = 0.9f,
            wPosition = 1.4f, // Điều bi thượng thừa
            wSafety = 0.0f, // Full công, không đánh safety
            wRisk = 1.5f,
            maxCutAngleDegrees = 78f, // Tận dụng các góc cắt khó
            minPotFeasibility = 0.08f,
            safetyPreferenceThreshold = 0.0f,
            aimNoiseDegrees = 0.0f, // Ngắm chính xác tuyệt đối
            powerNoiseFraction = 0.0f,
            ballInHandSampleCount = 65,
            allowKickShots = true,
            allowComboShots = true,
            allowBankShots = true,
            usePositionPlay = true, // Kích hoạt điều bi chuyên nghiệp
            allowSmartSpin = true
        };

        public static AIDifficultyProfile GetProfile(AIDifficulty diff)
        {
            switch (diff)
            {
                case AIDifficulty.Easy: return EasyProfile;
                case AIDifficulty.Hard: return HardProfile;
                case AIDifficulty.Normal:
                default: return NormalProfile;
            }
        }
    }
}
