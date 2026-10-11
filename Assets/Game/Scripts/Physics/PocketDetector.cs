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

        public event Action<int, BallController> OnPocketTriggered;

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
            BallController ball = other.GetComponent<BallController>();
            if (ball == null)
            {
                ball = other.GetComponentInParent<BallController>();
            }

            if (ball != null && !ball.IsPocketed)
            {
                ball.MarkPocketed(pocketId);
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
