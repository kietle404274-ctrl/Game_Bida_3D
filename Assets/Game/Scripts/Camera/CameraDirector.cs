using UnityEngine;

namespace Game.Billiards.Camera
{
    public enum CameraState
    {
        Aim,
        ShotWide,
        BallInHand,
        Cinematic,
        ShotFollow,
        OpponentView,
        TopDown
    }

    public class CameraDirector : MonoBehaviour
    {
        [Header("Target & References")]
        [Tooltip("Transform cá»§a bi cÃ¡i Ä‘á»ƒ camera ngáº¯m bÃ¡m theo")]
        [SerializeField] private Transform cueBallTarget;

        [Tooltip("Camera chÃ­nh")]
        [SerializeField] private UnityEngine.Camera mainCamera;

        [Header("Aim Camera Settings")]
        [Tooltip("Khoáº£ng cÃ¡ch tá»« camera Ä‘áº¿n bi cÃ¡i khi ngáº¯m")]
        [SerializeField] private float aimDistance = 1.15f;

        [Tooltip("Khoảng cách camera đến bi cái khi bật chế độ ngắm gần (phím V)")]
        [SerializeField] private float aimCloseDistance = 0.55f;

        // Static: giữ trạng thái V qua các lượt/ván trong cùng phiên chơi, không lưu ra ổ đĩa. Mặc định: góc nhìn xa.
        private static bool closeAim = false;
        public bool IsCloseAim => closeAim;
        public void ToggleCloseAim() { closeAim = !closeAim; }

        [Tooltip("Äá»™ cao bÃ¹ thÃªm so vá»›i tÃ¢m bi")]
        [SerializeField] private float aimHeightOffset = 0.08f;

        [Tooltip("GÃ³c Pitch tá»‘i thiá»ƒu (Ä‘á»™) Ä‘á»ƒ khÃ´ng xuyÃªn máº·t bÃ n")]
        [SerializeField] private float minPitch = 8f;

        [Tooltip("GÃ³c Pitch tá»‘i Ä‘a (Ä‘á»™)")]
        [SerializeField] private float maxPitch = 55f;

        [Tooltip("Äá»™ nháº¡y xoay ngáº¯m cá»§a chuá»™t")]
        [SerializeField] private float mouseSensitivity = 2.5f;

        [Tooltip("Tá»‘c Ä‘á»™ mÆ°á»£t mÃ  khi bÃ¡m vá»‹ trÃ­")]
        [SerializeField] private float smoothSpeed = 12f;

        [Header("Wide Shot Camera Settings")]
        [Tooltip("Vá»‹ trÃ­ camera gÃ³c rá»™ng theo dÃµi toÃ n bÃ n khi bi Ä‘ang cháº¡y")]
        [SerializeField] private Vector3 wideCameraPosition = new Vector3(0f, 3.2f, -1.5f);

        [Tooltip("Äiá»ƒm nhÃ¬n cá»§a camera gÃ³c rá»™ng (thÆ°á»ng lÃ  tÃ¢m bÃ n bida)")]
        [SerializeField] private Vector3 tableCenter = new Vector3(0f, 0.835f, 0f);

        [Tooltip("Tá»‘c Ä‘á»™ chuyá»ƒn Ä‘á»•i giá»¯a cÃ¡c gÃ³c camera")]
        [SerializeField] private float transitionSpeed = 4.0f;

        [Header("Shot Follow Camera Settings")]
        [Tooltip("Khoáº£ng cÃ¡ch camera Ä‘áº¿n bi cÃ¡i sau khi Ä‘Ã¡nh (xa hÆ¡n lÃºc ngáº¯m)")]
        [SerializeField] private float shotFollowDistance = 1.25f;

        [Tooltip("GÃ³c nghiÃªng xuá»‘ng (Ä‘á»™) cá»§a camera sau khi Ä‘Ã¡nh")]
        [SerializeField] private float shotFollowPitch = 27f;

