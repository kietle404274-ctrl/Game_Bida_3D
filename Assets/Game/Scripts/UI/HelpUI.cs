using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class HelpUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text helpContentText;

        // Mỗi dòng: { nhãn phím (bắt đầu bằng '@' nếu là khóa dịch), khóa dịch của chức năng }
        private static readonly string[][] Rows =
        {
            new[] { "@help_k_mouse",  "help_aim" },
            new[] { "@help_k_scroll", "help_power" },
            new[] { "@help_k_click",  "help_shoot" },
            new[] { "J",              "help_spin" },
            new[] { "K",              "help_elev" },
            new[] { "V",              "help_camera" },
            new[] { "WASD",           "help_place" },
            new[] { "ENTER",          "help_confirm" },
            new[] { "ALT",            "help_cursor" },
            new[] { "TAB",            "help_stats" },
            new[] { "R",              "help_rules" },
            new[] { "H",              "help_help" },
            new[] { "ESC",            "help_settings" },
        };

        private const int RowsInLeftColumn = 6;
        private const float ColumnWidth = 350f;
        private const float ColumnGap = 20f;
        private const float RowHeight = 50f;
        private const float KeySlotWidth = 124f;
        private const int LabelFontSize = 22;
        private const int KeyFontSize = 20;

        // Độ nghiêng chung (tỉ lệ inset/chiều cao) cho khớp hình bình hành của HUD
        private const float SlantRatio = 0.225f;

        // Bảng màu khoa học viễn tưởng: nền navy sâu + neon cyan, điểm nhấn vàng của HUD
        private static readonly Color NeonCyan = new Color(0.10f, 0.82f, 1f, 1f);
        private static readonly Color NeonCyanDeep = new Color(0.03f, 0.38f, 0.62f, 1f);
        private static readonly Color KeyFaceColor = new Color(0.10f, 0.20f, 0.34f, 1f);
        private static readonly Color KeyGlossColor = new Color(0.55f, 0.90f, 1f, 0.16f);
        private static readonly Color KeyShadowColor = new Color(0f, 0.02f, 0.06f, 0.65f);
        private static readonly Color KeyTextColor = new Color(0.82f, 0.97f, 1f, 1f);
        private static readonly Color LabelColor = new Color(0.90f, 0.95f, 1f, 1f);
        private static readonly Color RowBgColor = new Color(0.07f, 0.13f, 0.23f, 0.92f);

        private RectTransform rowsRoot;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;

            RefreshLocalizedTexts();
        }

        private void RefreshLocalizedTexts()
        {
            BuildRows();

            if (modalRoot != null)
            {
                Transform title = modalRoot.transform.Find("Title");
                if (title != null && title.GetComponent<Text>() != null)
                    title.GetComponent<Text>().text = LocalizationManager.Get("help_title");
            }

            if (closeButton != null)
            {
                Text t = closeButton.transform.Find("Label")?.GetComponent<Text>() ?? closeButton.GetComponentInChildren<Text>();
                if (t != null) t.text = LocalizationManager.Get("rules_close_btn");
            }
        }

        // ------------------------------------------------------------------ Dựng danh sách phím

        private void BuildRows()
        {
            if (modalRoot == null) return;

            // Khối văn bản dài cũ không còn dùng: thay bằng các dòng [PHÍM] + chức năng
            if (helpContentText != null) helpContentText.gameObject.SetActive(false);

            if (rowsRoot == null)
            {
                var go = new GameObject("HelpRows", typeof(RectTransform));
                go.layer = modalRoot.layer;
                go.transform.SetParent(modalRoot.transform, false);
                rowsRoot = go.GetComponent<RectTransform>();
                rowsRoot.anchorMin = rowsRoot.anchorMax = new Vector2(0.5f, 0.5f);
                rowsRoot.pivot = new Vector2(0.5f, 0.5f);
                rowsRoot.sizeDelta = new Vector2(ColumnWidth * 2f + ColumnGap, RowHeight * 7f);
                rowsRoot.anchoredPosition = new Vector2(0f, 6f);
            }

            for (int i = rowsRoot.childCount - 1; i >= 0; i--)
                Destroy(rowsRoot.GetChild(i).gameObject);

            Font font = helpContentText != null ? helpContentText.font : null;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int i = 0; i < Rows.Length; i++)
            {
                int col = i < RowsInLeftColumn ? 0 : 1;
                int rowIdx = col == 0 ? i : i - RowsInLeftColumn;

                string keyText = Rows[i][0];
                if (keyText.StartsWith("@")) keyText = LocalizationManager.Get(keyText.Substring(1));
                string label = LocalizationManager.Get(Rows[i][1]);

                CreateRow(col, rowIdx, keyText, label, font);
            }
        }

        private void CreateRow(int col, int rowIdx, string keyText, string label, Font font)
        {
            var row = new GameObject("Row", typeof(RectTransform));
            row.layer = modalRoot.layer;
            row.transform.SetParent(rowsRoot, false);
            var rt = row.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(ColumnWidth, RowHeight - 4f);
            rt.anchoredPosition = new Vector2(col * (ColumnWidth + ColumnGap), -rowIdx * RowHeight);

            Vector2 rowSize = rt.sizeDelta;
            MakeSkew(row.transform, "Bg", Vector2.zero, rowSize, RowBgColor, 0.65f);
            // Đường kẻ neon mảnh ở đáy mỗi dòng
            MakeSkew(row.transform, "Line", new Vector2(0f, -(rowSize.y * 0.5f) + 1f), new Vector2(rowSize.x, 2f),
                new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.45f), 1f, 0.5f);

            // Keycap nằm trong ô cố định bên trái, căn giữa
            float keyW = Mathf.Clamp(34f + keyText.Length * 14f, 56f, KeySlotWidth - 6f);
            CreateKeycap(row.transform, keyText, keyW, font, new Vector2(KeySlotWidth * 0.5f + 4f, 0f));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.layer = modalRoot.layer;
            labelGo.transform.SetParent(row.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 1f);
            lrt.offsetMin = new Vector2(KeySlotWidth + 10f, 0f);
            lrt.offsetMax = new Vector2(-12f, 0f);
            var txt = labelGo.GetComponent<Text>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = LabelFontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = LabelColor;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.resizeTextForBestFit = true;
            txt.resizeTextMinSize = 14;
            txt.resizeTextMaxSize = LabelFontSize;
            txt.raycastTarget = false;
        }

        /// <summary>
        /// Phím bấm hình bình hành kiểu HUD: bóng đổ, viền neon cyan (dày hơn ở đáy tạo cảm giác nổi),
        /// mặt navy chuyển sắc, vệt sáng trên và chữ phát sáng.
        /// </summary>
        private void CreateKeycap(Transform parent, string text, float width, Font font, Vector2 centerPos)
        {
            const float h = 40f;
            var root = new GameObject("Keycap", typeof(RectTransform));
            root.layer = modalRoot.layer;
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, h + 8f);
            rt.anchoredPosition = centerPos;

            // Bóng đổ lệch xuống
            MakeSkew(root.transform, "Shadow", new Vector2(0f, -4f), new Vector2(width + 2f, h), KeyShadowColor, 1f);
            // Viền neon: sáng ở trên, đậm dần xuống đáy
            MakeSkew(root.transform, "Edge", new Vector2(0f, 0f), new Vector2(width, h), NeonCyan, 0.55f);
            // Gờ đáy: dải cyan sáng ở sát đáy để phím trông nổi
            MakeSkew(root.transform, "Lip", new Vector2(0f, -(h * 0.5f) + 2f), new Vector2(width - 6f, 4f),
                new Color(0.55f, 0.95f, 1f, 0.9f), 1f);

            // Mặt phím (thu vào 2.5px mỗi bên, dày hơn phía đáy)
            Vector2 faceSize = new Vector2(width - 6f, h - 8f);
            Vector2 facePos = new Vector2(0f, 2.5f);
            MakeSkew(root.transform, "Face", facePos, faceSize, KeyFaceColor, 0.55f);
            MakeSkew(root.transform, "Gloss", new Vector2(0f, facePos.y + faceSize.y * 0.24f),
                new Vector2(faceSize.x - 8f, faceSize.y * 0.42f), KeyGlossColor, 0.2f);

            var tgo = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline));
            tgo.layer = modalRoot.layer;
            tgo.transform.SetParent(root.transform, false);
            var trt = tgo.GetComponent<RectTransform>();
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(width - 14f, faceSize.y);
            trt.anchoredPosition = facePos;
            var t = tgo.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = KeyFontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = KeyTextColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = KeyFontSize;
            t.raycastTarget = false;

            // Hào quang cyan quanh chữ
            var glow = tgo.GetComponent<Outline>();
            glow.effectColor = new Color(NeonCyan.r, NeonCyan.g, NeonCyan.b, 0.45f);
            glow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Tạo một khối hình bình hành (cùng độ nghiêng với HUD). slantScale co độ nghiêng cho khối mỏng.</summary>
        private static SkewedPanelGraphic MakeSkew(Transform parent, string name, Vector2 pos, Vector2 size, Color color,
            float bottomDarken, float slantScale = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var g = go.GetComponent<SkewedPanelGraphic>();
            float inset = Mathf.Round(size.y * SlantRatio * slantScale);
            // Cùng hướng với khung HUD: góc phải-trên và trái-dưới thu vào
            g.SetInsets(0f, inset, inset, 0f);
            g.SetBottomDarken(bottomDarken);
            g.color = color;
            g.raycastTarget = false;
            return g;
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("help")) return;
            RefreshLocalizedTexts();
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            HudModalState.Release("help");
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            HudModalState.Release("help");
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Hide();
                else Show();
            }
        }
    }
}
