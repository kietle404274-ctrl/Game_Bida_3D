using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hiệu ứng cho nút lớn ở màn hình kết thúc trận (Chơi lại / Về sảnh): trượt từ dưới lên có trễ,
    /// hover viền cam + sáng lên + phóng to, nhấn lún xuống, vệt sáng quét ngang định kỳ, nhịp sáng nhẹ (tuỳ chọn).
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ResultButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public BorderProgressGraphic frame;
        public Graphic body;
        public Graphic accent;
        public RectTransform shine;
        public RectTransform shineClip;

        public Color frameBase = new Color(0.30f, 0.78f, 0.95f, 0.75f);
        public Color frameHover = new Color(1f, 0.74f, 0.20f, 1f);
        public Color accentBase = new Color(1f, 0.8f, 0.2f, 1f);
        public Color accentHover = new Color(1f, 0.62f, 0.12f, 1f);
        public Color bodyBase = Color.gray;
        public Color bodyHover = Color.white;

        public float entranceDelay = 0.35f;
        public bool pulse;

        private CanvasGroup group;
        private RectTransform rt;
        private Vector2 restPos;
        private bool hovered, pressed, restCaptured;
        private float hoverT;
        private float shineTimer;
        private float shineProgress = 1f;
        private float entranceClock;
        private bool entering;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rt = transform as RectTransform;
        }

        private void OnEnable()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            if (rt == null) rt = transform as RectTransform;

            if (!restCaptured) { restPos = rt.anchoredPosition; restCaptured = true; }
            rt.anchoredPosition = restPos + new Vector2(0f, -70f);
            group.alpha = 0f;
            entranceClock = 0f;
            entering = true;
            hovered = pressed = false;
            hoverT = 0f;
            shineTimer = 1.4f;
            shineProgress = 1f;
            transform.localScale = Vector3.one;
        }

        public void OnPointerEnter(PointerEventData e) { hovered = true; if (shineProgress >= 1f) shineProgress = 0f; }
        public void OnPointerExit(PointerEventData e) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData e) { pressed = true; }
        public void OnPointerUp(PointerEventData e) { pressed = false; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Trượt vào
            float slide = 1f;
            if (entering)
            {
                entranceClock += dt;
                float t = Mathf.Clamp01((entranceClock - entranceDelay) / 0.5f);
                float p = t - 1f;
                slide = 1f + 2.2f * p * p * p + 1.2f * p * p; // ease-out-back
                rt.anchoredPosition = restPos + new Vector2(0f, Mathf.LerpUnclamped(-70f, 0f, slide));
                group.alpha = Mathf.Clamp01(t * 1.6f);
                if (t >= 1f) { entering = false; rt.anchoredPosition = restPos; group.alpha = 1f; }
            }

            // Hover / nhấn
            hoverT = Mathf.MoveTowards(hoverT, hovered ? 1f : 0f, dt * 10f);
            float e = hoverT * hoverT * (3f - 2f * hoverT);

            float pulseK = (pulse && !hovered) ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.6f) : 0f;
            float scale = Mathf.Lerp(1f, 1.06f, e) * (pressed ? 0.95f : 1f) * (1f + 0.015f * pulseK);
            transform.localScale = Vector3.one * scale;

            if (frame != null) frame.SetBaseColor(Color.Lerp(frameBase, frameHover, Mathf.Max(e, pulseK * 0.35f)));
            if (body != null) body.color = Color.Lerp(bodyBase, bodyHover, e * 0.55f + pulseK * 0.12f);
            if (accent != null) accent.color = Color.Lerp(accentBase, accentHover, e);

            // Vệt sáng quét ngang
            if (shine != null && shineClip != null)
            {
                if (shineProgress >= 1f)
                {
                    shineTimer -= dt;
                    if (shineTimer <= 0f) { shineProgress = 0f; shineTimer = 3.6f; }
                }
                else
                {
                    shineProgress = Mathf.Min(1f, shineProgress + dt / 0.7f);
                }

                float width = shineClip.rect.width;
                float x = Mathf.Lerp(-0.25f * width, 1.25f * width, shineProgress);
                shine.anchoredPosition = new Vector2(x, 0f);
                shine.gameObject.SetActive(shineProgress < 1f);
            }
        }
    }
}
