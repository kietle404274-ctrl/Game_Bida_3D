using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Giao diện phòng chờ online dựng hoàn toàn bằng code lúc chạy (không cần dựng lại scene):
    /// hai thẻ người chơi hình bình hành đối xứng (avatar + tên + vai trò + đèn trạng thái), chữ VS ở giữa,
    /// tiêu đề/mã phòng ở trên và thanh nút hành động nằm ngang ở dưới.
    /// </summary>
    public class OnlineRoomView : MonoBehaviour
    {
        private class PlayerCard
        {
            public RectTransform root;
            public BorderProgressGraphic frame;
            public Image ring;
            public Image avatar;
            public Text roleText;
            public Text nameText;
            public SkewedPanelGraphic chip;
            public Image led;
            public Text chipText;
            public Color accent;
            public bool empty;
        }

        private static readonly Color CardFill = new Color(0.06f, 0.10f, 0.18f, 0.98f);
        private static readonly Color ColorGreen = new Color(0.20f, 0.90f, 0.42f, 1f);
        private static readonly Color ColorOrange = new Color(1.00f, 0.62f, 0.20f, 1f);
        private static readonly Color ColorGrey = new Color(0.40f, 0.48f, 0.62f, 1f);
        private static readonly Color AccentP1 = new Color(0.36f, 0.80f, 1.00f, 1f);
        private static readonly Color AccentP2 = new Color(1.00f, 0.70f, 0.28f, 1f);

        private PlayerCard card1;
        private PlayerCard card2;
        private Image readyGlyph;

        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ dựng

        public static OnlineRoomView Build(RectTransform roomSub, Text roomTitle, Button copyButton, Text settingsText, Button configButton,
            Button readyButton, Button startButton, Button leaveButton, Text statusText)
        {
            if (roomSub == null) return null;
            OnlineRoomView view = roomSub.GetComponent<OnlineRoomView>();
            if (view != null) return view;
            view = roomSub.gameObject.AddComponent<OnlineRoomView>();

            // Ẩn hai thẻ cũ (hình chữ nhật đơn giản) do editor dựng
            HideOld(roomSub, "Player1Card");
            HideOld(roomSub, "Player2Card");

            // ---- Tiêu đề + nút copy ----
            if (roomTitle != null)
            {
                Place(roomTitle.rectTransform, new Vector2(-40f, 235f), new Vector2(460f, 36f));
                roomTitle.fontSize = 24;
            }
            if (copyButton != null) Place((RectTransform)copyButton.transform, new Vector2(260f, 235f), new Vector2(150f, 36f));

            GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            divider.transform.SetParent(roomSub, false);
            Place((RectTransform)divider.transform, new Vector2(0f, 211f), new Vector2(660f, 2f));
            Image dImg = divider.GetComponent<Image>();
            dImg.color = new Color(0.36f, 0.62f, 0.98f, 0.45f);
            dImg.raycastTarget = false;

            // ---- Hàng cấu hình ----
            if (settingsText != null)
            {
                Place(settingsText.rectTransform, new Vector2(-110f, 180f), new Vector2(440f, 28f));
                settingsText.fontSize = 15;
                settingsText.fontStyle = FontStyle.Bold;
                settingsText.color = new Color(0.58f, 0.72f, 0.92f, 1f);
            }
            if (configButton != null) Place((RectTransform)configButton.transform, new Vector2(250f, 180f), new Vector2(230f, 34f));

            // ---- Hai thẻ người chơi + VS ----
            view.card1 = BuildCard(roomSub, "CardP1", new Vector2(-172f, 30f), true, AccentP1);
            view.card2 = BuildCard(roomSub, "CardP2", new Vector2(172f, 30f), false, AccentP2);
            BuildVs(roomSub, new Vector2(0f, 30f));

            // ---- Thông báo trạng thái ----
            if (statusText != null) Place(statusText.rectTransform, new Vector2(0f, -112f), new Vector2(660f, 26f));

            // ---- Thanh nút ngang: SẴN SÀNG | BẮT ĐẦU | RỜI PHÒNG ----
            GameObject bar = new GameObject("ActionBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            bar.transform.SetParent(roomSub, false);
            Place((RectTransform)bar.transform, new Vector2(0f, -182f), new Vector2(660f, 58f));
            HorizontalLayoutGroup hlg = bar.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;
            AddToBar(bar.transform, readyButton, 1f);
            AddToBar(bar.transform, startButton, 1.2f);
            AddToBar(bar.transform, leaveButton, 0.9f);

            // ---- Icon vector cho từng nút ----
            view.readyGlyph = StyleButton(readyButton, UiIconLibrary.Check, 17);
            StyleButton(startButton, UiIconLibrary.Play, 16);
            StyleButton(leaveButton, UiIconLibrary.Exit, 16);
            StyleButton(copyButton, UiIconLibrary.Copy, 13);
            StyleButton(configButton, UiIconLibrary.Gear, 14);

            return view;
        }

        /// <summary>Đổi icon nút sẵn sàng: dấu tích khi chưa sẵn sàng, dấu X khi đã sẵn sàng (bấm để hủy).</summary>
        public void SetReadyIcon(bool cancel)
        {
            if (readyGlyph != null) readyGlyph.sprite = cancel ? UiIconLibrary.Cross : UiIconLibrary.Check;
        }

        /// <summary>Ẩn icon cũ của nút HUD, thêm icon vector mới bên trái và cho nhãn tự co vừa khung.</summary>
        private static Image StyleButton(Button b, Sprite icon, int fontSize)
        {
            if (b == null) return null;
            Transform old = b.transform.Find("Icon");
            if (old != null) old.gameObject.SetActive(false);

            Transform existing = b.transform.Find("Glyph");
            Image img;
            if (existing != null)
            {
                img = existing.GetComponent<Image>();
            }
            else
            {
                GameObject go = new GameObject("Glyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(b.transform, false);
                RectTransform gr = (RectTransform)go.transform;
                gr.anchorMin = gr.anchorMax = new Vector2(0f, 0.5f);
                gr.pivot = new Vector2(0.5f, 0.5f);
                gr.anchoredPosition = new Vector2(24f, 0f);
                gr.sizeDelta = new Vector2(22f, 22f);
                img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
            }
            img.sprite = icon;
            img.color = Color.white;

            foreach (Text t in b.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == "Q") continue;
                t.fontSize = fontSize;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = 10;
                t.resizeTextMaxSize = fontSize;
                RectTransform tr = t.rectTransform;
                tr.offsetMin = new Vector2(44f, tr.offsetMin.y);
                tr.offsetMax = new Vector2(-12f, tr.offsetMax.y);
            }
            return img;
        }

        private static void HideOld(Transform parent, string childName)
        {
            Transform t = parent.Find(childName);
            if (t != null) t.gameObject.SetActive(false);
        }

        private static void AddToBar(Transform bar, Button b, float flex)
        {
            if (b == null) return;
            b.transform.SetParent(bar, false);
            LayoutElement le = b.GetComponent<LayoutElement>();
            if (le == null) le = b.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = flex;
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.minHeight = 56f;
            le.preferredHeight = 56f;
        }

        private static PlayerCard BuildCard(Transform parent, string objName, Vector2 pos, bool leftSide, Color accent)
        {
            PlayerCard c = new PlayerCard { accent = accent };

            GameObject root = new GameObject(objName, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            c.root = (RectTransform)root.transform;
            Place(c.root, pos, new Vector2(300f, 225f));

            float a = leftSide ? 0f : 26f;
            float b = leftSide ? 26f : 0f;

            GameObject shapeGo = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            shapeGo.transform.SetParent(root.transform, false);
            Stretch((RectTransform)shapeGo.transform);
            SkewedPanelGraphic shape = shapeGo.GetComponent<SkewedPanelGraphic>();
            shape.SetInsets(a, b, b, a);
            shape.color = CardFill;
            shape.SetBottomDarken(0.72f);
            shape.raycastTarget = false;

            GameObject frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(root.transform, false);
            Stretch((RectTransform)frameGo.transform);
            c.frame = frameGo.GetComponent<BorderProgressGraphic>();
            c.frame.SetInsets(a, b, b, a);
            c.frame.SetBaseColor(accent);
            c.frame.raycastTarget = false;

            c.roleText = MakeText(root.transform, "Role", 13, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(c.roleText.rectTransform, new Vector2(0f, 96f), new Vector2(280f, 22f));

            c.ring = MakeImage(root.transform, "Ring", AvatarLibrary.RingSprite, accent);
            Place(c.ring.rectTransform, new Vector2(0f, 24f), new Vector2(116f, 116f));
            c.avatar = MakeImage(root.transform, "Avatar", AvatarLibrary.Get(0), Color.white);
            Place(c.avatar.rectTransform, new Vector2(0f, 24f), new Vector2(92f, 92f));

            c.nameText = MakeText(root.transform, "Name", 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(c.nameText.rectTransform, new Vector2(0f, -58f), new Vector2(280f, 32f));

            GameObject chipGo = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            chipGo.transform.SetParent(root.transform, false);
            Place((RectTransform)chipGo.transform, new Vector2(0f, -92f), new Vector2(214f, 28f));
            c.chip = chipGo.GetComponent<SkewedPanelGraphic>();
            c.chip.SetInsets(0f, 8f, 8f, 0f);
            c.chip.raycastTarget = false;

            c.led = MakeImage(chipGo.transform, "Led", AvatarLibrary.DotSprite, ColorGreen);
            Place(c.led.rectTransform, new Vector2(-86f, 0f), new Vector2(10f, 10f));

            c.chipText = MakeText(chipGo.transform, "Text", 13, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Place(c.chipText.rectTransform, new Vector2(6f, 0f), new Vector2(180f, 26f));
            return c;
        }

        private static void BuildVs(Transform parent, Vector2 pos)
        {
            GameObject root = new GameObject("Vs", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, pos, new Vector2(70f, 70f));

            Image bg = MakeImage(root.transform, "Bg", AvatarLibrary.DotSprite, new Color(0.05f, 0.08f, 0.15f, 0.97f));
            Stretch(bg.rectTransform);
            Image ring = MakeImage(root.transform, "Ring", AvatarLibrary.RingSprite, new Color(1f, 0.78f, 0.25f, 1f));
            Stretch(ring.rectTransform);

            Text t = MakeText(root.transform, "VsText", 24, new Color(1f, 0.82f, 0.30f, 1f), TextAnchor.MiddleCenter, FontStyle.BoldAndItalic);
            Stretch(t.rectTransform);
            t.text = "VS";
            Outline o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.9f, 0.4f, 0.05f, 0.55f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // ------------------------------------------------------------------ cập nhật

        /// <summary>Cập nhật toàn bộ nội dung 2 thẻ người chơi theo trạng thái phòng.</summary>
        public void Apply(string p1Name, bool p1Ready, int p1Avatar, bool hasP2, string p2Name, bool p2Ready, int p2Avatar, bool amIHost)
        {
            ApplyCard(card1, true, p1Name, p1Avatar, p1Ready, amIHost, "online_role_host");
            ApplyCard(card2, hasP2, p2Name, p2Avatar, p2Ready, !amIHost, "online_role_guest");
        }

        private static void ApplyCard(PlayerCard c, bool occupied, string playerName, int avatarIndex, bool ready, bool isMe, string roleKey)
        {
            if (c == null) return;
            c.empty = !occupied;
            string role = LocalizationManager.Get(roleKey);
            if (isMe && occupied) role += "  •  " + LocalizationManager.Get("online_you");
            c.roleText.text = role;

            if (!occupied)
            {
                c.avatar.sprite = AvatarLibrary.DotSprite;
                c.avatar.color = new Color(0.10f, 0.15f, 0.25f, 1f);
                c.nameText.text = LocalizationManager.Get("online_slot_empty");
                c.nameText.fontSize = 15;
                c.nameText.color = new Color(0.58f, 0.66f, 0.80f, 1f);
                c.frame.SetBaseColor(ColorGrey);
                c.ring.color = ColorGrey;
                c.roleText.color = ColorGrey;
                c.chip.gameObject.SetActive(false);
                return;
            }

            c.chip.gameObject.SetActive(true);
            c.avatar.sprite = AvatarLibrary.Get(avatarIndex < 0 ? 0 : avatarIndex);
            c.avatar.color = Color.white;
            c.nameText.text = playerName;
            c.nameText.fontSize = 22;
            c.nameText.color = Color.white;
            c.roleText.color = c.accent;

            Color state = ready ? ColorGreen : ColorOrange;
            c.frame.SetBaseColor(ready ? ColorGreen : c.accent);
            c.ring.color = ready ? ColorGreen : c.accent;
            c.chip.color = ready ? new Color(0.07f, 0.30f, 0.19f, 0.98f) : new Color(0.32f, 0.19f, 0.06f, 0.98f);
            c.led.color = state;
            c.chipText.color = state;
            c.chipText.text = ready ? LocalizationManager.Get("online_ready") : LocalizationManager.Get("online_not_ready");
        }

        private void Update()
        {
            // Ô trống: viền avatar nhấp nháy nhẹ báo đang chờ đối thủ
            if (card2 != null && card2.empty)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                Color c = ColorGrey;
                c.a = Mathf.Lerp(0.30f, 0.95f, pulse);
                card2.ring.color = c;
            }
        }

        // ------------------------------------------------------------------ tiện ích

        private static Text MakeText(Transform parent, string objName, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static Image MakeImage(Transform parent, string objName, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        private static void Place(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
