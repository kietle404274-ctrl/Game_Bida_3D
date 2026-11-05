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
        private Vector3 prevPosition;
        private Vector3 prevVelocity;
        private Vector3 prevAngular;
        private int fixedStepIndex = 0;
        private int lastCushionResponseStep = -1;
        private int lastBallResponseStep = -1;
        private readonly float[] lastPairHitAudioTime = new float[16];
        private float lastCushionHitAudioTime = -10f;
        private float lastAnyBounceTime = -10f;

        /// <summary>True nếu quả bi vừa nảy khỏi bi khác hoặc băng trong khoảng windowSec giây gần đây.</summary>
        public bool RecentlyBounced(float windowSec = 0.14f) => (Time.unscaledTime - lastAnyBounceTime) < windowSec;
        private static readonly Collider[] clusterHitsBuffer = new Collider[12];
        private static readonly Collider[] rackBreakHitsBuffer = new Collider[24];
        private static readonly BallController[] rackBallsBuffer = new BallController[16];
        private static readonly Vector3[] rackPosBuffer = new Vector3[16];
        private static readonly Vector3[] rackVelBuffer = new Vector3[16];
        private static readonly float[] rackProjBuffer = new float[16];
        private bool isAirborne = false;
        private float airborneRestY;
        private float airborneTime;
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

        /// <summary>True khi bi đang bay sau cú nhảy (rời mặt nỉ).</summary>
        public bool IsAirborne => isAirborne;

        /// <summary>Độ cao tâm bi so với mặt nỉ khi đang bay (0 nếu đang lăn trên nỉ).</summary>
        public float AirborneHeight => (isAirborne && rb != null) ? Mathf.Max(0f, rb.position.y - airborneRestY) : 0f;

        /// <summary>
        /// Phóng bi lên không (cú nhảy bi): mở khóa trục Y, đặt vận tốc/xoáy ban đầu.
        /// Bi sẽ được khóa lại về mặt nỉ khi hạ cánh (xem PhysicsTick).
        /// </summary>
        public void LaunchJump(Vector3 velocity, Vector3 angularVelocity)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb == null) return;

            airborneRestY = rb.position.y;
            airborneTime = 0f;
            isAirborne = true;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.None;
            rb.velocity = velocity;
            rb.angularVelocity = angularVelocity;
        }

        private void LandFromJump()
        {
            isAirborne = false;
            airborneTime = 0f;
            rb.constraints = RigidbodyConstraints.FreezePositionY;
            Vector3 p = rb.position;
            rb.position = new Vector3(p.x, airborneRestY, p.z);
            Vector3 v = rb.velocity;
            rb.velocity = new Vector3(v.x, 0f, v.z);
        }
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
                prevPosition = rb.position;
            }
            else
            {
                prevPosition = transform.position;
            }

            if (physicsConfig != null)
            {
                ApplyPhysicsConfig(physicsConfig);
            }

#if UNITY_EDITOR
            var mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial == null)
            {
                var fallbackMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AK Studio Art/The Billiards Room/Materials/Balls Billiard.mat");
                if (fallbackMat != null) mr.sharedMaterial = fallbackMat;
            }
