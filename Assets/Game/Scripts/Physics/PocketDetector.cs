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

        public event Action<int, BallController> OnPocketTriggered;

        private void Awake()
        {
            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                sc.isTrigger = true;
                if (sc.radius < 0.090f) sc.radius = 0.090f;
            }
            if (captureDistance < 0.082f)
            {
                captureDistance = 0.085f;
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

            if (ball != null && !ball.IsPocketed)
            {
                // Kiểm tra khoảng cách tâm bi tới tâm lỗ trên mặt phẳng XZ
                Vector2 ballXZ = new Vector2(ball.transform.position.x, ball.transform.position.z);
                Vector2 pocketXZ = new Vector2(transform.position.x, transform.position.z);
                float dist = Vector2.Distance(ballXZ, pocketXZ);

                // Chỉ công nhận vào lỗ khi tâm bi thực sự lọt vào lòng lỗ (dist <= captureDistance)
                // Loại bỏ hoàn toàn cảm giác bi bị "hút vào lỗ" khi chỉ vừa chạm nhẹ sượt qua viền ngoài
                if (dist <= captureDistance)
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
