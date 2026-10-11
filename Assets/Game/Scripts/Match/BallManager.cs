using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Physics;

namespace Game.Billiards.Match
{
    public class BallManager : MonoBehaviour
    {
        [Header("Configuration & References")]
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private TableWrapper tableWrapper;
        [SerializeField] private BallMovementMonitor movementMonitor;

        [Header("Balls Registry (0 = Cue, 1-15 = Object Balls)")]
        [SerializeField] private BallController[] balls = new BallController[16];

        public BallController CueBall => balls != null && balls.Length > 0 ? balls[0] : null;
        public BallController EightBall => balls != null && balls.Length > 8 ? balls[8] : null;
        public BallController[] AllBalls => balls;
        public PhysicsConfig PhysicsConfig => physicsConfig;
        public TableWrapper TableWrapper => tableWrapper;
        public BallMovementMonitor MovementMonitor => movementMonitor;

        public event Action<BallController, int> OnAnyBallPocketed;

        private void Awake()
        {
            InitializeRegistry();
        }

        public void InitializeRegistry()
        {
            if (balls == null || balls.Length < 16)
            {
                balls = new BallController[16];
            }

            // Gán BallController vào MovementMonitor và đăng ký sự kiện
            for (int i = 0; i < balls.Length; i++)
            {
                var ball = balls[i];
                if (ball != null)
                {
                    ball.SetBallNumber(i);
                    if (physicsConfig != null)
                    {
                        ball.ApplyPhysicsConfig(physicsConfig);
                    }
                    ball.OnBallPocketed -= HandleBallPocketed;
                    ball.OnBallPocketed += HandleBallPocketed;

                    if (movementMonitor != null)
                    {
                        movementMonitor.RegisterBall(ball);
                    }
                }
            }

            if (movementMonitor != null && physicsConfig != null)
            {
                movementMonitor.Initialize(physicsConfig, balls);
            }
        }

        public void RegisterBall(int number, BallController ball)
        {
            if (number >= 0 && number < balls.Length)
            {
                balls[number] = ball;
                if (ball != null)
                {
                    ball.SetBallNumber(number);
                    if (physicsConfig != null)
                    {
                        ball.ApplyPhysicsConfig(physicsConfig);
                    }
                    ball.OnBallPocketed -= HandleBallPocketed;
                    ball.OnBallPocketed += HandleBallPocketed;

                    if (movementMonitor != null)
                    {
                        movementMonitor.RegisterBall(ball);
                    }
                }
            }
        }

        public BallController GetBall(int ballNumber)
        {
            if (ballNumber >= 0 && ballNumber < balls.Length)
            {
                return balls[ballNumber];
            }
            return null;
        }

        public List<BallController> GetActiveObjectBalls()
        {
            List<BallController> active = new List<BallController>();
            for (int i = 1; i < balls.Length; i++)
            {
                if (balls[i] != null && !balls[i].IsPocketed && balls[i].gameObject.activeInHierarchy)
                {
                    active.Add(balls[i]);
                }
            }
            return active;
        }

