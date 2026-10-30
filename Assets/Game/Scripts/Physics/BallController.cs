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
        private Vector3 prevVelocity;
        private Vector3 prevAngular;
        private int fixedStepIndex = 0;
        private int lastCushionResponseStep = -1;
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
            }

            if (physicsConfig != null)
            {
                ApplyPhysicsConfig(physicsConfig);
            }
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
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.sleepThreshold = 0.005f;
            rb.maxAngularVelocity = 1000f;
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
            sphereCollider.sharedMaterial.frictionCombine = PhysicMaterialCombine.Average;
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
            isAirborne = false;
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
            fixedStepIndex++;
            PhysicsTick();

            // Luu trang thai ngay truoc buoc PhysX de xu ly va cham bang bang xung luc chinh xac
            if (rb != null)
            {
                prevVelocity = rb.velocity;
                prevAngular = rb.angularVelocity;
            }
        }

        private void PhysicsTick()
        {
            if (isPocketed || rb == null || rb.isKinematic || physicsConfig == null)
                return;

            // Đang bay (cú nhảy): chỉ trọng lực + PhysX, không ma sát nỉ; hạ cánh thì khóa lại về mặt nỉ
            if (isAirborne)
            {
                airborneTime += Time.fixedDeltaTime;
                Vector3 ap = rb.position;
                float vy = rb.velocity.y;
                bool nearCloth = ap.y <= airborneRestY + 0.0015f && Mathf.Abs(vy) < 0.15f;
                bool stuckNearCloth = airborneTime > 4f && ap.y > airborneRestY - 0.05f;
                if (airborneTime > 0.05f && (nearCloth || stuckNearCloth))
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

            // Dùng vận tốc/xoáy ngay trước bước vật lý (PhysX đã triệt thành phần pháp tuyến của rb.velocity)
            // Nếu trong cùng bước vật lý bi đã được xử lý phản hồi với một collider khác (vd. băng + má lỗ tại điểm nối),
            // phải tiếp tục từ vận tốc/xoáy đã cập nhật đó. Nếu dùng lại giá trị trước bước vật lý thì kết quả của lần xử lý
            // trước bị ghi đè, bi vẫn lún vào mặt kia và bị hãm/bật lệch bất thường.
            bool chained = (lastCushionResponseStep == fixedStepIndex);
            Vector3 v0 = chained ? rb.velocity : prevVelocity;
            Vector3 w0 = chained ? rb.angularVelocity : prevAngular;
            v0.y = 0f;

            // n hướng từ băng ra phía bi (từ collider sang ball).
            // Nếu bi đang chuyển động cùng hướng nXZ (đang tách xa băng hoặc trượt dọc mép má vát mở),
            // vn <= 0 có nghĩa bi không đâm vào bề mặt này -> bỏ qua, không tạo phản lực cản trở.
            float vn = -Vector3.Dot(v0, nXZ);
            if (vn < 0.02f)
                return;

            float m = rb.mass;
            float R = physicsConfig.BallRadius;
            float inertia = 0.4f * m * R * R;

            Vector3 up = Vector3.up;
            Vector3 t = Vector3.Cross(up, nXZ).normalized;

            // Phân biệt va chạm má lỗ (Facing/Jaw/Backstop) và Băng cao su chính:
            // - Băng cao su chính: có mũi nhô (CushionNoseHeight), nảy mạnh (e ~ 0.85 -> 0.60), ma sát thấp (0.12).
            // - Má lỗ / mép túi: mặt phẳng đứng (s = 0), đệm mút triệt tiêu lực nảy (restitution thấp ~ 0.42),
            //   ma sát cao hơn (~ 0.25) giúp hãm bi, không bị bật ngược ra ngoài bàn.
            string colName = collision.collider != null ? collision.collider.gameObject.name : string.Empty;
            string goName = collision.gameObject != null ? collision.gameObject.name : string.Empty;
            bool isPocketFacing = colName.StartsWith("Facing_") || colName.StartsWith("Jaw_") || colName.StartsWith("Backstop_")
                               || goName.StartsWith("Facing_") || goName.StartsWith("Jaw_") || goName.StartsWith("Backstop_");

            // Điểm chạm trên bi: má lỗ là mặt phẳng đứng (s = 0); băng cao su có mũi nhô s = CushionNoseHeight
            float s = isPocketFacing ? 0f : Mathf.Clamp(physicsConfig.CushionNoseHeight, 0f, 0.9f);
            float c = Mathf.Sqrt(Mathf.Max(0f, 1f - s * s));
            Vector3 rc = R * (-nXZ * c + up * s);

            // Vận tốc điểm chạm
            Vector3 u = v0 + Vector3.Cross(w0, rc);
            float un = Vector3.Dot(u, nXZ);
            if (un >= 0f)
                return;

            // 1. Xung pháp tuyến
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
            float jn = -(1f + e) * un / knn;

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
            Vector3 vPost = v0 + (jn * nXZ + jt * t) / m;
            vPost.y = 0f;
            Vector3 wPost = w0 + Vector3.Cross(rc, jTotal) / inertia;

            // Gán trực tiếp vận tốc sau va chạm, triệt tiêu hoàn toàn lực đẩy chống xuyên thấu (depenetration)
            // dôi dư do PhysX tự sinh ra ở các góc cạnh/mép lỗ.
            if (!rb.isKinematic)
            {
                rb.velocity = vPost;
                rb.angularVelocity = wPost;
                lastCushionResponseStep = fixedStepIndex;
            }
        }
    }
}
