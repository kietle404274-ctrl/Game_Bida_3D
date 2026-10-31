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

        [Header("Break Shot")]
        [Tooltip("Hệ số nhân lực cho cú phá bi (cú đầu khi dàn bi còn nguyên) để dàn bi nổ rộng như thực tế. Cú đánh thường không đổi.")]
        [SerializeField] private float breakPowerMultiplier = 1.6f;

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
                var cb = ballManager != null ? ballManager.CueBall : null;
                Debug.LogWarning($"[ShotExecutor] Không thể thực hiện cú đánh vì điều kiện chưa thỏa mãn (bi đang di chuyển hoặc thiếu bi cái). " +
                                 $"cueBall={(cb != null ? "ok" : "null")}, cuePocketed={(cb != null && cb.IsPocketed)}, " +
                                 $"allResting={(movementMonitor != null && movementMonitor.AreAllBallsResting)}");
                return false;
            }

            var cueBall = ballManager.CueBall;
            Rigidbody rb = cueBall.Rigidbody;
            if (rb == null) return false;

            // Nhận diện cú phá bi: 15 bi mục tiêu còn nguyên và vẫn đang gom trong khung tam giác
            bool isBreakShot = IsRackIntact();

            // Mở khóa toàn bộ dàn bi mục tiêu để sẵn sàng tiếp nhận va chạm vật lý nổ dàn
            ballManager.UnlockAllBallsKinematic();

            // 1. Tính toán xung lực chính (Impulse) trên mặt phẳng bàn XZ
            Vector3 shotDir = new Vector3(command.direction.x, 0f, command.direction.z).normalized;
            if (shotDir.sqrMagnitude < 0.001f)
            {
                shotDir = Vector3.forward;
            }

            float shotPower = Mathf.Clamp01(command.power01);
            float impulseMagnitude = shotPower * physicsConfig.MaxShotImpulse * (isBreakShot ? breakPowerMultiplier : 1f);
            Vector3 impulse = shotDir * impulseMagnitude;

            // Đảm bảo Rigidbody hoạt động ổn định và khóa hoàn toàn trục Y
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Tác động lực đẩy lên bi cái theo định luật xung lượng v = impulse / mass (chỉ chuyển động ngang trên mặt phẳng XZ)
            Vector3 launchVelocity = impulse / rb.mass;

            // 2. Xoáy vật lý: điểm chạm lệch tâm b (đơn vị bán kính bi) cho ω = 2.5 * v * b / R (bi đặc: I = 0.4 m R²)
            Vector2 contact = Vector2.ClampMagnitude(command.cueContact01, 1f);
            Vector2 b = contact * physicsConfig.MaxContactOffset;

            // Đánh lệch tâm làm hao tốc độ bi cái (một phần xung lượng biến thành xoáy)
            launchVelocity /= 1f + physicsConfig.OffsetSpeedLoss * b.sqrMagnitude;
            rb.velocity = launchVelocity;

            if (contact.sqrMagnitude > 0.001f && physicsConfig.SpinStrength > 0f)
            {
                const float ballRadius = 0.02855f;
                float v = launchVelocity.magnitude;
                float k = 2.5f * v / ballRadius * physicsConfig.SpinStrength;

                // Trục xoay cu-lê / trô nằm ngang vuông góc với hướng đánh
                Vector3 horizontalSpinAxis = Vector3.Cross(Vector3.up, shotDir).normalized;
                float topBackSpin = b.y * k;

                // Trục xoay ép phê (side-spin) xoay quanh trục thẳng đứng Y
                float sideSpin = -b.x * k;

                rb.angularVelocity = (horizontalSpinAxis * topBackSpin) + (Vector3.up * sideSpin);
            }

            cueBall.SyncPreStepState();

            // 2b. Nhảy bi: cơ nâng đủ góc thì một phần xung lực thành bật lên (phản lực mặt đá), còn lại là tốc độ ngang
            // v_ngang = v * cos(góc), v_đứng = min(v * sin(góc) * JumpGain, MaxJumpSpeed). Không áp dụng cho cú phá bi.
            float elevation = Mathf.Clamp(command.cueElevationDeg, 0f, physicsConfig.MaxCueElevation);
            if (!isBreakShot && elevation >= physicsConfig.MinJumpAngle)
            {
                float rad = elevation * Mathf.Deg2Rad;
                float speed = launchVelocity.magnitude;
                float vUp = Mathf.Min(speed * Mathf.Sin(rad) * physicsConfig.JumpGain, physicsConfig.MaxJumpSpeed);
                Vector3 jumpVelocity = launchVelocity * Mathf.Cos(rad) + Vector3.up * vUp;
                cueBall.LaunchJump(jumpVelocity, rb.angularVelocity);
            }

            // 3. Khởi động bộ giám sát chuyển động
            if (movementMonitor != null)
            {
                movementMonitor.StartMonitoring();
            }

            // 4. Kích hoạt Audio và VFX (Milestone M10)
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.PlayCueHit(command.power01, isBreakShot);
            }

            if (VFX.VFXManager.Instance != null)
            {
                VFX.VFXManager.Instance.PlayChalkPuff(cueBall.transform.position, shotDir);
                VFX.VFXManager.Instance.TriggerBreakShotFeedback(command.power01);
            }

            OnShotExecuted?.Invoke(command);
            return true;
        }

        /// <summary>
        /// True khi cả 15 bi mục tiêu còn trên bàn và nằm gọn trong khung tam giác (chưa bị phá).
        /// Khung tam giác rộng ~0.29m x sâu ~0.26m nên ngưỡng 0.33m đủ rộng cho dung sai.
        /// </summary>
        private bool IsRackIntact()
        {
            var objectBalls = ballManager.GetActiveObjectBalls();
            if (objectBalls == null || objectBalls.Count < 15) return false;

            Vector3 min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, 0f, float.MinValue);
            foreach (var b in objectBalls)
            {
                Vector3 p = b.transform.position;
                min.x = Mathf.Min(min.x, p.x); max.x = Mathf.Max(max.x, p.x);
                min.z = Mathf.Min(min.z, p.z); max.z = Mathf.Max(max.z, p.z);
            }

            return (max.x - min.x) < 0.33f && (max.z - min.z) < 0.33f;
        }
    }
}
