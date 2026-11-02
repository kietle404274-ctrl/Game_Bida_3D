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

        // Banner phụ màu Vàng Cam nhấp nháy hướng dẫn chọn vị trí đặt bi phá khai cuộc (Break Shot)
        private GameObject breakBannerGo;
        private RectTransform breakPanelRt;
        private CanvasGroup breakCanvasGroup;
        private SkewedPanelGraphic breakBodyGraphic;
        private BorderProgressGraphic breakFrameGraphic;
        private SkewedPanelGraphic breakAccentStrip;
        private Text breakTitleText;
        private Text breakSubText;
        private Text breakIconText;
        private Coroutine breakBannerCoroutine;
        private bool isTurnBannerVisible;
        private float cachedTargetY = DefaultY;

        private const float BannerW = 380f;
        private const float BreakBannerW = 470f;
        private const float BannerH = 68f;
        private const float Skew = 18f;

        private const float HiddenX = -500f;
        private const float RestX = 24f;
        private const float DefaultY = -155f;

        private GameplayHUD parentHud;

        // Bảng màu Xanh Lam cho thông báo lượt
        private static readonly Color CyanNeon = new Color(0f, 0.90f, 1f, 1f);
        private static readonly Color CyanSoft = new Color(0.50f, 0.88f, 1f, 1f);
        private static readonly Color DarkNavyBody = new Color(0.04f, 0.11f, 0.22f, 0.95f);
        private static readonly Color SubtitleColor = new Color(0.72f, 0.86f, 1f, 0.92f);

        // Bảng màu Vàng Cam (Orange-Amber) nhấp nháy cho thông báo chọn vị trí phá bi
        private static readonly Color AmberBright = new Color(1.00f, 0.78f, 0.10f, 1f);
        private static readonly Color OrangeDeep = new Color(1.00f, 0.48f, 0.00f, 1f);
        private static readonly Color DarkAmberBody = new Color(0.20f, 0.10f, 0.02f, 0.95f);
        private static readonly Color AmberSubColor = new Color(1.00f, 0.91f, 0.72f, 0.95f);

        private Font font;

        public static TurnSlideBannerUI Ensure(GameplayHUD hud)
        {
            if (hud == null) return null;

            var existing = FindObjectOfType<TurnSlideBannerUI>(true);
            if (existing != null) return existing;

            Canvas canvas = FindGameplayCanvas(hud);
            if (canvas == null) return null;

            var go = new GameObject("TurnSlideBanner");
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var banner = go.AddComponent<TurnSlideBannerUI>();
            banner.Build(hud);
            return banner;
        }

        private static Canvas FindGameplayCanvas(GameplayHUD hud)
        {
            if (hud != null)
            {
                if (hud.PlayerLeftPanel != null)
                {
                    var c = hud.PlayerLeftPanel.GetComponentInParent<Canvas>();
                    if (c != null && c.renderMode != RenderMode.WorldSpace) return c.rootCanvas != null ? c.rootCanvas : c;
                }
                if (hud.PowerBar != null)
                {
                    var c = hud.PowerBar.GetComponentInParent<Canvas>();
                    if (c != null && c.renderMode != RenderMode.WorldSpace) return c.rootCanvas != null ? c.rootCanvas : c;
                }
                var parentCanvas = hud.GetComponentInParent<Canvas>();
                if (parentCanvas != null && parentCanvas.renderMode != RenderMode.WorldSpace) return parentCanvas;
            }

            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c != null && c.name == "GameplayCanvas" && c.renderMode != RenderMode.WorldSpace)
                {
                    return c;
                }
            }

            foreach (var c in canvases)
            {
                if (c != null && c.renderMode != RenderMode.WorldSpace)
                {
                    return c;
                }
            }

            return null;
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

            BuildBreakPlacementBanner();

            gameObject.SetActive(false);
        }

        private void BuildBreakPlacementBanner()
        {
            if (breakBannerGo != null || transform.parent == null) return;

            breakBannerGo = new GameObject("BreakPlacementBanner");
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) breakBannerGo.layer = uiLayer;
            breakBannerGo.transform.SetParent(transform.parent, false);
            breakBannerGo.transform.SetAsLastSibling();

            breakPanelRt = breakBannerGo.AddComponent<RectTransform>();
            breakPanelRt.anchorMin = new Vector2(0f, 1f);
            breakPanelRt.anchorMax = new Vector2(0f, 1f);
            breakPanelRt.pivot = new Vector2(0f, 1f);
            breakPanelRt.sizeDelta = new Vector2(BreakBannerW, BannerH);
            breakPanelRt.anchoredPosition = new Vector2(HiddenX, cachedTargetY);

            breakCanvasGroup = breakBannerGo.AddComponent<CanvasGroup>();
            breakCanvasGroup.alpha = 0f;
            breakCanvasGroup.blocksRaycasts = false;
            breakCanvasGroup.interactable = false;

            // 1. Nền hình bình hành vát góc tông Nâu Hổ Phách đậm (Dark Amber)
            breakBodyGraphic = breakBannerGo.AddComponent<SkewedPanelGraphic>();
            breakBodyGraphic.color = DarkAmberBody;
            breakBodyGraphic.SetInsets(0f, Skew, Skew, 0f);
            breakBodyGraphic.SetBottomDarken(0.35f);
            breakBodyGraphic.raycastTarget = false;

            // 2. Khung viền Vàng Cam rực rỡ
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(breakBannerGo.transform, false);
            breakFrameGraphic = frameGo.GetComponent<BorderProgressGraphic>();
            breakFrameGraphic.raycastTarget = false;
            breakFrameGraphic.SetInsets(0f, Skew, Skew, 0f);
            breakFrameGraphic.SetBaseColor(AmberBright);
            Stretch(breakFrameGraphic.rectTransform, 0f);

            // 3. Dải nhấn sọc Vàng Cam ở cạnh trái
            var accentGo = new GameObject("AccentStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            accentGo.transform.SetParent(breakBannerGo.transform, false);
            breakAccentStrip = accentGo.GetComponent<SkewedPanelGraphic>();
            breakAccentStrip.color = AmberBright;
            breakAccentStrip.SetInsets(0f, 6f, 6f, 0f);
            breakAccentStrip.raycastTarget = false;
            var aRt = breakAccentStrip.rectTransform;
            aRt.anchorMin = new Vector2(0f, 0f);
            aRt.anchorMax = new Vector2(0f, 1f);
            aRt.pivot = new Vector2(0f, 0.5f);
            aRt.anchoredPosition = new Vector2(4f, 0f);
            aRt.sizeDelta = new Vector2(8f, -6f);

            // 4. Icon chỉ báo đặt bi
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            iconGo.transform.SetParent(breakBannerGo.transform, false);
            breakIconText = iconGo.GetComponent<Text>();
            breakIconText.font = font;
            breakIconText.fontSize = 24;
            breakIconText.fontStyle = FontStyle.Bold;
            breakIconText.alignment = TextAnchor.MiddleCenter;
            breakIconText.color = AmberBright;
            breakIconText.text = "✥";
            breakIconText.raycastTarget = false;
            var iRt = breakIconText.rectTransform;
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(30f, 0f);
            iRt.sizeDelta = new Vector2(30f, 30f);

            // 5. Tiêu đề chính: CHỌN VỊ TRÍ ĐỂ THỰC HIỆN CÚ PHÁ BI
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(breakBannerGo.transform, false);
            breakTitleText = titleGo.GetComponent<Text>();
            breakTitleText.font = font;
            breakTitleText.fontSize = 17;
            breakTitleText.fontStyle = FontStyle.Bold;
            breakTitleText.alignment = TextAnchor.MiddleLeft;
            breakTitleText.color = AmberBright;
            breakTitleText.supportRichText = true;
            breakTitleText.raycastTarget = false;
            var tShadow = titleGo.AddComponent<Shadow>();
            tShadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            tShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var tRt = breakTitleText.rectTransform;
            tRt.anchorMin = new Vector2(0f, 0.48f);
            tRt.anchorMax = new Vector2(1f, 1f);
            tRt.offsetMin = new Vector2(52f, 0f);
            tRt.offsetMax = new Vector2(-14f, -5f);

            // 6. Phụ đề hướng dẫn phím W/A/S/D
            var subGo = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subGo.transform.SetParent(breakBannerGo.transform, false);
            breakSubText = subGo.GetComponent<Text>();
            breakSubText.font = font;
            breakSubText.fontSize = 11;
            breakSubText.fontStyle = FontStyle.Normal;
            breakSubText.alignment = TextAnchor.MiddleLeft;
            breakSubText.color = AmberSubColor;
            breakSubText.supportRichText = true;
            breakSubText.raycastTarget = false;
            var sRt = breakSubText.rectTransform;
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0.50f);
            sRt.offsetMin = new Vector2(52f, 6f);
            sRt.offsetMax = new Vector2(-14f, 0f);

            breakBannerGo.SetActive(false);
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
                if (pRt != null && canvasRt != null && canvasRt.rect.height > 100f)
                {
                    Vector3[] corners = new Vector3[4];
                    pRt.GetWorldCorners(corners);
                    // corners[0] là góc dưới-trái của PlayerLeftPanel trong World space
                    Vector3 localBottomLeft = canvasRt.InverseTransformPoint(corners[0]);
                    float canvasTop = canvasRt.rect.height * (1f - canvasRt.pivot.y);
                    float distFromTop = canvasTop - localBottomLeft.y;
                    // Chỉ dùng vị trí động khi PlayerLeftPanel đã ở vị trí ổn định trên màn hình (không bị đẩy ra ngoài bởi HudEntranceAnimator)
                    if (distFromTop >= 40f && distFromTop <= 240f)
                    {
                        targetY = -(distFromTop + 20f); // cách mép dưới của Hub tên 20px
                    }
                }
            }

            // Đảm bảo luôn nằm thấp hơn mép dưới của Hub tên (tối thiểu -150f, tối đa -260f)
            targetY = Mathf.Clamp(targetY, -260f, -150f);
            cachedTargetY = targetY;

            if (panelRt != null)
            {
                panelRt.anchoredPosition = new Vector2(panelRt.anchoredPosition.x, targetY);
            }
        }

        /// <summary>
        /// Hiển thị thông báo Vàng Cam nhấp nháy phía trên bên trái hướng dẫn người chơi chọn vị trí đặt bi phá.
        /// </summary>
        public void ShowBreakPlacementPrompt()
        {
            ShowCustomPrompt(
                LocalizationManager.Get("break_place_banner_title"),
                LocalizationManager.Get("break_place_banner_sub"),
                amberBlink: true);
        }

        /// <summary>
        /// Hiển thị thông báo hướng dẫn / trạng thái khóa lực trong pha Đua Bi Băng Ngắn ở góc trên bên trái.
        /// </summary>
        public void ShowCustomPrompt(string title, string subtitle, bool amberBlink = true)
        {
            UpdateTargetY();
            BuildBreakPlacementBanner();
            if (breakBannerGo == null) return;

            gameObject.SetActive(true);
            breakBannerGo.SetActive(true);
            transform.SetAsLastSibling();
            breakBannerGo.transform.SetAsLastSibling();

            breakTitleText.text = title;
            breakSubText.text = subtitle;

            if (breakBannerCoroutine != null)
            {
                StopCoroutine(breakBannerCoroutine);
            }
            breakBannerCoroutine = StartCoroutine(CustomPromptBannerRoutine(amberBlink));
        }

        /// <summary>
        /// Ẩn thông báo chọn vị trí phá bi ngay khi người chơi thực hiện cú đánh hoặc hết lượt phá.
        /// </summary>
        public void HideBreakPlacementPrompt()
        {
            if (breakBannerGo == null || !breakBannerGo.activeSelf) return;

            if (breakBannerCoroutine != null)
            {
                StopCoroutine(breakBannerCoroutine);
                breakBannerCoroutine = null;
            }

            if (gameObject.activeInHierarchy)
            {
                breakBannerCoroutine = StartCoroutine(HideBreakBannerRoutine());
            }
            else
            {
                breakCanvasGroup.alpha = 0f;
                breakBannerGo.SetActive(false);
            }
        }

        private IEnumerator CustomPromptBannerRoutine(bool amberBlink)
        {
            float stackedY = cachedTargetY - (BannerH + 10f);
            float currentY = isTurnBannerVisible ? stackedY : cachedTargetY;
            bool alreadyShown = breakCanvasGroup != null && breakCanvasGroup.alpha > 0.65f && breakPanelRt.anchoredPosition.x > RestX - 40f;

            if (!alreadyShown)
            {
                breakPanelRt.anchoredPosition = new Vector2(HiddenX, currentY);
                breakCanvasGroup.alpha = 0f;

                // 1. Trượt từ mép trái vào
                const float inDuration = 0.35f;
                float elapsed = 0f;
                while (elapsed < inDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / inDuration);
                    float ease = 1f - Mathf.Pow(1f - t, 3f);

                    float targetSlotY = isTurnBannerVisible ? (cachedTargetY - (BannerH + 10f)) : cachedTargetY;
                    currentY = Mathf.Lerp(currentY, targetSlotY, Time.unscaledDeltaTime * 12f);
                    breakPanelRt.anchoredPosition = new Vector2(Mathf.Lerp(HiddenX, RestX, ease), currentY);
                    breakCanvasGroup.alpha = ease;
                    yield return null;
                }
            }

            if (amberBlink)
            {
                breakBodyGraphic.color = DarkAmberBody;
                breakSubText.color = AmberSubColor;
                breakIconText.text = "❖";
            }
            else
            {
                breakBodyGraphic.color = DarkNavyBody;
                breakSubText.color = SubtitleColor;
                breakIconText.text = "✓";
            }

            // 2. Duy trì hiển thị và nhấp nháy nhịp nhàng
            while (true)
            {
                float targetSlotY = isTurnBannerVisible ? (cachedTargetY - (BannerH + 10f)) : cachedTargetY;
                currentY = Mathf.Lerp(currentY, targetSlotY, Time.unscaledDeltaTime * 10f);
                breakPanelRt.anchoredPosition = new Vector2(RestX, currentY);

                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f);
                if (amberBlink)
                {
                    Color activeColor = Color.Lerp(OrangeDeep, AmberBright, pulse);
                    breakTitleText.color = Color.Lerp(new Color(1f, 0.68f, 0.15f, 1f), new Color(1f, 0.92f, 0.35f, 1f), pulse);
                    breakIconText.color = activeColor;
                    breakAccentStrip.color = activeColor;
                    breakFrameGraphic.SetBaseColor(new Color(activeColor.r, activeColor.g, activeColor.b, Mathf.Lerp(0.45f, 1.0f, pulse)));
                    breakCanvasGroup.alpha = Mathf.Lerp(0.78f, 1.0f, pulse);
                }
                else
                {
                    Color activeCyan = Color.Lerp(new Color(0f, 0.72f, 0.95f, 1f), CyanNeon, pulse);
                    breakTitleText.color = Color.Lerp(new Color(0.55f, 0.94f, 1f, 1f), Color.white, pulse);
                    breakIconText.color = activeCyan;
                    breakAccentStrip.color = activeCyan;
                    breakFrameGraphic.SetBaseColor(new Color(activeCyan.r, activeCyan.g, activeCyan.b, Mathf.Lerp(0.60f, 1.0f, pulse)));
                    breakCanvasGroup.alpha = 0.98f;
                }

                yield return null;
            }
        }

        private IEnumerator HideBreakBannerRoutine()
        {
            float startX = breakPanelRt.anchoredPosition.x;
            float currentY = breakPanelRt.anchoredPosition.y;
            float startAlpha = breakCanvasGroup.alpha;

            const float outDuration = 0.22f;
            float elapsed = 0f;
            while (elapsed < outDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / outDuration);
                float ease = t * t;

                breakPanelRt.anchoredPosition = new Vector2(Mathf.Lerp(startX, HiddenX, ease), currentY);
                breakCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }

            breakCanvasGroup.alpha = 0f;
            breakPanelRt.anchoredPosition = new Vector2(HiddenX, currentY);
            breakBannerGo.SetActive(false);
            breakBannerCoroutine = null;

            if (!isTurnBannerVisible)
            {
                gameObject.SetActive(false);
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
            isTurnBannerVisible = true;
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
            isTurnBannerVisible = false;
            slideCoroutine = null;

            if (breakBannerGo == null || !breakBannerGo.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
