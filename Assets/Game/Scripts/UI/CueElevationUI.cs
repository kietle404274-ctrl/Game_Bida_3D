using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Dựng và điều khiển giao diện chọn góc nâng cơ lúc chạy (không cần sửa scene):
    /// - Panel "GÓC CƠ [K]" hình bình hành nghiêng "\" nằm ngay trên khung "ĐIỂM CƠ [J]" (nghiêng "/") tạo thành chữ V,
    ///   viền sáng chạy theo góc đã chọn, thước đo góc có vạch chia, cây cơ phát sáng khi đủ góc nhảy.
    /// - Popup giữa màn hình cùng phong cách: thước đo góc lớn, kéo cơ quanh bi, thẻ thông số độ cao / tầm bay.
    /// </summary>
    public class CueElevationUI : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.07f, 0.11f, 0.19f, 0.94f);
        private static readonly Color ChipColor = new Color(0.11f, 0.17f, 0.29f, 0.96f);
        private static readonly Color Gold = new Color(0.96f, 0.78f, 0.26f, 1f);
        private static readonly Color Cyan = new Color(0.30f, 0.78f, 0.95f, 1f);
        private static readonly Color Wood = new Color(0.86f, 0.66f, 0.36f, 1f);
        private static readonly Color BallColor = new Color(0.96f, 0.96f, 0.93f, 1f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.28f);
        private static readonly Color BorderBase = new Color(0.30f, 0.78f, 0.95f, 0.55f);

        private static Sprite circleSprite;

        private class Tick
        {
            public Image image;
            public float angle;
        }

        private class Protractor
        {
            public RectTransform cuePivot;
            public Image pie;
            public Image cueGlow;
            public readonly List<Tick> ticks = new List<Tick>();
        }

        private CueElevationSelector selector;
        private Font font;

        // HUD nhỏ
        private Protractor hudGauge;
        private BorderProgressGraphic hudBorder;
        private Text hudTitleText;
        private Text hudAngleText;
        private Image hudTagBg;
        private Text hudTagText;

        // Popup
        private CanvasGroup popupGroup;
        private Protractor popupGauge;
        private BorderProgressGraphic popupBorder;
        private Text popupTitleText;
        private Text popupAngleText;
        private Image popupTagBg;
        private Text popupTagText;
        private Text popupDescText;
        private Text popupHeightLabel;
        private Text popupHeightValue;
        private Text popupRangeLabel;
        private Text popupRangeValue;
        private Text popupHintText;

        private Input.PlayerInputController inputController;
        private Match.BallManager ballManager;
        private bool jumpActive;

        public static void Build(CueElevationSelector owner)
        {
            var anchor = FindObjectOfType<SpinPopupVisibility>(true);
            if (anchor == null) return;

            Transform parent = anchor.transform.parent;
            if (parent == null) return;

            var ui = owner.gameObject.AddComponent<CueElevationUI>();
            ui.selector = owner;
            ui.font = FindFont(parent);
            ui.BuildHudPanel(parent);
            ui.BuildPopup(parent);

            owner.OnAngleChanged += ui.Refresh;
            owner.OnSelectorToggled += ui.HandleToggled;
            LocalizationManager.OnLanguageChanged -= ui.RefreshLocalizedTexts;
            LocalizationManager.OnLanguageChanged += ui.RefreshLocalizedTexts;
            ui.Refresh(owner.CurrentAngleDeg);
            ui.HandleToggled(false);
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            if (selector != null)
            {
                selector.OnAngleChanged -= Refresh;
                selector.OnSelectorToggled -= HandleToggled;
            }
        }

        private void RefreshLocalizedTexts()
        {
            if (hudTitleText != null) hudTitleText.text = LocalizationManager.Get("elev_title");
            if (popupTitleText != null) popupTitleText.text = LocalizationManager.Get("elev_popup_title");
            if (popupHeightLabel != null) popupHeightLabel.text = LocalizationManager.Get("elev_height");
            if (popupRangeLabel != null) popupRangeLabel.text = LocalizationManager.Get("elev_range");
            if (popupHintText != null) popupHintText.text = LocalizationManager.Get("elev_hint");
            if (selector != null) Refresh(selector.CurrentAngleDeg);
        }

        #region Helpers

        private static Font FindFont(Transform root)
        {
            var existing = root.GetComponentInChildren<Text>(true);
            if (existing != null && existing.font != null) return existing.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null) return circleSprite;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return circleSprite;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Khung tứ giác lệch cạnh (inset > 0 = thu vào theo trục X), giống khung ĐIỂM CƠ.</summary>
        private static SkewedPanelGraphic NewSkewed(string name, Transform parent, Color color, float tl, float tr, float bl, float br, float darken)
        {
            var rt = NewRect(name, parent);
            var g = rt.gameObject.AddComponent<SkewedPanelGraphic>();
            g.color = color;
            g.raycastTarget = false;
            g.SetInsets(tl, tr, bl, br);
            g.SetBottomDarken(darken);
            return g;
        }

        private static BorderProgressGraphic NewBorder(Transform parent, float tl, float tr, float bl, float br)
        {
            var rt = NewRect("Frame", parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var b = rt.gameObject.AddComponent<BorderProgressGraphic>();
            b.raycastTarget = false;
            b.SetInsets(tl, tr, bl, br);
            b.SetBaseColor(BorderBase);
            return b;
        }

        /// <summary>Dải vàng mỏng dọc cạnh trên, nghiêng khớp với khung cha.</summary>
        private static void AddTopStrip(Transform panel, float panelH, float stripH, float tl, float tr, float bl, float br, Color color)
        {
            float t = stripH / panelH;
            float f0 = 1f - t;
            float insetL0 = Mathf.Lerp(bl, tl, f0);
            float insetR0 = Mathf.Lerp(br, tr, f0);
            var strip = NewSkewed("AccentTop", panel, color, tl, tr, insetL0, insetR0, 1f);
            var rt = strip.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, stripH);
        }

        private Text NewText(string name, Transform parent, string text, int size, Color color, FontStyle style, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        /// <summary>Huy hiệu phím (ô vàng chứa chữ K).</summary>
        private void NewKeyBadge(Transform parent, Vector2 pos, float size, int fontSize)
        {
            var badge = NewImage("KeyBadge", parent, Gold);
            Place(badge.rectTransform, pos, new Vector2(size, size), new Vector2(0.5f, 0.5f));
            var letter = NewText("K", badge.transform, "K", fontSize, new Color(0.07f, 0.09f, 0.14f, 1f), FontStyle.Bold);
            Place(letter.rectTransform, Vector2.zero, new Vector2(size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Nhãn dạng viên thuốc: "NHẢY BI" (sáng, nhấp nháy) hoặc "THƯỜNG" (mờ).</summary>
        private Image NewTag(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, out Text label)
        {
            var bg = NewImage(name, parent, Cyan, GetCircleSprite());
            Place(bg.rectTransform, pos, size, new Vector2(0.5f, 0.5f));
            label = NewText("Label", bg.transform, LocalizationManager.Get("elev_normal"), fontSize, Color.white, FontStyle.Bold);
            Place(label.rectTransform, Vector2.zero, size, new Vector2(0.5f, 0.5f));
            return bg;
        }

        /// <summary>
        /// Thước đo góc nhìn ngang: mặt phẳng bàn, hình quạt góc, vạch chia, bi cái và cây cơ nằm bên phải bi.
        /// Gốc tọa độ của <paramref name="diagram"/> là tâm bi.
        /// </summary>
        private Protractor BuildProtractor(RectTransform diagram, float ballR, float cueLen, float cueThick,
            float tickR, float tickLen, float majorLen, float pieR, float step, float majorStep, int labelFont, bool labels)
        {
            var p = new Protractor();
            float max = selector.MaxAngleDeg;

            // Mặt phẳng bàn
            var baseline = NewImage("Baseline", diagram, new Color(1f, 1f, 1f, 0.4f));
            Place(baseline.rectTransform, Vector2.zero, new Vector2(tickR + majorLen + 8f, 1.5f), new Vector2(0f, 0.5f));

            // Hình quạt thể hiện góc đã chọn
            p.pie = NewImage("Pie", diagram, new Color(Cyan.r, Cyan.g, Cyan.b, 0.26f), GetCircleSprite());
            Place(p.pie.rectTransform, Vector2.zero, new Vector2(pieR * 2f, pieR * 2f), new Vector2(0.5f, 0.5f));
            p.pie.type = Image.Type.Filled;
            p.pie.fillMethod = Image.FillMethod.Radial360;
            p.pie.fillOrigin = (int)Image.Origin360.Right;
            p.pie.fillClockwise = false;

            // Vạch chia độ quanh cung
            for (float a = 0f; a <= max + 0.01f; a += step)
            {
                AddTick(diagram, p, a, tickR, tickLen, majorLen, majorStep, labels, labelFont);
            }
            if (Mathf.Abs(max % step) > 0.01f)
            {
                AddTick(diagram, p, max, tickR, tickLen, majorLen, majorStep, labels, labelFont);
            }

            // Bi cái
            var ball = NewImage("Ball", diagram, BallColor, GetCircleSprite());
            Place(ball.rectTransform, Vector2.zero, new Vector2(ballR * 2f, ballR * 2f), new Vector2(0.5f, 0.5f));
            var shade = NewImage("Shade", ball.rectTransform, new Color(0.78f, 0.78f, 0.76f, 0.55f), GetCircleSprite());
            Place(shade.rectTransform, new Vector2(ballR * 0.17f, -ballR * 0.2f), new Vector2(ballR * 1.45f, ballR * 1.45f), new Vector2(0.5f, 0.5f));
            var glint = NewImage("Glint", ball.rectTransform, new Color(1f, 1f, 1f, 0.85f), GetCircleSprite());
            Place(glint.rectTransform, new Vector2(-ballR * 0.37f, ballR * 0.4f), new Vector2(ballR * 0.4f, ballR * 0.4f), new Vector2(0.5f, 0.5f));

            // Cây cơ: xoay quanh tâm bi, đầu cơ chạm mặt bi
            p.cuePivot = NewRect("CuePivot", diagram);
            Place(p.cuePivot, Vector2.zero, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            float startX = ballR + cueThick * 0.5f;
            p.cueGlow = NewImage("CueGlow", p.cuePivot, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
            Place(p.cueGlow.rectTransform, new Vector2(startX - 2f, 0f), new Vector2(cueLen + 4f, cueThick * 2.4f), new Vector2(0f, 0.5f));
            var cue = NewImage("Cue", p.cuePivot, Wood);
            Place(cue.rectTransform, new Vector2(startX, 0f), new Vector2(cueLen, cueThick), new Vector2(0f, 0.5f));
            var tip = NewImage("Tip", p.cuePivot, Cyan);
            Place(tip.rectTransform, new Vector2(startX - cueThick * 0.5f, 0f), new Vector2(cueThick, cueThick), new Vector2(0f, 0.5f));
            var butt = NewImage("Butt", p.cuePivot, new Color(0.16f, 0.17f, 0.22f, 1f));
            Place(butt.rectTransform, new Vector2(startX + cueLen - cueLen * 0.18f, 0f), new Vector2(cueLen * 0.18f, cueThick * 1.15f), new Vector2(0f, 0.5f));

            return p;
        }

        private void AddTick(RectTransform diagram, Protractor p, float angle, float tickR, float tickLen, float majorLen, float majorStep, bool labels, int labelFont)
        {
            bool major = Mathf.Abs(angle % majorStep) < 0.01f || Mathf.Abs(angle - selector.MaxAngleDeg) < 0.01f;
            var pivot = NewRect("TickPivot", diagram);
            Place(pivot, Vector2.zero, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            pivot.localRotation = Quaternion.Euler(0f, 0f, angle);

            float len = major ? majorLen : tickLen;
            var img = NewImage("Tick", pivot, Dim);
            Place(img.rectTransform, new Vector2(tickR, 0f), new Vector2(len, major ? 2.5f : 1.5f), new Vector2(0f, 0.5f));
            p.ticks.Add(new Tick { image = img, angle = angle });

            if (labels && major)
            {
                var label = NewText("TickLabel", pivot, angle.ToString("0") + "°", labelFont, new Color(1f, 1f, 1f, 0.7f), FontStyle.Bold);
                Place(label.rectTransform, new Vector2(tickR + majorLen + 18f, 0f), new Vector2(46f, 20f), new Vector2(0.5f, 0.5f));
                label.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        private void RefreshProtractor(Protractor p, float angle, bool jumps, float pulse)
        {
            if (p == null) return;
            p.cuePivot.localRotation = Quaternion.Euler(0f, 0f, angle);
            p.pie.fillAmount = Mathf.Clamp01(angle / 360f);
            foreach (var t in p.ticks)
            {
                t.image.color = t.angle <= angle + 0.01f && angle > 0.01f ? Cyan : Dim;
            }
            var glow = p.cueGlow.color;
            glow.a = jumps ? 0.18f + 0.22f * pulse : 0f;
            p.cueGlow.color = glow;
        }

        private static void ApplyTag(Image bg, Text label, bool jumps, float pulse)
        {
            if (bg == null || label == null) return;
            if (jumps)
            {
                bg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.72f + 0.28f * pulse);
                label.text = LocalizationManager.Get("elev_jump");
                label.color = new Color(0.04f, 0.09f, 0.14f, 1f);
            }
            else
            {
                bg.color = new Color(1f, 1f, 1f, 0.12f);
                label.text = LocalizationManager.Get("elev_normal");
                label.color = new Color(1f, 1f, 1f, 0.55f);
            }
        }

        #endregion

        #region HUD panel

        private void BuildHudPanel(Transform parent)
        {
            const float w = 180f;
            const float h = 170f;
            const float cut = 20f;

            // Nghiêng "\" (ngược với khung ĐIỂM CƠ nghiêng "/") để hai khung ghép thành chữ V
            var panel = NewSkewed("ElevationPanel", parent, PanelColor, 0f, cut, cut, 0f, 0.45f);
            var panelRt = panel.rectTransform;
            panelRt.anchorMin = new Vector2(1f, 0f);
            panelRt.anchorMax = new Vector2(1f, 0f);
            panelRt.pivot = new Vector2(1f, 0f);
            panelRt.anchoredPosition = new Vector2(-30f, 262f);
            panelRt.sizeDelta = new Vector2(w, h);

            panelRt.gameObject.AddComponent<CanvasGroup>();
            panelRt.gameObject.AddComponent<AimOnlyHudVisibility>();

            hudBorder = NewBorder(panelRt, 0f, cut, cut, 0f);
            AddTopStrip(panelRt, h, 4f, 0f, cut, cut, 0f, Gold);

            // Tiêu đề + huy hiệu phím
            NewKeyBadge(panelRt, new Vector2(-66f, 62f), 20f, 14);
            hudTitleText = NewText("Title", panelRt, LocalizationManager.Get("elev_title"), 14, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(hudTitleText.rectTransform, new Vector2(-50f, 62f), new Vector2(110f, 24f), new Vector2(0f, 0.5f));

            // Thước đo góc
            var diagram = NewRect("Diagram", panelRt);
            Place(diagram, new Vector2(-30f, -26f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            hudGauge = BuildProtractor(diagram, 15f, 52f, 4f, 47f, 4f, 7f, 43f, 10f, 30f, 10, false);

            // Số độ lớn + nhãn trạng thái
            hudAngleText = NewText("AngleValue", panelRt, "0°", 28, Color.white, FontStyle.Bold);
            Place(hudAngleText.rectTransform, new Vector2(54f, -2f), new Vector2(70f, 36f), new Vector2(0.5f, 0.5f));
            hudTagBg = NewTag("Tag", panelRt, new Vector2(54f, -34f), new Vector2(66f, 18f), 11, out hudTagText);
        }

        #endregion

        #region Popup

        private void BuildPopup(Transform parent)
        {
            const float w = 680f;
            const float h = 640f;
            const float cut = 30f;

            var popupRt = NewRect("ElevationPopup", parent);
            popupRt.anchorMin = Vector2.zero;
            popupRt.anchorMax = Vector2.one;
            popupRt.offsetMin = Vector2.zero;
            popupRt.offsetMax = Vector2.zero;
            var dim = popupRt.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.68f);
            popupGroup = popupRt.gameObject.AddComponent<CanvasGroup>();

            // Thẻ nghiêng "\" cùng phong cách panel nhỏ
            var cardG = NewSkewed("Card", popupRt, new Color(0.06f, 0.09f, 0.16f, 0.98f), 0f, cut, cut, 0f, 0.55f);
            cardG.raycastTarget = true;
            Place(cardG.rectTransform, Vector2.zero, new Vector2(w, h), new Vector2(0.5f, 0.5f));
            var card = cardG.rectTransform;

            // Vùng nhận chuột: toàn bộ thẻ (nằm dưới các hình)
            var dragImg = NewImage("DragArea", card, new Color(0f, 0f, 0f, 0f));
            dragImg.raycastTarget = true;
            dragImg.rectTransform.anchorMin = Vector2.zero;
            dragImg.rectTransform.anchorMax = Vector2.one;
            dragImg.rectTransform.offsetMin = Vector2.zero;
            dragImg.rectTransform.offsetMax = Vector2.zero;

            popupBorder = NewBorder(card, 0f, cut, cut, 0f);
            AddTopStrip(card, h, 5f, 0f, cut, cut, 0f, Gold);

            NewKeyBadge(card, new Vector2(-228f, 282f), 32f, 22);
            popupTitleText = NewText("Title", card, LocalizationManager.Get("elev_popup_title"), 28, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(popupTitleText.rectTransform, new Vector2(-204f, 282f), new Vector2(420f, 40f), new Vector2(0f, 0.5f));

            // Thước đo góc lớn: tâm bi lệch trái để cây cơ bên phải có chỗ
            var diagram = NewRect("Diagram", card);
            Place(diagram, new Vector2(-130f, -40f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));

            var drag = dragImg.gameObject.AddComponent<DragArea>();
            drag.selector = selector;
            drag.origin = diagram;

            // Vạch giới hạn góc tối đa
            var maxPivot = NewRect("MaxPivot", diagram);
            Place(maxPivot, Vector2.zero, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f));
            maxPivot.localRotation = Quaternion.Euler(0f, 0f, selector.MaxAngleDeg);
            var maxLine = NewImage("MaxLine", maxPivot, new Color(0.95f, 0.38f, 0.38f, 0.55f));
            Place(maxLine.rectTransform, new Vector2(66f, 0f), new Vector2(190f, 1.5f), new Vector2(0f, 0.5f));

            popupGauge = BuildProtractor(diagram, 60f, 190f, 12f, 232f, 7f, 13f, 214f, 5f, 15f, 14, true);

            // Cột thông số bên phải
            popupAngleText = NewText("AngleValue", card, "0°", 58, Color.white, FontStyle.Bold);
            Place(popupAngleText.rectTransform, new Vector2(238f, 85f), new Vector2(170f, 70f), new Vector2(0.5f, 0.5f));
            popupTagBg = NewTag("Tag", card, new Vector2(238f, 38f), new Vector2(112f, 26f), 14, out popupTagText);

            popupHeightValue = BuildChip(card, "elev_height", new Vector2(228f, -28f), out popupHeightLabel);
            popupRangeValue = BuildChip(card, "elev_range", new Vector2(228f, -92f), out popupRangeLabel);

            popupDescText = NewText("Desc", card, "", 18, new Color(1f, 1f, 1f, 0.8f), FontStyle.Normal);
            Place(popupDescText.rectTransform, new Vector2(0f, -218f), new Vector2(640f, 28f), new Vector2(0.5f, 0.5f));

            popupHintText = NewText("Hint", card, LocalizationManager.Get("elev_hint"), 15, Color.white, FontStyle.Normal);
            Place(popupHintText.rectTransform, new Vector2(0f, -283f), new Vector2(640f, 30f), new Vector2(0.5f, 0.5f));

            popupRt.SetAsLastSibling();
        }

        /// <summary>Thẻ số liệu nhỏ (nhãn + giá trị) dạng hình bình hành.</summary>
        private Text BuildChip(Transform parent, string labelKey, Vector2 pos, out Text labelText)
        {
            var chip = NewSkewed("Chip_" + labelKey, parent, ChipColor, 0f, 10f, 10f, 0f, 0.7f);
            Place(chip.rectTransform, pos, new Vector2(150f, 52f), new Vector2(0.5f, 0.5f));
            var accent = NewImage("Accent", chip.transform, Cyan);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            accent.rectTransform.sizeDelta = new Vector2(3f, -14f);

            labelText = NewText("Label", chip.transform, LocalizationManager.Get(labelKey), 11, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(labelText.rectTransform, new Vector2(-62f, 12f), new Vector2(120f, 16f), new Vector2(0f, 0.5f));
            var v = NewText("Value", chip.transform, "--", 20, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(v.rectTransform, new Vector2(-62f, -9f), new Vector2(128f, 26f), new Vector2(0f, 0.5f));
            return v;
        }

        /// <summary>Kéo/bấm trong thẻ để chọn góc: góc của con trỏ so với tâm bi, giới hạn 0 đến tối đa (bên phải bi).</summary>
        private class DragArea : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public CueElevationSelector selector;
            public RectTransform origin;

            public void OnPointerDown(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left) Pick(e);
            }

            public void OnDrag(PointerEventData e)
            {
                if (e.button == PointerEventData.InputButton.Left) Pick(e);
            }

            private void Pick(PointerEventData e)
            {
                if (selector == null || origin == null) return;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(origin, e.position, e.pressEventCamera, out Vector2 local)) return;
                if (local.sqrMagnitude < 4f) return;

                float angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                if (angle < 0f) angle = 0f;
                selector.SetAngle(angle);
            }
        }

        #endregion

        #region Refresh

        private void HandleToggled(bool open)
        {
            if (popupGroup == null) return;
            popupGroup.alpha = open ? 1f : 0f;
            popupGroup.interactable = open;
            popupGroup.blocksRaycasts = open;
            if (open) Refresh(selector.CurrentAngleDeg);
        }

        private void Refresh(float angle)
        {
            float minJump = GetMinJumpAngle();
            jumpActive = angle >= minJump;
            float pulse = jumpActive ? 1f : 0f;

            string degrees = angle.ToString("0") + "°";
            Color valueColor = jumpActive ? Cyan : Color.white;

            RefreshProtractor(hudGauge, angle, jumpActive, pulse);
            RefreshProtractor(popupGauge, angle, jumpActive, pulse);

            // Viền sáng chạy theo tỉ lệ góc đã chọn
            float ratio = selector.MaxAngleDeg > 0.01f ? angle / selector.MaxAngleDeg : 0f;
            ApplyBorder(hudBorder, ratio);
            ApplyBorder(popupBorder, ratio);

            if (hudAngleText != null)
            {
                hudAngleText.text = degrees;
                hudAngleText.color = valueColor;
            }
            if (popupAngleText != null)
            {
                popupAngleText.text = degrees;
                popupAngleText.color = valueColor;
            }
            ApplyTag(hudTagBg, hudTagText, jumpActive, pulse);
            ApplyTag(popupTagBg, popupTagText, jumpActive, pulse);

            if (popupDescText != null)
            {
                popupDescText.text = jumpActive
                    ? LocalizationManager.Get("elev_desc_jump")
                    : LocalizationManager.Format("elev_desc_low", minJump.ToString("0"));
            }
            UpdatePrediction();
        }

        private static void ApplyBorder(BorderProgressGraphic border, float ratio)
        {
            if (border == null) return;
            if (ratio > 0.005f) border.SetProgress(ratio, Cyan);
            else border.ClearProgress();
        }

        private float GetMinJumpAngle()
        {
            var cfg = GetConfig();
            return cfg != null ? cfg.MinJumpAngle : 8f;
        }

        private Physics.PhysicsConfig GetConfig()
        {
            if (ballManager == null) ballManager = FindObjectOfType<Match.BallManager>();
            return ballManager != null ? ballManager.PhysicsConfig : null;
        }

        private void UpdatePrediction()
        {
            if (popupHeightValue == null || popupRangeValue == null) return;

            if (inputController == null) inputController = FindObjectOfType<Input.PlayerInputController>();
            var cfg = GetConfig();
            if (cfg != null && inputController != null &&
                CueElevationSelector.PredictJump(cfg, selector.CurrentAngleDeg, inputController.CurrentPower01,
                    inputController.CueContact01, out float height, out float range))
            {
                popupHeightValue.text = "~" + (height * 100f).ToString("0") + " cm";
                popupRangeValue.text = "~" + range.ToString("0.0") + " m";
            }
            else
            {
                popupHeightValue.text = "--";
                popupRangeValue.text = "--";
            }
        }

        private void Update()
        {
            // Nhãn "NHẢY BI" và cây cơ phát sáng nhấp nháy nhẹ khi đủ góc nhảy
            if (jumpActive)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                ApplyTag(hudTagBg, hudTagText, true, pulse);
                ApplyTag(popupTagBg, popupTagText, true, pulse);
                if (hudGauge != null) SetGlow(hudGauge, pulse);
                if (popupGauge != null) SetGlow(popupGauge, pulse);
            }

            // Giữ phần dự đoán bám theo lực/điểm cơ khi popup đang mở
            if (selector != null && selector.IsOpen) UpdatePrediction();
        }

        private static void SetGlow(Protractor p, float pulse)
        {
            var c = p.cueGlow.color;
            c.a = 0.18f + 0.22f * pulse;
            p.cueGlow.color = c;
        }

        #endregion
    }
}
