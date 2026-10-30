using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    /// <summary>
    /// Thuật toán giải vây khi bị giấu bi (Snookered):
    /// Sử dụng phương pháp phản xạ ảnh gương (Mirror Method) qua 4 thành băng để tính toán
    /// đường đánh 1 băng chạm bi mục tiêu hợp lệ, đảm bảo không phạm luật No-Ball-Contact.
    /// </summary>
    public static class KickShotPlanner
    {
        private struct RailSegment
        {
            public bool isXAxis; // true: băng ngắn (X cố định), false: băng dài (Z cố định)
            public float railCoord; // Tọa độ X hoặc Z của mép mũi băng
            public float minOther;
            public float maxOther;
            public Vector3 normal;
        }

        public static ShotCandidate FindBestKickShot(
            Vector3 cuePos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            if (state == null || ballManager == null || tableWrapper == null) return null;

            List<int> legalTargets = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            if (legalTargets.Count == 0) return null;

            float footX = TableWrapper.FootCushionX;
            float headX = TableWrapper.HeadCushionX;
            float sideZ = TableWrapper.CushionHalfWidth;

            // Định nghĩa 4 đoạn băng phẳng an toàn (tránh các miệng lỗ góc và lỗ giữa)
            RailSegment[] rails = new RailSegment[]
            {
                // Băng ngắn Foot (-X)
                new RailSegment { isXAxis = true, railCoord = footX, minOther = -0.42f, maxOther = 0.42f, normal = Vector3.right },
                // Băng ngắn Head (+X)
                new RailSegment { isXAxis = true, railCoord = headX, minOther = -0.42f, maxOther = 0.42f, normal = Vector3.left },
                // Băng dài Side (+Z), tránh lỗ giữa tại X in [-0.08, 0.08]
                new RailSegment { isXAxis = false, railCoord = sideZ, minOther = 0.08f, maxOther = 0.98f, normal = Vector3.back },
                new RailSegment { isXAxis = false, railCoord = sideZ, minOther = -0.98f, maxOther = -0.08f, normal = Vector3.back },
                // Băng dài Side (-Z)
                new RailSegment { isXAxis = false, railCoord = -sideZ, minOther = 0.08f, maxOther = 0.98f, normal = Vector3.forward },
                new RailSegment { isXAxis = false, railCoord = -sideZ, minOther = -0.98f, maxOther = -0.08f, normal = Vector3.forward },
            };

            ShotCandidate bestKick = null;
            float bestKickScore = -1f;

            for (int t = 0; t < legalTargets.Count; t++)
            {
                int ballNum = legalTargets[t];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null) continue;

                Vector3 targetPos = ball.transform.position;

                for (int r = 0; r < rails.Length; r++)
                {
                    RailSegment rail = rails[r];
                    Vector3 mirrorTarget;

                    if (rail.isXAxis)
                    {
                        mirrorTarget = new Vector3(2f * rail.railCoord - targetPos.x, targetPos.y, targetPos.z);
                    }
                    else
                    {
                        mirrorTarget = new Vector3(targetPos.x, targetPos.y, 2f * rail.railCoord - targetPos.z);
                    }

                    // Tìm giao điểm của tia (cuePos -> mirrorTarget) với đường thẳng băng
                    Vector3 delta = mirrorTarget - cuePos;
                    float hitOther;
                    Vector3 bouncePoint;

                    if (rail.isXAxis)
                    {
                        if (Mathf.Abs(delta.x) < 0.001f) continue;
                        float factor = (rail.railCoord - cuePos.x) / delta.x;
                        if (factor <= 0.05f || factor >= 0.95f) continue; // Điểm chạm phải nằm giữa cue và target
                        hitOther = cuePos.z + factor * delta.z;
                        if (hitOther < rail.minOther || hitOther > rail.maxOther) continue;
                        bouncePoint = new Vector3(rail.railCoord, cuePos.y, hitOther);
                    }
                    else
                    {
                        if (Mathf.Abs(delta.z) < 0.001f) continue;
                        float factor = (rail.railCoord - cuePos.z) / delta.z;
                        if (factor <= 0.05f || factor >= 0.95f) continue;
                        hitOther = cuePos.x + factor * delta.x;
                        if (hitOther < rail.minOther || hitOther > rail.maxOther) continue;
                        bouncePoint = new Vector3(hitOther, cuePos.y, rail.railCoord);
                    }

                    // Kiểm tra góc tới băng không quá nông (> 18 độ so với mặt băng)
                    Vector3 inDir = (bouncePoint - cuePos).normalized;
                    if (Vector3.Dot(inDir, -rail.normal) < 0.28f) continue;

                    // Kiểm tra độ thông thoáng của 2 đoạn đường:
                    // Đoạn 1: Cue -> Băng (loại trừ bi cái)
                    int[] ignoredSegment1 = new int[] { 0 };
                    float clearance1 = PathClearanceChecker.CalculateClearanceScore(cuePos, bouncePoint, ignoredSegment1, ballManager);
                    if (clearance1 <= 0f) continue;

                    // Đoạn 2: Băng -> Target (loại trừ bi mục tiêu)
                    int[] ignoredSegment2 = new int[] { 0, ballNum };
                    float clearance2 = PathClearanceChecker.CalculateClearanceScore(bouncePoint, targetPos, ignoredSegment2, ballManager);
                    if (clearance2 <= 0f) continue;

                    float dist1 = Vector3.Distance(cuePos, bouncePoint);
                    float dist2 = Vector3.Distance(bouncePoint, targetPos);
                    float totalDist = dist1 + dist2;

                    // Lực đánh: vừa đủ đi hết quãng đường + chạm băng (0.24 - 0.55)
                    float power = Mathf.Clamp(0.20f + totalDist * 0.16f, 0.22f, 0.55f);

                    float kickScore = (clearance1 * 0.5f + clearance2 * 0.5f) * (1f - Mathf.Clamp01(totalDist / 3.0f));

                    if (kickScore > bestKickScore)
                    {
                        bestKickScore = kickScore;
                        bestKick = new ShotCandidate
                        {
                            shotType = AIShotType.Kick,
                            targetBallNumber = ballNum,
                            targetPocketId = -1,
                            targetBallPosition = targetPos,
                            cueBallPosition = cuePos,
                            ghostBallPosition = bouncePoint,
                            aimDirection = inDir,
                            cutAngleDegrees = 0f,
                            cueToGhostDistance = dist1,
                            targetToPocketDistance = dist2,
                            estimatedPower01 = power,
                            estimatedSpin = Vector2.zero,
                            isSafety = false,
                            isLegalBreak = false,
                            potFeasibility = 0.05f,
                            angleScore = 0.8f,
                            distanceScore = Mathf.Clamp01(1f - totalDist / 2.5f),
                            clearanceScore = (clearance1 + clearance2) * 0.5f,
                            safetyScore = 0.6f,
                            foulRisk = 0.05f,
                            finalUtilityScore = 0.65f * kickScore
                        };
                    }
                }
            }

            return bestKick;
        }
    }
}
