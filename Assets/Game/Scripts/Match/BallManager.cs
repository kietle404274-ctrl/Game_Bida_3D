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
        public int[] LastRackBallNumbers { get; private set; }

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

            // Tự động tìm kiếm và đăng ký các quả bi trong scene nếu mảng còn thiếu
            bool hasMissingBalls = false;
            for (int i = 0; i < 16; i++)
            {
                if (balls[i] == null)
                {
                    hasMissingBalls = true;
                    break;
                }
            }

            if (hasMissingBalls)
            {
                var foundBalls = FindObjectsOfType<BallController>(true);
                foreach (var b in foundBalls)
                {
                    if (b == null) continue;
                    string bName = b.gameObject.name;
                    int parsedNum = -1;
                    int underscoreIdx = bName.LastIndexOf('_');
                    if (underscoreIdx >= 0 && underscoreIdx < bName.Length - 1)
                    {
                        int.TryParse(bName.Substring(underscoreIdx + 1), out parsedNum);
                    }
                    if (parsedNum < 0 || parsedNum > 15)
                    {
                        parsedNum = b.BallNumber;
                    }
                    if (parsedNum >= 0 && parsedNum < 16)
                    {
                        balls[parsedNum] = b;
                    }
                }
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
        /// - Đỉnh dàn bi tại Foot Spot (Apex)
        /// - Bi 8 đen nằm ở tâm dàn bi (hàng thứ 3, quả chính giữa)
        /// - Hai quả ở hai góc đáy (hàng 5): một quả trơn (solid) và một quả sọc (stripe)
        /// - Bi cái tại Head Spot
        /// - Triệt tiêu 100% hiện tượng tự lăn bằng cách khóa Kinematic đến khi cơ thủ thực hiện cú đánh
        /// </summary>
        public void ResetRack(int[] customRackBallNumbers = null)
        {
            if (tableWrapper == null || physicsConfig == null) return;

            // Đảm bảo toàn bộ 16 bi đã được liên kết
            InitializeRegistry();

            float radius = physicsConfig.BallRadius;
            float diameter = radius * 2f;
            float spacing = diameter * 1.0008f; // Khoảng đệm sát thực tế (~0.045mm) giữa các bi trong khuôn
            float rowSpacing = spacing * 0.8660254f; // spacing * sqrt(3)/2

            Vector3 footSpot = tableWrapper.FootSpot;
            Vector3 headSpot = tableWrapper.HeadSpot;

            // 1. Đặt bi cái tại Head Spot và khóa Kinematic để chống tự lăn
            if (balls[0] != null)
            {
                balls[0].ResetTo(headSpot, Quaternion.identity);
                balls[0].SetKinematic(true);
            }

            // 2. Chuẩn bị danh sách số bi cho dàn 15 quả theo WPA (từ tham số custom hoặc sinh mới)
            int[] rackBallNumbers = (customRackBallNumbers != null && customRackBallNumbers.Length == 15)
                ? customRackBallNumbers
                : GenerateWpaRackBallNumbersStatic();
            LastRackBallNumbers = (int[])rackBallNumbers.Clone();

            // Tạo seed tất định từ thứ tự 15 quả bi để sai số vi mô (±0.08mm) đồng bộ tuyệt đối 100% giữa Master & Client
            int rackSeed = 17;
            for (int i = 0; i < rackBallNumbers.Length; i++)
            {
                rackSeed = unchecked(rackSeed * 31 + rackBallNumbers[i]);
            }
            var rackRng = new System.Random(rackSeed);
            Vector3[] placedRackPositions = new Vector3[15];

            int ballIndex = 0;
            for (int row = 0; row < 5; row++)
            {
                int countInRow = row + 1;
                // Đỉnh dàn bi đặt ở footSpot.x, các hàng tiếp theo lùi dần về phía chân bàn (-X)
                float baseX = footSpot.x - (row * rowSpacing);

                for (int k = 0; k < countInRow; k++)
                {
                    // Các bi trong cùng hàng trải đều theo trục ngang Z (đối xứng qua footSpot.z)
                    float baseZ = footSpot.z + ((k - (row * 0.5f)) * spacing);

                    // Sai số tiếp xúc vi mô thực tế (±0.08mm cho các hàng sau đỉnh khuôn) để phá bỏ tính đối xứng gương máy móc
                    float jitterX = 0f;
                    float jitterZ = 0f;
                    if (row > 0)
                    {
                        jitterX = ((float)rackRng.NextDouble() * 2f - 1f) * 0.00008f;
                        jitterZ = ((float)rackRng.NextDouble() * 2f - 1f) * 0.00008f;
                    }

                    Vector3 ballPos = new Vector3(baseX + jitterX, footSpot.y, baseZ + jitterZ);

                    // Đảm bảo khoảng cách tối thiểu giữa các tâm bi không bao giờ chồng lấn (< diameter * 1.0002f)
                    float minDist = diameter * 1.0002f;
                    for (int prev = 0; prev < ballIndex; prev++)
                    {
                        Vector3 diff = ballPos - placedRackPositions[prev];
                        diff.y = 0f;
                        float dist = diff.magnitude;
                        if (dist < minDist && dist > 1e-6f)
                        {
                            ballPos = placedRackPositions[prev] + (diff / dist) * minDist;
                            ballPos.y = footSpot.y;
                        }
                    }
                    placedRackPositions[ballIndex] = ballPos;

                    int number = rackBallNumbers[ballIndex];
                    if (number >= 1 && number <= 15 && balls[number] != null)
                    {
                        balls[number].ResetTo(ballPos, Quaternion.Euler(0f, number * 24f, 0f));
                        // Khóa Kinematic cho 15 bi mục tiêu để triệt tiêu 100% hiện tượng tự lăn trước khi đánh
                        balls[number].SetKinematic(true);
                    }
                    ballIndex++;
                }
            }

            if (movementMonitor != null)
            {
                movementMonitor.StopMonitoring();
            }
        }

        /// <summary>
        /// Mở khóa Rigidbody của toàn bộ dàn bi khi bắt đầu cú đánh để PhysX mô phỏng va chạm nổ bi tự nhiên
        /// </summary>
        public void UnlockAllBallsKinematic()
        {
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null && !balls[i].IsPocketed && balls[i].gameObject.activeInHierarchy)
                {
                    balls[i].SetKinematic(false);
                }
            }
        }

        public static int[] GenerateWpaRackBallNumbersStatic(int? seed = null)
        {
            var rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            // Danh sách các bi trơn (1..7) và sọc (9..15)
            List<int> solids = new List<int> { 1, 2, 3, 4, 5, 6, 7 };
            List<int> stripes = new List<int> { 9, 10, 11, 12, 13, 14, 15 };

            // Trộn ngẫu nhiên
            ShuffleList(solids, rng);
            ShuffleList(stripes, rng);

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
            ShuffleList(remaining, rng);

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

        private static void ShuffleList(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int rnd = rng.Next(0, i + 1);
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