        public List<BallController> GetPocketedBalls()
        {
            List<BallController> pocketed = new List<BallController>();
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null && balls[i].IsPocketed)
                {
                    pocketed.Add(balls[i]);
                }
            }
            return pocketed;
        }

        /// <summary>
        /// Xếp dàn bi 15 quả đúng quy cách WPA 8-Ball:
        /// - Đỉnh dàn bi tại Foot Spot
        /// - Bi 8 đen nằm ở tâm dàn bi (hàng thứ 3, quả chính giữa)
        /// - Hai quả ở hai góc đáy (hàng 5): một quả trơn (solid) và một quả sọc (stripe)
        /// - Bi cái tại Head Spot
        /// </summary>
        public void ResetRack()
        {
            if (tableWrapper == null || physicsConfig == null) return;

            float radius = physicsConfig.BallRadius;
            float diameter = radius * 2f;
            float rowSpacing = diameter * 0.8660254f; // d * sqrt(3)/2

            Vector3 footSpot = tableWrapper.FootSpot;
            Vector3 headSpot = tableWrapper.HeadSpot;

            // 1. Đặt bi cái
            if (balls[0] != null)
            {
                balls[0].ResetTo(headSpot, Quaternion.identity);
            }

            // 2. Chuẩn bị danh sách số bi cho dàn 15 quả theo WPA
            // Thứ tự các vị trí trong tam giác:
            // Hàng 0 (1 bi): Apex
            // Hàng 1 (2 bi):
            // Hàng 2 (3 bi): Bi giữa (index 4) là bi 8
            // Hàng 3 (4 bi):
            // Hàng 4 (5 bi): Góc trái (index 10) là solid, góc phải (index 14) là stripe
            int[] rackBallNumbers = GenerateWpaRackBallNumbers();

            int ballIndex = 0;
            for (int row = 0; row < 5; row++)
            {
                int countInRow = row + 1;
                float z = footSpot.z + (row * rowSpacing);

                for (int k = 0; k < countInRow; k++)
                {
                    float x = (k - (row * 0.5f)) * diameter;
                    Vector3 ballPos = new Vector3(x, footSpot.y, z);

                    int number = rackBallNumbers[ballIndex];
                    if (number >= 1 && number <= 15 && balls[number] != null)
                    {
                        balls[number].ResetTo(ballPos, UnityEngine.Random.rotation);
                    }
                    ballIndex++;
                }
            }

            if (movementMonitor != null)
            {
                movementMonitor.StopMonitoring();
            }
        }

        private int[] GenerateWpaRackBallNumbers()
        {
            // Danh sách các bi trơn (1..7) và sọc (9..15)
            List<int> solids = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            List<int> stripes = new List<int> { 9, 10, 11, 12, 13, 14, 15 };

            // Trộn ngẫu nhiên
            Shuffle(solids);
            Shuffle(stripes);

            // Mảng 15 quả theo thứ tự hàng:
            // Index 0: Row 0 Apex
            // Index 1, 2: Row 1
            // Index 3, 4, 5: Row 2 (Index 4 là 8-ball)
            // Index 6, 7, 8, 9: Row 3
            // Index 10, 11, 12, 13, 14: Row 4 (Index 10 là solid, Index 14 là stripe)
            int[] rack = new int[15];

            // Đặt bi 8 vào vị trí tâm (index 4)
            rack[4] = 8;

            // Đặt hai góc đáy
            rack[10] = solids[0];
            solids.RemoveAt(0);

            rack[14] = stripes[0];
            stripes.RemoveAt(0);

            // Trộn các quả còn lại vào các vị trí trống
            List<int> remaining = new List<int>();
            remaining.AddRange(solids);
            remaining.AddRange(stripes);
            Shuffle(remaining);

            int remIdx = 0;
            for (int i = 0; i < 15; i++)
            {
                if (rack[i] == 0)
                {
                    rack[i] = remaining[remIdx++];
                }
            }

            return rack;
        }

        private void Shuffle(List<int> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int rnd = UnityEngine.Random.Range(0, i + 1);
                int temp = list[i];
                list[i] = list[rnd];
                list[rnd] = temp;
            }
        }

        private void HandleBallPocketed(BallController ball, int pocketId)
        {
            OnAnyBallPocketed?.Invoke(ball, pocketId);
        }

        public BallState[] CaptureSnapshot()
        {
            BallState[] snapshot = new BallState[16];
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null)
                {
                    snapshot[i] = balls[i].GetState();
                }
            }
            return snapshot;
        }

        public void RestoreSnapshot(BallState[] snapshot)
        {
            if (snapshot == null) return;
            for (int i = 0; i < snapshot.Length && i < balls.Length; i++)
            {
                if (balls[i] != null)
                {
                    balls[i].SetState(snapshot[i]);
                }
            }
        }
    }
}
