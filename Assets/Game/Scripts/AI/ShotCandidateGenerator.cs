using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public static class ShotCandidateGenerator
    {
        public const float BALL_RADIUS = 0.02855f;
        public const float BALL_DIAMETER = 2f * BALL_RADIUS; // 0.0571m

        public static List<ShotCandidate> GenerateAllPotCandidates(
            Vector3 cueBallPos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            List<ShotCandidate> candidates = new List<ShotCandidate>();
            if (state == null || ballManager == null) return candidates;

            List<int> targetBalls = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);

            float maxAngle = (profile != null) ? profile.maxCutAngleDegrees : 65f;

            for (int t = 0; t < targetBalls.Count; t++)
            {
                int ballNum = targetBalls[t];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null) continue;

                Vector3 targetPos = ball.transform.position;

                for (int p = 0; p < pockets.Length; p++)
                {
                    Vector3 pocketPos = pockets[p];
                    var candidate = EvaluatePotPair(cueBallPos, targetPos, pocketPos, ballNum, p, ballManager, tableWrapper, maxAngle);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);
                    }
                }
            }

            return candidates;
        }

        public static ShotCandidate EvaluatePotPair(
            Vector3 cuePos, 
            Vector3 targetPos, 
            Vector3 pocketPos, 
            int targetBallNum, 
            int pocketId, 
            BallManager ballManager, 
            TableWrapper table, 
            float maxCutAngle)
        {
            Vector3 targetToPocket = new Vector3(pocketPos.x - targetPos.x, 0f, pocketPos.z - targetPos.z);
            float distTargetToPocket = targetToPocket.magnitude;
            if (distTargetToPocket < 0.01f) return null;

            Vector3 targetDir = targetToPocket.normalized;

            // 1. Tính toán vị trí Ghost-ball: lùi lại đúng 1 đường kính bi theo hướng ngược với lỗ
            Vector3 ghostPos = targetPos - targetDir * BALL_DIAMETER;

            // Kiểm tra ghostPos có nằm trong mặt bàn không
            if (table != null && !table.IsInsidePlayArea(ghostPos, -0.02f))
            {
                return null;
            }

            // 2. Hướng ngắm từ Bi Cái tới Ghost Ball
            Vector3 cueToGhost = new Vector3(ghostPos.x - cuePos.x, 0f, ghostPos.z - cuePos.z);
            float distCueToGhost = cueToGhost.magnitude;
            if (distCueToGhost < 0.01f) return null;

            Vector3 aimDir = cueToGhost.normalized;

            // 3. Tính góc cắt (Cut Angle)
            float dot = Vector3.Dot(aimDir, targetDir);
            if (dot <= 0.05f) // Góc quá lớn (> 87 độ) hoặc đập ngược lại
            {
                return null;
            }

            float cutAngleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            if (cutAngleDeg > maxCutAngle)
            {
                return null;
            }

            // 4. Kiểm tra độ thông thoáng (Clearance) của 2 hành lang:
            // Hành lang 1: Cue -> Ghost
            int[] ignoredCuePath = new int[] { 0, targetBallNum };
            float cueClearance = PathClearanceChecker.CalculateClearanceScore(cuePos, ghostPos, ignoredCuePath, ballManager);
            if (cueClearance <= 0f)
            {
                return null; // Bị bi khác chặn đường ngắm
            }

            // Hành lang 2: Target -> Pocket
            int[] ignoredTargetPath = new int[] { targetBallNum };
            float targetClearance = PathClearanceChecker.CalculateClearanceScore(targetPos, pocketPos, ignoredTargetPath, ballManager);
            if (targetClearance <= 0f)
            {
                return null; // Bị bi khác chặn đường vào lỗ
            }

            // 5. Ước tính lực đánh (Power 0..1)
            float angleLossCompensation = 1f / Mathf.Max(0.25f, dot);
            float estimatedPower = Mathf.Clamp(0.14f + (distCueToGhost * 0.16f + distTargetToPocket * 0.22f) * angleLossCompensation, 0.12f, 0.95f);

            // 6. Tính toán các thành phần điểm hình học
            float angleScore = Mathf.Clamp01(1f - (cutAngleDeg / 90f));
            float distanceScore = Mathf.Clamp01(1f - ((distCueToGhost + distTargetToPocket) / 2.6f));
            float clearanceScore = (cueClearance * 0.5f) + (targetClearance * 0.5f);

            // Nguy cơ rơi bi cái (Scratch Risk): đường văng của bi cái sau va chạm
            // Bi cái văng theo góc lệch 90 độ so với hướng targetDir
            Vector3 cueTangentDir = Vector3.Cross(Vector3.up, targetDir);
            if (Vector3.Dot(cueTangentDir, aimDir) < 0f)
            {
                cueTangentDir = -cueTangentDir;
            }

            float foulRisk = EvaluateScratchRisk(ghostPos, cueTangentDir, table);

            // Khả thi ăn bi (Pot Feasibility)
            float potFeasibility = Mathf.Clamp01(angleScore * 0.5f + clearanceScore * 0.35f + distanceScore * 0.15f);

            ShotCandidate candidate = new ShotCandidate
            {
                targetBallNumber = targetBallNum,
                targetPocketId = pocketId,
                targetPocketPosition = pocketPos,
                ghostBallPosition = ghostPos,
                targetBallPosition = targetPos,
                cueBallPosition = cuePos,
                aimDirection = aimDir,
                cutAngleDegrees = cutAngleDeg,
                cueToGhostDistance = distCueToGhost,
                targetToPocketDistance = distTargetToPocket,
                estimatedPower01 = estimatedPower,
                estimatedSpin = Vector2.zero,
                isSafety = false,
                isLegalBreak = false,
                potFeasibility = potFeasibility,
                angleScore = angleScore,
                distanceScore = distanceScore,
                clearanceScore = clearanceScore,
                cueBallPositionScore = 0.5f,
                safetyScore = 0f,
                foulRisk = foulRisk
            };

            return candidate;
        }

        private static float EvaluateScratchRisk(Vector3 fromPos, Vector3 tangentDir, TableWrapper table)
        {
            if (table == null) return 0f;
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(table);

            float highestRisk = 0f;
            for (int i = 0; i < pockets.Length; i++)
            {
                Vector3 toPocket = pockets[i] - fromPos;
                toPocket.y = 0f;
                float dist = toPocket.magnitude;
                if (dist > 0.1f)
                {
                    float alignment = Vector3.Dot(tangentDir, toPocket.normalized);
                    if (alignment > 0.85f && dist < 0.6f) // Hướng thẳng vào lỗ gần
                    {
                        float risk = alignment * (1f - (dist / 0.6f));
                        if (risk > highestRisk) highestRisk = risk;
                    }
                }
            }

            return Mathf.Clamp01(highestRisk);
        }
    }
}
