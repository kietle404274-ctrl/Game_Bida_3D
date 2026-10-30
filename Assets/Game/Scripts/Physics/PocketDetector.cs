using System;
using UnityEngine;

namespace Game.Billiards.Physics
{
    [RequireComponent(typeof(Collider))]
    public class PocketDetector : MonoBehaviour
    {
        [Tooltip("ID của lỗ: 0..5 tương ứng 4 góc và 2 lỗ giữa")]
        [SerializeField] private int pocketId;

        public int PocketId => pocketId;
        public Vector3 PocketPosition => transform.position;

        [Tooltip("Bán kính bắt bi (m). Chỉ công nhận vào lỗ khi tâm quả bi thực sự đi qua miệng lỗ vào lòng lỗ (~0.085m)")]
        [SerializeField] private float captureDistance = 0.085f;

        [Tooltip("Bán kính bắt bi tối thiểu (m) khi bi chạy rất nhanh")]
        [SerializeField] private float minCaptureDistance = 0.065f;

        [Tooltip("Mức thu nhỏ bán kính bắt bi (m) cho mỗi 1 m/s vượt quá 2 m/s")]
        [SerializeField] private float fastSpeedShrink = 0.008f;

        public event Action<int, BallController> OnPocketTriggered;

        private void Awake()
        {
            bool isCorner = (pocketId != 2 && pocketId != 3);
            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                float minRadius = isCorner ? 0.115f : 0.090f;
                if (sc.radius < minRadius) sc.radius = minRadius;
            }

            if (isCorner)
            {
                if (captureDistance < 0.098f) captureDistance = 0.098f;
                if (minCaptureDistance < 0.078f) minCaptureDistance = 0.078f;
            }
            else
            {
                if (captureDistance < 0.082f) captureDistance = 0.085f;
            }
        }

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryPocketBall(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryPocketBall(other);
        }

        private void TryPocketBall(Collider other)
        {
            BallController ball = other.GetComponent<BallController>();
            if (ball == null)
            {
                ball = other.GetComponentInParent<BallController>();
            }

            if (ball != null && !ball.IsPocketed && ball.AirborneHeight < 0.02f)
            {
                // Kiểm tra khoảng cách tâm bi tới tâm lỗ trên mặt phẳng XZ
                Vector2 ballXZ = new Vector2(ball.transform.position.x, ball.transform.position.z);
                Vector2 pocketXZ = new Vector2(transform.position.x, transform.position.z);
                float dist = Vector2.Distance(ballXZ, pocketXZ);

                // Phân biệt lỗ góc và lỗ giữa: Lỗ góc có má vát hình phễu đón bi sâu hơn
                bool isCorner = (pocketId != 2 && pocketId != 3);
                float baseCap = isCorner ? Mathf.Max(captureDistance, 0.098f) : captureDistance;
                float minCap = isCorner ? Mathf.Max(minCaptureDistance, 0.078f) : minCaptureDistance;

                // Chỉ công nhận vào lỗ khi tâm bi thực sự lọt vào lòng lỗ (dist <= effective)
                // Bi chạy nhanh phải đi sâu hơn vào lòng lỗ mới rơi (bi nhanh dễ bật khỏi lỗ)
                float speed = ball.Rigidbody != null ? ball.Rigidbody.velocity.magnitude : 0f;
                float effective = Mathf.Max(minCap, baseCap - fastSpeedShrink * Mathf.Max(0f, speed - 2f));
                if (dist <= effective)
                {
                    ball.MarkPocketed(pocketId, transform.position);
                    OnPocketTriggered?.Invoke(pocketId, ball);

                    // Audio Feedback (Milestone M10)
                    if (Audio.AudioManager.Instance != null)
                    {
                        Audio.AudioManager.Instance.PlayPocketDrop();
                    }
                }
            }
        }
    }
}
