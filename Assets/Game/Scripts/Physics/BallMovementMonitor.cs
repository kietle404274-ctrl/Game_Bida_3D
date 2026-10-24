using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Billiards.Physics
{
    public class BallMovementMonitor : MonoBehaviour
    {
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private List<BallController> monitoredBalls = new List<BallController>();

        private float settleTimer = 0f;
        private float trackingDuration = 0f;
        private bool isTracking = false;
        private bool haveBallsMoved = false;

        public bool AreAllBallsResting { get; private set; } = true;
        public event Action OnAllBallsSettled;
        public event Action OnBallsStartedMoving;

        public void Initialize(PhysicsConfig config, IEnumerable<BallController> balls)
        {
            physicsConfig = config;
            monitoredBalls.Clear();
            if (balls != null)
            {
                monitoredBalls.AddRange(balls);
            }
        }

        public void RegisterBall(BallController ball)
        {
            if (ball != null && !monitoredBalls.Contains(ball))
            {
                monitoredBalls.Add(ball);
            }
        }

        public void StartMonitoring()
        {
            isTracking = true;
            haveBallsMoved = false;
            settleTimer = 0f;
            trackingDuration = 0f;
            AreAllBallsResting = false;
        }

        public void StopMonitoring()
        {
            isTracking = false;
        }

        /// <summary>
        /// Hủy theo dõi cú đánh đang dở (ví dụ ván kết thúc giữa chừng) và coi như bi đã dừng,
        /// không phát OnAllBallsSettled. Nếu chỉ StopMonitoring, AreAllBallsResting có thể kẹt ở false khiến ShotExecutor từ chối mọi cú đánh sau đó.
        /// </summary>
        public void ResetToResting()
        {
            isTracking = false;
            haveBallsMoved = false;
            settleTimer = 0f;
            trackingDuration = 0f;
            AreAllBallsResting = true;
        }

        private void FixedUpdate()
        {
            if (!isTracking || physicsConfig == null) return;

            trackingDuration += Time.fixedDeltaTime;
            // Watchdog: Nếu cú đánh kéo dài quá 10 giây (do va chạm liên tục hoặc bi rơi khỏi bàn), ép dừng để trận đấu không bị treo
            if (trackingDuration >= 10.0f)
            {
                ForceSettle();
                return;
            }

            bool anyBallMoving = false;
            float linearThresh = physicsConfig.RestLinearThreshold;
            float angularThresh = physicsConfig.RestAngularThreshold;

            for (int i = 0; i < monitoredBalls.Count; i++)
            {
                var ball = monitoredBalls[i];
                if (ball != null && !ball.IsPocketed && ball.gameObject.activeInHierarchy)
                {
                    if (ball.IsMoving(linearThresh, angularThresh))
                    {
                        anyBallMoving = true;
                        break;
                    }
                }
            }

            if (anyBallMoving)
            {
                if (!haveBallsMoved)
                {
                    haveBallsMoved = true;
                    OnBallsStartedMoving?.Invoke();
                }
                settleTimer = 0f;
                AreAllBallsResting = false;
            }
            else
            {
                // Khi không còn quả nào di chuyển trên ngưỡng
                settleTimer += Time.fixedDeltaTime;
                if (settleTimer >= physicsConfig.SettleDuration)
                {
                    if (!AreAllBallsResting)
                    {
                        ForceSettle();
                    }
                }
            }
        }

        public void ForceSettle()
        {
            AreAllBallsResting = true;
            isTracking = false;

            // Ép dừng triệt để các dao động vi mô còn lại
            for (int i = 0; i < monitoredBalls.Count; i++)
            {
                var ball = monitoredBalls[i];
                if (ball != null && !ball.IsPocketed)
                {
                    ball.StopAndRest();
                }
            }

            OnAllBallsSettled?.Invoke();
        }
    }
}
