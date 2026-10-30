using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    /// <summary>
    /// Thuật toán tìm vị trí đặt bi (Ball-in-Hand) thông minh theo tư duy cơ thủ thực tế:
    /// Thay vì rà lưới ngẫu nhiên, thuật toán tìm các bi mục tiêu dễ ăn nhất và đặt bi cái
    /// ngay sau lưng Ghost Ball trên một đường thẳng tắp (Straight-in shot, cự ly vàng 24cm - 40cm).
    /// </summary>
    public static class BallInHandPlanner
    {
        public static Vector3 FindBestPlacement(
            bool headStringOnly, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            if (tableWrapper == null || ballManager == null)
            {
                return (tableWrapper != null) ? tableWrapper.HeadSpot : new Vector3(0.502f, 0.8635f, 0f);
            }

            if (profile == null) profile = AIDifficultyProfile.NormalProfile;

            float y = tableWrapper.ClothSurfaceY + PathClearanceChecker.BALL_RADIUS;
            Vector3 defaultSpot = headStringOnly ? tableWrapper.HeadSpot : new Vector3(0f, y, 0f);
            Vector3 bestPosition = defaultSpot;
            float bestUtility = -999f;
            bool foundTargetOrientedPlacement = false;

            List<int> legalTargets = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);

            // =========================================================================
            // CHIẾN THUẬT 1: ĐẶT BI THẲNG HÀNG SAU LƯNG GHOST BALL (STRAIGHT-IN SHOT)
            // =========================================================================
            // Các cự ly vàng từ bi cái tới ghost ball (24cm, 32cm, 42cm)
            float[] testDistances = new float[] { 0.24f, 0.32f, 0.42f };
            // Các độ lệch góc: 0 độ (thẳng tắp), ±6 độ (lệch nhẹ né vật cản hoặc mượn góc điều bi)
            float[] testAngleOffsets = new float[] { 0f, -6f, 6f, -12f, 12f };

            for (int t = 0; t < legalTargets.Count; t++)
            {
                int ballNum = legalTargets[t];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null || ball.IsPocketed || !ball.gameObject.activeInHierarchy) continue;

                Vector3 targetPos = ball.transform.position;

                for (int p = 0; p < pockets.Length; p++)
                {
                    Vector3 pocketPos = pockets[p];
                    Vector3 toPocket = pocketPos - targetPos;
                    toPocket.y = 0f;
                    float distToPocket = toPocket.magnitude;
                    if (distToPocket < 0.05f) continue;

                    Vector3 targetDir = toPocket.normalized;

                    // Kiểm tra góc vào miệng lỗ hợp lệ
                    if (!TableStateAnalyzer.IsPocketAngleAcceptable(p, targetDir)) continue;

                    // Kiểm tra hành lang từ bi mục tiêu tới lỗ có hoàn toàn thông thoáng không
                    int[] ignoredTarget = new int[] { ballNum };
                    float targetClearance = PathClearanceChecker.CalculateClearanceScore(targetPos, pocketPos, ignoredTarget, ballManager);
                    if (targetClearance <= 0f) continue;

                    // Vị trí Ghost Ball của cú ăn này
                    Vector3 ghostPos = targetPos - targetDir * (2f * PathClearanceChecker.BALL_RADIUS);

                    // Thử các cự ly và góc đặt bi cái
                    for (int d = 0; d < testDistances.Length; d++)
                    {
                        float dist = testDistances[d];

                        for (int a = 0; a < testAngleOffsets.Length; a++)
                        {
                            float angleOffset = testAngleOffsets[a];
                            Vector3 placementDir = Quaternion.Euler(0f, angleOffset, 0f) * (-targetDir);
                            Vector3 candidatePos = ghostPos + placementDir * dist;
                            candidatePos.y = y;

                            // 1. Kiểm tra vị trí đặt có nằm trọn trong mặt bàn không
                            if (!tableWrapper.IsInsidePlayArea(candidatePos, 0.038f)) continue;

                            // 2. Kiểm tra quy tắc Head String (nếu foul on break)
                            if (headStringOnly && !tableWrapper.IsBehindHeadString(candidatePos)) continue;

                            // 3. Kiểm tra không đè lên bất kỳ quả bi nào khác
                            if (IsOverlappingAnyBall(candidatePos, ballManager)) continue;

                            // 4. Kiểm tra không rơi quá sát lỗ
                            if (IsTooCloseToAnyPocket(candidatePos, tableWrapper)) continue;

                            // 5. Kiểm tra hành lang từ vị trí đặt bi cái tới Ghost Ball có bị bi khác chắn không
                            int[] ignoredCue = new int[] { 0, ballNum };
                            float cueClearance = PathClearanceChecker.CalculateClearanceScore(candidatePos, ghostPos, ignoredCue, ballManager);
                            if (cueClearance <= 0f) continue;

                            // 6. Đánh giá chất lượng cú đánh từ vị trí đặt này
                            var shotCandidate = ShotCandidateGenerator.EvaluatePotPair(
                                candidatePos, 
                                targetPos, 
                                pocketPos, 
                                ballNum, 
                                p, 
                                ballManager, 
                                tableWrapper, 
                                65f, 
                                profile, 
                                state
                            );

                            if (shotCandidate == null) continue;

                            float score = UtilityEvaluator.EvaluateCandidate(shotCandidate, profile);

                            // Điểm thưởng cho cú đánh gần và thẳng (Straight-in Bonus)
                            float straightBonus = Mathf.Clamp01(1f - (shotCandidate.cutAngleDegrees / 20f)) * 0.40f;
                            float distanceBonus = Mathf.Clamp01(1f - (dist / 0.8f)) * 0.30f;
                            score += straightBonus + distanceBonus;

                            if (score > bestUtility)
                            {
                                bestUtility = score;
                                bestPosition = candidatePos;
                                foundTargetOrientedPlacement = true;
                            }
                        }
                    }
                }
            }

            // =========================================================================
            // CHIẾN THUẬT 2: FALLBACK KHI KHÔNG CÓ ĐƯỜNG ĐẶT THẲNG (BÀN QUÁ KẸT BI)
            // =========================================================================
            if (!foundTargetOrientedPlacement)
            {
                float margin = 0.05f;
                float minX = headStringOnly ? (tableWrapper.HeadStringX + margin) : (-tableWrapper.PlayableHalfLength + margin);
                float maxX = tableWrapper.PlayableHalfLength - margin;
                float minZ = -tableWrapper.PlayableHalfWidth + margin;
                float maxZ = tableWrapper.PlayableHalfWidth - margin;

                int xSteps = 9;
                int zSteps = 7;
                float xStepSize = (maxX - minX) / Mathf.Max(1, xSteps - 1);
                float zStepSize = (maxZ - minZ) / Mathf.Max(1, zSteps - 1);

                for (int xi = 0; xi < xSteps; xi++)
                {
                    float x = minX + xi * xStepSize;
                    for (int zi = 0; zi < zSteps; zi++)
                    {
                        float z = minZ + zi * zStepSize;
                        Vector3 samplePos = new Vector3(x, y, z);

                        if (IsOverlappingAnyBall(samplePos, ballManager)) continue;
                        if (IsTooCloseToAnyPocket(samplePos, tableWrapper)) continue;

                        var potCandidates = ShotCandidateGenerator.GenerateAllPotCandidates(samplePos, state, ballManager, tableWrapper, profile);
                        if (potCandidates.Count == 0) continue;

                        for (int c = 0; c < potCandidates.Count; c++)
                        {
                            float score = UtilityEvaluator.EvaluateCandidate(potCandidates[c], profile);
                            if (score > bestUtility)
                            {
                                bestUtility = score;
                                bestPosition = samplePos;
                            }
                        }
                    }
                }
            }

            // Với Easy, thêm chút sai lệch rất nhẹ (1-2cm) để tạo cảm giác tự nhiên của người chơi nghiệp dư
            if (profile.difficulty == AIDifficulty.Easy)
            {
                float jitterX = UnityEngine.Random.Range(-0.02f, 0.02f);
                float jitterZ = UnityEngine.Random.Range(-0.02f, 0.02f);
                Vector3 jittered = new Vector3(bestPosition.x + jitterX, bestPosition.y, bestPosition.z + jitterZ);

                if (tableWrapper.IsInsidePlayArea(jittered, 0.038f) && (!headStringOnly || tableWrapper.IsBehindHeadString(jittered)))
                {
                    if (!IsOverlappingAnyBall(jittered, ballManager) && !IsTooCloseToAnyPocket(jittered, tableWrapper))
                    {
                        bestPosition = jittered;
                    }
                }
            }

            return bestPosition;
        }

        private static bool IsOverlappingAnyBall(Vector3 pos, BallManager ballManager)
        {
            if (ballManager == null) return false;
            float minSafeDist = 2f * PathClearanceChecker.BALL_RADIUS + 0.008f; // 0.065m
            var allBalls = ballManager.AllBalls;

            for (int i = 1; i < allBalls.Length; i++)
            {
                var b = allBalls[i];
                if (b == null || !b.gameObject.activeInHierarchy || b.IsPocketed) continue;

                Vector3 bPos = b.transform.position;
                float distSqr = (bPos.x - pos.x) * (bPos.x - pos.x) + (bPos.z - pos.z) * (bPos.z - pos.z);
                if (distSqr < minSafeDist * minSafeDist)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTooCloseToAnyPocket(Vector3 pos, TableWrapper table)
        {
            if (table == null) return false;
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(table);
            float minPocketDistSqr = 0.09f * 0.09f;

            for (int i = 0; i < pockets.Length; i++)
            {
                Vector3 p = pockets[i];
                float distSqr = (p.x - pos.x) * (p.x - pos.x) + (p.z - pos.z) * (p.z - pos.z);
                if (distSqr < minPocketDistSqr)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