        [Tooltip("Tá»‘c Ä‘á»™ mÆ°á»£t khi camera lÃ¹i ra vá»‹ trÃ­ cá»‘ Ä‘á»‹nh sau khi Ä‘Ã¡nh")]
        [SerializeField] private float followSmoothSpeed = 5f;

        [Header("Opponent View Camera Settings")]
        [Tooltip("True: camera Ä‘áº·t á»Ÿ cuá»‘i bÃ n phÃ­a bÄƒng ngáº¯n Foot (-X); False: phÃ­a bÄƒng ngáº¯n Head (+X)")]
        [SerializeField] private bool opponentViewFromFootEnd = true;

        [Tooltip("Ná»­a kÃ­ch thÆ°á»›c máº·t bÃ n cáº§n luÃ´n náº±m trong khung hÃ¬nh (X = chiá»u dÃ i, Z = chiá»u rá»™ng, gá»“m cáº£ bÄƒng vÃ  lá»—)")]
        [SerializeField] private Vector2 tableFitHalfSize = new Vector2(1.20f, 0.63f);

        [Tooltip("Lá» an toÃ n (0..0.5, tÃ­nh theo ná»­a khung hÃ¬nh) Ä‘á»ƒ máº·t bÃ n khÃ´ng sÃ¡t mÃ©p mÃ n hÃ¬nh/HUD")]
        [SerializeField] private float opponentViewMargin = 0.12f;

        [Tooltip("Tá»‘c Ä‘á»™ mÆ°á»£t khi chuyá»ƒn sang camera lÆ°á»£t Ä‘á»‘i thá»§")]
        [SerializeField] private float opponentTransitionSpeed = 3f;

        [Tooltip("Äá»™ lá»‡ch camera lÆ°á»£t Ä‘á»‘i thá»§ so vá»›i tÃ¢m bÃ n theo trá»¥c dÃ i X (giá»¯ camera náº±m trong phÃ²ng). Dáº¥u tá»± Ä‘á»•i theo Ä‘áº§u bÃ n")]
        [SerializeField] private float opponentCamOffsetX = 2.0f;

        [Tooltip("Äá»™ cao camera lÆ°á»£t Ä‘á»‘i thá»§ (cÃ¹ng má»©c camera gÃ³c rá»™ng Ä‘Ã£ xÃ¡c nháº­n náº±m trong phÃ²ng)")]
        [SerializeField] private float opponentCamHeight = 2.5f;

        [Header("Top-Down (Ball In Hand) Camera Settings")]
        [Tooltip("Äá»™ cao camera nhÃ¬n tháº³ng tá»« trÃªn xuá»‘ng so vá»›i bi cÃ¡i khi Ä‘áº·t bi")]
        [SerializeField] private float topDownHeight = 1.6f;

        [Tooltip("Tá»‘c Ä‘á»™ camera bÃ¡m theo bi cÃ¡i khi Ä‘áº·t bi")]
        [SerializeField] private float topDownFollowSpeed = 12f;

        private float topDownYaw = 0f;

        private CameraState currentState = CameraState.Aim;
        private float currentYaw = 0f;
        private float currentPitch = 18f;

        // Camera sau cÃº Ä‘Ã¡nh: chá»‘t vá»‹ trÃ­ neo má»™t láº§n lÃºc Ä‘Ã¡nh rá»“i Ä‘á»©ng yÃªn, khÃ³a hÆ°á»›ng theo hÆ°á»›ng Ä‘Ã¡nh
        private Vector3 followAnchorPosition;
        private Quaternion followAnchorRotation;

        // Camera lÆ°á»£t Ä‘á»‘i thá»§: vá»‹ trÃ­/gÃ³c Ä‘Æ°á»£c tÃ­nh má»™t láº§n khi chuyá»ƒn, sau Ä‘Ã³ Ä‘á»©ng yÃªn
        private Vector3 opponentViewPosition;
        private Quaternion opponentViewRotation;