#endif
        }

        public void SetBallNumber(int number)
        {
            ballNumber = number;
            if (Application.isPlaying && ballNumber == 0) EnsureCueBallDots();
        }

        private void Start()
        {
            if (ballNumber == 0) EnsureCueBallDots();
        }

        private static Mesh dotMesh;

        /// <summary>
        /// Bi cái kiểu Aramith "Measles": 6 chấm đỏ nhỏ ở 6 hướng (±X, ±Y, ±Z cục bộ của bi).
        /// Chấm là các mesh con nên xoay theo bi, nhìn thấy rõ xoáy cu-lê / trô / ép-phê. Không dùng UV nên không phụ thuộc mesh bi.
        /// </summary>
        private void EnsureCueBallDots()
        {
            if (transform.Find("CueBallDots") != null) return;

            var ballRenderer = GetComponentInChildren<MeshRenderer>();
            if (ballRenderer == null || ballRenderer.sharedMaterial == null) return;

            if (dotMesh == null)
            {
                var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dotMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(tmp);
            }

            // Vật liệu đỏ bóng, dựa trên vật liệu bi để cùng shader (HDRP Lit)
            var redMat = new Material(ballRenderer.sharedMaterial) { name = "CueBallDotRed" };
            if (redMat.HasProperty("_BaseColorMap")) redMat.SetTexture("_BaseColorMap", null);
            if (redMat.HasProperty("_MainTex")) redMat.SetTexture("_MainTex", null);
            Color red = new Color(0.82f, 0.05f, 0.07f, 1f);
            if (redMat.HasProperty("_BaseColor")) redMat.SetColor("_BaseColor", red);
            if (redMat.HasProperty("_Color")) redMat.SetColor("_Color", red);

            var root = new GameObject("CueBallDots").transform;
            root.SetParent(transform, false);

            // Kích thước tính theo thế giới, quy đổi sang tọa độ cục bộ (bi có thể được scale)
            float worldRadius = 0.02855f;
            float worldDiameter = 0.0085f;
            float scale = Mathf.Max(0.0001f, transform.lossyScale.x);
            float localRadius = worldRadius / scale;
            float localDiameter = worldDiameter / scale;

            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                var dot = new GameObject("Dot");
                dot.transform.SetParent(root, false);
                // Hơi lún vào mặt bi, dẹt theo phương pháp tuyến để như chấm sơn
                dot.transform.localPosition = d * (localRadius - localDiameter * 0.02f);
                dot.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, d);
                dot.transform.localScale = new Vector3(localDiameter, localDiameter, localDiameter * 0.25f);

                var mf = dot.AddComponent<MeshFilter>();
                mf.sharedMesh = dotMesh;
                var mr = dot.AddComponent<MeshRenderer>();
                mr.sharedMaterial = redMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
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
            rb.collisionDetectionMode = rb.isKinematic ? CollisionDetectionMode.Discrete : CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0.005f;
            rb.maxAngularVelocity = 1000f;
            rb.constraints = RigidbodyConstraints.FreezePositionY;

            sphereCollider.radius = config.BallRadius;
            sphereCollider.contactOffset = 0.0028f;

            // Thiết lập hoặc đồng bộ PhysicMaterial theo chuẩn WPA
            if (sphereCollider.sharedMaterial == null)
            {
                sphereCollider.sharedMaterial = new PhysicMaterial($"BallMat_{ballNumber}");
            }
            sphereCollider.sharedMaterial.dynamicFriction = 0f; // Loại bỏ Collision Throw của PhysX để bi mục tiêu đi chuẩn theo pháp tuyến TOI
            sphereCollider.sharedMaterial.staticFriction = 0f;
            sphereCollider.sharedMaterial.bounciness = config.BallRestitution; // 0.95
            sphereCollider.sharedMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
            sphereCollider.sharedMaterial.bounceCombine = PhysicMaterialCombine.Average;
        }

        public void SetKinematic(bool kinematic)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                if (kinematic)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }
                else
                {
                    rb.isKinematic = false;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                }
                prevPosition = rb.position;
                prevVelocity = rb.velocity;
                prevAngular = rb.angularVelocity;
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

            if (isAirborne)
                return true;

            return rb.velocity.sqrMagnitude > (linearThreshold * linearThreshold) ||
                   rb.angularVelocity.sqrMagnitude > (angularThreshold * angularThreshold);
        }

        public void StopAndRest()
        {
            if (rb == null) return;
            if (isAirborne)
            {
                if (rb.position.y > airborneRestY - 0.05f) LandFromJump();
                else isAirborne = false; // bi đã văng khỏi bàn: để rơi tự do, luật "bi văng khỏi bàn" xử lý
            }
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            prevPosition = rb.position;
            prevVelocity = Vector3.zero;
            prevAngular = Vector3.zero;
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
                sphereCollider.contactOffset = 0.0028f;
            }

            isPocketed = false;
            isAirborne = false;
            lastCushionResponseStep = -1;
            lastBallResponseStep = -1;
            lastCushionHitAudioTime = -10f;
            lastAnyBounceTime = -10f;
            for (int i = 0; i < lastPairHitAudioTime.Length; i++)
            {
                lastPairHitAudioTime[i] = -10f;
            }
            gameObject.SetActive(true);

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.constraints = RigidbodyConstraints.FreezePositionY;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rotation;
            }
            transform.position = position;
            transform.rotation = rotation;
            prevPosition = position;
            prevVelocity = Vector3.zero;
            prevAngular = Vector3.zero;
        }

        public void MarkPocketed(int pocketId, Vector3? pocketDropPos = null)
        {
            if (isPocketed) return;

            isPocketed = true;
            StopAndRest();

            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
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
            fixedStepIndex++;
            PhysicsTick();

            // Lưu trạng thái ngay trước bước PhysX để xử lý va chạm băng và va chạm bi-bi bằng xung lực chính xác
            if (rb != null)
            {
                prevPosition = rb.position;
                prevVelocity = rb.velocity;
                prevAngular = rb.angularVelocity;
            }
        }

        private void PhysicsTick()
        {
            if (isPocketed || rb == null || rb.isKinematic || physicsConfig == null)
                return;

            // Đang bay (cú nhảy hoặc nảy Pop khi phá bi): chỉ trọng lực + PhysX, không ma sát nỉ; hạ cánh thì khóa lại về mặt nỉ
            if (isAirborne)
            {
                airborneTime += Time.fixedDeltaTime;
                Vector3 ap = rb.position;
                float vy = rb.velocity.y;
                if (airborneTime > 0.03f && ap.y <= airborneRestY + 0.0018f && vy <= 0.08f)
                {
                    // Nảy đệm 1 nhịp nhẹ trên mặt đá nếu bi đáp xuống với vận tốc đứng đáng kể, sau đó tiếp đất êm trên mặt nỉ
                    if (vy < -0.45f)
                    {
                        rb.position = new Vector3(ap.x, airborneRestY + 0.001f, ap.z);
                        rb.velocity = new Vector3(rb.velocity.x, -vy * 0.22f, rb.velocity.z);
                    }
                    else
                    {
                        LandFromJump();
                    }
                }
                else if (airborneTime > 4f && ap.y > airborneRestY - 0.05f)
                {
                    LandFromJump();
                }
                return;
            }

            // Khóa triệt để mọi xung lực nảy theo phương đứng Y (chỉ cho phép bi chuyển động ngang XZ)
            if (Mathf.Abs(rb.velocity.y) > 0.0001f)
            {
                Vector3 vCurr = rb.velocity;
                rb.velocity = new Vector3(vCurr.x, 0f, vCurr.z);
            }

            // Kiểm tra bi có đang tiếp xúc với mặt nỉ bàn hay không (độ cao y xấp xỉ bán kính bi)
            if (rb.velocity.sqrMagnitude < 0.0001f && rb.angularVelocity.sqrMagnitude < 0.0001f)
            {
                if (rb.velocity != Vector3.zero || rb.angularVelocity != Vector3.zero)
                {
                    StopAndRest();
                }
                return;
            }

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

            // Dừng dứt điểm (Snap to Rest): khi bi không còn trượt và vận tốc dài đã dưới ngưỡng dừng nghỉ
            // Triệt tiêu 100% hiện tượng trôi lờ đờ vi mô (micro-drifting) khiến hệ thống chờ oan 2-3 giây
            if (slipSpeed <= 0.015f && linearSpeed < physicsConfig.RestLinearThreshold)
            {
                StopAndRest();
                return;
            }

            // 1. PHA TRƯỢT (Sliding Phase / Spin Interaction):
            // Khi vận tốc trượt đáy còn đáng kể (> 0.015 m/s), ma sát trượt tác động:
            if (slipSpeed > 0.015f)
            {
                float normalForce = rb.mass * 9.81f;
                float frictionMagnitude = normalForce * physicsConfig.ClothFriction;

                // Khong cho luc ma sat vuot qua muc du de triet tieu van toc truot trong 1 buoc (chong rung quanh 0)
                float maxStopForce = slipSpeed * rb.mass / (Time.fixedDeltaTime * 3.5f);
                frictionMagnitude = Mathf.Min(frictionMagnitude, maxStopForce);

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
            else if (linearSpeed >= physicsConfig.RestLinearThreshold)
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

        public void SyncPreStepState()
        {
            if (rb != null)
            {
                prevPosition = rb.position;
                prevVelocity = rb.velocity;
                prevAngular = rb.angularVelocity;
            }
            else
            {
                prevPosition = transform.position;
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
                prevPosition = state.position;
                prevVelocity = state.velocity;
                prevAngular = state.angularVelocity;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            OnCollisionOccurred?.Invoke(this, collision);

            var otherBall = collision.gameObject.GetComponent<BallController>();
            if (otherBall != null)
            {
                // Giải va chạm bi-bi theo Time-of-Impact (TOI) liên tục 1 lần duy nhất cho cặp bi
                if (this.ballNumber < otherBall.BallNumber)
                {
                    HandleBallCollisionResponse(otherBall);
                }
            }
            else
            {
                // Va chạm với băng cao su hoặc má lỗ bàn bida
                HandleCushionCollisionResponse(collision);
            }

            // Audio Feedback (Milestone M10) - có chống lặp (debounce) theo cặp bi và theo băng
            // để tránh phát đúp âm thanh khi gói đồng bộ mạng (EVENT_BALLS_SYNC) hiệu chỉnh vị trí bi trên máy Client.
            if (Audio.AudioManager.Instance != null)
            {
                float now = Time.unscaledTime;
                float relativeSpeed = collision.relativeVelocity.magnitude;
                if (otherBall != null)
                {
                    // Chỉ để bi có số thứ tự nhỏ hơn phát âm thanh để tránh phát đúp âm thanh va chạm
                    if (this.ballNumber < otherBall.BallNumber)
                    {
                        int myIdx = Mathf.Clamp(this.ballNumber, 0, 15);
                        int otherIdx = Mathf.Clamp(otherBall.BallNumber, 0, 15);
                        if (now - lastPairHitAudioTime[otherIdx] >= 0.16f)
                        {
                            lastPairHitAudioTime[otherIdx] = now;
                            otherBall.lastPairHitAudioTime[myIdx] = now;
                            this.lastAnyBounceTime = now;
                            otherBall.lastAnyBounceTime = now;
                            Audio.AudioManager.Instance.PlayBallHit(relativeSpeed);
                        }
                    }
                }
                else if (collision.collider.name != "ClothBedCollider")
                {
                    // Chỉ phát âm thanh băng khi thực sự có phản xạ bật băng (hoặc bi đang bay đập thành bàn)
                    // và đã qua thời gian chống lặp 0.16s (bỏ qua va chạm sượt đầu hồi băng hoặc kéo vị trí do mạng).
                    bool validBounce = (lastCushionResponseStep == fixedStepIndex) || isAirborne;
                    if (validBounce && now - lastCushionHitAudioTime >= 0.16f)
                    {
                        lastCushionHitAudioTime = now;
                        lastAnyBounceTime = now;
                        Audio.AudioManager.Instance.PlayCushionHit(relativeSpeed);
                    }
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.collider == null || collision.collider.name == "ClothBedCollider")
                return;

            if (collision.gameObject.GetComponent<BallController>() == null)
            {
                HandleCushionCollisionResponse(collision);
            }
        }

        /// <summary>
        /// Giải va chạm giữa 2 quả bi theo thời điểm chạm liên tục (Continuous Time-of-Impact - TOI)
        /// trên mặt phẳng XZ tại đúng khoảng cách tâm 2R:
        /// - Khắc phục triệt để lỗi PhysX tính pháp tuyến tại bước rời rạc (khi 2 bi đã lún sâu vào nhau 10-30mm
        ///   làm xoay lệch góc chạy của bi mục tiêu 5°-15° so với thước ngắm).
        /// - Tự động bỏ qua khi va chạm thuộc cụm nhiều bi sát nhau (như cú phá dàn bi 15 quả) để giữ nguyên
        ///   khả năng giải đa vật thể của PhysX.
        /// - Bảo toàn vận tốc góc (prevAngular) của bi cái qua va chạm để xoáy cu-lê / trô hoạt động chuẩn xác trên nỉ.
        /// </summary>
        private void HandleBallCollisionResponse(BallController other)
        {
            if (other == null || rb == null || other.rb == null || physicsConfig == null)
                return;
            if (rb.isKinematic || other.rb.isKinematic)
                return;
            if (isAirborne || other.isAirborne || isPocketed || other.isPocketed)
                return;

            // Nếu 1 trong 2 bi đã vừa xử lý va chạm bi-bi trong cùng bước FixedUpdate -> giữ nguyên kết quả đa vật thể
            if (this.lastBallResponseStep == this.fixedStepIndex || other.lastBallResponseStep == other.fixedStepIndex)
                return;

            float R = physicsConfig.BallRadius;
            bool eitherNearCushion = IsNearStraightCushion(this.prevPosition, R, out _, out _, out _)
                                  || IsNearStraightCushion(other.prevPosition, R, out _, out _, out _);

            // Nếu 1 trong 2 bi đang nằm sát một quả bi thứ 3 ở giữa bàn (như trong dàn tam giác 15 bi lúc phá bi):
            // 1. Nếu là cú phá khuôn tốc độ cao (bi cái lao vào dàn bi đứng yên), giải chính xác điểm chạm TOI đầu tiên,
            //    truyền sóng xung kích đàn hồi (Elastic Shockwave) qua 5 hàng bi và tạo hiệu ứng "Squat & Pop" cho bi cái.
            // 2. Nếu là va chạm cụm nhỏ giữa trận, nhường cho bộ giải đa tiếp xúc PGS của PhysX.
            // Lưu ý: Nếu bi đang nằm sát băng (eitherNearCushion == true), BẮT BUỘC giải qua HandleBallCollisionResponse
            // vì băng trong PhysX có bounciness = 0 sẽ nuốt mất xung lực nếu để PhysX tự giải.
            if (!eitherNearCushion && IsInMultiBallCluster(this, other, R))
            {
                if (!TryResolveRackBreakImpact(this, other, R))
                {
                    this.lastBallResponseStep = this.fixedStepIndex;
                    other.lastBallResponseStep = other.fixedStepIndex;
                }
                return;
            }

            Vector3 p1 = this.prevPosition;
            Vector3 p2 = other.prevPosition;
            Vector3 v1 = (this.lastCushionResponseStep == this.fixedStepIndex)
                ? new Vector3(this.rb.velocity.x, 0f, this.rb.velocity.z)
                : new Vector3(this.prevVelocity.x, 0f, this.prevVelocity.z);
            Vector3 v2 = (other.lastCushionResponseStep == other.fixedStepIndex)
                ? new Vector3(other.rb.velocity.x, 0f, other.rb.velocity.z)
                : new Vector3(other.prevVelocity.x, 0f, other.prevVelocity.z);
            Vector3 w1 = (this.lastCushionResponseStep == this.fixedStepIndex) ? this.rb.angularVelocity : this.prevAngular;
            Vector3 w2 = (other.lastCushionResponseStep == other.fixedStepIndex) ? other.rb.angularVelocity : other.prevAngular;

            Vector3 d0 = new Vector3(p1.x - p2.x, 0f, p1.z - p2.z);
            Vector3 vRel = v1 - v2;

            float a = vRel.sqrMagnitude;
            if (a < 1e-8f)
                return;

            float b = Vector3.Dot(d0, vRel);
            // Hai bi phải đang tiến lại gần nhau trước va chạm (b < 0)
            if (b >= 0f)
                return;

            float twoR = 2f * R;
            float twoRSq = twoR * twoR;
            float c = d0.sqrMagnitude - twoRSq;
            float disc = b * b - a * c;

            float tHit;
            if (disc >= 0f)
            {
                tHit = (-b - Mathf.Sqrt(disc)) / a;
            }
            else
            {
                // Va chạm sượt cực mỏng ở rìa contactOffset: lấy thời điểm tiếp cận gần nhất
                tHit = -b / a;
            }

            float dt = Time.fixedDeltaTime;
            tHit = Mathf.Clamp(tHit, -4f * dt, 4f * dt);

            // Tọa độ tâm hai bi tại đúng thời điểm chạm hình học (khoảng cách 2 tâm = 2R)
            Vector3 c1 = new Vector3(p1.x + v1.x * tHit, p1.y, p1.z + v1.z * tHit);
            Vector3 c2 = new Vector3(p2.x + v2.x * tHit, p2.y, p2.z + v2.z * tHit);

            // Pháp tuyến đơn vị từ bi 1 (this) hướng sang bi 2 (other) tại đúng điểm Ghost Ball
            Vector3 n = new Vector3(c2.x - c1.x, 0f, c2.z - c1.z);
            if (n.sqrMagnitude < 1e-10f)
                return;
            n.Normalize();

            float vn = Vector3.Dot(v1 - v2, n);
            if (vn <= 0f)
                return;

            float e = Mathf.Clamp(physicsConfig.BallRestitution, 0.5f, 1f);
            float impulseScalar = 0.5f * (1f + e) * vn;
            Vector3 impulseVec = n * impulseScalar;

            Vector3 v1Post = v1 - impulseVec;
            Vector3 v2Post = v2 + impulseVec;
            v1Post.y = 0f;
            v2Post.y = 0f;

            // Đặt lại vị trí xuất phát từ đúng điểm tiếp xúc hình học (kèm khe hở an toàn 0.25mm để PhysX không tách lặp)
            Vector3 mid = 0.5f * (c1 + c2);
            float halfSep = R + 0.00025f;
            Vector3 pos1 = new Vector3(mid.x - n.x * halfSep, p1.y, mid.z - n.z * halfSep);
            Vector3 pos2 = new Vector3(mid.x + n.x * halfSep, p2.y, mid.z + n.z * halfSep);

            // Xử lý tức thì trường hợp 1 trong 2 bi đang nằm sát băng và bị đánh ép vào băng:
            // Giải ngay phản xạ bật băng và tách khe hở an toàn để bi mục tiêu văng ra dứt khoát,
            // tuyệt đối không bị bi cái chặn trước mặt hay bị băng bounciness=0 của PhysX giữ lại ở frame kế tiếp.
            if (IsNearStraightCushion(pos2, R, out Vector3 cNorm2, out float railCoord2, out bool isXRail2) &&
                Vector3.Dot(v2Post, cNorm2) < -0.015f)
            {
                ClampAndBounceRailBall(
                    ref pos2, ref v2Post, ref w2,
                    ref pos1, ref v1Post,
                    n, cNorm2, railCoord2, isXRail2, R, e);
                other.lastCushionResponseStep = other.fixedStepIndex;
            }
            else if (IsNearStraightCushion(pos1, R, out Vector3 cNorm1, out float railCoord1, out bool isXRail1) &&
                     Vector3.Dot(v1Post, cNorm1) < -0.015f)
            {
                ClampAndBounceRailBall(
                    ref pos1, ref v1Post, ref w1,
                    ref pos2, ref v2Post,
                    -n, cNorm1, railCoord1, isXRail1, R, e);
                this.lastCushionResponseStep = this.fixedStepIndex;
            }

            // Tiến thêm phần thời gian còn lại trong frame nếu cả 2 vị trí mới đều nằm gọn trong mặt bàn
            float tRem = Mathf.Clamp(dt - Mathf.Max(0f, tHit), 0f, dt);
            if (tRem > 0f)
            {
                Vector3 next1 = pos1 + v1Post * tRem;
                Vector3 next2 = pos2 + v2Post * tRem;
                float minX = TableWrapper.FootCushionX + R + 0.0005f;
                float maxX = TableWrapper.HeadCushionX - R - 0.0005f;
                float maxZ = TableWrapper.CushionHalfWidth - R - 0.0005f;
                float minSepSq = (2f * R + 0.0002f) * (2f * R + 0.0002f);

                if (next1.x >= minX && next1.x <= maxX && Mathf.Abs(next1.z) <= maxZ &&
                    next2.x >= minX && next2.x <= maxX && Mathf.Abs(next2.z) <= maxZ &&
                    (next2 - next1).sqrMagnitude >= minSepSq)
                {
                    pos1 = next1;
                    pos2 = next2;
                }
            }

            rb.position = pos1;
            other.rb.position = pos2;
            rb.velocity = v1Post;
            other.rb.velocity = v2Post;

            // Giữ nguyên hoặc cập nhật vận tốc góc sau phản xạ để xoáy hoạt động chuẩn xác trên nỉ
            rb.angularVelocity = w1;
            other.rb.angularVelocity = w2;

            this.lastBallResponseStep = this.fixedStepIndex;
            other.lastBallResponseStep = other.fixedStepIndex;
            float bounceNow = Time.unscaledTime;
            this.lastAnyBounceTime = bounceNow;
            other.lastAnyBounceTime = bounceNow;
        }

        /// <summary>
        /// Kiểm tra xem tâm bi tại <paramref name="pos"/> có đang nằm sát một trong 4 cạnh băng thẳng hay không
        /// (bỏ qua vùng miệng 6 lỗ). Trả về pháp tuyến hướng từ băng ra mặt bàn <paramref name="cushionNormal"/>.
        /// </summary>
        private static bool IsNearStraightCushion(Vector3 pos, float R, out Vector3 cushionNormal, out float railFaceCoord, out bool isXAxisRail)
        {
            const float margin = 0.0055f; // Sát băng trong phạm vi 5.5mm
            float threshold = R + margin;

            // 1. Băng ngắn Foot (-X) và Head (+X): giới hạn trong khoảng giữa 2 má lỗ góc
            if (Mathf.Abs(pos.z) <= TableWrapper.ShortCushionHalfSpanZ - 0.004f)
            {
                if (pos.x - TableWrapper.FootCushionX <= threshold)
                {
                    cushionNormal = Vector3.right;
                    railFaceCoord = TableWrapper.FootCushionX;
                    isXAxisRail = true;
                    return true;
                }
                if (TableWrapper.HeadCushionX - pos.x <= threshold)
                {
                    cushionNormal = Vector3.left;
                    railFaceCoord = TableWrapper.HeadCushionX;
                    isXAxisRail = true;
                    return true;
                }
            }

            // 2. Băng dài +Z và -Z: giới hạn ngoài miệng lỗ giữa và 4 lỗ góc
            if ((pos.x >= -1.008f && pos.x <= -0.078f) || (pos.x >= 0.066f && pos.x <= 0.996f))
            {
                if (TableWrapper.CushionHalfWidth - pos.z <= threshold)
                {
                    cushionNormal = Vector3.back;
                    railFaceCoord = TableWrapper.CushionHalfWidth;
                    isXAxisRail = false;
                    return true;
                }
                if (pos.z - (-TableWrapper.CushionHalfWidth) <= threshold)
                {
                    cushionNormal = Vector3.forward;
                    railFaceCoord = -TableWrapper.CushionHalfWidth;
                    isXAxisRail = false;
                    return true;
                }
            }

            cushionNormal = Vector3.zero;
            railFaceCoord = 0f;
            isXAxisRail = false;
            return false;
        }

        /// <summary>
        /// Giải phản xạ bật băng tức thì cho quả bi đang nằm sát băng (<paramref name="railPos"/>) khi bị bi kia (<paramref name="otherPos"/>)
        /// đâm ép vào băng:
        /// 1. Kẹp vị trí tâm bi sát băng không lún qua mặt phẳng băng.
        /// 2. Tính vận tốc &amp; xoáy bật băng bằng mô hình xung lực chuẩn.
        /// 3. Đảm bảo khe hở thoát hiểm trước mặt để ở bước PhysX kế tiếp bi bật ra không đâm ngược vào bi cái rồi bị ép lại vào băng bounciness=0.
        /// </summary>
        private void ClampAndBounceRailBall(
            ref Vector3 railPos, ref Vector3 railVel, ref Vector3 railAng,
            ref Vector3 otherPos, ref Vector3 otherVel,
            Vector3 hitNormalToRailBall, Vector3 cushionNormal, float railFaceCoord, bool isXAxisRail, float R, float ballRestitution)
        {
            // 1. Kẹp tọa độ tâm bi sát băng cách mặt băng đúng R + 0.4mm
            float safeCenterDist = R + 0.0004f;
            if (isXAxisRail)
            {
                float limitX = railFaceCoord + cushionNormal.x * safeCenterDist;
                if ((cushionNormal.x > 0f && railPos.x < limitX) || (cushionNormal.x < 0f && railPos.x > limitX))
                {
                    railPos.x = limitX;
                }
            }
            else
            {
                float limitZ = railFaceCoord + cushionNormal.z * safeCenterDist;
                if ((cushionNormal.z > 0f && railPos.z < limitZ) || (cushionNormal.z < 0f && railPos.z > limitZ))
                {
                    railPos.z = limitZ;
                }
            }

            // 2. Giải xung lực bật băng cao su cho quả bi sát băng
            ComputeCushionBounceImpulse(railVel, railAng, cushionNormal, false, out railVel, out railAng);

            // Đảm bảo vận tốc thoát khỏi băng luôn đủ mạnh và hướng ra ngoài mặt bàn
            float reboundOutSpeed = Vector3.Dot(railVel, cushionNormal);
            if (reboundOutSpeed < 0.08f)
            {
                railVel += cushionNormal * (0.08f - reboundOutSpeed);
                reboundOutSpeed = 0.08f;
            }

            // 3. Xử lý tách quỹ đạo trong thời gian nén cao su băng (~4.5ms)
            Vector3 railTangent = new Vector3(-cushionNormal.z, 0f, cushionNormal.x);
            float cutSin = Mathf.Abs(Vector3.Dot(hitNormalToRailBall, railTangent));
            if (cutSin >= 0.16f)
            {
                float otherIntoRailBall = Vector3.Dot(otherVel, hitNormalToRailBall);
                if (otherIntoRailBall > 0f)
                {
                    otherVel -= hitNormalToRailBall * otherIntoRailBall;
                }

                const float cushionCompressionTime = 0.0045f;
                otherPos += otherVel * cushionCompressionTime;
                railPos += railVel * cushionCompressionTime;
            }
            else
            {
                // Đánh trực diện (< 9°) vào bi sát băng: đặt bi cái lệch nhẹ và lùi ra ngoài để không khóa đường thoát của bi mục tiêu
                float sideSign = Vector3.Dot(hitNormalToRailBall, railTangent) >= 0f ? 1f : -1f;
                railVel += railTangent * (sideSign * reboundOutSpeed * 0.18f);
                otherPos = railPos + cushionNormal * (2f * R + 0.0025f) - railTangent * (sideSign * R * 0.35f);
            }

            // 4. Đảm bảo khoảng cách giữa 2 tâm bi >= 2R + 1.0mm và vận tốc tương đối theo phương nối tâm luôn tách xa nhau
            // (|d(t)| > 2R với mọi t > 0) để sang Frame 2 PhysX không bao giờ đẩy dội bi mục tiêu ngược lại vào băng.
            Vector3 delta = railPos - otherPos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float minSep = 2f * R + 0.0010f;
            Vector3 sepDir = dist > 1e-5f ? (delta / dist) : -cushionNormal;
            if (dist < minSep)
            {
                otherPos = railPos - sepDir * minSep;
            }

            // sepDir hướng từ otherPos (bi đâm) sang railPos (bi sát băng).
            // Nếu closingSpeed > 0 nghĩa là railPos đang bật ra nhanh hơn otherPos dọc theo sepDir -> truyền xung đẩy otherVel ra trước
            float closingSpeed = Vector3.Dot(otherVel - railVel, sepDir);
            if (closingSpeed > -0.02f)
            {
                otherVel -= sepDir * (closingSpeed + 0.04f);
            }
        }

        private static bool IsInMultiBallCluster(BallController b1, BallController b2, float radius)
        {
            float checkRadius = radius * 2.15f;
            int count1 = UnityEngine.Physics.OverlapSphereNonAlloc(b1.prevPosition, checkRadius, clusterHitsBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count1; i++)
            {
                var col = clusterHitsBuffer[i];
                if (col == null) continue;
                var bc = col.GetComponent<BallController>();
                if (bc != null && bc != b1 && bc != b2 && !bc.IsPocketed)
                    return true;
            }

            int count2 = UnityEngine.Physics.OverlapSphereNonAlloc(b2.prevPosition, checkRadius, clusterHitsBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count2; i++)
            {
                var col = clusterHitsBuffer[i];
                if (col == null) continue;
                var bc = col.GetComponent<BallController>();
                if (bc != null && bc != b1 && bc != b2 && !bc.IsPocketed)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Mô phỏng chuyên biệt cho cú phá khuôn (Rack Break Impact):
        /// - Xác định chính xác thời điểm chạm liên tục (Continuous TOI) khi bi cái lao vào viên bi đầu tiên của khuôn ở tốc độ cao.
        /// - Truyền sóng xung kích đàn hồi dây chuyền (Elastic Shockwave Propagation) qua 5 hàng bi trong khuôn tam giác:
        ///   + Hai bi cánh (Wing balls ở 2 góc ngoài hàng đáy) văng mạnh về phía 2 lỗ giữa.
        ///   + Các bi hàng trước và hàng giữa tách nêm (Wedge Decompression) kèm sai số góc vi mô tất định để dàn bi nổ giòn,
        ///     tự nhiên khắp mặt bàn mà không bị bộ giải lặp PGS của PhysX triệt tiêu 50% năng lượng hay tách đối xứng robot.
        /// - Tạo hiệu ứng "Squat &amp; Pop" chân thực cho bi cái: khi phá mạnh trực diện, bi cái nảy nhẹ lên khỏi mặt nỉ (2.5–4cm)
        ///   và ghìm lại quanh khu vực tâm bàn.
        /// </summary>
        private static bool TryResolveRackBreakImpact(BallController b1, BallController b2, float R)
        {
            Vector3 v1 = new Vector3(b1.prevVelocity.x, 0f, b1.prevVelocity.z);
            Vector3 v2 = new Vector3(b2.prevVelocity.x, 0f, b2.prevVelocity.z);

            BallController attacker;
            BallController apexHit;
            Vector3 vAttacker;
            Vector3 wAttacker;

            if (v1.sqrMagnitude >= 3.5f && v2.sqrMagnitude < 0.04f)
            {
                attacker = b1;
                apexHit = b2;
                vAttacker = v1;
                wAttacker = b1.prevAngular;
            }
            else if (v2.sqrMagnitude >= 3.5f && v1.sqrMagnitude < 0.04f)
            {
                attacker = b2;
                apexHit = b1;
                vAttacker = v2;
                wAttacker = b2.prevAngular;
            }
            else
            {
                return false;
            }

            Vector3 pAttacker = attacker.prevPosition;
            Vector3 pApex = apexHit.prevPosition;

            // Gom tất cả các bi mục tiêu đang đứng yên trong khuôn tam giác (bán kính 0.34m quanh điểm chạm)
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(pApex, 0.34f, rackBreakHitsBuffer, ~0, QueryTriggerInteraction.Ignore);
            int rackCount = 0;
            for (int i = 0; i < hitCount && rackCount < rackBallsBuffer.Length; i++)
            {
                var col = rackBreakHitsBuffer[i];
                if (col == null) continue;
                var bc = col.GetComponent<BallController>();
                if (bc == null || bc == attacker || bc.IsPocketed || bc.rb == null || bc.rb.isKinematic)
                    continue;

                Vector3 vBc = new Vector3(bc.prevVelocity.x, 0f, bc.prevVelocity.z);
                if (vBc.sqrMagnitude > 0.09f)
                    continue;

                bool duplicate = false;
                for (int k = 0; k < rackCount; k++)
                {
                    if (rackBallsBuffer[k] == bc)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                {
                    rackBallsBuffer[rackCount++] = bc;
                }
            }

            // Chỉ kích hoạt mô hình nổ khuôn đề-pa khi cụm bi tĩnh có từ 6 viên trở lên
            if (rackCount < 6)
                return false;

            // 1. Giải chính xác thời điểm chạm liên tục (TOI) giữa bi cái (attacker) và viên bi chạm đầu tiên (apexHit)
            Vector3 d0 = new Vector3(pAttacker.x - pApex.x, 0f, pAttacker.z - pApex.z);
            float a = vAttacker.sqrMagnitude;
            float b = Vector3.Dot(d0, vAttacker);
            if (a < 1e-6f || b >= 0f)
                return false;

            float twoR = 2f * R;
            float c = d0.sqrMagnitude - twoR * twoR;
            float disc = b * b - a * c;
            float tHit = disc >= 0f ? (-b - Mathf.Sqrt(disc)) / a : (-b / a);
            float dt = Time.fixedDeltaTime;
            tHit = Mathf.Clamp(tHit, -4f * dt, 4f * dt);

            Vector3 cAttacker = new Vector3(pAttacker.x + vAttacker.x * tHit, pAttacker.y, pAttacker.z + vAttacker.z * tHit);
            Vector3 nImpact = new Vector3(pApex.x - cAttacker.x, 0f, pApex.z - cAttacker.z);
            if (nImpact.sqrMagnitude < 1e-8f)
                nImpact = vAttacker.normalized;
            else
                nImpact.Normalize();

            float vn = Vector3.Dot(vAttacker, nImpact);
            if (vn <= 0.25f)
                return false;

            Vector3 vTan = vAttacker - nImpact * vn;
            float speedAttacker = vAttacker.magnitude;
            float fullness = Mathf.Clamp01(vn / Mathf.Max(0.01f, speedAttacker));

            // 2. Sắp xếp các bi trong khuôn theo thứ tự truyền sóng xung kích từ đỉnh khuôn xuống đáy khuôn
            for (int i = 0; i < rackCount; i++)
            {
                Vector3 pos = rackBallsBuffer[i].prevPosition;
                rackPosBuffer[i] = pos;
                rackVelBuffer[i] = Vector3.zero;
                rackProjBuffer[i] = (rackBallsBuffer[i] == apexHit)
                    ? -0.001f
                    : Vector3.Dot(new Vector3(pos.x - pApex.x, 0f, pos.z - pApex.z), nImpact);
            }

            for (int i = 0; i < rackCount - 1; i++)
            {
                int minIdx = i;
                for (int j = i + 1; j < rackCount; j++)
                {
                    if (rackProjBuffer[j] < rackProjBuffer[minIdx] ||
                        (Mathf.Abs(rackProjBuffer[j] - rackProjBuffer[minIdx]) < 1e-5f &&
                         rackBallsBuffer[j].BallNumber < rackBallsBuffer[minIdx].BallNumber))
                    {
                        minIdx = j;
                    }
                }
                if (minIdx != i)
                {
                    var tmpB = rackBallsBuffer[i]; rackBallsBuffer[i] = rackBallsBuffer[minIdx]; rackBallsBuffer[minIdx] = tmpB;
                    var tmpP = rackPosBuffer[i]; rackPosBuffer[i] = rackPosBuffer[minIdx]; rackPosBuffer[minIdx] = tmpP;
                    var tmpProj = rackProjBuffer[i]; rackProjBuffer[i] = rackProjBuffer[minIdx]; rackProjBuffer[minIdx] = tmpProj;
                }
            }

            // Cấp vận tốc xung kích đầu vào cho viên bi chạm đầu tiên (index 0)
            rackVelBuffer[0] = nImpact * (vn * 0.96f);

            // 3. Truyền sóng xung kích đàn hồi qua chuỗi tiếp xúc giữa các hàng bi (4 lượt lan truyền)
            float neighborDistSq = (2.35f * R) * (2.35f * R);
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = 0; i < rackCount; i++)
                {
                    Vector3 pi = rackPosBuffer[i];
                    for (int j = 0; j < rackCount; j++)
                    {
                        if (i == j) continue;
                        Vector3 d = new Vector3(rackPosBuffer[j].x - pi.x, 0f, rackPosBuffer[j].z - pi.z);
                        float distSq = d.sqrMagnitude;
                        if (distSq > neighborDistSq || distSq < 1e-9f)
                            continue;

                        float dist = Mathf.Sqrt(distSq);
                        Vector3 dirIJ = d / dist;
                        if (Vector3.Dot(dirIJ, nImpact) < -0.12f)
                            continue;

                        float relVn = Vector3.Dot(rackVelBuffer[i] - rackVelBuffer[j], dirIJ);
                        if (relVn > 0.005f)
                        {
                            Vector3 impulseTransfer = dirIJ * (relVn * 0.88f);
                            rackVelBuffer[j] += impulseTransfer;
                            rackVelBuffer[i] -= impulseTransfer * 0.72f;
                        }
                    }
                }
            }

            // 4. Hiệu ứng tách nêm tam giác (Wedge Decompression) & phân tán tự nhiên thực tế cho từng vùng bi trong khuôn
            Vector3 rackCenter = Vector3.zero;
            float maxProj = 0.01f;
            for (int i = 0; i < rackCount; i++)
            {
                rackCenter += rackPosBuffer[i];
                if (rackProjBuffer[i] > maxProj) maxProj = rackProjBuffer[i];
            }
            rackCenter /= rackCount;

            float minX = TableWrapper.FootCushionX + R + 0.001f;
            float maxX = TableWrapper.HeadCushionX - R - 0.001f;
            float maxZ = TableWrapper.CushionHalfWidth - R - 0.001f;

            for (int i = 0; i < rackCount; i++)
            {
                BallController bc = rackBallsBuffer[i];
                Vector3 pos = rackPosBuffer[i];
                Vector3 vel = rackVelBuffer[i];
                Vector3 relApex = new Vector3(pos.x - pApex.x, 0f, pos.z - pApex.z);
                float depth01 = Mathf.Clamp01(Mathf.Max(0f, rackProjBuffer[i]) / maxProj);

                Vector3 fromCenter = new Vector3(pos.x - rackCenter.x, 0f, pos.z - rackCenter.z);
                Vector3 outDir = fromCenter.sqrMagnitude > 1e-6f ? fromCenter.normalized : nImpact;

                // Sai số góc vi mô tất định theo mã số bi để triệt tiêu hoàn toàn hiện tượng đối xứng gương máy móc
                int hash = ((bc.BallNumber * 73856093) ^ (apexHit.BallNumber * 19349663) ^ (i * 83492791)) & 0xFFFF;
                float asym01 = (hash / 65535f) * 2f - 1f; // [-1 .. +1]

                // A. Hai bi cánh ở góc ngoài hàng đáy (Wing Balls): văng sắc nét về phía 2 lỗ giữa và nửa trên bàn
                if (depth01 > 0.70f && Mathf.Abs(relApex.z) > 3.0f * R)
                {
                    float zSign = Mathf.Sign(relApex.z);
                    Vector3 wingDir = new Vector3(nImpact.x * (0.28f + 0.08f * asym01), 0f, zSign * 0.94f).normalized;
                    float wingSpeed = Mathf.Max(vel.magnitude, vn * (0.44f + 0.12f * fullness));
                    vel = Vector3.Lerp(vel.normalized, wingDir, 0.68f).normalized * wingSpeed;
                }
                // B. Bi đỉnh khuôn (Apex) và hàng 2: sau khi truyền xung lực xuống đáy, bật nảy sang 2 cánh và hướng về băng đầu/giữa bàn
                else if (depth01 < 0.28f)
                {
                    float sideSign = Mathf.Abs(relApex.z) > 0.003f ? Mathf.Sign(relApex.z) : (asym01 >= 0f ? 1f : -1f);
                    Vector3 reboundDir = new Vector3(-nImpact.x * (0.32f + 0.15f * Mathf.Abs(asym01)), 0f, sideSign * 0.92f).normalized;
                    float minFrontSpeed = vn * (0.30f + 0.10f * fullness);
                    vel = (vel * 0.45f + reboundDir * minFrontSpeed);
                    if (vel.magnitude < minFrontSpeed)
                    {
                        vel = vel.normalized * minFrontSpeed;
                    }
                }
                // C. Các bi trung tâm (gồm bi số 8) và hàng đáy giữa: bung tỏa ra khỏi tâm khuôn và dội băng chân bàn
                else
                {
                    float minSpreadSpeed = vn * Mathf.Lerp(0.24f, 0.40f, fullness) * (0.88f + 0.22f * Mathf.Abs(asym01));
                    Vector3 spreadDir = (vel.sqrMagnitude > 0.01f ? (vel.normalized * 0.62f + outDir * 0.38f) : outDir).normalized;
                    spreadDir = Quaternion.Euler(0f, asym01 * 5.5f, 0f) * spreadDir;
                    float finalSpeed = Mathf.Max(vel.magnitude * 0.92f, minSpreadSpeed);
                    vel = spreadDir * finalSpeed;
                }

                vel.y = 0f;

                // Tách nhẹ tọa độ ban đầu dọc theo hướng văng (0.6mm - 1.5mm) để PhysX không khóa chồng lấn ở bước kế tiếp
                Vector3 sepPos = pos + (vel.sqrMagnitude > 1e-6f ? vel.normalized : outDir) * (0.0006f + depth01 * 0.0009f);
                sepPos.x = Mathf.Clamp(sepPos.x, minX, maxX);
                sepPos.z = Mathf.Clamp(sepPos.z, -maxZ, maxZ);
                sepPos.y = pos.y;

                bc.rb.position = sepPos;
                bc.rb.velocity = vel;
                bc.rb.angularVelocity = new Vector3(vel.z / R, asym01 * 6f, -vel.x / R);
                bc.lastBallResponseStep = bc.fixedStepIndex;
            }

            // 5. Phản hồi "Squat & Pop" chân thực cho bi cái (attacker) tại điểm nổ khuôn
            Vector3 spinLinearVel = new Vector3(-wAttacker.z * R, 0f, wAttacker.x * R);
            Vector3 vCuePostXZ = vTan * 0.82f - nImpact * (vn * 0.11f) + spinLinearVel * 0.22f;
            vCuePostXZ.y = 0f;

            Vector3 cueSafePos = new Vector3(
                Mathf.Clamp(cAttacker.x - nImpact.x * 0.0015f, minX, maxX),
                pAttacker.y,
                Mathf.Clamp(cAttacker.z - nImpact.z * 0.0015f, -maxZ, maxZ));
            attacker.rb.position = cueSafePos;

            // Khóa chặt trục Y cho bi cái khi phá khuôn (không tự bật nảy Pop để tránh văng khỏi bàn; chức năng Nhảy Bi chủ động bằng góc nâng cơ vẫn giữ nguyên ở ShotExecutor)
            attacker.isAirborne = false;
            attacker.rb.constraints = RigidbodyConstraints.FreezePositionY;
            attacker.rb.velocity = vCuePostXZ;
            attacker.rb.angularVelocity = wAttacker * 0.75f;
            attacker.lastBallResponseStep = attacker.fixedStepIndex;
            attacker.lastAnyBounceTime = Time.unscaledTime;

            return true;
        }

        /// <summary>
        /// Phản hồi va chạm băng cao su bằng mô hình xung lực vật rắn (impulse-based):
        /// - Mũi băng nằm cao hơn tâm bi (CushionNoseHeight) nên điểm chạm lệch khỏi tâm, tạo mô-men đổi xoáy lăn.
        /// - Hệ số nảy giảm theo tốc độ va chạm (CushionRestitutionSlow -> Fast).
        /// - Ma sát Coulomb tại băng (CushionFriction) chuyển xoáy ép-phê thành lực dọc băng (đổi góc nảy).
        /// Giả định PhysX cho băng có bounciness = 0 và friction = 0 nên toàn bộ phản hồi nằm ở đây.
        /// </summary>
        private void HandleCushionCollisionResponse(Collision collision)
        {
            if (collision.contactCount == 0 || rb == null || physicsConfig == null)
                return;

            // Bi đang bay: để PhysX xử lý, mô hình xung lực băng chỉ áp dụng cho bi lăn trên nỉ
            if (isAirborne)
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

            // Dùng vận tốc/xoáy ngay trước bước vật lý (PhysX đã triệt thành phần pháp tuyến của rb.velocity).
            // QUAN TRỌNG: Nếu trong cùng bước vật lý quả bi vừa nhận vận tốc từ va chạm bi-bi (lastBallResponseStep == fixedStepIndex)
            // hoặc vừa tăng tốc do bị bi khác đâm vào (gainedSpeedThisStep), hoặc đã phản hồi với một collider băng khác,
            // BẮT BUỘC phải lấy rb.velocity hiện tại! Nếu lấy prevVelocity (bằng 0 khi bi đang nằm sát băng) thì vận tốc văng ra
            // vừa nhận được sẽ bị xóa sạch về 0 khiến bi dính chặt vào băng như keo.
            bool gainedSpeedThisStep = rb.velocity.sqrMagnitude > prevVelocity.sqrMagnitude + 0.0001f;
            bool chained = (lastCushionResponseStep == fixedStepIndex)
                        || (lastBallResponseStep == fixedStepIndex)
                        || gainedSpeedThisStep;
            Vector3 v0 = chained ? rb.velocity : prevVelocity;
            Vector3 w0 = chained ? rb.angularVelocity : prevAngular;
            v0.y = 0f;

            if (!TryResolveCushionPlanarNormal(collision.collider, n, out Vector3 resolvedNormalXZ, out bool isPocketFacing))
            {
                if (!rb.isKinematic && v0.sqrMagnitude > rb.velocity.sqrMagnitude + 0.0001f)
                {
                    rb.velocity = v0;
                    rb.angularVelocity = w0;
                }
                return;
            }
            nXZ = resolvedNormalXZ;

            // n hướng từ băng ra phía bi (từ collider sang ball).
            // Nếu bi đang chuyển động cùng hướng nXZ (đang tách xa băng hoặc trượt dọc mép má vát mở),
            // vn < 0.02f có nghĩa bi không đâm vào bề mặt này -> chỉ khôi phục v0 nếu v0 lớn hơn vận tốc hiện tại
            // (tránh tuyệt đối việc ghi đè vận tốc v0 = 0 lên quả bi vừa được truyền lực!).
            float vn = -Vector3.Dot(v0, nXZ);
            if (vn < 0.02f)
            {
                if (!rb.isKinematic && v0.sqrMagnitude > rb.velocity.sqrMagnitude + 0.0001f)
                {
                    rb.velocity = v0;
                    rb.angularVelocity = w0;
                }
                return;
            }

            if (ComputeCushionBounceImpulse(v0, w0, nXZ, isPocketFacing, out Vector3 vPost, out Vector3 wPost))
            {
                // Gán trực tiếp vận tốc sau va chạm, triệt tiêu hoàn toàn lực đẩy chống xuyên thấu (depenetration)
                // dôi dư do PhysX tự sinh ra ở các góc cạnh/mép lỗ.
                if (!rb.isKinematic)
                {
                    rb.velocity = vPost;
                    rb.angularVelocity = wPost;
                    lastCushionResponseStep = fixedStepIndex;
                    lastAnyBounceTime = Time.unscaledTime;
                }
            }
        }

        /// <summary>
        /// Xác định pháp tuyến mặt phẳng chuẩn (true planar face normal) của băng thẳng hoặc má lỗ vát
        /// nhằm loại bỏ hoàn toàn sai lệch pháp tuyến tại đỉnh/cạnh (ghost vertex collision) ở 12 đầu mũi băng.
        /// Trả về false nếu va chạm vào mặt sàn nỉ hoặc mặt cắt đầu hồi (end-cap) của băng thẳng.
        /// </summary>
        public static bool TryResolveCushionPlanarNormal(
            Collider collider,
            Vector3 rawNormal,
            out Vector3 planarNormalXZ,
            out bool isPocketFacing)
        {
            planarNormalXZ = Vector3.zero;
            isPocketFacing = false;

            if (Mathf.Abs(rawNormal.y) > 0.6f)
                return false;

            Vector3 nXZ = new Vector3(rawNormal.x, 0f, rawNormal.z);
            if (nXZ.sqrMagnitude < 0.001f)
                return false;
            nXZ.Normalize();

            string colName = collider != null ? collider.gameObject.name : string.Empty;
            if (colName == "ClothBedCollider")
                return false;

            isPocketFacing = colName.StartsWith("Facing_") || colName.StartsWith("Jaw_") || colName.StartsWith("Backstop_");

            Vector3 faceNormal = Vector3.zero;
            bool isStraightCushion = false;
            if (colName.StartsWith("Cushion_Foot"))
            {
                faceNormal = Vector3.right;
                isStraightCushion = true;
            }
            else if (colName.StartsWith("Cushion_Head"))
            {
                faceNormal = Vector3.left;
                isStraightCushion = true;
            }
            else if (colName.StartsWith("Cushion_SidePos"))
            {
                faceNormal = Vector3.back;
                isStraightCushion = true;
            }
            else if (colName.StartsWith("Cushion_SideNeg"))
            {
                faceNormal = Vector3.forward;
                isStraightCushion = true;
            }
            else if ((colName.StartsWith("Facing_") || colName.StartsWith("Jaw_")) && collider != null)
            {
                // Trong TableWrapper.CreateFacingBox, trục +X cục bộ (transform.right) luôn hướng ra ngoài mặt va chạm
                Vector3 rightXZ = new Vector3(collider.transform.right.x, 0f, collider.transform.right.z);
                if (rightXZ.sqrMagnitude > 0.001f)
                {
                    faceNormal = rightXZ.normalized;
                }
            }

            if (faceNormal.sqrMagnitude > 0.5f)
            {
                // Nếu va chạm vào mặt cắt đầu hồi (end-cap) của băng thẳng (nXZ lệch quá nhiều so với pháp tuyến mặt băng),
                // bỏ qua để đầu cắt vuông góc của BoxCollider băng không hất bi lệch hướng.
                if (isStraightCushion && Vector3.Dot(nXZ, faceNormal) < 0.75f)
                {
                    return false;
                }
                nXZ = faceNormal;
            }

            planarNormalXZ = nXZ;
            return true;
        }

        /// <summary>
        /// Dự đoán chính xác hướng bật băng sau va chạm cho thước ngắm (AimGuide) khi đánh A-băng (Kick / Bank shot):
        /// 1. Tính vận tốc phóng (v0) và vận tốc góc xoáy (w0) ban đầu giống hệt ShotExecutor.ExecuteShot.
        /// 2. Mô phỏng từng bước tương tác ma sát trượt &amp; ma sát lăn trên nỉ (ApplyClothSpinInteraction) trên quãng đường
        ///    từ vị trí bi cái tới điểm chạm băng để thu được vận tốc và xoáy thực tế ngay trước khi chạm băng.
        /// 3. Giải xung lực bật băng (ComputeCushionBounceImpulseStatic) có xét đầy đủ tốc độ chạm, độ cao mũi băng (CushionNoseHeight),
        ///    ma sát Coulomb tại băng (CushionFriction) và xoáy ép-phê / cu-lê / trô.
        /// </summary>
        public static Vector3 PredictCushionBounceDirection(
            PhysicsConfig config,
            Vector3 shotDirXZ,
            float distanceToCushion,
            float power01,
            Vector2 cueContact01,
            Vector3 cushionNormalXZ,
            bool isPocketFacing)
        {
            Vector3 dir = new Vector3(shotDirXZ.x, 0f, shotDirXZ.z);
            if (dir.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            dir.Normalize();

            Vector3 nXZ = new Vector3(cushionNormalXZ.x, 0f, cushionNormalXZ.z);
            if (nXZ.sqrMagnitude < 0.0001f)
                return dir;
            nXZ.Normalize();

            if (config == null)
            {
                Vector3 mirror = dir - 2f * Vector3.Dot(dir, nXZ) * nXZ;
                return mirror.sqrMagnitude > 0.0001f ? mirror.normalized : -dir;
            }

            float m = Mathf.Max(0.01f, config.BallMass);
            float r = Mathf.Max(0.005f, config.BallRadius);
            float inertia = 0.4f * m * r * r;

            // 1. Khởi tạo vận tốc dài v và vận tốc góc w ban đầu giống hệt ShotExecutor.ExecuteShot
            float clampedPower = Mathf.Clamp(power01, 0.05f, 1f);
            float impulseMag = clampedPower * config.MaxShotImpulse;
            Vector3 v = dir * (impulseMag / m);

            Vector2 contact = Vector2.ClampMagnitude(cueContact01, 1f);
            Vector2 b = contact * config.MaxContactOffset;
            v /= 1f + config.OffsetSpeedLoss * b.sqrMagnitude;

            Vector3 w = Vector3.zero;
            if (contact.sqrMagnitude > 0.001f && config.SpinStrength > 0f)
            {
                float speed0 = v.magnitude;
                float k = 2.5f * speed0 / r * config.SpinStrength;
                Vector3 horizontalSpinAxis = Vector3.Cross(Vector3.up, dir).normalized;
                float topBackSpin = b.y * k;
                float sideSpin = -b.x * k;
                w = (horizontalSpinAxis * topBackSpin) + (Vector3.up * sideSpin);
            }

            // 2. Tiến hóa vận tốc & xoáy trên mặt nỉ dọc theo quãng đường distanceToCushion
            float dt = Time.fixedDeltaTime > 0.0005f ? Time.fixedDeltaTime : 0.005f;
            float linearDragFactor = Mathf.Clamp01(1f - config.LinearDrag * dt);
            float angularDragFactor = Mathf.Clamp01(1f - config.AngularDrag * dt);
            float traveled = 0f;
            int maxSteps = 600;

            for (int step = 0; step < maxSteps && traveled < distanceToCushion; step++)
            {
                float linearSpeed = v.magnitude;
                if (linearSpeed < 0.02f)
                    break;

                float remDist = distanceToCushion - traveled;
                float stepDt = Mathf.Min(dt, remDist / linearSpeed);
                if (stepDt <= 1e-5f)
                    break;

                Vector3 vSlip = new Vector3(
                    v.x + (w.z * r),
                    0f,
                    v.z - (w.x * r)
                );
                float slipSpeed = vSlip.magnitude;

                if (slipSpeed > 0.015f)
                {
                    float normalForce = m * 9.81f;
                    float frictionMagnitude = normalForce * config.ClothFriction;
                    float maxStopForce = slipSpeed * m / (dt * 3.5f);
                    frictionMagnitude = Mathf.Min(frictionMagnitude, maxStopForce);

                    Vector3 frictionDir = -vSlip / slipSpeed;
                    Vector3 frictionForce = frictionDir * frictionMagnitude;
                    Vector3 torque = new Vector3(-r * frictionForce.z, 0f, r * frictionForce.x);

                    v += (frictionForce / m) * stepDt;
                    w += (torque / inertia) * stepDt;
                }
                else
                {
                    w = new Vector3(v.z / r, w.y, -v.x / r);
                    float rollDecel = config.RollingResistance * 9.81f;
                    v -= v.normalized * Mathf.Min(linearSpeed * 0.5f, rollDecel * stepDt);
                }

                if (Mathf.Abs(w.y) > 0.01f)
                {
                    w.y = Mathf.MoveTowards(w.y, 0f, config.SpinDecay * stepDt);
                }

                float dragRatio = stepDt / dt;
                v *= Mathf.Lerp(1f, linearDragFactor, dragRatio);
                w *= Mathf.Lerp(1f, angularDragFactor, dragRatio);

                traveled += 0.5f * (linearSpeed + v.magnitude) * stepDt;
            }

            // Đảm bảo vẫn đủ tốc độ chạm băng để giải xung lực bật băng (nếu lực đặt quá nhẹ không tới băng thì chuẩn hóa theo tốc độ tối thiểu)
            if (-Vector3.Dot(v, nXZ) < 0.08f)
            {
                float fallbackSpeed = Mathf.Max(0.25f, v.magnitude);
                v = dir * fallbackSpeed;
            }

            // 3. Giải xung lực bật băng chính xác theo mô hình vật lý thực tế
            if (ComputeCushionBounceImpulseStatic(config, m, v, w, nXZ, isPocketFacing, out Vector3 vPost, out _))
            {
                vPost.y = 0f;
                if (vPost.sqrMagnitude > 0.0001f)
                {
                    return vPost.normalized;
                }
            }

            Vector3 fallbackReflect = dir - 2f * Vector3.Dot(dir, nXZ) * nXZ;
            return fallbackReflect.sqrMagnitude > 0.0001f ? fallbackReflect.normalized : -dir;
        }

        private bool ComputeCushionBounceImpulse(
            Vector3 v0, Vector3 w0, Vector3 nXZ, bool isPocketFacing,
            out Vector3 vPost, out Vector3 wPost)
        {
            return ComputeCushionBounceImpulseStatic(physicsConfig, rb.mass, v0, w0, nXZ, isPocketFacing, out vPost, out wPost);
        }

        public static bool ComputeCushionBounceImpulseStatic(
            PhysicsConfig physicsConfig, float m,
            Vector3 v0, Vector3 w0, Vector3 nXZ, bool isPocketFacing,
            out Vector3 vPost, out Vector3 wPost)
        {
            vPost = v0;
            wPost = w0;

            if (physicsConfig == null)
                return false;

            float vn = -Vector3.Dot(v0, nXZ);
            if (vn < 0.02f)
                return false;

            float R = physicsConfig.BallRadius;
            float inertia = 0.4f * m * R * R;

            Vector3 up = Vector3.up;
            Vector3 t = Vector3.Cross(up, nXZ).normalized;

            // Điểm chạm trên bi: má lỗ là mặt phẳng đứng (s = 0); băng cao su có mũi nhô s = CushionNoseHeight
            float s = isPocketFacing ? 0f : Mathf.Clamp(physicsConfig.CushionNoseHeight, 0f, 0.9f);
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            Vector3 rc = R * (-nXZ * c + up * s);

            // Vận tốc điểm chạm dùng cho ma sát tiếp tuyến (t, up)
            Vector3 u = v0 + Vector3.Cross(w0, rc);

            // 1. Xung pháp tuyến dựa trên vận tốc tiếp cận tâm bi theo phương ngang (vn > 0),
            // không cộng thành phần xoáy vào un để tránh lỗi xoáy ngược triệt tiêu xung nảy băng.
            float e;
            if (isPocketFacing)
            {
                float facingRes = physicsConfig.PocketFacingRestitution;
                float eSlow = facingRes;
                float eFast = facingRes * 0.75f;
                e = Mathf.Lerp(eSlow, eFast, Mathf.Clamp01(vn / Mathf.Max(0.01f, physicsConfig.CushionRestitutionSpeedRef)));
            }
            else
            {
                e = Mathf.Lerp(physicsConfig.CushionRestitutionSlow,
                               physicsConfig.CushionRestitutionFast,
                               Mathf.Clamp01(vn / Mathf.Max(0.01f, physicsConfig.CushionRestitutionSpeedRef)));
            }

            float knn = 1f / m + Vector3.Cross(rc, nXZ).sqrMagnitude / inertia;
            float jn = (1f + e) * vn / knn;

            // 2. Xung ma sát trong mặt phẳng (t, up); thành phần thẳng đứng của vận tốc tuyến tính bị khóa bởi mặt nỉ
            float ut = Vector3.Dot(u, t);
            float uu = Vector3.Dot(u, up);

            // Ma trận độ cứng K_ff: cột = Δu (theo t, up) khi tác dụng xung đơn vị theo t hoặc up
            Vector3 dUt = t / m + Vector3.Cross(Vector3.Cross(rc, t) / inertia, rc);
            Vector3 dUu = Vector3.Cross(Vector3.Cross(rc, up) / inertia, rc); // không có phần tuyến tính theo Y
            float k11 = Vector3.Dot(dUt, t), k21 = Vector3.Dot(dUt, up);
            float k12 = Vector3.Dot(dUu, t), k22 = Vector3.Dot(dUu, up);
            float det = k11 * k22 - k12 * k21;

            float jt = 0f, ju = 0f;
            float frictionCoeff = isPocketFacing ? physicsConfig.PocketFacingFriction : physicsConfig.CushionFriction;
            if (Mathf.Abs(det) > 1e-12f)
            {
                // Giải K * J = -u_f (bám hoàn toàn), sau đó kẹp theo nón ma sát Coulomb
                jt = (-ut * k22 + uu * k12) / det;
                ju = (-uu * k11 + ut * k21) / det;
                float jfMag = Mathf.Sqrt(jt * jt + ju * ju);
                float jfMax = frictionCoeff * jn;
                if (jfMag > jfMax && jfMag > 1e-9f)
                {
                    float scale = jfMax / jfMag;
                    jt *= scale;
                    ju *= scale;
                }
            }

            // 3. Áp dụng xung
            Vector3 jTotal = jn * nXZ + jt * t + ju * up;
            vPost = v0 + (jn * nXZ + jt * t) / m;
            vPost.y = 0f;
            wPost = w0 + Vector3.Cross(rc, jTotal) / inertia;
            return true;
        }
    }
}
