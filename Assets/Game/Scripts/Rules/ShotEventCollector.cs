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
        private List<int> initialContactCandidates = new List<int>();
        private float firstContactTimestamp = -1f;
        private List<int> pocketedBalls = new List<int>();
        private Dictionary<int, int> pocketedBallPockets = new Dictionary<int, int>();
        private List<int> ballsThatHitRail = new List<int>();
        private List<int> ballsThatHitRailAfterContact = new List<int>();
        private bool cueBallScratch = false;
        private bool anyBallOffTable = false;
        private List<int> ballsDrivenOffTable = new List<int>();
        private bool isTracking = false;

        public int FirstContactBall => firstContactBall;
        public IReadOnlyList<int> InitialContactCandidates => initialContactCandidates;
        public IReadOnlyList<int> PocketedBalls => pocketedBalls;
        public IReadOnlyList<int> BallsThatHitRail => ballsThatHitRail;
        public IReadOnlyList<int> BallsThatHitRailAfterContact => ballsThatHitRailAfterContact;
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
            firstContactTimestamp = -1f;
            initialContactCandidates.Clear();
            pocketedBalls.Clear();
            pocketedBallPockets.Clear();
            ballsThatHitRail.Clear();
            ballsThatHitRailAfterContact.Clear();
            cueBallScratch = false;
            anyBallOffTable = false;
            ballsDrivenOffTable.Clear();
        }

        private void FixedUpdate()
        {
            // Lớp bảo vệ bổ sung: Nếu bi cái di chuyển với tốc độ cao đâm thẳng vào bi mục tiêu
            // mà PhysX bỏ lỡ OnCollisionEnter, kiểm tra khoảng cách tâm (chính xác bán kính, KHÔNG cộng dung sai thừa)
            if (!isTracking || firstContactBall != -1 || ballManager == null) return;

            var cueBall = ballManager.CueBall;
            if (cueBall == null || cueBall.IsPocketed) return;

            var cueRb = cueBall.Rigidbody;
            if (cueRb == null || cueRb.velocity.sqrMagnitude < 0.01f) return;

            Vector3 cueVel = cueRb.velocity;
            cueVel.y = 0f;
            Vector3 cuePos = cueBall.transform.position;

            // Bán kính tiếp xúc chuẩn: 2 * R (0.0571m), không có margin ảo để tránh bắt nhầm khi sượt qua
            float maxContactDist = 2f * 0.02855f;
            float contactThresholdSqr = maxContactDist * maxContactDist;

            var allBalls = ballManager.AllBalls;
            if (allBalls == null) return;

            int bestBallNum = -1;
            float bestDistSqr = float.MaxValue;

            for (int i = 1; i < allBalls.Length; i++)
            {
                var b = allBalls[i];
                if (b == null || b.IsPocketed || !b.gameObject.activeInHierarchy) continue;

                // ĐIỀU KIỆN TIÊN QUYẾT: Bi mục tiêu phải thực sự nhận động lượng và di chuyển (v > 0.045 m/s)
                // Nếu bi mục tiêu đứng yên, tuyệt đối không được gán va chạm (tránh bắt nhầm khi sượt qua hoặc xuất phát gần)
                if (b.Rigidbody == null || b.Rigidbody.velocity.sqrMagnitude <= 0.002f) continue;

                Vector3 diff = b.transform.position - cuePos;
                diff.y = 0f;
                float distSqr = diff.sqrMagnitude;
                if (distSqr <= contactThresholdSqr)
                {
                    // Chỉ nhận khi bi cái đang thực sự di chuyển HƯỚNG VỀ bi mục tiêu (Dot > 0)
                    if (Vector3.Dot(cueVel, diff) > 0.01f)
                    {
                        if (distSqr < bestDistSqr)
                        {
                            bestDistSqr = distSqr;
                            bestBallNum = b.BallNumber;
                        }
                    }
                }
            }

            if (bestBallNum != -1)
            {
                firstContactBall = bestBallNum;
                firstContactTimestamp = Time.time;
                if (!initialContactCandidates.Contains(bestBallNum))
                {
                    initialContactCandidates.Add(bestBallNum);
                }
                Debug.Log($"[ShotEventCollector] First Contact (FixedUpdate Proximity Fallback): Bi #{bestBallNum}");
                OnFirstContactOccurred?.Invoke(firstContactBall);
            }
        }

        private void HandleBallCollision(BallController ball, Collision collision)
        {
            if (!isTracking || ball == null) return;

            // 1. Kiểm tra va chạm đầu tiên của bi cái với bi mục tiêu (hỗ trợ cả 2 chiều callback từ PhysX)
            BallController targetBall = null;
            if (ball.IsCueBall)
            {
                BallController otherBall = collision.gameObject.GetComponent<BallController>();
                if (otherBall == null) otherBall = collision.gameObject.GetComponentInParent<BallController>();
                if (otherBall != null && !otherBall.IsCueBall && !otherBall.IsPocketed)
                {
                    targetBall = otherBall;
                }
            }
            else if (!ball.IsCueBall && !ball.IsPocketed)
            {
                BallController otherBall = collision.gameObject.GetComponent<BallController>();
                if (otherBall == null) otherBall = collision.gameObject.GetComponentInParent<BallController>();
                if (otherBall != null && otherBall.IsCueBall)
                {
                    targetBall = ball;
                }
            }

            if (targetBall != null)
            {
                float relVel = collision.relativeVelocity.magnitude;
                // Bỏ qua các va chạm rung siêu nhỏ (< 0.015 m/s) không phải cú đâm thực sự
                if (relVel >= 0.015f)
                {
                    int targetNum = targetBall.BallNumber;
                    if (firstContactBall == -1)
                    {
                        firstContactBall = targetNum;
                        firstContactTimestamp = Time.time;
                        if (!initialContactCandidates.Contains(targetNum))
                        {
                            initialContactCandidates.Add(targetNum);
                        }
                        Debug.Log($"[ShotEventCollector] First Contact: Bi #{targetNum}, Vận tốc va chạm: {relVel:F3} m/s");
                        OnFirstContactOccurred?.Invoke(firstContactBall);
                    }
                    else if (firstContactTimestamp > 0f && (Time.time - firstContactTimestamp) <= 0.060f)
                    {
                        // Cửa sổ va chạm đồng thời mở rộng (Split-hit window: 60ms ~ 3 physics frames)
                        if (!initialContactCandidates.Contains(targetNum))
                        {
                            initialContactCandidates.Add(targetNum);
                            Debug.Log($"[ShotEventCollector] Split-hit Candidate: Bi #{targetNum} (delta {Time.time - firstContactTimestamp:F3}s)");
                        }
                    }
                }
            }

            // 2. Kiểm tra bi chạm băng (cushions, má lỗ facings, jaws, backstops)
            string colName = collision.gameObject.name.ToLower();
            if (colName.Contains("cushion") || colName.Contains("cushions") || colName.Contains("rail") ||
                colName.Contains("facing") || colName.Contains("jaw") || colName.Contains("backstop"))
            {
                if (!ballsThatHitRail.Contains(ball.BallNumber))
                {
                    ballsThatHitRail.Add(ball.BallNumber);
                }

                // Ghi nhận riêng các lần chạm băng SAU KHI đã tiếp xúc bi mục tiêu (WPA Rule 6.3)
                if (firstContactBall != -1)
                {
                    if (!ballsThatHitRailAfterContact.Contains(ball.BallNumber))
                    {
                        ballsThatHitRailAfterContact.Add(ball.BallNumber);
                    }
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

            result.initialContactCandidates.AddRange(this.initialContactCandidates);
            result.ballsDrivenOffTable.AddRange(this.ballsDrivenOffTable);
            result.pocketedBalls.AddRange(this.pocketedBalls);
            foreach (var kvp in this.pocketedBallPockets)
            {
                result.pocketedBallPockets[kvp.Key] = kvp.Value;
            }
            result.ballsThatHitRail.AddRange(this.ballsThatHitRail);
            result.ballsThatHitRailAfterContact.AddRange(this.ballsThatHitRailAfterContact);

            return result;
        }
    }
}
