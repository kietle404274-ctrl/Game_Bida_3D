using System;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.Physics
{
    public class ShotExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PhysicsConfig physicsConfig;
        [SerializeField] private BallManager ballManager;
        [SerializeField] private BallMovementMonitor movementMonitor;

        public event Action<ShotCommand> OnShotExecuted;

        public void Initialize(PhysicsConfig config, BallManager bManager, BallMovementMonitor monitor)
        {
            physicsConfig = config;
            ballManager = bManager;
            movementMonitor = monitor;
        }

        public bool CanExecuteShot()
        {
            if (ballManager == null || movementMonitor == null || physicsConfig == null)
                return false;

            var cueBall = ballManager.CueBall;
            if (cueBall == null || cueBall.IsPocketed || !cueBall.gameObject.activeInHierarchy)
                return false;

            return movementMonitor.AreAllBallsResting;
        }

        public bool ExecuteShot(ShotCommand command)
        {
            if (!CanExecuteShot())
            {
                Debug.LogWarning("[ShotExecutor] Không thể thực hiện cú đánh vì điều kiện chưa thỏa mãn (bi đang di chuyển hoặc thiếu bi cái).");
                return false;
            }

            var cueBall = ballManager.CueBall;
            Rigidbody rb = cueBall.Rigidbody;
            if (rb == null) return false;

            // 1. Tính toán xung lực chính (Impulse) trên mặt phẳng bàn XZ
            Vector3 shotDir = new Vector3(command.direction.x, 0f, command.direction.z).normalized;
            if (shotDir.sqrMagnitude < 0.001f)
            {
                shotDir = Vector3.forward;
            }

            float shotPower = Mathf.Clamp01(command.power01);
            float impulseMagnitude = shotPower * physicsConfig.MaxShotImpulse;
            Vector3 impulse = shotDir * impulseMagnitude;

            // Đảm bảo Rigidbody hoạt động ổn định
            rb.isKinematic = false;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Tác động lực đẩy lên bi cái
            rb.AddForce(impulse, ForceMode.Impulse);

            // 2. Tác động xoáy (Topspin, Backspin, Sidespin) nếu có tiếp xúc lệch tâm
            Vector2 contact = Vector2.ClampMagnitude(command.cueContact01, 1f);
            if (contact.sqrMagnitude > 0.001f && physicsConfig.SpinStrength > 0f)
            {
                // Trục xoay cu-lê / trô nằm ngang vuông góc với hướng đánh
                Vector3 horizontalSpinAxis = Vector3.Cross(Vector3.up, shotDir).normalized;
                float topBackSpin = contact.y * shotPower * physicsConfig.SpinStrength;

                // Trục xoay ép phê (side-spin) xoay quanh trục thẳng đứng Y
                float sideSpin = -contact.x * shotPower * physicsConfig.SpinStrength;

                Vector3 initialAngularVelocity = (horizontalSpinAxis * topBackSpin) + (Vector3.up * sideSpin);
                rb.angularVelocity = initialAngularVelocity;
            }

            // 3. Khởi động bộ giám sát chuyển động
            if (movementMonitor != null)
            {
                movementMonitor.StartMonitoring();
            }

            OnShotExecuted?.Invoke(command);
            return true;
        }
    }
}
