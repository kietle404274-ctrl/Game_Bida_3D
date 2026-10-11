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

            sphereCollider.radius = config.BallRadius;

            // Thiết lập PhysicMaterial nếu chưa có
            if (sphereCollider.sharedMaterial == null)
            {
                PhysicMaterial ballMat = new PhysicMaterial($"BallMat_{ballNumber}")
                {
                    dynamicFriction = config.ClothFriction,
                    staticFriction = config.ClothFriction,
                    bounciness = config.BallRestitution,
                    frictionCombine = PhysicMaterialCombine.Multiply,
                    bounceCombine = PhysicMaterialCombine.Maximum
                };
                sphereCollider.sharedMaterial = ballMat;
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
        }

        public void ResetTo(Vector3 position, Quaternion rotation)
        {
            isPocketed = false;
            gameObject.SetActive(true);

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = position;
                rb.rotation = rotation;
            }
            transform.position = position;
            transform.rotation = rotation;
        }

        public void MarkPocketed(int pocketId)
        {
            if (isPocketed) return;

            isPocketed = true;
            StopAndRest();

            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Đặt bi xuống dưới bàn và tắt renderer tạm thời
            transform.position = new Vector3(transform.position.x, -10f, transform.position.z);
            gameObject.SetActive(false);

            OnBallPocketed?.Invoke(this, pocketId);
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
        /// </summary>
        private void ApplyClothSpinInteraction()
        {
            float r = physicsConfig.BallRadius;
            Vector3 v = rb.velocity;
            Vector3 w = rb.angularVelocity;

            // Vận tốc trượt tại điểm tiếp xúc đáy bi với mặt nỉ (r_contact = 0, -r, 0)
            // v_slip = v + (w x r_contact)
            // w x (0, -r, 0) = (-w.z * r, 0, w.x * r)
            Vector3 vSlip = new Vector3(
                v.x - (w.z * r),
                0f,
                v.z + (w.x * r)
            );

            float slipSpeed = vSlip.magnitude;
            if (slipSpeed > 0.005f)
            {
                // Lực ma sát động cản trở vận tốc trượt
                float normalForce = rb.mass * 9.81f;
                float frictionMagnitude = normalForce * physicsConfig.ClothFriction;

                Vector3 frictionDir = -vSlip / slipSpeed;
                Vector3 frictionForce = frictionDir * frictionMagnitude;

                // Tác dụng lực ma sát lên trọng tâm
                rb.AddForce(frictionForce, ForceMode.Force);

                // Momen lực (torque) đưa bi dần về trạng thái lăn thuần túy
                // tau = r_contact x F = (0, -r, 0) x (Fx, 0, Fz) = (-r * Fz, 0, r * Fx)
                Vector3 torque = new Vector3(-r * frictionForce.z, 0f, r * frictionForce.x);
                rb.AddTorque(torque, ForceMode.Force);
            }

            // Suy giảm xoáy đứng (sidespin / ép phê) theo thời gian
            if (Mathf.Abs(w.y) > 0.01f)
            {
                float decayRate = physicsConfig.SpinDecay * Time.fixedDeltaTime;
                float newWy = Mathf.MoveTowards(w.y, 0f, decayRate * Mathf.Abs(w.y));
                rb.angularVelocity = new Vector3(w.x, newWy, w.z);
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

            // Audio Feedback (Milestone M10)
            if (Audio.AudioManager.Instance != null)
            {
                float relativeSpeed = collision.relativeVelocity.magnitude;
                var otherBall = collision.gameObject.GetComponent<BallController>();
                if (otherBall != null)
                {
                    // Chỉ để bi có số thứ tự nhỏ hơn phát âm thanh để tránh phát đúp âm thanh va chạm
                    if (this.ballNumber < otherBall.BallNumber)
                    {
                        Audio.AudioManager.Instance.PlayBallHit(relativeSpeed);
                    }
                }
                else
                {
                    // Va chạm với băng cao su hoặc thành bàn
                    Audio.AudioManager.Instance.PlayCushionHit(relativeSpeed);
                }
            }
        }
    }
}
