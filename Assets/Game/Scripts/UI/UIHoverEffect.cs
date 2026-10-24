using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hiệu ứng hover cho nút UI: khi rê chuột vào nút phóng to nhẹ, dịch sang phải một chút và đổi sang màu nhấn
    /// (vàng cam mặc định). Dùng thời gian không bị ảnh hưởng bởi timeScale để vẫn hoạt động khi game tạm dừng.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float shiftRight = 10f;
        [SerializeField] private bool changeColor = true;
        [SerializeField] private Color hoverColor = new Color(1f, 0.62f, 0.12f, 1f);
        [SerializeField] private Color hoverFrameColor = new Color(1f, 0.86f, 0.45f, 1f);
        [SerializeField] private float speed = 14f;

        private RectTransform rt;
        private Graphic background;
        private BorderProgressGraphic frame;
        private Button button;

        private Color baseColor;
        private Color baseFrameColor;
        private Vector2 basePosition;
        private Vector3 baseScale = Vector3.one;

        private bool initialized;
        private bool hovered;
        private bool pressed;
        private bool selected;
        private float t; // 0 = bình thường, 1 = hover

        public static UIHoverEffect Attach(GameObject target, bool changeColor, Color? hoverColor = null, Color? hoverFrameColor = null, float shiftRight = 10f, float hoverScale = 1.06f)
        {
            var fx = target.GetComponent<UIHoverEffect>();
            if (fx == null) fx = target.AddComponent<UIHoverEffect>();
            fx.changeColor = changeColor;
            if (hoverColor.HasValue) fx.hoverColor = hoverColor.Value;
            if (hoverFrameColor.HasValue) fx.hoverFrameColor = hoverFrameColor.Value;
            fx.shiftRight = shiftRight;
            fx.hoverScale = hoverScale;
            fx.initialized = false;
            if (fx.isActiveAndEnabled)
            {
                fx.Init();
                fx.ResetVisual();
            }
            return fx;
        }

        /// <summary>
        /// Gắn hiệu ứng hover cho mọi Button trong scene (kể cả popup đang ẩn).
        /// Nút kiểu HUD vát góc: phóng to + dịch phải + vàng cam; nút khác: chỉ phóng to nhẹ.
        /// </summary>
        public static void AttachToSceneButtons()
        {
            foreach (Button b in FindObjectsOfType<Button>(true))
            {
                if (b == null) continue;

                if (b.targetGraphic is SkewedPanelGraphic)
                {
                    Attach(b.gameObject, true);
                }
                else
                {
                    Attach(b.gameObject, false, null, null, 0f, 1.05f);
                }
            }
        }

        private void Init()
        {
            if (initialized) return;
            initialized = true;

            rt = transform as RectTransform;
            button = GetComponent<Button>();
            background = (button != null && button.targetGraphic != null) ? button.targetGraphic : GetComponent<Graphic>();
            frame = GetComponentInChildren<BorderProgressGraphic>(true);

            baseScale = transform.localScale;
            basePosition = rt != null ? rt.anchoredPosition : Vector2.zero;
            if (background != null) baseColor = background.color;
            if (frame != null) baseFrameColor = frame.BaseColor;
        }

        private void OnEnable()
        {
            Init();
            ResetVisual();
        }

        private void OnDisable()
        {
            if (!initialized) return;
            hovered = false;
            pressed = false;
            ResetVisual();
        }

        /// <summary>Giữ nút ở trạng thái hover (vàng cam, phóng to) — dùng cho tab đang được chọn.</summary>
        public void SetSelected(bool value)
        {
            selected = value;
        }

        private void ResetVisual()
        {
            if (selected) return;
            // Chỉ trả vị trí về gốc nếu đang bị dịch do hover (tránh ghi đè vị trí do layout sắp xếp)
            if (t > 0f && rt != null) rt.anchoredPosition = basePosition;
            t = 0f;
            transform.localScale = baseScale;
            if (changeColor)
            {
                if (background != null) background.color = baseColor;
                if (frame != null) frame.SetBaseColor(baseFrameColor);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            Init();
            if (!hovered)
            {
                // Lấy lại vị trí gốc tại thời điểm hover (layout có thể đã sắp xếp lại nút)
                if (t <= 0.001f && rt != null) basePosition = rt.anchoredPosition;
            }
            hovered = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) { pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private bool IsInteractable()
        {
            return button == null || button.interactable;
        }

        private void Update()
        {
            if (!initialized) return;

            bool active = (hovered || selected) && IsInteractable();
            float target = active ? 1f : 0f;
            if (Mathf.Approximately(t, target) && !active)
            {
                return;
            }

            t = Mathf.MoveTowards(t, target, Time.unscaledDeltaTime * speed);
            float e = t * t * (3f - 2f * t); // smoothstep

            float scale = Mathf.Lerp(1f, hoverScale, e) * (pressed ? 0.97f : 1f);
            transform.localScale = baseScale * scale;
            if (rt != null) rt.anchoredPosition = basePosition + new Vector2(shiftRight * e, 0f);

            if (changeColor)
            {
                if (background != null) background.color = Color.Lerp(baseColor, hoverColor, e);
                if (frame != null) frame.SetBaseColor(Color.Lerp(baseFrameColor, hoverFrameColor, e));
            }
        }
    }
}
