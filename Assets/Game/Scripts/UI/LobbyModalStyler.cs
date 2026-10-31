using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Làm đẹp và đồng bộ phong cách các popup ở sảnh (cấu hình AI, online, cài đặt, luật, thoát) ngay lúc chạy:
    /// tiêu đề có gạch chân, nhãn mục in hoa, ô nhập/dropdown/slider tối + viền xanh, hiệu ứng mở popup.
    /// Không cần dựng lại scene.
    /// </summary>
    public static class LobbyModalStyler
    {
        public static readonly Color Accent = new Color(0.36f, 0.62f, 0.98f, 1f);
        public static readonly Color AccentGreen = new Color(0.20f, 0.78f, 0.40f, 1f);
        public static readonly Color AccentBlue = new Color(0.24f, 0.55f, 0.98f, 1f);
        public static readonly Color AccentRed = new Color(0.94f, 0.27f, 0.27f, 1f);

        private static readonly Color FieldBg = new Color(0.06f, 0.10f, 0.18f, 1f);
        private static readonly Color FieldLine = new Color(0.30f, 0.45f, 0.68f, 0.85f);
        private static readonly Color LabelColor = new Color(0.58f, 0.72f, 0.92f, 1f);
        private static readonly Color ItemHover = new Color(0.95f, 0.58f, 0.12f, 1f);

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ chung

        /// <summary>Áp phong cách chung cho một popup (tiêu đề, nhãn, ô nhập, dropdown, slider, hiệu ứng mở).</summary>
        public static void StyleModal(GameObject modal, Color accent)
        {
            if (modal == null) return;

            if (modal.GetComponent<CanvasGroup>() == null) modal.AddComponent<CanvasGroup>();
            if (modal.GetComponent<ModalPopIn>() == null) modal.AddComponent<ModalPopIn>();

            foreach (Text t in modal.GetComponentsInChildren<Text>(true))
            {
                string n = t.gameObject.name;
                if (n == "Title") StyleTitle(t, accent);
                else if (!n.Contains(" ") && ((n.EndsWith("Label") && n != "Label") || n.StartsWith("Lbl"))) StyleSectionLabel(t);
            }

            foreach (InputField f in modal.GetComponentsInChildren<InputField>(true)) StyleInput(f);
            foreach (Dropdown d in modal.GetComponentsInChildren<Dropdown>(true)) StyleDropdown(d);
            foreach (Slider s in modal.GetComponentsInChildren<Slider>(true)) StyleSlider(s);
        }

        private static void StyleTitle(Text t, Color accent)
        {
            t.fontSize = 28;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            if (t.transform.Find("Underline") != null) return;

            GameObject go = new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(t.transform, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -8f);
            r.sizeDelta = new Vector2(120f, 3f);
            Image img = go.GetComponent<Image>();
            img.color = accent;
            img.raycastTarget = false;
        }

        public static void StyleSectionLabel(Text t)
        {
            string s = t.text ?? "";
            int paren = s.IndexOf('(');
            if (paren > 0) s = s.Substring(0, paren);
            s = s.Trim().TrimEnd(':').ToUpperInvariant();
            t.text = s;
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.color = LabelColor;
            t.alignment = TextAnchor.MiddleLeft;

            if (t.transform.Find("Mark") != null) return;
            GameObject go = new GameObject("Mark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(t.transform, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0f, 0.5f);
            r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-8f, 0f);
            r.sizeDelta = new Vector2(3f, 14f);
            Image img = go.GetComponent<Image>();
            img.color = Accent;
            img.raycastTarget = false;
        }

        private static void StyleInput(InputField f)
        {
            Image bg = f.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = null;
                bg.type = Image.Type.Simple;
                bg.color = new Color(1f, 1f, 1f, 0f); // vùng bấm; hình vẽ do khung bình hành bên dưới
            }
            Outline oldLine = f.GetComponent<Outline>();
            if (oldLine != null) oldLine.enabled = false;

            SkewedPanelGraphic shape;
            BorderProgressGraphic frame;
            AddShapeAndFrame(f.transform, FieldBg, FieldLine, 10f, out shape, out frame, false);

            InputFocusOutline fx = f.GetComponent<InputFocusOutline>();
            if (fx == null) fx = f.gameObject.AddComponent<InputFocusOutline>();
            fx.Init(f, frame, FieldLine, AccentGreen);

            if (f.GetComponent<InputCaretFix>() == null) f.gameObject.AddComponent<InputCaretFix>();

            f.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.45f);
            f.caretColor = Color.white;
            if (f.textComponent != null)
            {
                f.textComponent.fontStyle = FontStyle.Bold;
                RectTransform tr = f.textComponent.rectTransform;
                tr.offsetMin = new Vector2(14f, tr.offsetMin.y);
            }
            if (f.placeholder is Text ph)
            {
                RectTransform pr = ph.rectTransform;
                pr.offsetMin = new Vector2(14f, pr.offsetMin.y);
            }
        }

        // ------------------------------------------------------------------ dropdown

        public static void StyleDropdown(Dropdown d)
        {
            bool isCompact = ((RectTransform)d.transform).sizeDelta.x > 0f && ((RectTransform)d.transform).sizeDelta.x <= 200f;

            Image bg = d.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = null;
                bg.type = Image.Type.Simple;
                bg.color = new Color(1f, 1f, 1f, 0f); // vùng bấm; hình vẽ do khung bình hành bên dưới
            }
            Outline oldLine = d.GetComponent<Outline>();
            if (oldLine != null) oldLine.enabled = false;

            SkewedPanelGraphic shape;
            BorderProgressGraphic frame;
            AddShapeAndFrame(d.transform, FieldBg, FieldLine, isCompact ? 8f : 10f, out shape, out frame, false);
            d.targetGraphic = shape; // hover/nhấn tô màu lên khung bình hành
            d.transition = Selectable.Transition.ColorTint;

            ColorBlock cb = d.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.25f, 1.35f, 1.6f, 1f);
            cb.pressedColor = new Color(0.8f, 0.9f, 1f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.08f;
            d.colors = cb;

            if (d.captionText != null)
            {
                if (d.captionText.font == null) d.captionText.font = Font;
                d.captionText.fontStyle = FontStyle.Bold;
                d.captionText.color = Color.white;
                if (isCompact)
                {
                    d.captionText.fontSize = 13;
                    d.captionText.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                RectTransform cr = d.captionText.rectTransform;
                cr.offsetMin = new Vector2(isCompact ? 11f : 14f, cr.offsetMin.y);
                cr.offsetMax = new Vector2(isCompact ? -24f : -34f, cr.offsetMax.y);
            }

            // Mũi tên: thay hình nút mặc định bằng tam giác vẽ tay
            Transform arrow = d.transform.Find("Arrow");
            if (arrow != null)
            {
                if (isCompact)
                {
                    ((RectTransform)arrow).anchoredPosition = new Vector2(-13f, 0f);
                }
                Image old = arrow.GetComponent<Image>();
                if (old != null) old.enabled = false;
                if (arrow.Find("Tri") == null)
                {
                    GameObject tri = new GameObject("Tri", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
                    tri.transform.SetParent(arrow, false);
                    RectTransform tr = tri.GetComponent<RectTransform>();
                    tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
                    tr.pivot = new Vector2(0.5f, 0.5f);
                    tr.anchoredPosition = Vector2.zero;
                    tr.sizeDelta = isCompact ? new Vector2(11f, 7f) : new Vector2(13f, 8f);
                    SkewedPanelGraphic g = tri.GetComponent<SkewedPanelGraphic>();
                    g.SetInsets(0f, 0f, isCompact ? 5.5f : 6.5f, isCompact ? 5.5f : 6.5f);
                    g.color = LabelColor;
                    g.raycastTarget = false;
                }
            }

            StyleDropdownTemplate(d);
        }

        private static void StyleDropdownTemplate(Dropdown d)
        {
            RectTransform template = d.template;
            if (template == null) return;

            bool isCompact = ((RectTransform)d.transform).sizeDelta.x > 0f && ((RectTransform)d.transform).sizeDelta.x <= 200f;
            float itemH = isCompact ? 34f : 40f;
            int count = Mathf.Max(1, d.options.Count);
            template.sizeDelta = new Vector2(template.sizeDelta.x, itemH * count + 8f);

            Transform viewport = template.Find("Viewport");
            if (viewport != null)
            {
                RectTransform vpRt = viewport as RectTransform;
                if (vpRt != null && count <= 5)
                {
                    vpRt.offsetMin = Vector2.zero;
                    vpRt.offsetMax = Vector2.zero;
                }
                Mask vpMask = viewport.GetComponent<Mask>();
                Image vpImg = viewport.GetComponent<Image>();
                if (vpMask != null && (vpImg == null || vpImg.sprite == null))
                {
                    vpMask.enabled = false;
                    if (vpImg != null) vpImg.enabled = false;
                    if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            Transform scrollbar = template.Find("Scrollbar");
            if (scrollbar != null && count <= 5)
            {
                scrollbar.gameObject.SetActive(false);
            }

            Image tbg = template.GetComponent<Image>();
            if (tbg != null)
            {
                tbg.sprite = null;
                tbg.type = Image.Type.Simple;
                tbg.color = new Color(1f, 1f, 1f, 0f);
            }
            Outline oldTline = template.GetComponent<Outline>();
            if (oldTline != null) oldTline.enabled = false;

            SkewedPanelGraphic tShape;
            BorderProgressGraphic tFrame;
            AddShapeAndFrame(template, new Color(0.05f, 0.09f, 0.16f, 0.99f), new Color(0.40f, 0.62f, 0.92f, 0.95f), isCompact ? 8f : 10f, out tShape, out tFrame, true);

            Transform item = template.Find("Viewport/Content/Item");
            if (item == null) return;

            RectTransform itemRt = item as RectTransform;
            itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, itemH);

            Toggle toggle = item.GetComponent<Toggle>();
            Transform itemBg = item.Find("Item Background");
            Image bgImg = itemBg != null ? itemBg.GetComponent<Image>() : null;
            if (bgImg != null) bgImg.enabled = false;

            // Nền mỗi dòng cũng là hình bình hành để đồng bộ với khung dropdown
            SkewedPanelGraphic itemShape = null;
            Transform existingShape = item.Find("Shape");
            if (existingShape != null)
            {
                itemShape = existingShape.GetComponent<SkewedPanelGraphic>();
            }
            else
            {
                GameObject sg = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
                sg.transform.SetParent(item, false);
                sg.transform.SetAsFirstSibling();
                RectTransform sr = sg.GetComponent<RectTransform>();
                sr.anchorMin = Vector2.zero;
                sr.anchorMax = Vector2.one;
                sr.offsetMin = Vector2.zero;
                sr.offsetMax = Vector2.zero;
                itemShape = sg.GetComponent<SkewedPanelGraphic>();
                itemShape.SetInsets(0f, isCompact ? 6f : 8f, isCompact ? 6f : 8f, 0f);
                itemShape.raycastTarget = false;
                itemShape.color = Color.white; // màu thực do ColorBlock của Toggle quyết định
            }
            if (toggle != null)
            {
                toggle.transition = Selectable.Transition.ColorTint;
                if (itemShape != null) toggle.targetGraphic = itemShape;
                ColorBlock cb = toggle.colors;
                cb.normalColor = new Color(1f, 1f, 1f, 0f);
                cb.highlightedColor = ItemHover;
                cb.pressedColor = new Color(0.78f, 0.40f, 0.05f, 1f);
                cb.selectedColor = ItemHover; // Unity ưu tiên Selected hơn Highlighted: hover sẽ tự select dòng nên phải có màu
                cb.fadeDuration = 0.06f;
                toggle.colors = cb;
            }

            // Dấu chọn: vạch xanh lá bên trái
            Transform check = item.Find("Item Checkmark");
            if (check != null)
            {
                Image ci = check.GetComponent<Image>();
                if (ci != null)
                {
                    ci.sprite = null;
                    ci.type = Image.Type.Simple;
                    ci.color = AccentGreen;
                }
                RectTransform cr = check as RectTransform;
                cr.anchorMin = new Vector2(0f, 0.12f);
                cr.anchorMax = new Vector2(0f, 0.88f);
                cr.pivot = new Vector2(0f, 0.5f);
                cr.anchoredPosition = new Vector2(2f, 0f);
                cr.sizeDelta = new Vector2(4f, 0f);
            }

            Transform label = item.Find("Item Label");
            if (label != null)
            {
                Text lt = label.GetComponent<Text>();
                if (lt != null)
                {
                    if (lt.font == null) lt.font = Font;
                    lt.fontSize = isCompact ? 14 : 16;
                    lt.fontStyle = FontStyle.Bold;
                    lt.color = new Color(0.90f, 0.95f, 1f, 1f);
                }
                RectTransform lr = label as RectTransform;
                lr.offsetMin = new Vector2(isCompact ? 14f : 20f, 1f);
                lr.offsetMax = new Vector2(isCompact ? -6f : -10f, -2f);
            }
        }

        // ------------------------------------------------------------------ slider

        private static void StyleSlider(Slider s)
        {
            Transform bg = s.transform.Find("Background");
            if (bg != null)
            {
                Image bi = bg.GetComponent<Image>();
                if (bi != null)
                {
                    bi.sprite = null;
                    bi.type = Image.Type.Simple;
                    bi.color = new Color(0.07f, 0.12f, 0.20f, 1f);
                }
                RectTransform br = bg as RectTransform;
                br.anchorMin = new Vector2(0f, 0.5f);
                br.anchorMax = new Vector2(1f, 0.5f);
                br.sizeDelta = new Vector2(0f, 8f);
                br.anchoredPosition = Vector2.zero;
            }

            Transform fill = s.transform.Find("Fill Area/Fill");
            if (fill != null)
            {
                Image fi = fill.GetComponent<Image>();
                if (fi != null)
                {
                    fi.sprite = null;
                    fi.type = Image.Type.Simple;
                    fi.color = AccentGreen;
                }
            }
            Transform fillArea = s.transform.Find("Fill Area");
            if (fillArea != null)
            {
                RectTransform fr = fillArea as RectTransform;
                fr.anchorMin = new Vector2(0f, 0.5f);
                fr.anchorMax = new Vector2(1f, 0.5f);
                fr.sizeDelta = new Vector2(-14f, 8f);
                fr.anchoredPosition = Vector2.zero;
            }

            Transform handle = s.transform.Find("Handle Slide Area/Handle");
            if (handle != null)
            {
                RectTransform hr = handle as RectTransform;
                hr.sizeDelta = new Vector2(22f, 22f);
                Image hi = handle.GetComponent<Image>();
                if (hi != null) hi.color = Color.white;
                Outline ho = handle.GetComponent<Outline>();
                if (ho == null) ho = handle.gameObject.AddComponent<Outline>();
                ho.effectDistance = new Vector2(2f, -2f);
                ho.effectColor = AccentGreen;
            }

            // Hiển thị phần trăm bên phải nhãn của slider
            if (s.transform.Find("Percent") == null)
            {
                GameObject go = new GameObject("Percent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(s.transform, false);
                RectTransform r = go.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(1f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(1f, 0f);
                r.anchoredPosition = new Vector2(0f, 6f);
                r.sizeDelta = new Vector2(80f, 24f);
                Text t = go.GetComponent<Text>();
                t.font = Font;
                t.fontSize = 15;
                t.fontStyle = FontStyle.Bold;
                t.alignment = TextAnchor.MiddleRight;
                t.color = AccentGreen;
                t.raycastTarget = false;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.text = Mathf.RoundToInt(s.normalizedValue * 100f) + "%";
                Slider captured = s;
                s.onValueChanged.AddListener(_ => t.text = Mathf.RoundToInt(captured.normalizedValue * 100f) + "%");
            }
        }

        // ------------------------------------------------------------------ modal cấu hình AI

        /// <summary>Sắp lại bố cục popup "Cấu hình đấu với AI" và thay 3 nút độ khó bằng thẻ 3 màu.</summary>
        public static DifficultyCardUI[] StyleDifficultyModal(GameObject modal, Button easy, Button normal, Button hard)
        {
            if (modal == null) return null;

            RectTransform root = modal.GetComponent<RectTransform>();
            root.sizeDelta = new Vector2(600f, 660f);

            Place(modal.transform, "Title", 0f, 0f, null);
            Place(modal.transform, "NameLabel", 226f, 24f, 520f);
            Place(modal.transform, "PlayerNameInput", 190f, 44f, 520f);
            Place(modal.transform, "DiffLabel", 142f, 24f, 520f);
            Place(modal.transform, "DiffButtons", 98f, 64f, 520f);
            Place(modal.transform, "RaceLabel", 40f, 24f, 520f);
            Place(modal.transform, "RaceDropdown", 6f, 44f, 520f);
            Place(modal.transform, "TimerLabel", -40f, 24f, 520f);
            Place(modal.transform, "TimerDropdown", -74f, 44f, 520f);
            Place(modal.transform, "RackIntroToggle", -122f, 32f, 520f);
            Place(modal.transform, "AimGuideToggle", -160f, 32f, 520f);
            Place(modal.transform, "BtnStartMatch", -218f, 54f, 520f);
            Place(modal.transform, "BtnClose", -280f, 44f, 520f);

            Text diffLabel = FindText(modal.transform, "DiffLabel");
            if (diffLabel != null) diffLabel.text = LocalizationManager.Get("diff_level_header");

            HorizontalLayoutGroup hz = modal.transform.Find("DiffButtons")?.GetComponent<HorizontalLayoutGroup>();
            if (hz != null)
            {
                hz.spacing = 14f;
                hz.childAlignment = TextAnchor.MiddleCenter;
                hz.childControlWidth = true;
                hz.childControlHeight = true;
                hz.childForceExpandWidth = true;
                hz.childForceExpandHeight = true;
            }

            StyleModal(modal, Accent);
            if (diffLabel != null) diffLabel.text = LocalizationManager.Get("diff_level_header");
            Text title = FindText(modal.transform, "Title");
            if (title != null) title.text = LocalizationManager.Get("diff_title");

            // Nút bắt đầu: nhấn xanh lá
            Button start = modal.transform.Find("BtnStartMatch")?.GetComponent<Button>();
            if (start != null) TintHudButton(start, new Color(0.08f, 0.38f, 0.24f, 0.98f), AccentGreen);

            DifficultyCardUI[] cards = new DifficultyCardUI[3];
            cards[0] = MakeCard(easy, AccentGreen, LocalizationManager.Get("diff_easy"), 1);
            cards[1] = MakeCard(normal, AccentBlue, LocalizationManager.Get("diff_normal"), 2);
            cards[2] = MakeCard(hard, AccentRed, LocalizationManager.Get("diff_hard"), 3);
            return cards;
        }

        private static DifficultyCardUI MakeCard(Button b, Color accent, string title, int level)
        {
            if (b == null) return null;
            DifficultyCardUI card = b.GetComponent<DifficultyCardUI>();
            if (card == null) card = b.gameObject.AddComponent<DifficultyCardUI>();
            card.Build(accent, title, level);
            return card;
        }

        /// <summary>Thêm nền + viền hình bình hành (cùng kiểu nút HUD) làm con của một control.</summary>
        private static void AddShapeAndFrame(Transform parent, Color fill, Color line, float cut, out SkewedPanelGraphic shape, out BorderProgressGraphic frame, bool frameOnTop)
        {
            Transform existingShape = parent.Find("Shape");
            Transform existingFrame = parent.Find("Frame");
            if (existingShape != null && existingFrame != null)
            {
                shape = existingShape.GetComponent<SkewedPanelGraphic>();
                frame = existingFrame.GetComponent<BorderProgressGraphic>();
                return;
            }

            GameObject sg = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            sg.transform.SetParent(parent, false);
            Stretch(sg.GetComponent<RectTransform>());
            shape = sg.GetComponent<SkewedPanelGraphic>();
            shape.SetInsets(0f, cut, cut, 0f);
            shape.color = fill;
            shape.raycastTarget = false;
            sg.transform.SetAsFirstSibling();

            GameObject fg = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            fg.transform.SetParent(parent, false);
            Stretch(fg.GetComponent<RectTransform>());
            frame = fg.GetComponent<BorderProgressGraphic>();
            frame.SetInsets(0f, cut, cut, 0f);
            frame.SetBaseColor(line);
            frame.raycastTarget = false;
            if (frameOnTop) fg.transform.SetAsLastSibling();
            else fg.transform.SetSiblingIndex(1);
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Tô màu nền + viền cho nút kiểu HUD (SkewedPanelGraphic + BorderProgressGraphic).</summary>
        public static void TintHudButton(Button b, Color body, Color frameColor)
        {
            if (b == null) return;
            if (b.targetGraphic != null) b.targetGraphic.color = body;
            BorderProgressGraphic frame = b.GetComponentInChildren<BorderProgressGraphic>(true);
            if (frame != null) frame.SetBaseColor(frameColor);
        }

        private static Text FindText(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            return t != null ? t.GetComponent<Text>() : null;
        }

        private static void Place(Transform parent, string childName, float y, float height, float? width)
        {
            Transform t = parent.Find(childName);
            if (t == null) return;
            RectTransform rt = t as RectTransform;
            if (!width.HasValue) return; // tiêu đề giữ neo trên cùng
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width.Value, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }
    }
}
