using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
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
                return (tableWrapper != null) ? tableWrapper.HeadSpot : new Vector3(0f, 0.8635f, -0.56f);
            }

            if (profile == null) profile = AIDifficultyProfile.NormalProfile;

            float margin = 0.05f;
            float minX = headStringOnly ? (tableWrapper.HeadStringX + margin) : (-tableWrapper.PlayableHalfLength + margin);
            float maxX = tableWrapper.PlayableHalfLength - margin;
            float minZ = -tableWrapper.PlayableHalfWidth + margin;
            float maxZ = tableWrapper.PlayableHalfWidth - margin;
            float y = tableWrapper.ClothSurfaceY + PathClearanceChecker.BALL_RADIUS;

            int sampleCount = profile.ballInHandSampleCount;
            int xSteps = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(sampleCount * 0.6f)));
            int zSteps = Mathf.Max(3, Mathf.RoundToInt(sampleCount / (float)xSteps));

            Vector3 bestPosition = headStringOnly ? tableWrapper.HeadSpot : new Vector3(0f, y, 0f);
            float bestUtility = -999f;

            float xStepSize = (maxX - minX) / Mathf.Max(1, xSteps - 1);
            float zStepSize = (maxZ - minZ) / Mathf.Max(1, zSteps - 1);

            for (int xi = 0; xi < xSteps; xi++)
            {
                float x = minX + xi * xStepSize;
                for (int zi = 0; zi < zSteps; zi++)
                {
                    float z = minZ + zi * zStepSize;
                    Vector3 samplePos = new Vector3(x, y, z);

                    // 1. Kiểm tra không đè lên bi khác
                    if (IsOverlappingAnyBall(samplePos, ballManager))
                        continue;

                    // 2. Kiểm tra không rơi quá gần lỗ
                    if (IsTooCloseToAnyPocket(samplePos, tableWrapper))
                        continue;

                    // 3. Phân tích hình học nhanh các cú pot từ vị trí thử nghiệm này
                    var potCandidates = ShotCandidateGenerator.GenerateAllPotCandidates(samplePos, state, ballManager, tableWrapper, profile);
                    if (potCandidates.Count == 0) continue;

                    // Đánh giá max utility tại vị trí này
                    float maxPotScore = -999f;
                    for (int c = 0; c < potCandidates.Count; c++)
                    {
                        float score = UtilityEvaluator.EvaluateCandidate(potCandidates[c], profile);
                        if (score > maxPotScore)
                        {
                            maxPotScore = score;
                        }
                    }

                    if (maxPotScore > bestUtility)
                    {
                        bestUtility = maxPotScore;
                        bestPosition = samplePos;
                    }
                }
            }

            // Với Easy, thêm một chút sai số ngẫu nhiên nhẹ vào vị trí đặt bi
            if (profile.difficulty == AIDifficulty.Easy)
            {
                float jitterX = UnityEngine.Random.Range(-0.04f, 0.04f);
                float jitterZ = UnityEngine.Random.Range(-0.04f, 0.04f);
                Vector3 jittered = new Vector3(bestPosition.x + jitterX, bestPosition.y, bestPosition.z + jitterZ);

                if (tableWrapper.IsInsidePlayArea(jittered, margin) && (!headStringOnly || tableWrapper.IsBehindHeadString(jittered)))
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
            float minPocketDistSqr = 0.10f * 0.10f;

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
