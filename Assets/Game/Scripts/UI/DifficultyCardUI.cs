using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Thẻ chọn độ khó kiểu HUD: hình bình hành vát góc, dải màu phía trên, tiêu đề + mô tả + thanh độ mạnh.
    /// Được dựng đè lên một Button có sẵn (giữ nguyên onClick); màu chọn/hover chuyển mượt bằng thời gian không phụ thuộc timeScale.
    /// </summary>
    public class DifficultyCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color BodyIdle = new Color(0.07f, 0.11f, 0.19f, 0.96f);

        private Color accent;
        private SkewedPanelGraphic body;
        private BorderProgressGraphic frame;
        private Image strip;
        private Text title;
        private Image[] bars;
        private int level;

        private bool built;
        private bool selected;
        private bool hovered;
        private bool pressed;
        private float selT;
        private float hovT;

        public void Build(Color accentColor, string titleText, int strengthLevel)
        {
            if (built) return;
            built = true;
            accent = accentColor;
            level = Mathf.Clamp(strengthLevel, 1, 3);

            // Ẩn hình nút mặc định nhưng giữ làm vùng bấm
            Image baseImg = GetComponent<Image>();
            if (baseImg != null)
            {
                baseImg.sprite = null;
                baseImg.type = Image.Type.Simple;
                baseImg.color = new Color(0f, 0f, 0f, 0f);
                baseImg.raycastTarget = true;
            }
            Button btn = GetComponent<Button>();
            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = baseImg;
            }
            foreach (Text old in GetComponentsInChildren<Text>(true))
            {
                old.gameObject.SetActive(false);
            }

            const float cut = 14f;


            GameObject bodyGo = new GameObject("Body", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            bodyGo.transform.SetParent(transform, false);
            Stretch(bodyGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            body = bodyGo.GetComponent<SkewedPanelGraphic>();
            body.raycastTarget = false;
            body.SetInsets(0f, cut, cut, 0f);
            body.color = BodyIdle;

            GameObject frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(transform, false);
            Stretch(frameGo.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            frame = frameGo.GetComponent<BorderProgressGraphic>();
            frame.raycastTarget = false;
            frame.SetInsets(0f, cut, cut, 0f);
            frame.SetBaseColor(Dim(accent, 0.45f));

            // Dải màu phía trên
            strip = NewImage("Strip", Vector2.zero, Vector2.zero);
            RectTransform sr = strip.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0.5f, 1f);
            sr.offsetMin = new Vector2(0f, -5f);
            sr.offsetMax = new Vector2(-cut, 0f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            title = NewText("Title", titleText, 20, FontStyle.Bold, font);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 0.3f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(4f, 0f);
            tr.offsetMax = new Vector2(-4f, -6f);

            // 3 vạch độ mạnh ở đáy thẻ
            bars = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                Image bar = NewImage("Bar" + i, Vector2.zero, Vector2.zero);
                RectTransform br = bar.rectTransform;
                br.anchorMin = new Vector2(0.5f, 0f);
                br.anchorMax = new Vector2(0.5f, 0f);
                br.pivot = new Vector2(0.5f, 0f);
                br.sizeDelta = new Vector2(26f, 4f);
                br.anchoredPosition = new Vector2((i - 1) * 32f, 9f);
                bars[i] = bar;
            }

            Apply(0f, 0f);
        }

        public void SetSelected(bool value)
        {
            selected = value;
        }

        public void SetTitle(string newTitle)
        {
            if (title != null && !string.IsNullOrEmpty(newTitle))
            {
                title.text = newTitle;
            }
        }

        private static Color Dim(Color c, float k)
        {
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        private static void Stretch(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = min;
            rt.offsetMax = max;
        }

        private Image NewImage(string imgName, Vector2 offMin, Vector2 offMax)
        {
            GameObject go = new GameObject(imgName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            Stretch(go.GetComponent<RectTransform>(), offMin, offMax);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private Text NewText(string textName, string content, int size, FontStyle style, Font font)
        {
            GameObject go = new GameObject(textName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(transform, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public void OnPointerEnter(PointerEventData eventData) { hovered = true; }
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { pressed = false; }

        private void OnDisable()
        {
            hovered = false;
            pressed = false;
        }

        private void Update()
        {
            if (!built) return;
            float dt = Time.unscaledDeltaTime;
            float selTarget = selected ? 1f : 0f;
            float hovTarget = hovered ? 1f : 0f;
            if (Mathf.Approximately(selT, selTarget) && Mathf.Approximately(hovT, hovTarget) && !pressed) return;
            selT = Mathf.MoveTowards(selT, selTarget, dt * 7f);
            hovT = Mathf.MoveTowards(hovT, hovTarget, dt * 10f);
            Apply(selT, hovT);
        }

        private void Apply(float s, float h)
        {
            float se = s * s * (3f - 2f * s);
            float he = h * h * (3f - 2f * h);

            Color selBody = new Color(accent.r * 0.42f, accent.g * 0.42f, accent.b * 0.42f, 0.98f);
            Color hovBody = new Color(accent.r * 0.20f + 0.05f, accent.g * 0.20f + 0.08f, accent.b * 0.20f + 0.12f, 0.97f);
            Color b = Color.Lerp(BodyIdle, hovBody, he);
            body.color = Color.Lerp(b, selBody, se);

            Color frameIdle = Dim(accent, 0.45f);
            Color frameHover = Dim(accent, 0.8f);
            Color frameSel = Color.Lerp(accent, Color.white, 0.25f);
            Color f = Color.Lerp(frameIdle, frameHover, he);
            frame.SetBaseColor(Color.Lerp(f, frameSel, se));

            strip.color = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.55f, 1f, Mathf.Max(se, he * 0.6f)));

            title.color = Color.Lerp(new Color(0.72f, 0.80f, 0.90f, 1f), Color.white, Mathf.Max(se, he));

            for (int i = 0; i < bars.Length; i++)
            {
                bool lit = i < level;
                Color on = Color.Lerp(accent, Color.white, 0.15f * se);
                Color off = new Color(0.20f, 0.27f, 0.38f, 1f);
                Color idleLit = Dim(accent, 0.6f);
                bars[i].color = lit ? Color.Lerp(idleLit, on, Mathf.Max(se, he * 0.6f)) : off;
            }

            float scale = 1f + 0.05f * se + 0.025f * he;
            if (pressed) scale *= 0.97f;
            transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
