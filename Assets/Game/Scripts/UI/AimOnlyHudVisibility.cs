using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>Chỉ hiện panel khi người chơi đang được phép ngắm bắn.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AimOnlyHudVisibility : MonoBehaviour
    {
        /// <summary>Cho phép hiển thị riêng thanh lực (PowerBarUI) trong pha Đua Bi Băng Ngắn (Lag Shot).</summary>
        public static bool ForceShowPowerBar { get; set; }

        private CanvasGroup group;
        private Input.PlayerInputController input;
        private bool isPowerBarElement;
        private float nextSearchTime;
        private bool hasAppliedState;
        private bool lastShowState;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            isPowerBarElement = GetComponent<PowerBarUI>() != null || GetComponentInChildren<PowerBarUI>(true) != null;
        }

        private void Update()
        {
            if (input == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 0.5f;
                input = FindObjectOfType<Input.PlayerInputController>();
            }
            bool show = (isPowerBarElement && ForceShowPowerBar) || input == null || input.IsAimingAllowed;
            if (!hasAppliedState || lastShowState != show)
            {
                hasAppliedState = true;
                lastShowState = show;
                group.alpha = show ? 1f : 0f;
                group.interactable = show;
                group.blocksRaycasts = show;
            }
        }
    }
}
