using System;
using UnityEngine;
using Game.Billiards.Core;

namespace Game.Billiards.AI
{
    public enum AIShotType
    {
        DirectPot,
        Combination,
        Bank,
        Kick,
        Safety,
        Break
    }

    [Serializable]
    public class ShotCandidate
    {
        [Header("Tactical Shot Classification")]
        public AIShotType shotType = AIShotType.DirectPot;
        public int intermediateBallNumber = -1; // Bi A chạm đầu tiên trong cú combo
        public Vector3 predictedCueStopPos; // Vị trí bi cái dự kiến dừng lại sau cú đánh

        [Header("Target & Goal")]
        public int targetBallNumber = -1;
        public int targetPocketId = -1;
        public Vector3 targetPocketPosition;
        public Vector3 ghostBallPosition;
        public Vector3 targetBallPosition;
        public Vector3 cueBallPosition;

        [Header("Geometric Metrics")]
        public Vector3 aimDirection; // Từ bi cái tới ghost ball (chuẩn hóa trên mặt bàn XZ)
        public float cutAngleDegrees; // Góc giữa đường cue->ghost và target->pocket
        public float cueToGhostDistance;
        public float targetToPocketDistance;
        public float pocketEntryAngleDegrees; // Góc nghiêng khi bi mục tiêu đi vào miệng lỗ

        [Header("Shot Parameters")]
        public float estimatedPower01 = 0.35f;
        public Vector2 estimatedSpin = Vector2.zero; // x: sidespin, y: top/backspin
        public bool isSafety = false;
        public bool isLegalBreak = false;

        [Header("Utility Evaluation Components (0..1)")]
        public float potFeasibility = 0f;
        public float angleScore = 0f;
        public float distanceScore = 0f;
        public float clearanceScore = 0f;
        public float cueBallPositionScore = 0f;
        public float safetyScore = 0f;
        public float foulRisk = 0f;
        public float finalUtilityScore = 0f;

        public ShotCommand ToShotCommand(int actorId, AIDifficultyProfile profile)
        {
            Vector3 finalDir = aimDirection;
            float finalPower = estimatedPower01;

            if (profile != null)
            {
                // Thêm sai số ngắm (Noise) theo góc ngẫu nhiên quanh trục Y
                float noiseAngle = UnityEngine.Random.Range(-profile.aimNoiseDegrees, profile.aimNoiseDegrees);
                finalDir = Quaternion.Euler(0f, noiseAngle, 0f) * aimDirection;
                finalDir = new Vector3(finalDir.x, 0f, finalDir.z).normalized;

                // Thêm sai số lực
                float powerNoise = UnityEngine.Random.Range(-profile.powerNoiseFraction, profile.powerNoiseFraction);
                float maxAllowed = (targetPocketId >= 0) ? 0.70f : 1.0f;
                finalPower = Mathf.Clamp(finalPower + powerNoise, 0.08f, maxAllowed);
            }

            int calledBall = (isSafety || isLegalBreak || targetPocketId < 0) ? -1 : targetBallNumber;
            int calledPocket = (isSafety || isLegalBreak || targetPocketId < 0) ? -1 : targetPocketId;

            return new ShotCommand(
                shooterActorId: actorId,
                direction: finalDir,
                power01: finalPower,
                cueContact01: estimatedSpin,
                calledBallNumber: calledBall,
                calledPocketId: calledPocket,
                calledSafety: isSafety
            );
        }

        public ShotCandidate Clone()
        {
            return (ShotCandidate)this.MemberwiseClone();
        }
    }
}
