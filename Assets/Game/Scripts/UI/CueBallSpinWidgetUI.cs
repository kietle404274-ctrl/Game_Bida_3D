using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hình bi cái trên HUD: bấm hoặc kéo để chọn điểm cơ chạm (cu-lê, trô, ép-phê trái/phải).
    /// Nhấp đúp để đưa về giữa.
    /// </summary>
    public class CueBallSpinWidgetUI : MonoBehaviour, IPointerDownHandler, IDragHandler,
        IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CueBallContactSelector selector;
        [SerializeField] private RectTransform dot;
        [SerializeField] private Text valueText;
        [SerializeField] private float dotRange = 0.85f;

        private RectTransform rect;
        private bool pointerInside;
        private bool dragging;

        /// <summary>True khi chuột đang ở trên hoặc đang kéo widget (để chặn xoay ngắm và bắn).</summary>
        public static bool IsHovered { get; private set; }

        private void Awake()
        {
            rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (selector != null)
            {
                selector.OnContactPointChanged += Refresh;
                selector.OnSelectorToggled += HandleToggled;
                Refresh(selector.CurrentContact);
            }
        }

        private void OnDisable()
        {
            if (selector != null)
            {
                selector.OnContactPointChanged -= Refresh;
                selector.OnSelectorToggled -= HandleToggled;
            }
            pointerInside = false;
            dragging = false;
            UpdateHover();
        }

        private void HandleToggled(bool open)
        {
            if (open) return;
            pointerInside = false;
            dragging = false;
            UpdateHover();
        }

        public void OnPointerEnter(PointerEventData e) { pointerInside = true; UpdateHover(); }
        public void OnPointerExit(PointerEventData e) { pointerInside = false; UpdateHover(); }
        public void OnPointerUp(PointerEventData e) { dragging = false; UpdateHover(); }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            dragging = true;
            UpdateHover();
            if (e.clickCount >= 2 && selector != null) { selector.ResetToCenter(); return; }
            Pick(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Pick(e);
        }

        private void Pick(PointerEventData e)
        {
            if (selector == null || rect == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, e.pressEventCamera, out Vector2 local)) return;
            float radius = rect.rect.width * 0.5f;
            if (radius <= 0f) return;
            Vector2 c = Vector2.ClampMagnitude(local / radius / dotRange, 1f);
            selector.SetContact(c);
        }

        private void Refresh(Vector2 contact)
        {
            if (rect == null) rect = (RectTransform)transform;
            if (dot != null) dot.anchoredPosition = contact * (rect.rect.width * 0.5f * dotRange);
            if (valueText != null) valueText.text = Describe(contact);
        }

        private static string Describe(Vector2 c)
        {
            const float t = 0.15f;
            if (c.magnitude < t) return "Giữa";
            string v = c.y > t ? "Cu-lê" : (c.y < -t ? "Trô" : "");
            string h = c.x > t ? "Phải" : (c.x < -t ? "Trái" : "");
            if (v.Length > 0 && h.Length > 0) return v + " + " + h;
            return v.Length > 0 ? v : h;
        }

        private void UpdateHover()
        {
            IsHovered = pointerInside || dragging;
        }
    }
}