        // Camera Micro-Shake (M10)
        private float shakeTimer = 0f;
        private float totalShakeDuration = 0f;
        private float currentShakeIntensity = 0f;

        public CameraState CurrentState => currentState;
        public UnityEngine.Camera MainCamera => mainCamera;

        /// <summary>
        /// HÆ°á»›ng ngáº¯m cÆ¡ trÃªn máº·t pháº³ng náº±m ngang XZ (hÆ°á»›ng tá»« camera tá»›i bi cÃ¡i)
        /// </summary>
        public Vector3 AimDirection
        {
            get
            {
                Quaternion rot = Quaternion.Euler(0f, currentYaw, 0f);
                return (rot * Vector3.forward).normalized;
            }
        }

        private void Awake()
        {
            if (mainCamera == null)
            {
                mainCamera = UnityEngine.Camera.main;
            }
        }

        private void Start()
        {
            if (cueBallTarget == null)
            {
                var ballMgr = FindObjectOfType<Match.BallManager>();
                if (ballMgr != null && ballMgr.CueBall != null)
                {
                    SetCueBallTarget(ballMgr.CueBall.transform);
                }
                else
                {
                    var whiteBall = GameObject.Find("GameBall_00");
                    if (whiteBall != null) SetCueBallTarget(whiteBall.transform);
                }
            }
        }

        public void SetCueBallTarget(Transform target)
        {
            cueBallTarget = target;
            if (target != null)
            {
                // HÆ°á»›ng ngáº¯m máº·c Ä‘á»‹nh ban Ä‘áº§u hÆ°á»›ng vá» tÃ¢m bÃ n
                Vector3 toCenter = (tableCenter - target.position);
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude > 0.01f)
                {
                    currentYaw = Quaternion.LookRotation(toCenter).eulerAngles.y;
                }
            }
        }

