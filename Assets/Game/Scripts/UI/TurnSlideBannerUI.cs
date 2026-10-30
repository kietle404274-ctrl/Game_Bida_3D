using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Banner thông báo lượt đánh trượt từ góc trên bên trái vào (ngay bên dưới HUD tên người chơi PlayerLeftPanel).
    /// Thiết kế hiện đại với tông màu Xanh Lam (Neon Cyan / Electric Blue), nền kính mờ dark navy xuyên thấu.
    /// Tự động dựng lúc runtime, không phụ thuộc vào scene hay prefab.
    /// </summary>
    public class TurnSlideBannerUI : MonoBehaviour
    {
        private RectTransform panelRt;
        private CanvasGroup canvasGroup;
        private SkewedPanelGraphic bodyGraphic;
        private BorderProgressGraphic frameGraphic;
        private SkewedPanelGraphic accentStrip;
        private Text titleText;
        private Text subText;
        private Text iconText;

        private Coroutine slideCoroutine;

        private const float BannerW = 380f;
        private const float BannerH = 68f;
        private const float Skew = 18f;

        private const float HiddenX = -450f;
        private const float RestX = 24f;
        private const float DefaultY = -155f;

        private GameplayHUD parentHud;

        // Bảng màu Xanh Lam theo yêu cầu
        private static readonly Color CyanNeon = new Color(0f, 0.90f, 1f, 1f);
        private static readonly Color CyanSoft = new Color(0.50f, 0.88f, 1f, 1f);
        private static readonly Color DarkNavyBody = new Color(0.04f, 0.11f, 0.22f, 0.95f);
        private static readonly Color SubtitleColor = new Color(0.72f, 0.86f, 1f, 0.92f);

        private Font font;

        public static TurnSlideBannerUI Ensure(GameplayHUD hud)
        {
            if (hud == null) return null;

            var existing = hud.GetComponentInChildren<TurnSlideBannerUI>(true);
            if (existing != null) return existing;

            Canvas canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return null;

            var go = new GameObject("TurnSlideBanner");
            go.transform.SetParent(canvas.transform, false);

            var banner = go.AddComponent<TurnSlideBannerUI>();
            banner.Build(hud);
            return banner;
        }

        private void Build(GameplayHUD hud)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            parentHud = hud;

            panelRt = gameObject.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.sizeDelta = new Vector2(BannerW, BannerH);

            UpdateTargetY();

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            // 1. Nền hình bình hành vát góc màu Dark Navy
            bodyGraphic = gameObject.AddComponent<SkewedPanelGraphic>();
            bodyGraphic.color = DarkNavyBody;
            bodyGraphic.SetInsets(0f, Skew, Skew, 0f);
            bodyGraphic.SetBottomDarken(0.35f);
            bodyGraphic.raycastTarget = false;

            // 2. Khung viền vi tế màu Neon Cyan
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(transform, false);
            frameGraphic = frameGo.GetComponent<BorderProgressGraphic>();
            frameGraphic.raycastTarget = false;
            frameGraphic.SetInsets(0f, Skew, Skew, 0f);
            frameGraphic.SetBaseColor(new Color(CyanNeon.r, CyanNeon.g, CyanNeon.b, 0.80f));
            Stretch(frameGraphic.rectTransform, 0f);

            // 3. Dải nhấn sọc xanh lam rực rỡ ở cạnh trái
            var accentGo = new GameObject("AccentStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            accentGo.transform.SetParent(transform, false);
            accentStrip = accentGo.GetComponent<SkewedPanelGraphic>();
            accentStrip.color = CyanNeon;
            accentStrip.SetInsets(0f, 6f, 6f, 0f);
            accentStrip.raycastTarget = false;
            var aRt = accentStrip.rectTransform;
            aRt.anchorMin = new Vector2(0f, 0f);
            aRt.anchorMax = new Vector2(0f, 1f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.anchoredPosition = new Vector2(4f, 0f);
            aRt.sizeDelta = new Vector2(8f, -6f);

            // 4. Icon nhịp điệu / mũi tên
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            iconGo.transform.SetParent(transform, false);
            iconText = iconGo.GetComponent<Text>();
            iconText.font = font;
            iconText.fontSize = 26;
            iconText.fontStyle = FontStyle.Bold;
            iconText.alignment = TextAnchor.MiddleCenter;
            iconText.color = CyanNeon;
            iconText.text = "▶";
            iconText.raycastTarget = false;
            var iRt = iconText.rectTransform;
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(30f, 0f);
            iRt.sizeDelta = new Vector2(30f, 30f);

            // 5. Tiêu đề chính (ĐẾN LƯỢT BẠN / ĐẾN LƯỢT ĐỐI THỦ)
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(transform, false);
            titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 20;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleLeft;
            titleText.color = CyanNeon;
            titleText.supportRichText = true;
            titleText.raycastTarget = false;
            var tShadow = titleGo.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            tShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var tRt = titleText.rectTransform;
            tRt.anchorMin = new Vector2(0f, 0.48f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = new Vector2(52f, 0f);
            tRt.offsetMax = new Vector2(-15f, -6f);

            // 6. Phụ đề giải thích
            var subGo = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subGo.transform.SetParent(transform, false);
            subText = subGo.GetComponent<Text>();
            subText.font = font;
            subText.fontSize = 12;
            subText.fontStyle = FontStyle.Normal;
            subText.alignment = TextAnchor.MiddleLeft;
            subText.color = SubtitleColor;
            subText.supportRichText = true;
            subText.raycastTarget = false;
            var sRt = subText.rectTransform;
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0.52f);
            sRt.offsetMin = new Vector2(52f, 6f);
            sRt.offsetMax = new Vector2(-15f, 0f);

            gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>
        /// Cập nhật vị trí Y để luôn nằm bên dưới PlayerLeftPanel một khoảng cách thông thoáng.
        /// </summary>
        private void UpdateTargetY()
        {
            float targetY = DefaultY;
            if (parentHud != null && parentHud.PlayerLeftPanel != null)
            {
                var pRt = parentHud.PlayerLeftPanel.GetComponent<RectTransform>();
                var canvas = GetComponentInParent<Canvas>();
                var canvasRt = canvas != null ? canvas.transform as RectTransform : null;
                if (pRt != null && canvasRt != null)
                {
                    Vector3[] corners = new Vector3[4];
                    pRt.GetWorldCorners(corners);
                    // corners[0] là góc dưới-trái của PlayerLeftPanel trong World space
                    Vector3 localBottomLeft = canvasRt.InverseTransformPoint(corners[0]);
                    float canvasTop = canvasRt.rect.height * (1f - canvasRt.pivot.y);
                    float distFromTop = canvasTop - localBottomLeft.y;
                    targetY = -(distFromTop + 20f); // cách mép dưới của Hub tên 20px
                }
            }

            // Đảm bảo luôn nằm thấp hơn mép dưới của Hub tên (tối thiểu -150f)
            targetY = Mathf.Min(targetY, -150f);

            if (panelRt != null)
            {
                panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, targetY);
            }
        }

        /// <summary>
        /// Hiển thị banner trượt từ trái ra thông báo lượt đánh cho người chơi hoặc đối thủ.
        /// </summary>
        public void ShowTurn(bool isLocalPlayer, string shooterName)
        {
            UpdateTargetY();

            if (slideCoroutine != null)
            {
                StopCoroutine(slideCoroutine);
            }

            gameObject.SetActive(true);

            if (isLocalPlayer)
            {
                titleText.text = $"<color=#00E5FF><b>{LocalizationManager.Get("turn_banner_your_turn")}</b></color>";
                subText.text = LocalizationManager.Get("turn_banner_your_turn_sub");
                iconText.text = "▶";
                iconText.color = CyanNeon;
                accentStrip.color = CyanNeon;
                frameGraphic.SetBaseColor(new Color(CyanNeon.r, CyanNeon.g, CyanNeon.b, 0.85f));
            }
            else
            {
                string dispName = string.IsNullOrEmpty(shooterName) ? LocalizationManager.Get("turn_banner_opponent") : shooterName;
                titleText.text = $"<color=#80D8FF><b>{LocalizationManager.Get("turn_banner_opponent")}</b></color>";
                subText.text = LocalizationManager.Format("turn_banner_opponent_sub", dispName);
                iconText.text = "▶";
                iconText.color = CyanSoft;
                accentStrip.color = CyanSoft;
                frameGraphic.SetBaseColor(new Color(CyanSoft.r, CyanSoft.g, CyanSoft.b, 0.70f));
            }

            slideCoroutine = StartCoroutine(SlideRoutine());
        }

        private IEnumerator SlideRoutine()
        {
            float currentY = panelRt.anchoredPosition.y;
            panelRt.anchoredPosition = new Vector2(HiddenX, currentY);
            canvasGroup.alpha = 0f;

            // 1. Trượt từ mép trái vào (Slide-In)
            const float inDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / inDuration);
                // Ease out cubic
                float ease = 1f - Mathf.Pow(1f - t, 3);

                panelRt.anchoredPosition = new Vector2(Mathf.Lerp(HiddenX, RestX, ease), currentY);
                canvasGroup.alpha = ease;
                yield return null;
            }

            panelRt.anchoredPosition = new Vector2(RestX, currentY);
            canvasGroup.alpha = 1f;

            // 2. Dừng lại hiển thị cho người chơi quan sát
            yield return new WaitForSecondsRealtime(2.2f);

            // 3. Trượt lùi nhẹ về bên trái và mờ dần (Slide-Out)
            const float outDuration = 0.28f;
            elapsed = 0f;
            while (elapsed < outDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / outDuration);
                float ease = t * t; // Ease in quad

                panelRt.anchoredPosition = new Vector2(Mathf.Lerp(RestX, HiddenX, ease), currentY);
                canvasGroup.alpha = 1f - t;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            panelRt.anchoredPosition = new Vector2(HiddenX, currentY);
            gameObject.SetActive(false);
            slideCoroutine = null;
        }
    }
}
