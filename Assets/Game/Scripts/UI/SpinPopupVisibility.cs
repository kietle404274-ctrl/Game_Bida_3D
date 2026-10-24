using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>Popup chọn điểm cơ: hiện giữa màn hình khi bộ chọn mở (phím J), ẩn khi xác nhận (Enter).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SpinPopupVisibility : MonoBehaviour
    {
        [SerializeField] private CueBallContactSelector selector;
        private CanvasGroup group;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            Apply(false);
        }

        private void Update()
        {
            if (selector == null) selector = FindObjectOfType<CueBallContactSelector>();
            Apply(selector != null && selector.IsOpen);
        }

        private void Apply(bool open)
        {
            group.alpha = open ? 1f : 0f;
            group.interactable = open;
            group.blocksRaycasts = open;
        }
    }
}
