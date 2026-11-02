using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Thẻ HUD thông báo đối thủ thoát trận hiển thị trực tiếp trên giao diện bàn Bida trong 5 giây
    /// trước khi chuyển sang màn hình thống kê.
    /// Thiết kế đồng bộ phong cách HUD Dark-Navy &amp; StatsPanelUI (khung vát chéo, viền chạy tiến trình chu vi,
    /// hộp tỷ số xử thắng tuyệt đối N : 0 và thanh đếm ngược mượt).
    /// </summary>
    public class OpponentLeftOverlayUI : MonoBehaviour
    {
        private static OpponentLeftOverlayUI instance;

        private GameObject rootGo;
        private CanvasGroup canvasGroup;
        private RectTransform cardRt;
        private SkewedPanelGraphic cardBody;
        private BorderProgressGraphic perimeterBorder;
        private Text tagText;
        private Text statusText;
        private Text winHeadlineText;
        private Text scoreBoxText;
        private Text countdownText;
        private RectTransform bottomBarFillRt;

        private const float CardW = 560f;
        private const float CardH = 278f;
        private const float Bevel = 24f;

        private static readonly Color DimmerColor = new Color(0.01f, 0.03f, 0.10f, 0.36f);
        private static readonly Color BodyNavy = new Color(0.03f, 0.09f, 0.28f, 0.95f);
        private static readonly Color BorderBase = new Color(0.30f, 0.78f, 0.96f, 0.38f);
        private static readonly Color BorderArc = new Color(0.25f, 0.92f, 1.00f, 0.98f);
        private static readonly Color TagBgColor = new Color(0.08f, 0.18f, 0.44f, 0.95f);
        private static readonly Color TagTextColor = new Color(0.58f, 0.90f, 1.00f, 1.00f);
        private static readonly Color StatusCoral = new Color(1.00f, 0.52f, 0.36f, 1.00f);
        private static readonly Color WinGold = new Color(1.00f, 0.85f, 0.28f, 1.00f);
        private static readonly Color ScoreFrameColor = new Color(0.40f, 0.85f, 1.00f, 0.50f);
        private static readonly Color ScoreBgColor = new Color(0.01f, 0.03f, 0.14f, 0.92f);
        private static readonly Color CountdownMuted = new Color(0.76f, 0.86f, 0.98f, 0.95f);

        private float showElapsed;

        public static OpponentLeftOverlayUI Show(GameplayHUD hud, int winsA, int winsB, float totalSeconds = 5f)
        {
            Ensure(hud);
            if (instance == null) return null;

            instance.Display(winsA, winsB, totalSeconds);
            return instance;
        }

        public static void UpdateProgress(float remainingSeconds, float totalSeconds)
        {
            if (instance == null || instance.rootGo == null || !instance.rootGo.activeSelf) return;
            instance.ApplyCountdown(remainingSeconds, totalSeconds);
        }

        public static void HideImmediate()
        {
            if (instance != null && instance.rootGo != null)
            {
                instance.rootGo.SetActive(false);
            }
        }

        private static void Ensure(GameplayHUD hud)
        {
            if (instance != null && instance.rootGo != null) return;

            Canvas canvas = null;
            if (hud != null)
            {
                canvas = hud.RulesButton != null ? hud.RulesButton.GetComponentInParent<Canvas>() : null;
                if (canvas == null) canvas = hud.GetComponentInParent<Canvas>();
            }
            if (canvas == null)
            {
                var all = FindObjectsOfType<Canvas>(true);
                foreach (var c in all)
                {
                    if (c != null && c.name == "GameplayCanvas" && c.renderMode != RenderMode.WorldSpace)
                    {
                        canvas = c;
                        break;
                    }
                }
                if (canvas == null)
                {
                    foreach (var c in all)
                    {
                        if (c != null && c.renderMode != RenderMode.WorldSpace)
                        {
                            canvas = c;
                            break;
                        }
                    }
                }
            }
            if (canvas == null) return;
            canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

            var go = new GameObject("OpponentLeftOverlayUI", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();

            instance = go.AddComponent<OpponentLeftOverlayUI>();
            instance.Build();
        }

        private void Build()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 1. Lớp phủ mờ nhẹ để nổi bật thẻ HUD nhưng vẫn nhìn rõ bàn Bida phía sau
            rootGo = new GameObject("Root", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            rootGo.layer = gameObject.layer;
            rootGo.transform.SetParent(transform, false);
            var rootRt = (RectTransform)rootGo.transform;
            Stretch(rootRt);

            var dimmer = rootGo.GetComponent<Image>();
            dimmer.color = DimmerColor;
            dimmer.raycastTarget = true;

            canvasGroup = rootGo.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // 2. Thẻ HUD chính giữa màn hình (vát chéo góc trên-phải & dưới-trái)
            var cardGo = new GameObject("HudCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            cardGo.layer = gameObject.layer;
            cardGo.transform.SetParent(rootRt, false);
            cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, 16f);
            cardRt.sizeDelta = new Vector2(CardW, CardH);

            cardBody = cardGo.GetComponent<SkewedPanelGraphic>();
            cardBody.color = BodyNavy;
            cardBody.SetInsets(0f, Bevel, Bevel, 0f);
            cardBody.SetBottomDarken(0.45f);
            cardBody.raycastTarget = false;

            var cardShadow = cardGo.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            // Dải ánh sáng xanh phần đỉnh thẻ
            var topGlowGo = new GameObject("TopGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            topGlowGo.layer = gameObject.layer;
            topGlowGo.transform.SetParent(cardRt, false);
            var topGlow = topGlowGo.GetComponent<SkewedPanelGraphic>();
            topGlow.color = new Color(0.12f, 0.40f, 0.90f, 0.26f);
            topGlow.SetInsets(0f, Bevel, 0f, 0f);
            topGlow.raycastTarget = false;
            Anchor(topGlow.rectTransform, 0f, 0.62f, 1f, 1f);

            // 3. Viền tiến trình chạy quanh chu vi thẻ (BorderProgressGraphic)
            var frameGo = new GameObject("PerimeterFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.layer = gameObject.layer;
            frameGo.transform.SetParent(cardRt, false);
            perimeterBorder = frameGo.GetComponent<BorderProgressGraphic>();
            perimeterBorder.raycastTarget = false;
            perimeterBorder.SetInsets(0f, Bevel, Bevel, 0f);
            perimeterBorder.SetBaseColor(BorderBase);
            Stretch(perimeterBorder.rectTransform);

            // 4. Nhãn nhỏ phía trên (● THÔNG BÁO TRẬN ĐẤU)
            var tagGo = new GameObject("TopTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            tagGo.layer = gameObject.layer;
            tagGo.transform.SetParent(cardRt, false);
            var tagGraphic = tagGo.GetComponent<SkewedPanelGraphic>();
            tagGraphic.color = TagBgColor;
            tagGraphic.SetInsets(10f, 0f, 0f, 10f);
            tagGraphic.raycastTarget = false;
            var tagRt = tagGraphic.rectTransform;
            tagRt.anchorMin = tagRt.anchorMax = new Vector2(0.5f, 1f);
            tagRt.pivot = new Vector2(0.5f, 1f);
            tagRt.anchoredPosition = new Vector2(0f, -16f);
            tagRt.sizeDelta = new Vector2(236f, 28f);

            tagText = MakeText(tagRt, "TagLabel", font, 13, FontStyle.Bold, TagTextColor);
            Stretch(tagText.rectTransform);

            // 5. Dòng 1: ĐỐI THỦ ĐÃ THOÁT TRẬN
            statusText = MakeText(cardRt, "StatusLine", font, 21, FontStyle.Bold, StatusCoral);
            var statusRt = statusText.rectTransform;
            statusRt.anchorMin = new Vector2(0.06f, 0.64f);
            statusRt.anchorMax = new Vector2(0.94f, 0.80f);
            statusRt.offsetMin = statusRt.offsetMax = Vector2.zero;

            // 6. Dòng 2: BẠN THẮNG TRẬN NÀY
            winHeadlineText = MakeText(cardRt, "WinHeadline", font, 34, FontStyle.Bold, WinGold);
            var winRt = winHeadlineText.rectTransform;
            winRt.anchorMin = new Vector2(0.05f, 0.45f);
            winRt.anchorMax = new Vector2(0.95f, 0.65f);
            winRt.offsetMin = winRt.offsetMax = Vector2.zero;
            var winGlow = winHeadlineText.gameObject.AddComponent<Outline>();
            winGlow.effectColor = new Color(0.60f, 0.32f, 0.00f, 0.45f);
            winGlow.effectDistance = new Vector2(1.5f, -1.5f);

            // 7. Hộp Tỷ Số Xử Thắng ở giữa (VD: 5 : 0 hoặc 3 : 0) đồng bộ kiểu dáng StatsPanelUI
            var scoreFrameGo = new GameObject("ScoreFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            scoreFrameGo.layer = gameObject.layer;
            scoreFrameGo.transform.SetParent(cardRt, false);
            var scoreFrame = scoreFrameGo.GetComponent<SkewedPanelGraphic>();
            scoreFrame.color = ScoreFrameColor;
            scoreFrame.SetInsets(0f, 0f, 14f, 14f);
            scoreFrame.raycastTarget = false;
            var sfRt = scoreFrame.rectTransform;
            sfRt.anchorMin = sfRt.anchorMax = new Vector2(0.5f, 0.29f);
            sfRt.pivot = new Vector2(0.5f, 0.5f);
            sfRt.anchoredPosition = Vector2.zero;
            sfRt.sizeDelta = new Vector2(204f, 54f);

            var scoreBgGo = new GameObject("ScoreBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            scoreBgGo.layer = gameObject.layer;
            scoreBgGo.transform.SetParent(sfRt, false);
            var scoreBg = scoreBgGo.GetComponent<SkewedPanelGraphic>();
            scoreBg.color = ScoreBgColor;
            scoreBg.SetInsets(0f, 0f, 14f, 14f);
            scoreBg.raycastTarget = false;
            Stretch(scoreBg.rectTransform, 2f);

            scoreBoxText = MakeText(scoreBg.rectTransform, "ScoreValue", font, 34, FontStyle.Bold, Color.white);
            scoreBoxText.supportRichText = true;
            Stretch(scoreBoxText.rectTransform);

            // 8. Thanh tiến trình ngang + dòng đếm ngược 5s ở đáy thẻ
            var trackGo = new GameObject("ProgressTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            trackGo.layer = gameObject.layer;
            trackGo.transform.SetParent(cardRt, false);
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.01f, 0.04f, 0.14f, 0.85f);
            trackImg.raycastTarget = false;
            var trackRt = trackImg.rectTransform;
            trackRt.anchorMin = new Vector2(0.12f, 0.12f);
            trackRt.anchorMax = new Vector2(0.88f, 0.14f);
            trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;

            var fillGo = new GameObject("ProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.layer = gameObject.layer;
            fillGo.transform.SetParent(trackRt, false);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = BorderArc;
            fillImg.raycastTarget = false;
            bottomBarFillRt = fillImg.rectTransform;
            bottomBarFillRt.anchorMin = Vector2.zero;
            bottomBarFillRt.anchorMax = Vector2.one;
            bottomBarFillRt.offsetMin = bottomBarFillRt.offsetMax = Vector2.zero;

            countdownText = MakeText(cardRt, "CountdownSub", font, 15, FontStyle.Normal, CountdownMuted);
            var cdRt = countdownText.rectTransform;
            cdRt.anchorMin = new Vector2(0.08f, 0.02f);
            cdRt.anchorMax = new Vector2(0.92f, 0.11f);
            cdRt.offsetMin = cdRt.offsetMax = Vector2.zero;

            rootGo.SetActive(false);
        }

        private void Display(int winsA, int winsB, float totalSeconds)
        {
            transform.SetAsLastSibling();
            rootGo.SetActive(true);
            showElapsed = 0f;
            canvasGroup.alpha = 0f;
            cardRt.localScale = new Vector3(0.90f, 0.90f, 1f);

            tagText.text = LocalizationManager.Get("net_opp_left_tag", "● THÔNG BÁO TRẬN ĐẤU");
            statusText.text = LocalizationManager.Get("net_opp_left_short", "ĐỐI THỦ ĐÃ THOÁT TRẬN");
            winHeadlineText.text = LocalizationManager.Get("net_you_win_match_short", "BẠN THẮNG TRẬN NÀY");
            scoreBoxText.text = "<color=#33D9FF>" + winsA + "</color><color=#8FA6D6>  :  </color><color=#FF9E1F>" + winsB + "</color>";

            ApplyCountdown(totalSeconds, totalSeconds);
        }

        private void ApplyCountdown(float remainingSeconds, float totalSeconds)
        {
            float ratio = totalSeconds > 0.01f ? Mathf.Clamp01(remainingSeconds / totalSeconds) : 0f;
            int sec = Mathf.Clamp(Mathf.CeilToInt(remainingSeconds), 1, Mathf.Max(1, Mathf.CeilToInt(totalSeconds)));

            if (perimeterBorder != null)
            {
                perimeterBorder.SetProgress(ratio, BorderArc);
            }

            if (bottomBarFillRt != null)
            {
                bottomBarFillRt.anchorMax = new Vector2(ratio, 1f);
            }

            if (countdownText != null)
            {
                countdownText.text = LocalizationManager.Format("net_left_countdown_sub", sec);
            }
        }

        private void Update()
        {
            if (rootGo == null || !rootGo.activeSelf) return;

            if (showElapsed < 0.22f)
            {
                showElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(showElapsed / 0.22f);
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                canvasGroup.alpha = ease;
                float s = Mathf.Lerp(0.90f, 1.0f, ease);
                cardRt.localScale = new Vector3(s, s, 1f);
            }
        }

        private static Text MakeText(Transform parent, string name, Font font, int size, FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 11;
            t.resizeTextMaxSize = size;
            t.raycastTarget = false;

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.70f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}

