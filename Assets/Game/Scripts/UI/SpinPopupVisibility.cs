using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>Popup chọn điểm cơ: hiện giữa màn hình khi bộ chọn mở (phím J), ẩn khi xác nhận (Enter).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SpinPopupVisibility : MonoBehaviour
    {
        [SerializeField] private CueBallContactSelector selector;
        private CanvasGroup group;
        private float nextSearchTime;
        private bool hasAppliedState;
        private bool lastOpenState;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            Apply(false, true);
        }

        private void Update()
        {
            if (selector == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 0.5f;
                selector = FindObjectOfType<CueBallContactSelector>();
            }
            Apply(selector != null && selector.IsOpen, false);
        }

        private void Apply(bool open, bool force)
        {
            if (!force && hasAppliedState && lastOpenState == open) return;
            hasAppliedState = true;
            lastOpenState = open;
            group.alpha = open ? 1f : 0f;
            group.interactable = open;
            group.blocksRaycasts = open;
        }
    }
}
