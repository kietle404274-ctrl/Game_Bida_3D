using System;
using UnityEngine;
using Game.Billiards.Core;

namespace Game.Billiards.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class BallController : MonoBehaviour
    {
        [Header("Ball Identification")]
        [Tooltip("Số của bi: 0 = bi cái (White Ball), 1-7 = bi trơn (Solids), 8 = bi 8 đen, 9-15 = bi sọc (Stripes)")]
        [SerializeField] private int ballNumber;

        [Header("Physics Config Reference")]
        [SerializeField] private PhysicsConfig physicsConfig;

        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private bool isPocketed = false;
        private Vector3 initialScale;
        private Coroutine pocketDropCoroutine;

        public int BallNumber => ballNumber;
        public bool IsPocketed => isPocketed;
        public bool IsCueBall => ballNumber == 0;
        public bool IsEightBall => ballNumber == 8;
        public bool IsSolid => ballNumber >= 1 && ballNumber <= 7;
        public bool IsStripe => ballNumber >= 9 && ballNumber <= 15;
        public Rigidbody Rigidbody => rb;
        public SphereCollider SphereCollider => sphereCollider;

        public event Action<BallController, int> OnBallPocketed;
        public event Action<BallController, Collision> OnCollisionOccurred;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();
            if (transform.localScale != Vector3.zero)
            {
                initialScale = transform.localScale;
            }

            if (rb != null)
            {
                rb.constraints = RigidbodyConstraints.FreezePositionY;
            }

            if (physicsConfig != null)
            {
                ApplyPhysicsConfig(physicsConfig);
            }
        }

        public void SetBallNumber(int number)
        {
            ballNumber = number;
        }

        public void ApplyPhysicsConfig(PhysicsConfig config)
        {
            physicsConfig = config;
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (sphereCollider == null) sphereCollider = GetComponent<SphereCollider>();

            rb.mass = config.BallMass;
            rb.drag = config.LinearDrag;
            rb.angularDrag = config.AngularDrag;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0.005f;
            rb.maxAngularVelocity = 100f;
            rb.constraints = RigidbodyConstraints.FreezePositionY;

            sphereCollider.radius = config.BallRadius;

            // Thiết lập hoặc đồng bộ PhysicMaterial theo chuẩn WPA
            if (sphereCollider.sharedMaterial == null)
            {
                sphereCollider.sharedMaterial = new PhysicMaterial($"BallMat_{ballNumber}");
            }
            sphereCollider.sharedMaterial.dynamicFriction = 0.05f; // Chuẩn ma sát bóng-bóng WPA
            sphereCollider.sharedMaterial.staticFriction = 0.05f;
            sphereCollider.sharedMaterial.bounciness = config.BallRestitution; // 0.95
            sphereCollider.sharedMaterial.frictionCombine = PhysicMaterialCombine.Multiply;
            sphereCollider.sharedMaterial.bounceCombine = PhysicMaterialCombine.Average;
        }

        public void SetKinematic(bool kinematic)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (kinematic)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                rb.isKinematic = kinematic;
                if (kinematic)
                {
                    rb.Sleep();
                }
            }
        }

        public bool IsMoving(float linearThreshold, float angularThreshold)
        {
            if (isPocketed || rb == null || rb.isKinematic)
                return false;

            return rb.velocity.sqrMagnitude > (linearThreshold * linearThreshold) ||
                   rb.angularVelocity.sqrMagnitude > (angularThreshold * angularThreshold);
        }

        public void StopAndRest()
        {
            if (rb == null) return;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.Sleep();
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            if (pocketDropCoroutine != null)
            {
                StopCoroutine(pocketDropCoroutine);
                pocketDropCoroutine = null;
            }
            if (initialScale != Vector3.zero)
            {
                transform.localScale = initialScale;
            }
            if (sphereCollider != null)
            {
                sphereCollider.enabled = true;
            }

            isPocketed = false;
            gameObject.SetActive(true);

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.FreezePositionY;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rotation;
            }
            transform.position = position;
            transform.rotation = rotation;
        }

        public void MarkPocketed(int pocketId, Vector3? pocketDropPos = null)
        {
            if (isPocketed) return;

            isPocketed = true;
            StopAndRest();

            if (rb != null)
            {
                rb.isKinematic = true;
            }
            if (sphereCollider != null)
            {
                sphereCollider.enabled = false;
            }

            OnBallPocketed?.Invoke(this, pocketId);

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                if (pocketDropCoroutine != null)
                {
                    StopCoroutine(pocketDropCoroutine);
                }
                Vector3 targetPos = pocketDropPos.HasValue ? pocketDropPos.Value : transform.position;
                pocketDropCoroutine = StartCoroutine(AnimatePocketDrop(targetPos));
            }
            else
            {
                // Trong EditMode hoặc batchmode test: hoàn tất đồng bộ tức thì
                transform.position = new Vector3(transform.position.x, -10f, transform.position.z);
                gameObject.SetActive(false);
            }
        }

        private System.Collections.IEnumerator AnimatePocketDrop(Vector3 targetPocketPos)
        {
            Vector3 startPos = transform.position;
            // Rơi xuống sâu 12cm vào trong lòng lỗ 3D
            Vector3 endPos = new Vector3(targetPocketPos.x, startPos.y - 0.12f, targetPocketPos.z);
            Vector3 rollAxis = Vector3.Cross(Vector3.up, (endPos - startPos).normalized);
            if (rollAxis.sqrMagnitude < 0.01f) rollAxis = Vector3.right;

            float duration = 0.40f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Quỹ đạo XZ: uốn mượt mà vào tâm lỗ
                float tXZ = Mathf.SmoothStep(0f, 1f, t);
                float curX = Mathf.Lerp(startPos.x, endPos.x, tXZ);
                float curZ = Mathf.Lerp(startPos.z, endPos.z, tXZ);

                // Quỹ đạo Y: rơi trọng lực gia tốc hướng xuống lòng lỗ (ease-in quadrat)
                float tY = t * t;
                float curY = Mathf.Lerp(startPos.y, endPos.y, tY);

                transform.position = new Vector3(curX, curY, curZ);

                // Hiệu ứng quay bi chúc đầu tự nhiên vào lòng lỗ
                transform.Rotate(rollAxis, 320f * Time.deltaTime, Space.World);

                // Thu nhỏ nhẹ tạo cảm giác chìm sâu vào họng túi 3D
                if (initialScale != Vector3.zero)
                {
                    float scaleFactor = Mathf.Lerp(1f, 0.85f, t);
                    transform.localScale = initialScale * scaleFactor;
                }

                yield return null;
            }

            // Hoàn tất hiệu ứng rơi vào lỗ: ẩn bi và phục hồi cấu hình
            transform.position = new Vector3(targetPocketPos.x, -10f, targetPocketPos.z);
            if (initialScale != Vector3.zero)
            {
                transform.localScale = initialScale;
            }
            if (sphereCollider != null)
            {
                sphereCollider.enabled = true;
            }
            gameObject.SetActive(false);
            pocketDropCoroutine = null;
        }

        public BallState GetState()
        {
            return new BallState(
                ballNumber,
                isPocketed,
                transform.position,
                transform.rotation,
                rb != null ? rb.velocity : Vector3.zero,
                rb != null ? rb.angularVelocity : Vector3.zero
            );
        }

        private void FixedUpdate()
        {
            if (isPocketed || rb == null || rb.isKinematic || physicsConfig == null)
                return;

            // Khóa triệt để mọi xung lực nảy theo phương đứng Y (chỉ cho phép bi chuyển động ngang XZ)
            if (Mathf.Abs(rb.velocity.y) > 0.0001f)
            {
                Vector3 vCurr = rb.velocity;
                rb.velocity = new Vector3(vCurr.x, 0f, vCurr.z);
            }

            // Kiểm tra bi có đang tiếp xúc với mặt nỉ bàn hay không (độ cao y xấp xỉ bán kính bi)
            if (rb.velocity.sqrMagnitude < 0.0001f && rb.angularVelocity.sqrMagnitude < 0.0001f)
                return;

            ApplyClothSpinInteraction();
        }

        /// <summary>
        /// Mô phỏng tương tác ma sát trượt và ma sát lăn giữa bi và nỉ bàn:
        /// - Khi có xoáy cu-lê (topspin): ma sát nỉ kéo bi tiến tới sau va chạm.
        /// - Khi có xoáy trô (backspin): ma sát nỉ kéo giật lùi bi cái về sau.
        /// - Khi có ép phê (sidespin): suy giảm xoáy đứng theo spinDecay.
        /// - Chuyển pha mượt mà giữa Trượt (Sliding) và Lăn Thuần Túy (Pure Rolling).
        /// </summary>
        private void ApplyClothSpinInteraction()
        {
            float r = physicsConfig.BallRadius;
            Vector3 v = rb.velocity;
            Vector3 w = rb.angularVelocity;

            // Vận tốc tiếp xúc tại điểm đáy bi với mặt nỉ bàn:
            // r_contact = (0, -r, 0)
            // v_contact = v + (w x r_contact)
            // w x (0, -r, 0) = (+w.z * r, 0, -w.x * r)
            Vector3 vSlip = new Vector3(
                v.x + (w.z * r),
                0f,
                v.z - (w.x * r)
            );

            float slipSpeed = vSlip.magnitude;
            float linearSpeed = v.magnitude;

            // 1. PHA TRƯỢT (Sliding Phase / Spin Interaction):
            // Khi vận tốc trượt đáy còn đáng kể (> 0.015 m/s), ma sát trượt tác động:
            if (slipSpeed > 0.015f)
            {
                float normalForce = rb.mass * 9.81f;
                float frictionMagnitude = normalForce * physicsConfig.ClothFriction;

                Vector3 frictionDir = -vSlip / slipSpeed;
                Vector3 frictionForce = frictionDir * frictionMagnitude;

                // Lực ma sát tác dụng lên trọng tâm
                rb.AddForce(frictionForce, ForceMode.Force);

                // Mô-men xoắn: tau = r_contact x F_friction = (0, -r, 0) x (Fx, 0, Fz) = (-r * Fz, 0, r * Fx)
                Vector3 torque = new Vector3(-r * frictionForce.z, 0f, r * frictionForce.x);
                rb.AddTorque(torque, ForceMode.Force);
            }
            // 2. PHA LĂN THUẦN TÚY (Pure Rolling Phase):
            // Khi vận tốc trượt triệt tiêu (v_contact ~ 0), bi lăn mượt mà không trượt trên nỉ
            else if (linearSpeed > 0.005f)
            {
                // Khóa đồng bộ vận tốc góc chính xác theo vận tốc dài để hiển thị quả bi lăn tự nhiên (v = w x r)
                rb.angularVelocity = new Vector3(v.z / r, rb.angularVelocity.y, -v.x / r);

                // Lực cản lăn thực tế (Rolling Resistance, mu_r ~ 0.010 chuẩn WPA Tournament)
                // Deceleration đều đặn, không giật, giúp bi giảm tốc mượt mà và dừng tự nhiên
                float rollDecel = physicsConfig.RollingResistance * 9.81f;
                Vector3 rollResistance = -v.normalized * (rb.mass * rollDecel);
                rb.AddForce(rollResistance, ForceMode.Force);
            }

            // 3. Suy giảm xoáy đứng (sidespin / ép-phê) theo thời gian (chuẩn deceleration ~8 rad/s^2)
            if (Mathf.Abs(w.y) > 0.01f)
            {
                float decayStep = physicsConfig.SpinDecay * Time.fixedDeltaTime;
                float newWy = Mathf.MoveTowards(w.y, 0f, decayStep);
                rb.angularVelocity = new Vector3(rb.angularVelocity.x, newWy, rb.angularVelocity.z);
            }
        }

        public void SetState(BallState state)
        {
            ballNumber = state.ballNumber;
            isPocketed = state.isPocketed;

            if (isPocketed)
            {
                MarkPocketed(-1);
            }
            else
            {
                ResetTo(state.position, state.rotation);
                if (rb != null)
                {
                    rb.velocity = state.velocity;
                    rb.angularVelocity = state.angularVelocity;
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnCollisionOccurred?.Invoke(this, collision);

            var otherBall = collision.gameObject.GetComponent<BallController>();
            if (otherBall == null)
            {
                // Va chạm với băng cao su hoặc má lỗ bàn bida
                HandleCushionCollisionResponse(collision);
            }

            // Audio Feedback (Milestone M10)
            if (Audio.AudioManager.Instance != null)
            {
                float relativeSpeed = collision.relativeVelocity.magnitude;
                if (otherBall != null)
                {
                    // Chỉ để bi có số thứ tự nhỏ hơn phát âm thanh để tránh phát đúp âm thanh va chạm
                    if (this.ballNumber < otherBall.BallNumber)
                    {
                        Audio.AudioManager.Instance.PlayBallHit(relativeSpeed);
                    }
                }
                else if (collision.collider.name != "ClothBedCollider")
                {
                    // Va chạm với băng cao su hoặc thành bàn (bỏ qua tiếp xúc với mặt nỉ)
                    Audio.AudioManager.Instance.PlayCushionHit(relativeSpeed);
                }
            }
        }

        /// <summary>
        /// Xử lý phản hồi va chạm băng cao su và má lỗ theo đặc tả Mục 9 tài liệu vật lý bida:
        /// 1. Đảo chiều thành phần xoáy lăn (Rolling Spin Reversal):
        ///    Mũi băng cao su tiếp xúc phía trên tâm bi sinh mô-men quay đảo ngược chiều xoáy lăn
        ///    để bi sau khi bật băng đồng bộ ngay với hướng chuyển động mới, triệt tiêu hiện tượng dính/ì bóng do nỉ.
        /// 2. Chuyển đổi xoáy đứng ép-phê (Sidespin Transfer):
        ///    Ép-phê (w.y) ma sát với băng sinh xung lực tiếp tuyến làm mở rộng góc nảy (ép-phê thuận)
        ///    hoặc thu hẹp góc nảy (ép-phê nghịch), đồng thời tiêu hao một phần năng lượng xoay.
        /// </summary>
        private void HandleCushionCollisionResponse(Collision collision)
        {
            if (collision.contactCount == 0 || rb == null || physicsConfig == null)
                return;

            ContactPoint contact = collision.GetContact(0);
            Vector3 n = contact.normal;

            // Bỏ qua nếu tiếp xúc mặt phẳng nỉ (n.y lớn)
            if (Mathf.Abs(n.y) > 0.6f)
                return;

            Vector3 nXZ = new Vector3(n.x, 0f, n.z);
            if (nXZ.sqrMagnitude < 0.001f)
                return;
            nXZ.Normalize();

            // Vector tiếp tuyến nằm ngang dọc theo thành băng (hướng sang phải khi nhìn vào băng)
            Vector3 tXZ = Vector3.Cross(nXZ, Vector3.up).normalized;

            Vector3 w = rb.angularVelocity;
            float r = physicsConfig.BallRadius;
            float eCushion = physicsConfig.CushionRestitution;

            // 1. ĐẢO CHIỀU XOÁY LĂN (Rolling Spin Reversal)
            // Trục quay của thành phần xoay lăn vuông góc với nXZ, tức nằm dọc theo tXZ
            float rollSpin = Vector3.Dot(w, tXZ);
            Vector3 newW = w - ((1f + eCushion) * rollSpin) * tXZ;

            // 2. CHUYỂN ĐỔI ÉP-PHÊ THÀNH XUNG LỰC TIẾP TUYẾN DỌC BĂNG (Sidespin Transfer)
            // contact.x > 0 (ép-phê phải) -> w.y < 0 -> lực đẩy sang phải theo hướng tXZ
            if (Mathf.Abs(w.y) > 0.2f)
            {
                float spinTransfer = physicsConfig.CushionSpinTransfer;
                float tangentImpulse = -w.y * r * spinTransfer;
                rb.velocity += tXZ * tangentImpulse;

                // Băng cao su hấp thụ 25% độ xoáy đứng do ma sát
                newW.y *= 0.75f;
            }

            rb.angularVelocity = newW;
        }
    }
}
