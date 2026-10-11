using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public static class PathClearanceChecker
    {
        public const float BALL_RADIUS = 0.02855f;
        public const float MIN_CLEARANCE_RADIUS = 2f * BALL_RADIUS; // 0.0571m (hai bi chạm nhau)

        public static bool IsSegmentClear(Vector3 start, Vector3 end, float clearanceRadius, int[] ignoredBallNumbers, BallManager ballManager, out float minDistanceFound)
        {
            minDistanceFound = float.MaxValue;
            if (ballManager == null) return true;

            Vector2 a = new Vector2(start.x, start.z);
            Vector2 b = new Vector2(end.x, end.z);
            Vector2 ab = b - a;
            float abLengthSqr = ab.sqrMagnitude;

            if (abLengthSqr < 0.0001f)
            {
                return true;
            }

            HashSet<int> ignoredSet = (ignoredBallNumbers != null) ? new HashSet<int>(ignoredBallNumbers) : new HashSet<int>();

            var allBalls = ballManager.AllBalls;
            if (allBalls == null) return true;

            for (int i = 0; i < allBalls.Length; i++)
            {
                var ball = allBalls[i];
                if (ball == null || !ball.gameObject.activeInHierarchy || ball.IsPocketed)
                    continue;

                if (ignoredSet.Contains(ball.BallNumber))
                    continue;

                Vector3 bPos = ball.transform.position;
                Vector2 p = new Vector2(bPos.x, bPos.z);

                // Tính khoảng cách từ điểm P đến đoạn thẳng AB
                float t = Vector2.Dot(p - a, ab) / abLengthSqr;
                t = Mathf.Clamp01(t);
                Vector2 closestPoint = a + t * ab;

                float dist = Vector2.Distance(p, closestPoint);
                if (dist < minDistanceFound)
                {
                    minDistanceFound = dist;
                }

                // Nếu khoảng cách nhỏ hơn bán kính thông thoáng tối thiểu -> Bị chặn
                if (dist < clearanceRadius)
                {
                    return false;
                }
            }

            return true;
        }

        public static float CalculateClearanceScore(Vector3 start, Vector3 end, int[] ignoredBallNumbers, BallManager ballManager)
        {
            // Clearance Score trả về từ 0.0 (bị chặn) đến 1.0 (rất thoáng)
            float minClearance = MIN_CLEARANCE_RADIUS * 1.05f; // Có một chút biên độ an toàn
            bool clear = IsSegmentClear(start, end, minClearance, ignoredBallNumbers, ballManager, out float minDistanceFound);

            if (!clear)
            {
                return 0f;
            }

            // Khoảng cách từ minClearance (0.06m) đến 4 * ballRadius (0.12m)
            float safeMargin = MIN_CLEARANCE_RADIUS * 2.2f;
            float normalized = Mathf.InverseLerp(minClearance, safeMargin, minDistanceFound);
            return Mathf.Clamp01(normalized);
        }
    }
}
