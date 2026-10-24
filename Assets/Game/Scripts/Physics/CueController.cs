using System.Collections;
using UnityEngine;

namespace Game.Billiards.Physics
{
    public class CueController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Transform gốc của gậy cơ 3D")]
        [SerializeField] private Transform cueStickRoot;

        [Tooltip("Transform bi cái")]
        [SerializeField] private Transform cueBallTarget;

        [Header("Positioning & Offsets")]
        [Tooltip("Khoảng cách tối thiểu từ đầu cơ tới mép bi khi lực = 0")]
        [SerializeField] private float baseDistance = 0.045f;

        [Tooltip("Độ lùi tối đa của cơ khi kéo lực 100% (m)")]
        [SerializeField] private float maxPullback = 0.25f;

        [Tooltip("Độ nghiêng của cơ so với mặt phẳng ngang (độ)")]
        [SerializeField] private float cueElevationAngle = 3.5f;

        [Header("Animation Settings")]
        [Tooltip("Thời gian thực hiện cú đánh đẩy tới khi bắn (giây)")]
        [SerializeField] private float strokeDuration = 0.08f;

        private bool isAiming = true;
        private bool isStriking = false;
        private float currentPower01 = 0f;
        private Vector3 currentAimDirection = Vector3.forward;
        private Transform collidersStrippedFor;

        /// <summary>
        /// Cây cơ chỉ là hình ảnh minh họa: lực đánh được áp qua ShotExecutor. Tắt toàn bộ collider/rigidbody để cơ xoay chạm bi mục tiêu cũng không thể hất bi.
        /// </summary>
        private void StripCuePhysics()
        {
            if (cueStickRoot == null || collidersStrippedFor == cueStickRoot) return;
            collidersStrippedFor = cueStickRoot;

            foreach (var rbody in cueStickRoot.GetComponentsInChildren<Rigidbody>(true))
            {
                rbody.isKinematic = true;
                rbody.detectCollisions = false;
            }
            foreach (var col in cueStickRoot.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }
        }

        public bool IsVisible => cueStickRoot != null && cueStickRoot.gameObject.activeSelf;
        public Transform StickRoot => cueStickRoot != null ? cueStickRoot : transform;

        private void Start()
        {
            if (cueStickRoot == null) cueStickRoot = transform;
            AutoFindCueBallTargetIfMissing();

            var camDir = FindObjectOfType<Camera.CameraDirector>();
            Vector3 initialDir = camDir != null ? camDir.AimDirection : Vector3.forward;
            UpdateAim(initialDir, 0.35f);
        }

        private void AutoFindCueBallTargetIfMissing()
        {
            if (cueBallTarget == null)
            {
                var ballMgr = FindObjectOfType<Match.BallManager>();
                if (ballMgr != null && ballMgr.CueBall != null)
                {
                    cueBallTarget = ballMgr.CueBall.transform;
                }
                else
                {
                    var whiteBall = GameObject.Find("GameBall_00");
                    if (whiteBall != null) cueBallTarget = whiteBall.transform;
                }
            }
        }

        public void Initialize(Transform cueStick, Transform cueBall)
        {
            cueStickRoot = cueStick != null ? cueStick : transform;
            cueBallTarget = cueBall;
            SetAimingState(true);
            if (cueBallTarget != null)
            {
                UpdateAim(Vector3.forward, 0.35f);
            }
        }

        public void SetCueBallTarget(Transform target)
        {
            cueBallTarget = target;
            if (cueBallTarget != null)
            {
                var camDir = FindObjectOfType<Camera.CameraDirector>();
                Vector3 aimDir = camDir != null ? camDir.AimDirection : Vector3.forward;
                UpdateAim(aimDir, currentPower01 > 0 ? currentPower01 : 0.35f);
            }
        }

        /// <summary>
        /// Đặt cây cơ nằm yên tại một vị trí/góc cố định (dùng cho cảnh trưng bày ở sảnh). Hướng forward của cơ là hướng đầu cơ.
        /// </summary>
        public void PlaceAtRest(Vector3 position, Quaternion rotation)
        {
            if (cueStickRoot == null) cueStickRoot = transform;
            StripCuePhysics();
            isStriking = false;
            SetAimingState(true);
            cueStickRoot.position = position;
            cueStickRoot.rotation = rotation;
        }

        public bool IsStriking => isStriking;

        public void SetAimingState(bool aiming)
        {
            isAiming = aiming;
            // Ẩn cơ sẽ dừng StrokeRoutine (chạy trên chính cây cơ) nên phải tự reset cờ, nếu không UpdateAim bị khóa mãi
            if (!aiming) isStriking = false;
            if (cueStickRoot != null)
            {
                cueStickRoot.gameObject.SetActive(aiming);
            }
        }

        public void UpdateAim(Vector3 aimDirection, float power01)
        {
            if (cueStickRoot == null) cueStickRoot = transform;
            StripCuePhysics();
            AutoFindCueBallTargetIfMissing();

            if (!isAiming || isStriking || cueStickRoot == null || cueBallTarget == null)
                return;

            currentAimDirection = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;
            currentPower01 = Mathf.Clamp01(power01);

            // Tính toán khoảng cách lùi cơ dựa theo lực
            float distance = baseDistance + (currentPower01 * maxPullback);

            // Hướng cơ chĩa từ đuôi tới đầu cơ (hướng ngược với aimDirection)
            Vector3 backward = -currentAimDirection;
            Vector3 ballPos = cueBallTarget.position;

            // Nâng cao đuôi cơ một góc cueElevationAngle để cơ tự nhiên trên bàn
            Quaternion elevationRot = Quaternion.AngleAxis(cueElevationAngle, Vector3.Cross(Vector3.up, currentAimDirection));
            Vector3 cueOffset = elevationRot * backward * distance;

            cueStickRoot.position = ballPos + cueOffset;

            // Xoay gậy cơ sao cho đầu cơ hướng thẳng vào tâm bi cái
            Vector3 lookDirection = (ballPos - cueStickRoot.position).normalized;
            cueStickRoot.rotation = Quaternion.LookRotation(lookDirection);
        }

        public void TriggerStrikeAnimation(System.Action onImpactCallback)
        {
            if (cueStickRoot == null)
            {
                onImpactCallback?.Invoke();
                return;
            }

            StartCoroutine(StrokeRoutine(onImpactCallback));
        }

        private IEnumerator StrokeRoutine(System.Action onImpactCallback)
        {
            isStriking = true;
            Vector3 startPos = cueStickRoot.position;
            Vector3 targetImpactPos = cueBallTarget != null
                ? cueBallTarget.position - (currentAimDirection * 0.02855f)
                : startPos + (currentAimDirection * (baseDistance + currentPower01 * maxPullback));

            float elapsed = 0f;
            while (elapsed < strokeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / strokeDuration);
                // Gia tốc khi cơ đẩy về phía trước (EaseInQuad)
                float curveT = t * t;
                cueStickRoot.position = Vector3.Lerp(startPos, targetImpactPos, curveT);
                yield return null;
            }

            // Thời điểm cơ chạm bi
            onImpactCallback?.Invoke();

            // Ẩn cơ khi bóng bắt đầu lăn
            yield return new WaitForSeconds(0.04f);
            SetAimingState(false);
            isStriking = false;
        }
    }
}
