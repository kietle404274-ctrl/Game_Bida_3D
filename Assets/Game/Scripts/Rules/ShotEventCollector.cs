using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.Rules
{
    public class ShotEventCollector : MonoBehaviour
    {
        [SerializeField] private BallManager ballManager;
        [SerializeField] private TableWrapper tableWrapper;

        private int firstContactBall = -1;
        private List<int> pocketedBalls = new List<int>();
        private Dictionary<int, int> pocketedBallPockets = new Dictionary<int, int>();
        private List<int> ballsThatHitRail = new List<int>();
        private bool cueBallScratch = false;
        private bool anyBallOffTable = false;
        private List<int> ballsDrivenOffTable = new List<int>();
        private bool isTracking = false;

        public int FirstContactBall => firstContactBall;
        public IReadOnlyList<int> PocketedBalls => pocketedBalls;
        public IReadOnlyList<int> BallsThatHitRail => ballsThatHitRail;
        public bool CueBallScratch => cueBallScratch;
        public bool AnyBallOffTable => anyBallOffTable;
        public IReadOnlyList<int> BallsDrivenOffTable => ballsDrivenOffTable;

        public event Action<int> OnFirstContactOccurred;
        public event Action<BallController, int> OnBallPocketDetected;

        public void Initialize(BallManager bManager, TableWrapper tWrapper)
        {
            ballManager = bManager;
            tableWrapper = tWrapper;

            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (ballManager != null)
            {
                var allBalls = ballManager.AllBalls;
                for (int i = 0; i < allBalls.Length; i++)
                {
                    if (allBalls[i] != null)
                    {
                        allBalls[i].OnCollisionOccurred -= HandleBallCollision;
                        allBalls[i].OnCollisionOccurred += HandleBallCollision;

                        allBalls[i].OnBallPocketed -= HandleBallPocketed;
                        allBalls[i].OnBallPocketed += HandleBallPocketed;
                    }
                }
            }

            if (tableWrapper != null && tableWrapper.Pockets != null)
            {
                for (int i = 0; i < tableWrapper.Pockets.Length; i++)
                {
                    if (tableWrapper.Pockets[i] != null)
                    {
                        tableWrapper.Pockets[i].OnPocketTriggered -= HandlePocketTrigger;
                        tableWrapper.Pockets[i].OnPocketTriggered += HandlePocketTrigger;
                    }
                }
            }
        }

        public void StartTracking()
        {
            ResetForNewShot();
            isTracking = true;
        }

        public void StopTracking()
        {
            isTracking = false;
        }

        public void ResetForNewShot()
        {
            firstContactBall = -1;
            pocketedBalls.Clear();
            pocketedBallPockets.Clear();
            ballsThatHitRail.Clear();
            cueBallScratch = false;
            anyBallOffTable = false;
            ballsDrivenOffTable.Clear();
        }

        private void HandleBallCollision(BallController ball, Collision collision)
        {
            if (!isTracking || ball == null) return;

            // 1. Kiểm tra va chạm đầu tiên của bi cái với bi mục tiêu
            if (ball.IsCueBall && firstContactBall == -1)
            {
                BallController otherBall = collision.gameObject.GetComponent<BallController>();
                if (otherBall == null) otherBall = collision.gameObject.GetComponentInParent<BallController>();

                if (otherBall != null && !otherBall.IsCueBall)
                {
                    firstContactBall = otherBall.BallNumber;
                    OnFirstContactOccurred?.Invoke(firstContactBall);
                }
            }

            // 2. Kiểm tra bi chạm băng (cushions)
            // Nhận diện collider của băng bàn qua tên hoặc tag hoặc collider trong Table
            string colName = collision.gameObject.name.ToLower();
            if (colName.Contains("cushion") || colName.Contains("cushions") || colName.Contains("rail"))
            {
                if (!ballsThatHitRail.Contains(ball.BallNumber))
                {
                    ballsThatHitRail.Add(ball.BallNumber);
                }
            }
        }

        private void HandlePocketTrigger(int pocketId, BallController ball)
        {
            if (!isTracking || ball == null) return;

            RecordPocket(ball.BallNumber, pocketId);
            OnBallPocketDetected?.Invoke(ball, pocketId);
        }

        private void HandleBallPocketed(BallController ball, int pocketId)
        {
            if (!isTracking || ball == null) return;

            RecordPocket(ball.BallNumber, pocketId);
        }

        private void RecordPocket(int ballNum, int pocketId)
        {
            if (ballNum == 0)
            {
                cueBallScratch = true;
            }
            else
            {
                if (!pocketedBalls.Contains(ballNum))
                {
                    pocketedBalls.Add(ballNum);
                    pocketedBallPockets[ballNum] = pocketId;
                }
            }
        }

        public void RecordBallOffTable(int ballNum)
        {
            if (!ballsDrivenOffTable.Contains(ballNum))
            {
                ballsDrivenOffTable.Add(ballNum);
                anyBallOffTable = true;
            }
        }

        public ShotResult BuildShotResult()
        {
            // Kiểm tra các bi văng khỏi bàn dựa trên tọa độ Y và ranh giới bàn
            if (ballManager != null && tableWrapper != null)
            {
                var allBalls = ballManager.AllBalls;
                for (int i = 0; i < allBalls.Length; i++)
                {
                    var ball = allBalls[i];
                    if (ball == null || !ball.gameObject.activeInHierarchy || ball.IsPocketed) continue;

                    Vector3 pos = ball.transform.position;
                    // Nếu bi rơi thấp hơn mặt nỉ hoặc văng ra ngoài bàn mà chưa vào lỗ
                    bool belowTable = pos.y < (tableWrapper.ClothSurfaceY - 0.08f);
                    bool outsideBounds = !tableWrapper.IsInsidePlayArea(pos, margin: -0.15f);

                    if (belowTable || outsideBounds)
                    {
                        RecordBallOffTable(ball.BallNumber);
                    }
                }
            }

            ShotResult result = new ShotResult
            {
                firstContactBall = this.firstContactBall,
                cueBallScratch = this.cueBallScratch,
                anyBallOffTable = this.anyBallOffTable
            };

            result.ballsDrivenOffTable.AddRange(this.ballsDrivenOffTable);
            result.pocketedBalls.AddRange(this.pocketedBalls);
            foreach (var kvp in this.pocketedBallPockets)
            {
                result.pocketedBallPockets[kvp.Key] = kvp.Value;
            }
            result.ballsThatHitRail.AddRange(this.ballsThatHitRail);

            return result;
        }
    }
}
