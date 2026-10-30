using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    /// <summary>
    /// Thuật toán dự đoán vị trí bi cái sau cú đánh (Position Play) dành riêng cho cấp độ Khó (Hard).
    /// Ước tính điểm dừng của bi cái theo đường tiếp tuyến (Tangent Line) và phản xạ băng (nếu có),
    /// sau đó kiểm tra xem tại điểm dừng đó AI có góc ăn bi tiếp theo (Run-out continuation) thuận lợi hay không.
    /// </summary>
    public static class PositionPlayPredictor
    {
        public static float EvaluatePositionQuality(
            ShotCandidate candidate, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper)
        {
            if (candidate == null || state == null || ballManager == null || tableWrapper == null)
                return 0.5f;

            Vector3 ghostPos = candidate.ghostBallPosition;
            Vector3 aimDir = candidate.aimDirection;
            // Hướng truyền xung lực từ bi cái sang bi mục tiêu (luôn chuẩn xác cho cả Direct, Combo, Bank Shot)
            Vector3 targetDir = (candidate.targetBallPosition - candidate.ghostBallPosition);
            targetDir.y = 0f;
            if (targetDir.sqrMagnitude < 0.001f)
            {
                targetDir = (candidate.targetPocketPosition - candidate.targetBallPosition);
                targetDir.y = 0f;
            }
            if (targetDir.sqrMagnitude < 0.001f) return 0.5f;
            targetDir.Normalize();

            // 1. Tính hướng văng tiếp tuyến (90 độ so với hướng bi mục tiêu)
            Vector3 tangentDir = Vector3.Cross(Vector3.up, targetDir).normalized;
            if (Vector3.Dot(tangentDir, aimDir) < 0f)
            {
                tangentDir = -tangentDir;
            }

            // 2. Dự đoán vị trí dừng của bi cái có tính đến xoáy trô/cù-lê và nảy băng 1-băng thực tế
            Vector3 stopPos = PredictCueStopPosition(ghostPos, aimDir, targetDir, candidate.cutAngleDegrees, candidate.estimatedPower01, candidate.estimatedSpin, tableWrapper);
            candidate.predictedCueStopPos = stopPos;

            // 3. Kiểm tra nguy cơ rơi lỗ của bi cái tại điểm dừng
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);
            for (int p = 0; p < pockets.Length; p++)
            {
                float distToPocket = Vector2.Distance(new Vector2(stopPos.x, stopPos.z), new Vector2(pockets[p].x, pockets[p].z));
                if (distToPocket < 0.12f)
                {
                    return 0.0f; // Nguy cơ cực cao bi cái rớt lỗ -> Điểm 0!
                }
            }

            // 4. Đánh giá thế bi tiếp theo từ điểm dừng stopPos
            List<int> legalTargets = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            // Loại bỏ bi hiện tại đang được đánh
            legalTargets.Remove(candidate.targetBallNumber);

            if (legalTargets.Count == 0)
            {
                // Đây đã là bi cuối cùng trong nhóm -> Điểm vị trí phụ thuộc vào việc có góc ăn bi 8 không
                var ball8 = ballManager.GetBall(8);
                if (ball8 != null && !ball8.IsPocketed && ball8.gameObject.activeInHierarchy)
                {
                    float bestScore8 = EvaluateSingleBallPotPotential(stopPos, ball8.transform.position, pockets, 8, ballManager);
                    return Mathf.Clamp01(bestScore8);
                }
                return 0.8f;
            }

            // Tìm quả bi tiếp theo có góc ăn tốt nhất từ stopPos
            float highestNextPotScore = 0.1f;
            for (int i = 0; i < legalTargets.Count; i++)
            {
                var nextBall = ballManager.GetBall(legalTargets[i]);
                if (nextBall == null) continue;

                float potScore = EvaluateSingleBallPotPotential(stopPos, nextBall.transform.position, pockets, legalTargets[i], ballManager);
                if (potScore > highestNextPotScore)
                {
                    highestNextPotScore = potScore;
                }
            }

            return Mathf.Clamp01(highestNextPotScore);
        }

        private static float EvaluateSingleBallPotPotential(
            Vector3 fromCuePos, 
            Vector3 targetPos, 
            Vector3[] pockets, 
            int ballNum, 
            BallManager ballManager)
        {
            float bestPot = 0f;

            for (int p = 0; p < pockets.Length; p++)
            {
                Vector3 toPocket = pockets[p] - targetPos;
                toPocket.y = 0f;
                float distPocket = toPocket.magnitude;
                if (distPocket < 0.05f) continue;
                Vector3 targetDir = toPocket.normalized;
                if (!TableStateAnalyzer.IsPocketAngleAcceptable(p, targetDir)) continue;

                Vector3 ghostPos = targetPos - targetDir * (2f * PathClearanceChecker.BALL_RADIUS);
                Vector3 toGhost = ghostPos - fromCuePos;
                toGhost.y = 0f;
                float distCue = toGhost.magnitude;
                if (distCue < 0.05f) continue;
                Vector3 aimDir = toGhost.normalized;

                float dot = Vector3.Dot(aimDir, targetDir);
                if (dot < 0.35f) continue; // Góc cắt quá khó (> 70 độ)

                // Kiểm tra sơ bộ độ thông thoáng
                int[] ignored = new int[] { 0, ballNum };
                float c1 = PathClearanceChecker.CalculateClearanceScore(fromCuePos, ghostPos, ignored, ballManager);
                if (c1 <= 0.1f) continue;

                float c2 = PathClearanceChecker.CalculateClearanceScore(targetPos, pockets[p], new int[] { ballNum }, ballManager);
                if (c2 <= 0.1f) continue;

                float angleScore = dot;
                float nextPocketDistScore = Mathf.Clamp01(1f - (distPocket / 1.35f));
                if (distPocket < 0.25f) nextPocketDistScore = 1.0f; // Thế bi dọn bàn cực dễ!

                float nextCueDistScore = Mathf.Clamp01(1f - (distCue / 1.8f));
                float score = angleScore * 0.35f + nextPocketDistScore * 0.35f + c1 * 0.20f + nextCueDistScore * 0.10f;
                if (distPocket < 0.25f && c1 > 0.8f && c2 > 0.8f) score = Mathf.Max(score, 0.95f);

                if (score > bestPot) bestPot = score;
            }

            return bestPot;
        }

        /// <summary>
        /// Dự đoán vị trí dừng của bi cái có tính đến xoáy trô/cù-lê và mô hình phản xạ nảy băng 1-băng thực tế.
        /// </summary>
        public static Vector3 PredictCueStopPosition(
            Vector3 ghostPos,
            Vector3 aimDir,
            Vector3 targetDir,
            float cutAngleDeg,
            float power01,
            Vector2 spin,
            TableWrapper tableWrapper)
        {
            Vector3 tangentDir = Vector3.Cross(Vector3.up, targetDir).normalized;
            if (Vector3.Dot(tangentDir, aimDir) < 0f)
            {
                tangentDir = -tangentDir;
            }

            Vector3 departureDir;
            float rollDistance;

            if (cutAngleDeg < 12f)
            {
                // Cú đánh gần như thẳng (< 12 độ):
                if (spin.y < -0.2f)
                {
                    // Trô / Hãm bi (Draw/Backspin): bi cái giật lùi về sau theo hướng -aimDir
                    departureDir = -aimDir;
                    rollDistance = Mathf.Clamp(power01 * (1.1f * Mathf.Abs(spin.y)), 0.08f, 1.25f);
                }
                else if (spin.y > 0.2f)
                {
                    // Cù-lê (Follow/Topspin): bi cái trôi tới phía trước theo hướng aimDir
                    departureDir = aimDir;
                    rollDistance = Mathf.Clamp(power01 * (1.3f * (1f + spin.y)), 0.20f, 1.60f);
                }
                else
                {
                    // Dừng tự nhiên / Stun: bi cái chỉ lăn rất nhẹ theo hướng ngắm rồi dừng
                    departureDir = aimDir;
                    rollDistance = Mathf.Clamp(power01 * 0.35f, 0.05f, 0.25f);
                }
            }
            else
            {
                // Cú đánh có góc cắt:
                float sinCut = Mathf.Sin(cutAngleDeg * Mathf.Deg2Rad);
                float baseDistance = Mathf.Clamp(power01 * 1.55f * Mathf.Max(0.22f, sinCut), 0.15f, 1.70f);

                if (spin.y > 0.2f)
                {
                    // Cù-lê khi có góc cắt: Xoáy lăn tới uốn cong bi cái về phía trước so với tiếp tuyến
                    float followWeight = Mathf.Clamp01(spin.y * 0.45f);
                    departureDir = (tangentDir * (1f - followWeight) + aimDir * followWeight).normalized;
                    rollDistance = baseDistance * (1f + spin.y * 0.30f);
                }
                else if (spin.y < -0.2f)
                {
                    // Trô khi có góc cắt: Xoáy giật lùi kéo bi cái về phía sau so với tiếp tuyến
                    float drawWeight = Mathf.Clamp01(Mathf.Abs(spin.y) * 0.50f);
                    departureDir = (tangentDir * (1f - drawWeight) - targetDir * drawWeight).normalized;
                    rollDistance = baseDistance * (1f + Mathf.Abs(spin.y) * 0.20f);
                }
                else
                {
                    // Lăn tự nhiên trên đường tiếp tuyến 90 độ (Stun tangent line)
                    departureDir = tangentDir;
                    rollDistance = baseDistance;
                }
            }

            departureDir.y = 0f;
            if (departureDir.sqrMagnitude < 0.001f) departureDir = tangentDir;
            departureDir.Normalize();

            // Mô phỏng va chạm và nảy băng thực tế (1-Cushion Rebound)
            float r = PathClearanceChecker.BALL_RADIUS;
            float minX = TableWrapper.FootCushionX + r + 0.015f;
            float maxX = TableWrapper.HeadCushionX - r - 0.015f;
            float minZ = -TableWrapper.CushionHalfWidth + r + 0.015f;
            float maxZ = TableWrapper.CushionHalfWidth - r - 0.015f;

            Vector3 p0 = ghostPos;
            p0.y = 0f;

            float tHit = float.MaxValue;
            Vector3 cushionNormal = Vector3.zero;

            // Kiểm tra va chạm Foot Cushion (-X)
            if (departureDir.x < -0.001f)
            {
                float t = (minX - p0.x) / departureDir.x;
                if (t > 0.001f && t < tHit)
                {
                    float hitZ = p0.z + departureDir.z * t;
                    if (hitZ >= minZ - 0.05f && hitZ <= maxZ + 0.05f)
                    {
                        tHit = t;
                        cushionNormal = Vector3.right; // +X
                    }
                }
            }
            // Kiểm tra va chạm Head Cushion (+X)
            if (departureDir.x > 0.001f)
            {
                float t = (maxX - p0.x) / departureDir.x;
                if (t > 0.001f && t < tHit)
                {
                    float hitZ = p0.z + departureDir.z * t;
                    if (hitZ >= minZ - 0.05f && hitZ <= maxZ + 0.05f)
                    {
                        tHit = t;
                        cushionNormal = Vector3.left; // -X
                    }
                }
            }
            // Kiểm tra va chạm Side Cushion Neg (-Z)
            if (departureDir.z < -0.001f)
            {
                float t = (minZ - p0.z) / departureDir.z;
                if (t > 0.001f && t < tHit)
                {
                    float hitX = p0.x + departureDir.x * t;
                    if (hitX >= minX - 0.05f && hitX <= maxX + 0.05f)
                    {
                        tHit = t;
                        cushionNormal = Vector3.forward; // +Z
                    }
                }
            }
            // Kiểm tra va chạm Side Cushion Pos (+Z)
            if (departureDir.z > 0.001f)
            {
                float t = (maxZ - p0.z) / departureDir.z;
                if (t > 0.001f && t < tHit)
                {
                    float hitX = p0.x + departureDir.x * t;
                    if (hitX >= minX - 0.05f && hitX <= maxX + 0.05f)
                    {
                        tHit = t;
                        cushionNormal = Vector3.back; // -Z
                    }
                }
            }

            Vector3 finalPos;
            if (tHit < rollDistance)
            {
                // Bi cái chạm băng: tính điểm chạm và vector phản xạ nảy ra
                Vector3 hitPoint = p0 + departureDir * tHit;
                Vector3 reboundDir = (departureDir - 2f * Vector3.Dot(departureDir, cushionNormal) * cushionNormal).normalized;
                float remainingDist = (rollDistance - tHit) * 0.58f; // Hệ số tiêu hao lực khi đập băng
                finalPos = hitPoint + reboundDir * remainingDist;
            }
            else
            {
                // Không chạm băng, bi cái dừng tự nhiên trên mặt nỉ
                finalPos = p0 + departureDir * rollDistance;
            }

            // Kẹp an toàn cuối cùng trong ranh giới bàn
            finalPos.x = Mathf.Clamp(finalPos.x, minX, maxX);
            finalPos.z = Mathf.Clamp(finalPos.z, minZ, maxZ);
            finalPos.y = tableWrapper.ClothSurfaceY + r;

            return finalPos;
        }
    }
}
