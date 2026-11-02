using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Giao diện Vòng Quay Ngẫu Nhiên (Spin Wheel) cỡ lớn phong cách HUD Esports hiện đại
    /// dùng để chọn cơ thủ thực hiện cú phá bi mở màn (Rack 1).
    /// Chia thành 8 ô quạt xen kẽ giữa Bạn (Neon Cyan) và Đối Thủ (Amber Gold),
    /// vành kim loại 24 bóng đèn LED đuổi sáng, huy hiệu Bi số 8 3D ở tâm và kim chỉ có nảy vật lý.
    /// </summary>
    public class BreakTossWheelUI : MonoBehaviour
    {
        private const int SliceCount = 8;
        private const float SliceAngle = 360f / SliceCount; // 45 độ mỗi ô

        private CanvasGroup rootGroup;
        private RectTransform cardRt;
        private RectTransform wheelContainerRt;
        private WheelDiskGraphic wheelDisk;
        private RectTransform pointerRt;
        private Text titleText;
        private Text subText;
        private Text statusText;
        private SkewedPanelGraphic statusBadgeBg;
        private BorderProgressGraphic statusBadgeFrame;

        private AudioSource tickAudioSource;
        private AudioClip tickClip;
        private AudioClip winClip;

        private bool isPlaying;
        public bool IsPlaying => isPlaying;

        // Bảng màu cao cấp
        private static readonly Color LocalSliceOuter = new Color(0.00f, 0.68f, 0.98f, 1f);
        private static readonly Color LocalSliceMid = new Color(0.02f, 0.40f, 0.72f, 1f);
        private static readonly Color LocalSliceInner = new Color(0.02f, 0.18f, 0.38f, 1f);

        private static readonly Color OpponentSliceOuter = new Color(0.98f, 0.48f, 0.05f, 1f);
        private static readonly Color OpponentSliceMid = new Color(0.72f, 0.26f, 0.03f, 1f);
        private static readonly Color OpponentSliceInner = new Color(0.35f, 0.10f, 0.02f, 1f);

        private static readonly Color CyanGlow = new Color(0.00f, 0.94f, 1.00f, 1f);
        private static readonly Color AmberGlow = new Color(1.00f, 0.78f, 0.14f, 1f);

        /// <summary>
        /// Hiển thị và chạy vòng quay chọn người phá bi đầu tiên.
        /// winnerIndex: 0 (Player A) hoặc 1 (Player B).
        /// localPlayerIndex: 0 hoặc 1 (để hiển thị đúng màu "BẠN" và "ĐỐI THỦ" trên máy của từng người).
        /// </summary>
        public static Coroutine PlayWheel(
            MonoBehaviour host,
            int winnerIndex,
            int localPlayerIndex,
            string playerAName,
            string playerBName,
            int deterministicSeed,
            Action onComplete)
        {
            var canvas = FindTargetCanvas();
            if (canvas == null)
            {
                onComplete?.Invoke();
                return null;
            }

            var existing = FindObjectOfType<BreakTossWheelUI>(true);
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            var go = new GameObject("BreakTossWheelModal");
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0) go.layer = uiLayer;
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var ui = go.AddComponent<BreakTossWheelUI>();
            string localLabel = LocalizationManager.Get("wheel_you_short", "BẠN");
            string oppName = (localPlayerIndex == 0) ? playerBName : playerAName;
            if (string.IsNullOrWhiteSpace(oppName))
            {
                oppName = LocalizationManager.Get("turn_banner_opponent", "ĐỐI THỦ");
            }
            string oppShort = TruncateLabel(oppName, 12);

            bool didLocalWin = (winnerIndex == localPlayerIndex);
            string winnerFullName = didLocalWin ? localLabel : oppName;

            ui.BuildUI(localLabel, oppShort);
            return ui.StartCoroutine(ui.SpinRoutine(didLocalWin, winnerFullName, deterministicSeed, onComplete));
        }

        private static string TruncateLabel(string s, int maxChars)
        {
            if (string.IsNullOrEmpty(s)) return "ĐỐI THỦ";
            s = s.Trim().ToUpperInvariant();
            if (s.Length <= maxChars) return s;
            return s.Substring(0, maxChars - 1) + "…";
        }

        private static Canvas FindTargetCanvas()
        {
            var hud = FindObjectOfType<GameplayHUD>(true);
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
            }

            var canvases = FindObjectsOfType<Canvas>(true);
            foreach (var c in canvases)
            {
                if (c != null && c.name == "GameplayCanvas" && c.renderMode != RenderMode.WorldSpace)
                    return c;
            }
            foreach (var c in canvases)
            {
                if (c != null && c.renderMode != RenderMode.WorldSpace)
                    return c;
            }
            return null;
        }

        private void BuildUI(string localLabel, string opponentLabel)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var rootRt = gameObject.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            rootGroup = gameObject.AddComponent<CanvasGroup>();
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = false;

            // 1. Lớp phủ tối mờ toàn màn hình
            var backdropImg = gameObject.AddComponent<Image>();
            backdropImg.color = new Color(0.01f, 0.04f, 0.09f, 0.86f);
            backdropImg.raycastTarget = true;

            // 2. Khung Card HUD trung tâm cỡ lớn (720 x 690) vát góc hiện đại
            var cardGo = new GameObject("Container", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            cardGo.transform.SetParent(transform, false);
            cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(720f, 690f);
            cardRt.anchoredPosition = Vector2.zero;

            var cardBg = cardGo.GetComponent<SkewedPanelGraphic>();
            cardBg.color = new Color(0.04f, 0.09f, 0.18f, 0.96f);
            cardBg.SetInsets(0f, 34f, 34f, 0f);
            cardBg.SetBottomDarken(0.45f);
            cardBg.raycastTarget = false;

            var cardFrameGo = new GameObject("CardFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            cardFrameGo.transform.SetParent(cardRt, false);
            var cfRt = cardFrameGo.GetComponent<RectTransform>();
            cfRt.anchorMin = Vector2.zero;
            cfRt.anchorMax = Vector2.one;
            cfRt.offsetMin = Vector2.zero;
            cfRt.offsetMax = Vector2.zero;
            var cfGraphic = cardFrameGo.GetComponent<BorderProgressGraphic>();
            cfGraphic.SetInsets(0f, 34f, 34f, 0f);
            cfGraphic.SetBaseColor(new Color(0.18f, 0.76f, 1.00f, 0.72f));
            cfGraphic.raycastTarget = false;

            // 3. Tiêu đề phía trên cỡ lớn
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(cardRt, false);
            titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 30;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.96f, 0.99f, 1f, 1f);
            titleText.text = LocalizationManager.Get("wheel_title", "VÒNG QUAY PHÁ BI ĐẦU TIÊN");
            titleText.raycastTarget = false;
            var titleShadow = titleGo.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0.65f, 1f, 0.55f);
            titleShadow.effectDistance = new Vector2(0f, -2f);
            var titleRt = titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -18f);
            titleRt.sizeDelta = new Vector2(680f, 40f);

            // 4. Phụ đề
            var subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            subGo.transform.SetParent(cardRt, false);
            subText = subGo.GetComponent<Text>();
            subText.font = font;
            subText.fontSize = 15;
            subText.fontStyle = FontStyle.Normal;
            subText.alignment = TextAnchor.MiddleCenter;
            subText.color = new Color(0.68f, 0.86f, 1f, 0.92f);
            subText.text = LocalizationManager.Get("wheel_subtitle", "Quay ngẫu nhiên chọn cơ thủ thực hiện cú phá bi mở màn");
            subText.raycastTarget = false;
            var subRt = subText.rectTransform;
            subRt.anchorMin = new Vector2(0.5f, 1f);
            subRt.anchorMax = new Vector2(0.5f, 1f);
            subRt.pivot = new Vector2(0.5f, 1f);
            subRt.anchoredPosition = new Vector2(0f, -58f);
            subRt.sizeDelta = new Vector2(650f, 26f);

            // 5. Đĩa vòng quay tròn cỡ lớn (Đường kính 455px, 8 ô quạt xen kẽ)
            const float wheelDiameter = 455f;
            var wheelGo = new GameObject("WheelDisk", typeof(RectTransform), typeof(CanvasRenderer), typeof(WheelDiskGraphic));
            wheelGo.transform.SetParent(cardRt, false);
            wheelContainerRt = wheelGo.GetComponent<RectTransform>();
            wheelContainerRt.anchorMin = new Vector2(0.5f, 0.5f);
            wheelContainerRt.anchorMax = new Vector2(0.5f, 0.5f);
            wheelContainerRt.pivot = new Vector2(0.5f, 0.5f);
            wheelContainerRt.anchoredPosition = new Vector2(0f, 8f);
            wheelContainerRt.sizeDelta = new Vector2(wheelDiameter, wheelDiameter);

            wheelDisk = wheelGo.GetComponent<WheelDiskGraphic>();
            wheelDisk.raycastTarget = false;

            // 6. Chữ tên trên 8 ô của vòng quay (Ô chẵn = Bạn, Ô lẻ = Đối thủ)
            float labelRadius = wheelDiameter * 0.295f;
            for (int i = 0; i < SliceCount; i++)
            {
                bool isLocalSlice = (i % 2 == 0);
                float centerAngleFromTopCW = i * SliceAngle + SliceAngle * 0.5f;
                float mathRad = (90f - centerAngleFromTopCW) * Mathf.Deg2Rad;

                var lblGo = new GameObject($"SliceLabel_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGo.transform.SetParent(wheelContainerRt, false);
                var lbl = lblGo.GetComponent<Text>();
                lbl.font = font;
                lbl.fontSize = isLocalSlice ? 19 : 16;
                lbl.fontStyle = FontStyle.Bold;
                lbl.alignment = TextAnchor.MiddleCenter;
                lbl.color = isLocalSlice ? new Color(0.94f, 0.99f, 1f, 1f) : new Color(1f, 0.97f, 0.88f, 1f);
                lbl.text = isLocalSlice ? localLabel : opponentLabel;
                lbl.raycastTarget = false;

                var sh = lblGo.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.92f);
                sh.effectDistance = new Vector2(1.5f, -1.5f);

                var lRt = lbl.rectTransform;
                lRt.anchorMin = new Vector2(0.5f, 0.5f);
                lRt.anchorMax = new Vector2(0.5f, 0.5f);
                lRt.pivot = new Vector2(0.5f, 0.5f);
                lRt.anchoredPosition = new Vector2(Mathf.Cos(mathRad) * labelRadius, Mathf.Sin(mathRad) * labelRadius);
                lRt.sizeDelta = new Vector2(138f, 36f);
                lRt.localRotation = Quaternion.Euler(0f, 0f, -centerAngleFromTopCW);
            }

            // 7. Huy hiệu Bi Số 8 ở tâm vòng quay
            var centerTextGo = new GameObject("CenterBadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            centerTextGo.transform.SetParent(cardRt, false);
            var centerTxt = centerTextGo.GetComponent<Text>();
            centerTxt.font = font;
            centerTxt.fontSize = 27;
            centerTxt.fontStyle = FontStyle.Bold;
            centerTxt.alignment = TextAnchor.MiddleCenter;
            centerTxt.color = new Color(0.08f, 0.12f, 0.20f, 1f);
            centerTxt.text = "8";
            centerTxt.raycastTarget = false;
            var cRt = centerTxt.rectTransform;
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = wheelContainerRt.anchoredPosition;
            cRt.sizeDelta = new Vector2(56f, 56f);

            // 8. Kim chỉ Vàng Hoàng Kim ở đỉnh 12h (Pointer)
            var pointerGo = new GameObject("TopPointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(WheelPointerGraphic));
            pointerGo.transform.SetParent(cardRt, false);
            pointerRt = pointerGo.GetComponent<RectTransform>();
            pointerRt.anchorMin = new Vector2(0.5f, 0.5f);
            pointerRt.anchorMax = new Vector2(0.5f, 0.5f);
            pointerRt.pivot = new Vector2(0.5f, 0.85f);
            pointerRt.anchoredPosition = new Vector2(0f, wheelContainerRt.anchoredPosition.y + wheelDiameter * 0.5f + 8f);
            pointerRt.sizeDelta = new Vector2(48f, 58f);
            pointerGo.GetComponent<WheelPointerGraphic>().raycastTarget = false;

            // 9. Thanh thông báo kết quả vát góc kiểu HUD bên dưới vòng quay
            var badgeGo = new GameObject("StatusBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            badgeGo.transform.SetParent(cardRt, false);
            statusBadgeBg = badgeGo.GetComponent<SkewedPanelGraphic>();
            statusBadgeBg.color = new Color(0.04f, 0.15f, 0.28f, 0.96f);
            statusBadgeBg.SetInsets(0f, 16f, 16f, 0f);
            statusBadgeBg.SetBottomDarken(0.35f);
            statusBadgeBg.raycastTarget = false;
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 0f);
            badgeRt.anchorMax = new Vector2(0.5f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0f);
            badgeRt.anchoredPosition = new Vector2(0f, 22f);
            badgeRt.sizeDelta = new Vector2(580f, 58f);

            var badgeFrameGo = new GameObject("BadgeFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            badgeFrameGo.transform.SetParent(badgeRt, false);
            var bfRt = badgeFrameGo.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;
            statusBadgeFrame = badgeFrameGo.GetComponent<BorderProgressGraphic>();
            statusBadgeFrame.SetInsets(0f, 16f, 16f, 0f);
            statusBadgeFrame.SetBaseColor(CyanGlow);
            statusBadgeFrame.raycastTarget = false;

            var statusGo = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            statusGo.transform.SetParent(badgeRt, false);
            statusText = statusGo.GetComponent<Text>();
            statusText.font = font;
            statusText.fontSize = 21;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = CyanGlow;
            statusText.supportRichText = true;
            statusText.raycastTarget = false;
            statusText.text = LocalizationManager.Get("wheel_spinning", "ĐANG QUAY CHỌN NGƯỜI PHÁ BI...");
            var stShadow = statusGo.AddComponent<Shadow>();
            stShadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            stShadow.effectDistance = new Vector2(1.5f, -1.5f);
            var stRt = statusText.rectTransform;
            stRt.anchorMin = Vector2.zero;
            stRt.anchorMax = Vector2.one;
            stRt.offsetMin = new Vector2(16f, 4f);
            stRt.offsetMax = new Vector2(-16f, -4f);

            SetupProceduralAudio();
        }

        private void SetupProceduralAudio()
        {
            tickAudioSource = gameObject.AddComponent<AudioSource>();
            tickAudioSource.playOnAwake = false;
            tickAudioSource.spatialBlend = 0f;
            tickAudioSource.volume = 0.45f;

            int sampleRate = 22050;
            int tickSamples = sampleRate / 45;
            float[] tickData = new float[tickSamples];
            for (int i = 0; i < tickSamples; i++)
            {
                float t = (float)i / tickSamples;
                float env = Mathf.Pow(1f - t, 3f);
                tickData[i] = Mathf.Sin(2f * Mathf.PI * 1150f * i / sampleRate) * env * 0.7f;
            }
            tickClip = AudioClip.Create("WheelTick", tickSamples, 1, sampleRate, false);
            tickClip.SetData(tickData, 0);

            int winSamples = sampleRate / 4;
            float[] winData = new float[winSamples];
            for (int i = 0; i < winSamples; i++)
            {
                float t = (float)i / winSamples;
                float env = Mathf.Pow(1f - t, 1.6f);
                float freq = (t < 0.45f) ? 660f : 880f;
                winData[i] = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate) * env * 0.6f;
            }
            winClip = AudioClip.Create("WheelWin", winSamples, 1, sampleRate, false);
            winClip.SetData(winData, 0);
        }

        private IEnumerator SpinRoutine(bool didLocalWin, string winnerDisplayName, int seed, Action onComplete)
        {
            isPlaying = true;
            HudModalState.ForceOpen("break_wheel");

            const float fadeInDur = 0.24f;
            float elapsed = 0f;
            while (elapsed < fadeInDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeInDur);
                rootGroup.alpha = t;
                cardRt.localScale = Vector3.Lerp(new Vector3(0.88f, 0.88f, 1f), Vector3.one, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            rootGroup.alpha = 1f;
            cardRt.localScale = Vector3.one;

            System.Random rng = new System.Random(seed);
            int pairIndex = rng.Next(0, SliceCount / 2); // 0..3
            int winningSliceIndex = pairIndex * 2 + (didLocalWin ? 0 : 1);

            float sliceCenterAngle = winningSliceIndex * SliceAngle + SliceAngle * 0.5f;
            float jitter = (float)(rng.NextDouble() * 20.0 - 10.0);
            int fullSpins = 5;
            float targetRotationDeg = fullSpins * 360f + sliceCenterAngle + jitter;

            const float spinDuration = 2.75f;
            elapsed = 0f;
            int lastBoundaryIndex = 0;
            float pointerBounceAngle = 0f;

            while (elapsed < spinDuration)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                float t = Mathf.Clamp01(elapsed / spinDuration);
                float ease = 1f - Mathf.Pow(1f - t, 4f);
                float currentDeg = targetRotationDeg * ease;

                wheelContainerRt.localRotation = Quaternion.Euler(0f, 0f, currentDeg);
                wheelDisk.SetBulbPhase(currentDeg * 0.10f);

                int boundaryIndex = Mathf.FloorToInt(currentDeg / SliceAngle);
                if (boundaryIndex > lastBoundaryIndex)
                {
                    lastBoundaryIndex = boundaryIndex;
                    pointerBounceAngle = -30f * Mathf.Clamp01(1.15f - t * 0.7f);
                    if (tickAudioSource != null && tickClip != null)
                    {
                        tickAudioSource.pitch = Mathf.Lerp(1.18f, 0.92f, t);
                        tickAudioSource.PlayOneShot(tickClip);
                    }
                }

                pointerBounceAngle = Mathf.Lerp(pointerBounceAngle, 0f, dt * 18f);
                pointerRt.localRotation = Quaternion.Euler(0f, 0f, pointerBounceAngle);

                yield return null;
            }

            wheelContainerRt.localRotation = Quaternion.Euler(0f, 0f, targetRotationDeg);
            pointerRt.localRotation = Quaternion.identity;

            wheelDisk.SetHighlightedSlice(winningSliceIndex, 1f);
            if (tickAudioSource != null && winClip != null)
            {
                tickAudioSource.pitch = 1f;
                tickAudioSource.PlayOneShot(winClip);
            }

            if (didLocalWin)
            {
                statusBadgeBg.color = new Color(0.03f, 0.30f, 0.50f, 0.98f);
                statusBadgeFrame.SetBaseColor(CyanGlow);
                statusText.color = CyanGlow;
                statusText.text = LocalizationManager.Get("wheel_win_you", "🎯 BẠN ĐƯỢC QUYỀN PHÁ BI TRƯỚC!");
            }
            else
            {
                statusBadgeBg.color = new Color(0.38f, 0.16f, 0.03f, 0.98f);
                statusBadgeFrame.SetBaseColor(AmberGlow);
                statusText.color = AmberGlow;
                statusText.text = LocalizationManager.Format("wheel_win_opponent", winnerDisplayName);
            }

            const float holdDuration = 1.0f;
            elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(elapsed * 16f);
                wheelDisk.SetHighlightedSlice(winningSliceIndex, Mathf.Lerp(0.45f, 1.0f, pulse));
                statusBadgeBg.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(1.04f, 1.04f, 1f), pulse);
                yield return null;
            }

            const float fadeOutDur = 0.22f;
            elapsed = 0f;
            while (elapsed < fadeOutDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDur);
                rootGroup.alpha = 1f - t;
                yield return null;
            }

            HudModalState.Release("break_wheel");
            isPlaying = false;
            onComplete?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (isPlaying)
            {
                HudModalState.Release("break_wheel");
                isPlaying = false;
            }
            if (tickClip != null) Destroy(tickClip);
            if (winClip != null) Destroy(winClip);
        }

        /// <summary>
        /// Graphic vẽ đĩa vòng quay 8 ô cao cấp, hào quang neon ngoài, vành 24 đèn LED, đinh tán kim cương và huy hiệu Bi số 8 3D ở tâm.
        /// </summary>
        private class WheelDiskGraphic : MaskableGraphic
        {
            private int highlightedSlice = -1;
            private float highlightIntensity = 0f;
            private float bulbPhase = 0f;

            public void SetHighlightedSlice(int sliceIdx, float intensity)
            {
                highlightedSlice = sliceIdx;
                highlightIntensity = intensity;
                SetVerticesDirty();
            }

            public void SetBulbPhase(float phase)
            {
                bulbPhase = phase;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;
                float maxRadius = Mathf.Min(r.width, r.height) * 0.5f;
                if (maxRadius <= 4f) return;

                float outerHaloR = maxRadius;
                float outerRimR = maxRadius * 0.96f;
                float innerBezelR = maxRadius * 0.855f;
                float midSliceR = maxRadius * 0.53f;
                float hubOuterR = maxRadius * 0.205f;
                float hubSphereR = maxRadius * 0.165f;
                float hubWhitePatchR = maxRadius * 0.088f;

                // 0. Hào quang phát sáng nhẹ bao quanh đĩa quay
                DrawRing(vh, outerRimR * 0.98f, outerHaloR, 64,
                    new Color(0.0f, 0.85f, 1.0f, 0.35f),
                    new Color(0.0f, 0.85f, 1.0f, 0.0f));

                // 1. Vành Bezel kép mạ Vàng Hoàng Kim & Xanh Đêm
                DrawRing(vh, innerBezelR * 0.98f, outerRimR, 72,
                    new Color(0.09f, 0.14f, 0.24f, 1f),
                    new Color(0.88f, 0.68f, 0.20f, 1f));
                DrawRing(vh, innerBezelR * 0.97f, innerBezelR * 1.02f, 72,
                    new Color(1.0f, 0.86f, 0.35f, 1f),
                    new Color(0.55f, 0.38f, 0.08f, 1f));

                // 2. Vẽ 8 ô quạt với chuyển sắc 2 tầng (Hub -> Mid -> Outer) mịn màng
                const int stepsPerSlice = 10;
                for (int s = 0; s < SliceCount; s++)
                {
                    bool isLocal = (s % 2 == 0);
                    Color cIn = isLocal ? LocalSliceInner : OpponentSliceInner;
                    Color cMid = isLocal ? LocalSliceMid : OpponentSliceMid;
                    Color cOut = isLocal ? LocalSliceOuter : OpponentSliceOuter;

                    if (s == highlightedSlice && highlightIntensity > 0.01f)
                    {
                        Color glow = isLocal ? CyanGlow : AmberGlow;
                        cIn = Color.Lerp(cIn, glow, highlightIntensity * 0.55f);
                        cMid = Color.Lerp(cMid, glow, highlightIntensity * 0.65f);
                        cOut = Color.Lerp(cOut, Color.Lerp(glow, Color.white, 0.42f), highlightIntensity * 0.82f);
                    }

                    float startCW = s * SliceAngle;
                    float endCW = (s + 1) * SliceAngle;

                    for (int step = 0; step < stepsPerSlice; step++)
                    {
                        float a0 = (90f - Mathf.Lerp(startCW, endCW, (float)step / stepsPerSlice)) * Mathf.Deg2Rad;
                        float a1 = (90f - Mathf.Lerp(startCW, endCW, (float)(step + 1) / stepsPerSlice)) * Mathf.Deg2Rad;

                        Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
                        Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));

                        // Tầng trong (Hub -> Mid)
                        AddQuad(vh, d0 * hubOuterR, d1 * hubOuterR, d1 * midSliceR, d0 * midSliceR, cIn, cIn, cMid, cMid);
                        // Tầng ngoài (Mid -> InnerBezel)
                        AddQuad(vh, d0 * midSliceR, d1 * midSliceR, d1 * innerBezelR, d0 * innerBezelR, cMid, cMid, cOut, cOut);
                    }
                }

                // 2b. Vòng chỉ mảnh trang trí giữa đĩa
                DrawRing(vh, midSliceR - 1.2f, midSliceR + 1.2f, 64,
                    new Color(1f, 1f, 1f, 0.16f),
                    new Color(1f, 1f, 1f, 0.16f));

                // 3. Vẽ 8 thanh chia ô (Spokes) màu Vàng Ánh Kim + đinh tán kim cương ở đầu thanh
                Color spokeLight = new Color(1.0f, 0.90f, 0.50f, 0.96f);
                Color spokeDark = new Color(0.72f, 0.48f, 0.12f, 0.96f);
                for (int s = 0; s < SliceCount; s++)
                {
                    float angleRad = (90f - s * SliceAngle) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
                    Vector2 n = new Vector2(-dir.y, dir.x) * 2.2f;

                    Vector2 a = dir * hubOuterR - n;
                    Vector2 b = dir * hubOuterR + n;
                    Vector2 c = dir * innerBezelR + n;
                    Vector2 d = dir * innerBezelR - n;
                    AddQuad(vh, a, b, c, d, spokeDark, spokeLight, spokeLight, spokeDark);

                    // Đinh tán hình thoi nơi thanh chia chạm vành bezel
                    Vector2 pinCenter = dir * (innerBezelR - 4f);
                    DrawDisc(vh, pinCenter, 4.5f, 4, new Color(1f, 0.95f, 0.72f, 1f));
                }

                // 4. Vẽ 24 bóng đèn LED đuổi sáng trên vành bezel
                const int bulbCount = 24;
                float bulbOrbitR = (innerBezelR + outerRimR) * 0.5f;
                for (int b = 0; b < bulbCount; b++)
                {
                    float aRad = (90f - b * (360f / bulbCount)) * Mathf.Deg2Rad;
                    Vector2 center = new Vector2(Mathf.Cos(aRad) * bulbOrbitR, Mathf.Sin(aRad) * bulbOrbitR);
                    bool lit = ((b + Mathf.FloorToInt(bulbPhase)) % 2 == 0);
                    bool isCyanBulb = (b % 4 == 0);
                    if (lit)
                    {
                        Color haloCol = isCyanBulb ? new Color(0f, 0.95f, 1f, 0.45f) : new Color(1f, 0.82f, 0.20f, 0.45f);
                        Color coreCol = isCyanBulb ? new Color(0.80f, 0.99f, 1f, 1f) : new Color(1f, 0.98f, 0.75f, 1f);
                        DrawDisc(vh, center, 6.8f, 10, haloCol);
                        DrawDisc(vh, center, 3.8f, 10, coreCol);
                    }
                    else
                    {
                        DrawDisc(vh, center, 3.2f, 8, new Color(0.35f, 0.26f, 0.12f, 0.85f));
                    }
                }

                // 5. Vẽ huy hiệu trung tâm (3D Bi Số 8 Đen Bóng viền Vàng Hoàng Kim)
                DrawRing(vh, hubSphereR, hubOuterR, 40, AmberGlow, new Color(0.58f, 0.36f, 0.06f, 1f));
                DrawDisc(vh, Vector2.zero, hubSphereR, 36, new Color(0.06f, 0.08f, 0.13f, 1f));
                // Vệt bóng phản quang trên mặt bi số 8
                DrawDisc(vh, new Vector2(-hubSphereR * 0.28f, hubSphereR * 0.32f), hubSphereR * 0.25f, 16, new Color(1f, 1f, 1f, 0.14f));
                // Lòng trắng chứa số 8 ở chính giữa
                DrawDisc(vh, Vector2.zero, hubWhitePatchR, 28, new Color(0.96f, 0.97f, 0.99f, 1f));
            }

            private static void DrawRing(VertexHelper vh, float rIn, float rOut, int segments, Color cIn, Color cOut)
            {
                for (int i = 0; i < segments; i++)
                {
                    float a0 = (i / (float)segments) * Mathf.PI * 2f;
                    float a1 = ((i + 1) / (float)segments) * Mathf.PI * 2f;

                    Vector2 p0In = new Vector2(Mathf.Cos(a0) * rIn, Mathf.Sin(a0) * rIn);
                    Vector2 p1In = new Vector2(Mathf.Cos(a1) * rIn, Mathf.Sin(a1) * rIn);
                    Vector2 p1Out = new Vector2(Mathf.Cos(a1) * rOut, Mathf.Sin(a1) * rOut);
                    Vector2 p0Out = new Vector2(Mathf.Cos(a0) * rOut, Mathf.Sin(a0) * rOut);

                    AddQuad(vh, p0In, p1In, p1Out, p0Out, cIn, cIn, cOut, cOut);
                }
            }

            private static void DrawDisc(VertexHelper vh, Vector2 center, float radius, int segments, Color col)
            {
                int centerIdx = vh.currentVertCount;
                vh.AddVert(center, col, Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float a = (i / (float)segments) * Mathf.PI * 2f;
                    Vector2 pt = center + new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                    vh.AddVert(pt, col, Vector2.zero);
                }
                for (int i = 0; i < segments; i++)
                {
                    vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 2 + i);
                }
            }

            private static void AddQuad(VertexHelper vh, Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3,
                Color c0, Color c1, Color c2, Color c3)
            {
                int idx = vh.currentVertCount;
                vh.AddVert(v0, c0, Vector2.zero);
                vh.AddVert(v1, c1, Vector2.zero);
                vh.AddVert(v2, c2, Vector2.zero);
                vh.AddVert(v3, c3, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
        }

        /// <summary>
        /// Graphic vẽ kim chỉ (Pointer) hình mũi giáo Vàng Hoàng Kim đính ngọc Cyan ở đỉnh 12h.
        /// </summary>
        private class WheelPointerGraphic : MaskableGraphic
        {
            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = rectTransform.rect;
                float w = r.width * 0.5f;
                float yTop = r.yMax;
                float yMid = Mathf.Lerp(r.yMin, r.yMax, 0.68f);
                float yTip = r.yMin;

                Color goldBright = new Color(1.0f, 0.90f, 0.28f, 1f);
                Color goldDark = new Color(0.82f, 0.46f, 0.04f, 1f);
                Color shadowCol = new Color(0f, 0f, 0f, 0.62f);

                Vector2 sOff = new Vector2(2.5f, -3.5f);
                AddTriangle(vh,
                    new Vector2(-w * 0.80f, yMid) + sOff,
                    new Vector2(w * 0.80f, yMid) + sOff,
                    new Vector2(0f, yTip) + sOff,
                    shadowCol, shadowCol, shadowCol);

                AddTriangle(vh,
                    new Vector2(0f, yTop),
                    new Vector2(-w * 0.78f, yMid),
                    new Vector2(0f, yTip),
                    goldBright, goldBright, goldBright);

                AddTriangle(vh,
                    new Vector2(0f, yTop),
                    new Vector2(0f, yTip),
                    new Vector2(w * 0.78f, yMid),
                    goldBright, goldDark, goldDark);
            }

            private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
            {
                int idx = vh.currentVertCount;
                vh.AddVert(a, ca, Vector2.zero);
                vh.AddVert(b, cb, Vector2.zero);
                vh.AddVert(c, cc, Vector2.zero);
                vh.AddTriangle(idx, idx + 1, idx + 2);
            }
        }
    }
}
