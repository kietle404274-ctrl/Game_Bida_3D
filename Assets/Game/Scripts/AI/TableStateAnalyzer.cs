using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public static class TableStateAnalyzer
    {
        public static List<int> GetLegalTargetBalls(MatchState state, BallManager ballManager)
        {
            List<int> targets = new List<int>();
            if (state == null || ballManager == null) return targets;

            PlayerGroup shooterGroup = state.CurrentShooterGroup;

            if (state.tableOpen || shooterGroup == PlayerGroup.Open)
            {
                // Bàn mở: được chọn bất kỳ bi mục tiêu nào từ 1 đến 15 (trừ bi 8)
                for (int i = 1; i <= 15; i++)
                {
                    if (i == 8) continue;
                    var b = ballManager.GetBall(i);
                    if (b != null && !b.IsPocketed && b.gameObject.activeInHierarchy)
                    {
                        targets.Add(i);
                    }
                }
            }
            else
            {
                int start = (shooterGroup == PlayerGroup.Solids) ? 1 : 9;
                int end = start + 6;

                for (int i = start; i <= end; i++)
                {
                    var b = ballManager.GetBall(i);
                    if (b != null && !b.IsPocketed && b.gameObject.activeInHierarchy)
                    {
                        targets.Add(i);
                    }
                }

                // Nếu đã dọn sạch toàn bộ 7 bi trong nhóm -> Bi 8 là mục tiêu hợp lệ duy nhất!
                if (targets.Count == 0)
                {
                    var b8 = ballManager.GetBall(8);
                    if (b8 != null && !b8.IsPocketed && b8.gameObject.activeInHierarchy)
                    {
                        targets.Add(8);
                    }
                }
            }

            return targets;
        }

        /// <summary>
        /// Trả về tọa độ chuẩn xác của tâm cửa miệng lỗ (Pocket Mouth Center) trên mặt nỉ bàn bida.
        /// Nằm chính xác tại trung điểm của 2 mũi băng mép lỗ trên mặt nỉ:
        /// - Lỗ góc Foot (0, 1): X = -1.040m, Z = ±0.470m (nằm chính giữa mũi băng ngắn và mũi băng dài)
        /// - Lỗ góc Head (4, 5): X = +1.030m, Z = ±0.470m
        /// - Lỗ giữa Side (2, 3): X = 0.000m, Z = ±0.495m (ngay trên mép nỉ giữa 2 má lỗ dài)
        /// Khi bi đi thẳng vào tâm cửa miệng lỗ, bi sẽ lọt qua khe hở giữa 2 mũi băng với biên độ an toàn
        /// tối đa và không bao giờ bị va vào má vát cao su.
        /// </summary>
        public static Vector3[] GetPocketPositions(TableWrapper tableWrapper)
        {
            Vector3[] pockets = new Vector3[6];
            float y = (tableWrapper != null) ? tableWrapper.ClothSurfaceY : 0.835f;

            // Tọa độ tâm cửa miệng lỗ (Pocket Mouth Center) chính xác trên mặt nỉ:
            pockets[0] = new Vector3(-1.040f, y, 0.470f);  // Pocket 0: Foot Left (-X, +Z)
            pockets[1] = new Vector3(-1.040f, y, -0.470f); // Pocket 1: Foot Right (-X, -Z)
            pockets[2] = new Vector3(0.000f, y, 0.495f);   // Pocket 2: Mid Left (+Z)
            pockets[3] = new Vector3(0.000f, y, -0.495f);  // Pocket 3: Mid Right (-Z)
            pockets[4] = new Vector3(1.030f, y, 0.470f);   // Pocket 4: Head Left (+X, +Z)
            pockets[5] = new Vector3(1.030f, y, -0.470f);  // Pocket 5: Head Right (+X, -Z)

            return pockets;
        }

        /// <summary>
        /// Vector hướng vào trong lòng lỗ (Inward Pocket Direction).
        /// </summary>
        public static Vector3 GetPocketInwardNormal(int pocketId)
        {
            switch (pocketId)
            {
                case 0: return new Vector3(-0.7071f, 0f, 0.7071f);  // Foot Left (-X, +Z)
                case 1: return new Vector3(-0.7071f, 0f, -0.7071f); // Foot Right (-X, -Z)
                case 2: return new Vector3(0f, 0f, 1f);             // Mid Left (+Z)
                case 3: return new Vector3(0f, 0f, -1f);            // Mid Right (-Z)
                case 4: return new Vector3(0.7071f, 0f, 0.7071f);   // Head Left (+X, +Z)
                case 5: return new Vector3(0.7071f, 0f, -0.7071f);  // Head Right (+X, -Z)
                default: return Vector3.forward;
            }
        }

        /// <summary>
        /// Kiểm tra góc vào miệng lỗ (Pocket Acceptance Angle):
        /// Ngăn AI cố tình bắn vào mép/má lỗ ở những góc cắt bi không thể lọt.
        /// </summary>
        public static bool IsPocketAngleAcceptable(int pocketId, Vector3 targetDir)
        {
            Vector3 inward = GetPocketInwardNormal(pocketId);
            float dot = Vector3.Dot(targetDir, inward);

            if (pocketId == 2 || pocketId == 3)
            {
                // Lỗ giữa (Side Pocket): Chỉ chấp nhận góc vào <= 48 độ so với trục vuông góc thành bàn (dot >= 0.67)
                // Nếu góc quá chéo, má vát của 2 mũi băng dài sẽ chặn bi lại.
                return dot >= 0.67f;
            }
            else
            {
                // Lỗ góc (Corner Pocket): Chỉ chấp nhận góc vào <= 65 độ so với trục phân giác (dot >= 0.42)
                return dot >= 0.42f;
            }
        }
    }
}
