using UnityEngine;

namespace Game.Billiards.Physics
{
    [CreateAssetMenu(fileName = "PhysicsConfig", menuName = "Billiards/Physics Config")]
    public class PhysicsConfig : ScriptableObject
    {
        [Header("Ball Properties")]
        [Tooltip("Khối lượng quả bi (kg), chuẩn thi đấu ~0.17kg")]
        [SerializeField] private float ballMass = 0.17f;

        [Tooltip("Bán kính quả bi (m), chuẩn ~0.0285m (đường kính ~5.7cm)")]
        [SerializeField] private float ballRadius = 0.02855f;

        [Tooltip("Lực tác động tối đa khi lực bắn đạt 100% (1.0 N.s cho vận tốc tối đa ~5.9 m/s)")]
        [SerializeField] private float maxShotImpulse = 1.0f;

        [Header("Drag & Friction")]
        [Tooltip("Lực cản tuyến tính làm chậm bi khi lăn")]
        [SerializeField] private float linearDrag = 0.16f;

        [Tooltip("Lực cản góc khi bi xoay")]
        [SerializeField] private float angularDrag = 0.12f;

        [Tooltip("Ma sát giữa bi và nỉ mặt bàn")]
        [SerializeField] private float clothFriction = 0.20f;

        [Tooltip("Hệ số cản lăn thuần túy trên nỉ (mu_r ~ 0.015 - 0.030, nỉ thường có cảm giác ma sát rõ rệt)")]
        [SerializeField] private float rollingResistance = 0.032f;

        [Header("Restitution (Bouncing)")]
        [Tooltip("Độ nảy giữa bi với bi (0..1)")]
        [SerializeField] private float ballRestitution = 0.95f;

        [Tooltip("Độ nảy giữa bi với băng bàn (0..1)")]
        [SerializeField] private float cushionRestitution = 0.78f;

        [Header("Spin Settings")]
        [Tooltip("Hệ số áp dụng xoáy (spin/english) lên bi cái")]
        [SerializeField] private float spinStrength = 16.0f;

        [Tooltip("Tốc độ suy giảm xoáy theo thời gian (rad/s^2)")]
        [SerializeField] private float spinDecay = 8.0f;

        [Tooltip("Hệ số truyền ép-phê khi va chạm băng cao su (0.15 - 0.25)")]
        [SerializeField] private float cushionSpinTransfer = 0.20f;

        [Header("Stop & Settle Thresholds")]
        [Tooltip("Ngưỡng vận tốc dài (m/s) xem như bi đã dừng")]
        [SerializeField] private float restLinearThreshold = 0.015f;

        [Tooltip("Ngưỡng vận tốc góc (rad/s) xem như bi đã dừng xoay")]
        [SerializeField] private float restAngularThreshold = 0.1f;

        [Tooltip("Thời gian liên tục dưới ngưỡng vận tốc trước khi chốt bi đã dừng hẳn (giây)")]
        [SerializeField] private float settleDuration = 0.25f;

        public float BallMass => ballMass;
        public float BallRadius => ballRadius;
        public float MaxShotImpulse => maxShotImpulse;
        public float LinearDrag => linearDrag;
        public float AngularDrag => angularDrag;
        public float ClothFriction => clothFriction;
        public float RollingResistance => rollingResistance;
        public float BallRestitution => ballRestitution;
        public float CushionRestitution => cushionRestitution;
        public float SpinStrength => spinStrength;
        public float SpinDecay => spinDecay;
        public float CushionSpinTransfer => cushionSpinTransfer;
        public float RestLinearThreshold => restLinearThreshold;
        public float RestAngularThreshold => restAngularThreshold;
        public float SettleDuration => settleDuration;
    }
}
