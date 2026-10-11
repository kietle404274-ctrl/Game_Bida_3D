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
        }
    }
}
