using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhysicsConfig = Game.Billiards.Physics.PhysicsConfig;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Bộ chọn góc nâng cơ (nhảy bi). Phím K mở popup, kéo cơ quanh bi (0 đến MaxAngleDeg), Enter chốt.
    /// Góc chỉ có hiệu lực cho 1 cú đánh và được reset về 0 sau mỗi cú.
    /// UI (panel nhỏ + popup) được dựng tự động lúc chạy bởi <see cref="CueElevationUI"/>, không cần sửa scene.
    /// </summary>
    public class CueElevationSelector : MonoBehaviour
    {
        [Tooltip("Góc nâng cơ tối đa cho phép chọn (độ). Khớp với PhysicsConfig.MaxCueElevation")]
        [SerializeField] private float maxAngleDeg = 60f;

        [Tooltip("Tốc độ chỉnh góc khi giữ phím mũi tên (độ/giây)")]
        [SerializeField] private float keyAdjustSpeed = 35f;

        private float currentAngleDeg;
        private bool isOpen;
        private Input.PlayerInputController inputController;

        public static CueElevationSelector Instance { get; private set; }

        public float CurrentAngleDeg => currentAngleDeg;
        public float MaxAngleDeg => maxAngleDeg;
        public bool IsOpen => isOpen;

        public event Action<float> OnAngleChanged;
        public event Action<bool> OnSelectorToggled;

        #region Bootstrap

        private static bool watcherCreated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            watcherCreated = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWatcher()
        {
            if (watcherCreated) return;
            watcherCreated = true;
            var go = new GameObject("CueElevationBootstrap");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<Watcher>();
        }

        /// <summary>Theo dõi: khi scene trận đấu có HUD điểm cơ (SpinPopupVisibility) mà chưa có bộ chọn góc thì tạo.</summary>
        private class Watcher : MonoBehaviour
        {
            private float nextCheck;

            private void Update()
            {
                if (Instance != null) return;
                if (Time.unscaledTime < nextCheck) return;
                nextCheck = Time.unscaledTime + 0.5f;

                var anchor = FindObjectOfType<SpinPopupVisibility>(true);
                if (anchor == null) return;

                var go = new GameObject("CueElevationSystem");
                go.AddComponent<CueElevationSelector>();
            }
        }

        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private bool uiBuilt;

        /// <summary>
        /// Dựng ngay UI chọn góc cơ (nếu chưa có) để các hiệu ứng HUD (trượt vào khi vào trận) bao gồm cả panel này,
        /// thay vì đợi Watcher/Start tạo muộn hơn.
        /// </summary>
        public static void EnsureBuilt()
        {
            if (Instance == null)
            {
                if (FindObjectOfType<SpinPopupVisibility>(true) == null) return;
                new GameObject("CueElevationSystem").AddComponent<CueElevationSelector>();
            }
            if (Instance != null) Instance.BuildUiOnce();
        }

        private void BuildUiOnce()
        {
            if (uiBuilt || Instance != this) return;
            uiBuilt = true;
            CueElevationUI.Build(this);
        }

        private void Start()
        {
            BuildUiOnce();
        }

        private void OnDestroy()
        {
            HudModalState.Release("elevation");
            if (Instance == this) Instance = null;
        }

        public void SetOpen(bool open)
        {
            if (open == isOpen) return;
            if (open)
            {
                if (!HudModalState.TryOpen("elevation")) return;
            }
            else
            {
                HudModalState.Release("elevation");
            }
            isOpen = open;
            OnSelectorToggled?.Invoke(isOpen);
        }

        public void SetAngle(float degrees)
        {
            float clamped = Mathf.Clamp(degrees, 0f, maxAngleDeg);
            if (Mathf.Approximately(clamped, currentAngleDeg)) return;
            currentAngleDeg = clamped;
            ApplyToCue();
            OnAngleChanged?.Invoke(currentAngleDeg);
        }

        public void ResetAngle()
        {
            SetAngle(0f);
        }

        /// <summary>Cây cơ 3D nâng theo góc đã chọn ngay lập tức (kể cả khi popup đang mở).</summary>
        private void ApplyToCue()
        {
            var cue = FindObjectOfType<Physics.CueController>();
            if (cue == null) return;
            cue.SetElevation(currentAngleDeg);

            var cam = FindObjectOfType<Camera.CameraDirector>();
            if (inputController == null) inputController = FindObjectOfType<Input.PlayerInputController>();
            if (cam != null && inputController != null)
            {
                cue.UpdateAim(cam.AimDirection, inputController.CurrentPower01);
            }
        }

        private void Update()
        {
            if (!isOpen)
            {
                // Phím K mở popup chọn góc nâng (chỉ khi đang được phép ngắm)
                if (UnityEngine.Input.GetKeyDown(KeyCode.K))
                {
                    if (inputController == null) inputController = FindObjectOfType<Input.PlayerInputController>();
                    if (inputController == null || inputController.IsAimingAllowed) SetOpen(true);
                }
                return;
            }

            // Enter: chốt và quay lại thao tác bình thường
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SetOpen(false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                ResetAngle();
                return;
            }

            float dir = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) dir += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) dir -= 1f;
            if (Mathf.Abs(dir) > 0.01f)
            {
                SetAngle(currentAngleDeg + dir * keyAdjustSpeed * Time.unscaledDeltaTime);
            }
        }

        /// <summary>
        /// Dự đoán cú nhảy theo cùng công thức với ShotExecutor (bỏ qua ma sát không khí và nảy sau khi hạ cánh).
        /// Trả về false nếu góc chưa đủ để bi nhảy. height = độ cao tối đa của tâm bi (m), range = tầm bay ngang tới lần chạm nỉ đầu tiên (m).
        /// </summary>
        public static bool PredictJump(PhysicsConfig cfg, float angleDeg, float power01, Vector2 contact, out float height, out float range)
        {
            height = 0f;
            range = 0f;
            if (cfg == null) return false;

            float angle = Mathf.Clamp(angleDeg, 0f, cfg.MaxCueElevation);
            if (angle < cfg.MinJumpAngle) return false;

            Vector2 b = Vector2.ClampMagnitude(contact, 1f) * cfg.MaxContactOffset;
            float v0 = Mathf.Clamp01(power01) * cfg.MaxShotImpulse / cfg.BallMass;
            v0 /= 1f + cfg.OffsetSpeedLoss * b.sqrMagnitude;

            float rad = angle * Mathf.Deg2Rad;
            float vUp = Mathf.Min(v0 * Mathf.Sin(rad) * cfg.JumpGain, cfg.MaxJumpSpeed);
            float vH = v0 * Mathf.Cos(rad);
            const float g = 9.81f;

            height = vUp * vUp / (2f * g);
            range = vH * (2f * vUp / g);
            return true;
        }
    }
}
