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

        public bool IsVisible => cueStickRoot != null && cueStickRoot.gameObject.activeSelf;

        public void Initialize(Transform cueStick, Transform cueBall)
        {
            cueStickRoot = cueStick;
            cueBallTarget = cueBall;
            SetAimingState(true);
        }

        public void SetCueBallTarget(Transform target)
        {
            cueBallTarget = target;
        }

        public void SetAimingState(bool aiming)
        {
            isAiming = aiming;
            if (cueStickRoot != null)
            {
                cueStickRoot.gameObject.SetActive(aiming);
            }
        }

        public void UpdateAim(Vector3 aimDirection, float power01)
        {
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
