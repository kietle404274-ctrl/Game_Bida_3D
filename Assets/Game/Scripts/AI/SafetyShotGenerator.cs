using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public static class SafetyShotGenerator
    {
        public static ShotCandidate GenerateBestSafety(
            Vector3 cueBallPos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            if (state == null || ballManager == null) return null;

            List<int> targetBalls = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            if (targetBalls.Count == 0) return null;

            ShotCandidate bestSafety = null;
            float bestSafetyScore = -1f;

            for (int i = 0; i < targetBalls.Count; i++)
            {
                int ballNum = targetBalls[i];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null) continue;

                Vector3 targetPos = ball.transform.position;

                // Kiểm tra đường ngắm thẳng từ cuePos tới targetPos có thông thoáng không
                int[] ignored = new int[] { 0, ballNum };
                float clearance = PathClearanceChecker.CalculateClearanceScore(cueBallPos, targetPos, ignored, ballManager);
                if (clearance <= 0f) continue;

                Vector3 cueToTarget = new Vector3(targetPos.x - cueBallPos.x, 0f, targetPos.z - cueBallPos.z);
                float dist = cueToTarget.magnitude;
                Vector3 aimDir = cueToTarget.normalized;

                // Để đảm bảo Rail requirement: lực phải đủ đẩy bi mục tiêu chạm băng gần nhất
                // Tìm khoảng cách từ targetPos tới 4 băng:
                float halfW = (tableWrapper != null) ? tableWrapper.PlayableHalfWidth : 0.56f;
                float halfL = (tableWrapper != null) ? tableWrapper.PlayableHalfLength : 1.12f;

                float distToRailX = halfL - Mathf.Abs(targetPos.x);
                float distToRailZ = halfW - Mathf.Abs(targetPos.z);
                float minDistToRail = Mathf.Min(distToRailX, distToRailZ);

                // Lực vừa đủ để chạm bi mục tiêu và lăn tới băng (0.18 - 0.35)
                float power = Mathf.Clamp(0.15f + (dist * 0.12f + minDistToRail * 0.20f), 0.18f, 0.40f);

                // Điểm an toàn: khoảng cách bi cái càng xa trung tâm bàn / đối thủ càng tốt
                float safetyScore = Mathf.Clamp01(0.5f + (dist / 2.0f) * 0.3f + clearance * 0.2f);

                if (safetyScore > bestSafetyScore)
                {
                    bestSafetyScore = safetyScore;
                    bestSafety = new ShotCandidate
                    {
                        shotType = AIShotType.Safety,
                        targetBallNumber = ballNum,
                        targetPocketId = -1,
                        targetBallPosition = targetPos,
                        cueBallPosition = cueBallPos,
                        ghostBallPosition = targetPos - aimDir * (2f * PathClearanceChecker.BALL_RADIUS),
                        aimDirection = aimDir,
                        cutAngleDegrees = 0f,
                        cueToGhostDistance = dist,
                        targetToPocketDistance = minDistToRail,
                        estimatedPower01 = power,
                        estimatedSpin = new Vector2(0f, -0.3f), // Kéo nhẹ lại (backspin) để bi cái ở lại xa
                        isSafety = true,
                        isLegalBreak = false,
                        potFeasibility = 0.05f,
                        angleScore = 1f,
                        distanceScore = 0.7f,
                        clearanceScore = clearance,
                        safetyScore = safetyScore,
                        foulRisk = 0.05f
                    };
                }
            }

            return bestSafety;
        }
    }
}
