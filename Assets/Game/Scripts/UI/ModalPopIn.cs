using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>Hiệu ứng mở popup: mờ dần + phóng từ 0.94 lên 1 (dùng thời gian không phụ thuộc timeScale).</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ModalPopIn : MonoBehaviour
    {
        private const float Duration = 0.18f;
        private CanvasGroup group;
        private float t;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            t = 0f;
            Apply();
        }

        private void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.unscaledDeltaTime / Duration);
            Apply();
        }

        private void Apply()
        {
            float e = 1f - (1f - t) * (1f - t);
            group.alpha = e;
            float s = Mathf.Lerp(0.94f, 1f, e);
            transform.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Viền ô nhập đổi sang màu nhấn khi đang gõ.</summary>
    public class InputFocusOutline : MonoBehaviour
    {
        private InputField field;
        private BorderProgressGraphic frame;
        private Color idle;
        private Color focus;
        private Color current;

        public void Init(InputField input, BorderProgressGraphic border, Color idleColor, Color focusColor)
        {
            field = input;
            frame = border;
            idle = idleColor;
            focus = focusColor;
            current = idleColor;
            if (frame != null) frame.SetBaseColor(idle);
        }

        private void Update()
        {
            if (field == null || frame == null) return;
            Color target = field.isFocused ? focus : idle;
            if (current == target) return;
            current = Color.Lerp(current, target, Time.unscaledDeltaTime * 12f);
            frame.SetBaseColor(current);
        }
    }
}
