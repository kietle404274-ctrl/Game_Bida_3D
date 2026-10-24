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

        public static Vector3[] GetPocketPositions(TableWrapper tableWrapper)
        {
            Vector3[] pockets = new Vector3[6];

            if (tableWrapper != null && tableWrapper.Pockets != null && tableWrapper.Pockets.Length >= 6)
            {
                for (int i = 0; i < 6; i++)
                {
                    var p = tableWrapper.Pockets[i];
                    if (p != null)
                    {
                        pockets[i] = p.transform.position;
                    }
                    else
                    {
                        pockets[i] = GetDefaultPocketPosition(i, tableWrapper);
                    }
                }
            }
            else
            {
                for (int i = 0; i < 6; i++)
                {
                    pockets[i] = GetDefaultPocketPosition(i, tableWrapper);
                }
            }

            return pockets;
        }

        private static Vector3 GetDefaultPocketPosition(int pocketId, TableWrapper table)
        {
            float halfW = (table != null) ? table.PlayableHalfWidth : 0.56f;
            float halfL = (table != null) ? table.PlayableHalfLength : 1.12f;
            float y = (table != null) ? table.ClothSurfaceY : 0.835f;

            switch (pocketId)
            {
                case 0: return new Vector3(-halfL, y, halfW);  // Foot Left
                case 1: return new Vector3(-halfL, y, -halfW); // Foot Right
                case 2: return new Vector3(0f, y, halfW);      // Mid Left
                case 3: return new Vector3(0f, y, -halfW);     // Mid Right
                case 4: return new Vector3(halfL, y, halfW);   // Head Left
                case 5: return new Vector3(halfL, y, -halfW);  // Head Right
                default: return new Vector3(0f, y, 0f);
            }
        }
    }
}
