using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Banner thông báo lỗi (foul) / thắng ván, cùng ngôn ngữ thiết kế với HUD: khung bình hành xanh đậm tối dần xuống dưới,
    /// viền sáng, nhãn màu (đỏ = lỗi, xanh lá = thắng) và dải màu ở đáy. Trượt từ trên xuống, có vệt sáng quét; banner lỗi rung nhẹ.
    /// Tự dựng lúc chạy. Dùng: FoulPopupUI.Show(FoulType.CueBallScratch), FoulPopupUI.Show("TÊN LỖI"), FoulPopupUI.ShowWin("TÊN 2 - 1").
    /// </summary>
    public class FoulPopupUI : MonoBehaviour
    {
        private static FoulPopupUI instance;

        private RectTransform panel;
        private CanvasGroup group;
        private SkewedPanelGraphic body;
        private BorderProgressGraphic frame;
        private SkewedPanelGraphic accent;
        private RectTransform accentRt;
        private SkewedPanelGraphic badge;
        private RectTransform badgeRt;
        private Text badgeText;
        private Text titleText;
        private Text subText;
        private RectTransform shine;
        private RectTransform shineClip;
        private Coroutine routine;

        private const float PanelW = 820f;
        private const float PanelH = 118f;
        private const float Skew = 26f;
        private static readonly Vector2 BasePos = new Vector2(0f, 250f);

        private static readonly Color BodyFoul = new Color(0.36f, 0.10f, 0.12f, 0.98f);
        private static readonly Color BodyWin = new Color(0.06f, 0.30f, 0.15f, 0.98f);
        private static readonly Color FoulColor = new Color(1f, 0.32f, 0.32f, 1f);
        private static readonly Color WinColor = new Color(0.25f, 0.98f, 0.50f, 1f);
        private static readonly Color NavyText = new Color(0.05f, 0.09f, 0.18f, 1f);

        public static void Show(FoulType foul)
        {
            Ensure();
            instance.Display(GetTitle(foul), LocalizationManager.Get("foul_sub"), LocalizationManager.Get("foul_badge"), true, 2.2f);
        }

        public static void Show(string title)
        {
            Ensure();
            instance.Display(title, LocalizationManager.Get("foul_sub"), LocalizationManager.Get("foul_badge"), true, 2.2f);
        }

        /// <summary>Banner thắng ván: nhãn xanh lá, tên người thắng và tỉ số.</summary>
        public static void ShowWin(string text, float holdSeconds = 2.0f)
        {
            Ensure();
            instance.Display(text, LocalizationManager.Get("win_sub"), LocalizationManager.Get("win_badge"), false, holdSeconds);
        }

        /// <summary>Banner thắng ván: nền xanh lá, nhãn xanh lá, thông báo người chơi thắng ván.</summary>
        public static void ShowRackWin(string title, string scoreSubtitle, float holdSeconds = 2.2f)
        {
            Ensure();
            instance.Display(title, scoreSubtitle, LocalizationManager.Get("win_badge"), false, holdSeconds);
        }

        /// <summary>Banner thua ván: nền đỏ, nhãn đỏ, thông báo người chơi thua ván.</summary>
        public static void ShowRackLose(string title, string scoreSubtitle, float holdSeconds = 2.2f)
        {
            Ensure();
            instance.Display(title, scoreSubtitle, LocalizationManager.Get("lose_badge"), true, holdSeconds);
        }

        private static readonly Color BodyBreak = new Color(0.04f, 0.18f, 0.32f, 0.98f);
        private static readonly Color BreakColor = new Color(0f, 0.88f, 1f, 1f); // Neon Cyan

        /// <summary>Banner thông báo cú phá bi có bi vào lỗ thành công.</summary>
        public static void ShowBreakSuccess(string title, string subtitle, float holdSeconds = 2.4f)
        {
            Ensure();
            instance.DisplayCustom(title, subtitle, LocalizationManager.Get("break_badge"), BreakColor, BodyBreak, false, holdSeconds, true);
        }

        private static void Ensure()
        {
            if (instance == null)
            {
                var go = new GameObject("FoulPopup");
                instance = go.AddComponent<FoulPopupUI>();
                instance.Build();
            }
        }

        public static string GetTitle(FoulType foul)
        {
            switch (foul)
            {
                case FoulType.CueBallScratch: return LocalizationManager.Get("foul_scratch");
                case FoulType.WrongBallFirst: return LocalizationManager.Get("foul_wrong_ball");
                case FoulType.NoBallHit: return LocalizationManager.Get("foul_no_ball_hit");
                case FoulType.NoRailAfterContact: return LocalizationManager.Get("foul_no_rail");
                case FoulType.IllegalBreak: return LocalizationManager.Get("foul_illegal_break");
                case FoulType.BallDrivenOffTable: return LocalizationManager.Get("foul_off_table");
                case FoulType.ShotClockExpired: return LocalizationManager.Get("foul_timeout");
                case FoulType.BadCueBallPlacement: return LocalizationManager.Get("foul_bad_placement");
                case FoulType.PlayingOutOfTurn: return LocalizationManager.Get("foul_out_of_turn");
                case FoulType.BallsStillMoving: return LocalizationManager.Get("foul_balls_moving");
                case FoulType.DoubleHit: return LocalizationManager.Get("foul_double_hit");
                case FoulType.PushShot: return LocalizationManager.Get("foul_push_shot");
                default: return LocalizationManager.Get("foul_generic");
            }
        }

        // ------------------------------------------------------------------ Dựng UI

        private Font font;

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            panelGo.transform.SetParent(transform, false);
            panel = (RectTransform)panelGo.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelW, PanelH);
            panel.anchoredPosition = BasePos;
            body = panelGo.GetComponent<SkewedPanelGraphic>();
            body.SetInsets(0f, Skew, Skew, 0f);
            body.SetBottomDarken(0.5f);
            body.raycastTarget = false;
            group = panelGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            // Viền sáng chạy theo chu vi khung xéo
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(panel, false);
            frame = frameGo.GetComponent<BorderProgressGraphic>();
            frame.raycastTarget = false;
            frame.SetInsets(0f, Skew, Skew, 0f);
            Stretch(frame.rectTransform, 0f);

            // Dải màu dưới đáy
            const float stripH = 6f;
            accent = MakeSkewed(panel, "Accent", Color.white, Skew * 0.9f, Skew * 0.1f, Skew, 0f);
            accentRt = accent.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(1f, 0f);
            accentRt.pivot = new Vector2(0.5f, 0f);
            accentRt.anchoredPosition = Vector2.zero;
            accentRt.sizeDelta = new Vector2(0f, stripH);

            // Nhãn (LỖI / THẮNG VÁN) dạng bình hành tô màu
            badge = MakeSkewed(panel, "Badge", Color.white, 14f, 0f, 0f, 14f);
            badgeRt = badge.rectTransform;
            badgeRt.anchorMin = badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(30f, 4f);
            badgeRt.sizeDelta = new Vector2(176f, 60f);
            badgeText = MakeText(badgeRt, "BadgeText", 24, NavyText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(badgeText.rectTransform, 0f);

            titleText = MakeText(panel, "Title", 40, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0.42f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(224f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-40f, -8f);
            var ts = titleText.gameObject.AddComponent<Shadow>();
            ts.effectColor = new Color(0f, 0f, 0f, 0.55f);
            ts.effectDistance = new Vector2(2f, -2f);

            subText = MakeText(panel, "Sub", 17, new Color(0.72f, 0.82f, 0.96f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            subText.rectTransform.anchorMin = new Vector2(0f, 0f);
            subText.rectTransform.anchorMax = new Vector2(1f, 0f);
            subText.rectTransform.offsetMin = new Vector2(226f, 16f);
            subText.rectTransform.offsetMax = new Vector2(-40f, 44f);

            // Vệt sáng quét ngang
            var clipGo = new GameObject("ShineClip", typeof(RectTransform), typeof(RectMask2D));
            clipGo.transform.SetParent(panel, false);
            shineClip = (RectTransform)clipGo.transform;
            Stretch(shineClip, 3f);
            var sh = MakeSkewed(shineClip, "Shine", new Color(1f, 1f, 1f, 0.20f), 0f, 16f, 16f, 0f);
            shine = sh.rectTransform;
            shine.anchorMin = new Vector2(0f, 0f);
            shine.anchorMax = new Vector2(0f, 1f);
            shine.pivot = new Vector2(0.5f, 0.5f);
            shine.sizeDelta = new Vector2(70f, 0f);
            shine.gameObject.SetActive(false);

            panel.gameObject.SetActive(false);
        }

        private SkewedPanelGraphic MakeSkewed(Transform parent, string name, Color color, float tl, float tr, float bl, float br)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.transform.SetParent(parent, false);
            var g = go.GetComponent<SkewedPanelGraphic>();
            g.color = color;
            g.raycastTarget = false;
            g.SetInsets(tl, tr, bl, br);
            return g;
        }

        private Text MakeText(Transform parent, string name, int size, Color color, TextAnchor align, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.supportRichText = true;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        // ------------------------------------------------------------------ Hiển thị

        private bool currentIsFoul;
        private float currentHold = 2f;

        private void Display(string title, string subtitle, string tag, bool foul, float holdSeconds)
        {
            Color tint = foul ? FoulColor : WinColor;
            Color bodyColor = foul ? BodyFoul : BodyWin;
            DisplayCustom(title, subtitle, tag, tint, bodyColor, foul, holdSeconds, false);
        }

        private void DisplayCustom(string title, string subtitle, string tag, Color tint, Color bodyColor, bool foul, float holdSeconds, bool playPocketSound = false)
        {
            body.color = bodyColor;
            frame.SetBaseColor(new Color(tint.r, tint.g, tint.b, 0.85f));
            accent.color = tint;
            badge.color = tint;
            badgeText.text = tag;
            titleText.text = title;
            subText.text = subtitle;

            currentIsFoul = foul;
            currentHold = holdSeconds;

            var am = Audio.AudioManager.Instance;
            if (am != null)
            {
                if (playPocketSound) am.PlayPocketDrop();
                else if (foul) am.PlayFoul();
                else am.PlayWinFanfare();
            }

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Animate());
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.4f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        private IEnumerator Animate()
        {
            panel.gameObject.SetActive(true);
            panel.localScale = Vector3.one;
            accentRt.localScale = new Vector3(0f, 1f, 1f);
            badgeRt.localScale = Vector3.zero;
            shine.gameObject.SetActive(false);

            // Trượt từ trên xuống
            float t = 0f;
            const float inDur = 0.55f;
            while (t < inDur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / inDur);
                float e = EaseOutBack(p);
                group.alpha = Mathf.Clamp01(p * 2.5f);
                panel.anchoredPosition = BasePos + new Vector2(0f, Mathf.LerpUnclamped(140f, 0f, e));
                panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, p);
                accentRt.localScale = new Vector3(Mathf.Clamp01((p - 0.15f) / 0.7f), 1f, 1f);
                badgeRt.localScale = Vector3.one * Mathf.LerpUnclamped(0f, 1f, EaseOutBack(Mathf.Clamp01((p - 0.2f) / 0.6f)));
                yield return null;
            }
            panel.anchoredPosition = BasePos;
            panel.localScale = Vector3.one;
            accentRt.localScale = Vector3.one;
            badgeRt.localScale = Vector3.one;
            group.alpha = 1f;

            // Giữ + vệt sáng quét + rung nhẹ nếu là lỗi
            float hold = 0f;
            float shineClock = 0f;
            shine.gameObject.SetActive(true);
            float width = shineClip.rect.width;
            while (hold < currentHold)
            {
                hold += Time.unscaledDeltaTime;
                shineClock += Time.unscaledDeltaTime;

                float sp = Mathf.Clamp01(shineClock / 0.8f);
                shine.anchoredPosition = new Vector2(Mathf.Lerp(-0.2f * width, 1.2f * width, sp), 0f);
                shine.gameObject.SetActive(sp < 1f);

                if (currentIsFoul && hold < 0.45f)
                {
                    float k = 1f - hold / 0.45f;
                    panel.anchoredPosition = BasePos + new Vector2(Mathf.Sin(hold * 70f) * 9f * k, 0f);
                }
                else
                {
                    panel.anchoredPosition = BasePos;
                }
                yield return null;
            }

            // Mờ dần + trượt lên
            t = 0f;
            const float outDur = 0.35f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / outDur);
                group.alpha = 1f - p;
                panel.anchoredPosition = BasePos + new Vector2(0f, 40f * p);
                yield return null;
            }

            panel.anchoredPosition = BasePos;
            group.alpha = 0f;
            panel.gameObject.SetActive(false);
            routine = null;
        }
    }
}