        public void RotateAim(float mouseDeltaX, float mouseDeltaY)
        {
            if (currentState != CameraState.Aim) return;

            currentYaw += mouseDeltaX * mouseSensitivity;
            currentPitch -= mouseDeltaY * mouseSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        public void SwitchToShotCamera()
        {
            currentState = CameraState.ShotWide;
        }

        /// <summary>
        /// Chế độ điện ảnh: CameraDirector không tự điều khiển camera, script bên ngoài (RackIntroSequence) toàn quyền.
        /// </summary>
        public void SwitchToCinematicCamera()
        {
            currentState = CameraState.Cinematic;
        }

        /// <summary>
        /// Sau khi Ä‘Ã¡nh: camera lÃ¹i ra phÃ­a sau vá»‹ trÃ­ bi cÃ¡i lÃºc Ä‘Ã¡nh, theo Ä‘Ãºng hÆ°á»›ng vá»«a Ä‘Ã¡nh, cao hÆ¡n vÃ  xa hÆ¡n.
        /// Vá»‹ trÃ­ Ä‘Æ°á»£c chá»‘t má»™t láº§n rá»“i Ä‘á»©ng yÃªn (khÃ´ng bÃ¡m theo bi cÃ¡i) Ä‘á»ƒ ngÆ°á»i chÆ¡i khÃ´ng bá»‹ loáº¡n gÃ³c nhÃ¬n.
        /// </summary>
        public void SwitchToShotFollowCamera(Vector3 shotDirection)
        {
            Vector3 flat = new Vector3(shotDirection.x, 0f, shotDirection.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                currentYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            }

            if (cueBallTarget != null)
            {
                Vector3 focusPoint = cueBallTarget.position + (Vector3.up * aimHeightOffset);
                followAnchorRotation = Quaternion.Euler(shotFollowPitch, currentYaw, 0f);
                followAnchorPosition = focusPoint - (followAnchorRotation * Vector3.forward * shotFollowDistance);

                if (followAnchorPosition.y < tableCenter.y + 0.15f)
                {
                    followAnchorPosition.y = tableCenter.y + 0.15f;
                }
            }
            else
            {
                followAnchorPosition = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
                followAnchorRotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.identity;
            }

            currentState = CameraState.ShotFollow;
        }

        public void SwitchToAimCamera()
        {
            currentState = CameraState.Aim;
        }

        /// <summary>
        /// Äáº·t bi: camera nhÃ¬n tháº³ng tá»« trÃªn xuá»‘ng vÃ  di chuyá»ƒn theo bi cÃ¡i. Trá»¥c dÃ i bÃ n náº±m ngang mÃ n hÃ¬nh.
        /// </summary>
        public void SwitchToTopDownCamera()
        {
            topDownYaw = currentYaw;
            currentState = CameraState.TopDown;
        }

        /// <summary>
        /// Xoay camera nhÃ¬n tá»« trÃªn xuá»‘ng quanh trá»¥c tháº³ng Ä‘á»©ng theo chuá»™t khi Ä‘áº·t bi.
        /// </summary>
        public void RotateTopDown(float mouseDeltaX)
        {
            if (currentState != CameraState.TopDown) return;
            topDownYaw += mouseDeltaX * mouseSensitivity;
        }

        /// <summary>
        /// LÆ°á»£t Ä‘á»‘i thá»§: camera Ä‘áº·t á»Ÿ cuá»‘i bÃ n (phÃ­a sau bÄƒng ngáº¯n), nhÃ¬n dá»c theo chiá»u dÃ i bÃ n,
        /// khoáº£ng cÃ¡ch Ä‘Æ°á»£c tá»± tÃ­nh Ä‘á»ƒ toÃ n bá»™ máº·t bÃ n náº±m trá»n trong khung hÃ¬nh. Camera Ä‘á»©ng yÃªn vÃ  khÃ´ng cho xoay.
        /// </summary>
        public void SwitchToOpponentCamera()
        {
            // Vá»‹ trÃ­ cá»‘ Ä‘á»‹nh trong phÃ²ng á»Ÿ cuá»‘i bÃ n, nhÃ¬n tháº³ng vÃ o tÃ¢m bÃ n
            float sign = opponentViewFromFootEnd ? -1f : 1f;
            opponentViewPosition = new Vector3(tableCenter.x + sign * opponentCamOffsetX, opponentCamHeight, tableCenter.z);
            opponentViewRotation = Quaternion.LookRotation(tableCenter - opponentViewPosition, Vector3.up);

            currentState = CameraState.OpponentView;
        }

        /// <summary>
        /// TÃ¬m khoáº£ng cÃ¡ch nhá» nháº¥t tá»« tÃ¢m bÃ n sao cho 8 gÃ³c máº·t bÃ n (kÃ¨m lá») náº±m trong khung hÃ¬nh thá»±c cá»§a camera.
        /// </summary>
        private float ComputeFitDistance(Quaternion rotation)
        {
            const float fallback = 3.6f;
            if (mainCamera == null) return fallback;

            Matrix4x4 projection;
            try
            {
                float aspect = mainCamera.aspect > 0.01f ? mainCamera.aspect : (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
                projection = Matrix4x4.Perspective(mainCamera.fieldOfView, aspect, 0.1f, 100f);
            }
            catch (System.Exception)
            {
                return fallback;
            }
            float limit = Mathf.Clamp01(1f - opponentViewMargin * 2f);
            Vector3 forward = rotation * Vector3.forward;

            for (float d = 1.2f; d <= 10f; d += 0.02f)
            {
                Vector3 camPos = tableCenter - (forward * d);
                Matrix4x4 worldToCamera = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.TRS(camPos, rotation, Vector3.one).inverse;
                Matrix4x4 viewProjection = projection * worldToCamera;

                bool allInside = true;
                for (int ix = -1; ix <= 1 && allInside; ix += 2)
                {
                    for (int iz = -1; iz <= 1 && allInside; iz += 2)
                    {
                        Vector3 corner = new Vector3(tableCenter.x + ix * tableFitHalfSize.x, tableCenter.y, tableCenter.z + iz * tableFitHalfSize.y);
                        Vector4 clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1f);
                        if (clip.w <= 0.01f ||
                            Mathf.Abs(clip.x / clip.w) > limit ||
                            Mathf.Abs(clip.y / clip.w) > limit)
                        {
                            allInside = false;
                        }
                    }
                }

                if (allInside) return d;
            }

            return fallback;
        }

        public void TriggerCameraShake(float duration = 0.22f, float intensity = 0.04f)
        {
            shakeTimer = duration;
            totalShakeDuration = duration;
            currentShakeIntensity = intensity;
        }

        private void LateUpdate()
        {
            if (mainCamera == null) return;

            switch (currentState)
            {
                case CameraState.Aim:
                    UpdateAimCamera();
                    break;

                case CameraState.ShotWide:
                    UpdateWideShotCamera();
                    break;

                case CameraState.ShotFollow:
                    UpdateShotFollowCamera();
                    break;

                case CameraState.OpponentView:
                    UpdateOpponentViewCamera();
                    break;

                case CameraState.TopDown:
                    UpdateTopDownCamera();
                    break;
            }

            // Ãp dá»¥ng Camera Micro-Shake náº¿u Ä‘ang kÃ­ch hoáº¡t (M10)
            if (shakeTimer > 0f && totalShakeDuration > 0f)
            {
                shakeTimer -= Time.deltaTime;
                float progress = Mathf.Clamp01(shakeTimer / totalShakeDuration);
                float currentIntensity = currentShakeIntensity * progress;

                Vector3 shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * 55f) * currentIntensity,
                    Mathf.Cos(Time.time * 45f) * currentIntensity,
                    0f
                );
                mainCamera.transform.position += shakeOffset;
            }
        }

        private void UpdateAimCamera()
        {
            if (cueBallTarget == null) return;

            Vector3 focusPoint = cueBallTarget.position + (Vector3.up * aimHeightOffset);
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * (closeAim ? aimCloseDistance : aimDistance));

            // Giá»¯ camera luÃ´n á»Ÿ trÃªn máº·t ná»‰ Ã­t nháº¥t 0.15m Ä‘á»ƒ khÃ´ng bao giá» cáº¯t qua bÃ n
            if (desiredPosition.y < tableCenter.y + 0.15f)
            {
                desiredPosition.y = tableCenter.y + 0.15f;
            }

            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desiredPosition, Time.deltaTime * smoothSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, Quaternion.LookRotation(focusPoint - mainCamera.transform.position), Time.deltaTime * smoothSpeed);
        }

        private void UpdateWideShotCamera()
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, wideCameraPosition, Time.deltaTime * transitionSpeed);
            Quaternion lookRot = Quaternion.LookRotation(tableCenter - mainCamera.transform.position);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, lookRot, Time.deltaTime * transitionSpeed);
        }

        private void UpdateShotFollowCamera()
        {
            // Chá»‰ lÆ°á»›t mÆ°á»£t tá»›i Ä‘iá»ƒm neo Ä‘Ã£ chá»‘t rá»“i Ä‘á»©ng yÃªn, khÃ´ng theo bi cÃ¡i
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, followAnchorPosition, Time.deltaTime * followSmoothSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, followAnchorRotation, Time.deltaTime * followSmoothSpeed);
        }

        private void UpdateTopDownCamera()
        {
            if (cueBallTarget == null) return;

            Vector3 desired = cueBallTarget.position + Vector3.up * topDownHeight;
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, desired, Time.deltaTime * topDownFollowSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, Quaternion.Euler(90f, topDownYaw, 0f), Time.deltaTime * topDownFollowSpeed);
        }

        private void UpdateOpponentViewCamera()
        {
            mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, opponentViewPosition, Time.deltaTime * opponentTransitionSpeed);
            mainCamera.transform.rotation = Quaternion.Slerp(mainCamera.transform.rotation, opponentViewRotation, Time.deltaTime * opponentTransitionSpeed);
        }
    }
}

