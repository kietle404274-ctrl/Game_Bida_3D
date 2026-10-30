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
        [Tooltip("Lực cản tuyến tính kiểu không khí (tỉ lệ với vận tốc). Thực tế bằng 0; chỉ để lại để chỉnh cảm giác nếu muốn bi dừng sớm hơn")]
        [SerializeField] private float linearDrag = 0.0f;

        [Tooltip("Lực cản góc khi bi xoay")]
        [SerializeField] private float angularDrag = 0.12f;

        [Tooltip("Ma sát giữa bi và nỉ mặt bàn")]
        [SerializeField] private float clothFriction = 0.20f;

        [Tooltip("Hệ số cản lăn thuần túy trên nỉ (gia tốc cản = hệ số x g). Thực tế 0.005 - 0.020; 0.015 ≈ 0.15 m/s2")]
        [SerializeField] private float rollingResistance = 0.015f;

        [Header("Restitution (Bouncing)")]
        [Tooltip("Độ nảy giữa bi với bi (0..1)")]
        [SerializeField] private float ballRestitution = 0.95f;

        [Tooltip("Độ nảy giữa bi với băng bàn (0..1) - chuẩn bida thực tế ~0.55-0.65")]
        [SerializeField] private float cushionRestitution = 0.58f;

        [Header("Spin Settings")]
        [Tooltip("Hệ số nhân xoáy do điểm chạm cơ so với công thức vật lý thực (1.0 = thực tế: w = 2.5 * v * b / R^2)")]
        [SerializeField] private float spinStrength = 1.0f;

        [Tooltip("Điểm chạm cơ tối đa (tính theo bán kính bi) ngứng với mép vòng chọn điểm cơ trên UI. Thực tế ~0.5-0.6R, xa hơn sẽ trượt cơ.")]
        [SerializeField] private float maxContactOffset = 0.6f;

        [Tooltip("Hao tốc khi đánh lệch tâm: v được chia cho (1 + k * (b/R)^2). 0 = không hao")]
        [SerializeField] private float offsetSpeedLoss = 1.0f;

        [Tooltip("Tốc độ suy giảm xoáy đứng (ép-phê) trên nỉ (rad/s^2). Thực tế khoảng 15-30")]
        [SerializeField] private float spinDecay = 25.0f;

        [Tooltip("(Cũ, không còn dùng) Hệ số truyền ép-phê khi va chạm băng - đã thay bằng mô hình xung lực")]
        [SerializeField] private float cushionSpinTransfer = 0.20f;

        [Header("Cushion Impulse Model")]
        [Tooltip("Độ cao mũi băng so với tâm bi (tính theo bán kính). Chuẩn băng bida ~0.27R")]
        [SerializeField] private float cushionNoseHeight = 0.27f;

        [Tooltip("Ma sát giữa bi và băng cao su (Coulomb). Chuẩn bida ~0.15 - 0.18")]
        [SerializeField] private float cushionFriction = 0.16f;

        [Tooltip("Độ nảy băng khi bi chạm chậm (Chuẩn WPA ~0.58 - 0.62)")]
        [SerializeField] private float cushionRestitutionSlow = 0.60f;

        [Tooltip("Độ nảy băng khi bi chạm nhanh >= tốc độ tham chiếu (Chuẩn WPA ~0.42 - 0.45)")]
        [SerializeField] private float cushionRestitutionFast = 0.44f;

        [Tooltip("Vận tốc pháp tuyến (m/s) ứng với độ nảy 'nhanh' (Chuẩn bida ~3.5 m/s)")]
        [SerializeField] private float cushionRestitutionSpeedRef = 3.5f;

        [Header("Pocket Facing / Jaw Model")]
        [Tooltip("Hệ số nảy của má lỗ (Pocket Facing). Thực tế vật liệu đệm má lỗ hấp thụ nhiều lực hơn băng cao su (~0.40 - 0.45)")]
        [SerializeField] private float pocketFacingRestitution = 0.42f;

        [Tooltip("Hệ số ma sát của má lỗ. Ma sát cao (~0.25) giúp giảm tốc độ bi khi liếm mép lỗ")]
        [SerializeField] private float pocketFacingFriction = 0.25f;

        [Header("Simulation")]
        [Tooltip("Bước thời gian vật lý cố định (s). 0.01 = 100Hz cho mô phỏng bida chính xác hơn")]
        [SerializeField] private float fixedTimestep = 0.01f;

        [Header("Jump Shot")]
        [Tooltip("Hệ số đổi tốc độ bắn thành tốc độ bật lên (phản lực của mặt đá): v_đứng = v * sin(góc) * gain")]
        [SerializeField] private float jumpGain = 0.6f;

        [Tooltip("Tốc độ bật lên tối đa (m/s). 2.0 ~ cao 0.2m; đánh quá mạnh bi có thể bay khỏi bàn")]
        [SerializeField] private float maxJumpSpeed = 2.0f;

        [Tooltip("Góc nâng cơ tối thiểu (độ) để bi nhảy. Dưới góc này cơ chỉ nâng hình ảnh")]
        [SerializeField] private float minJumpAngle = 8f;

        [Tooltip("Góc nâng cơ tối đa (độ) cho phép chọn (thực tế nhảy bi không quá ~60°)")]
        [SerializeField] private float maxCueElevation = 60f;

        [Tooltip("Độ nảy của mặt nỉ khi bi hạ cánh sau cú nhảy (0 = không nảy)")]
        [SerializeField] private float clothBounce = 0.25f;

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
        public float MaxContactOffset => maxContactOffset;
        public float OffsetSpeedLoss => offsetSpeedLoss;
        public float CushionNoseHeight => cushionNoseHeight;
        public float CushionFriction => cushionFriction;
        public float CushionRestitutionSlow => cushionRestitutionSlow;
        public float CushionRestitutionFast => cushionRestitutionFast;
        public float CushionRestitutionSpeedRef => cushionRestitutionSpeedRef;
        public float PocketFacingRestitution => pocketFacingRestitution;
        public float PocketFacingFriction => pocketFacingFriction;
        public float FixedTimestep => fixedTimestep;
        public float RestLinearThreshold => restLinearThreshold;
        public float RestAngularThreshold => restAngularThreshold;
        public float SettleDuration => settleDuration;
        public float JumpGain => jumpGain;
        public float MaxJumpSpeed => maxJumpSpeed;
        public float MinJumpAngle => minJumpAngle;
        public float MaxCueElevation => maxCueElevation;
        public float ClothBounce => clothBounce;
    }
}
