using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Quản lý dòng hiển thị "Bàn thi đấu" + nút "Đổi" trong các Popup Cấu hình trận đấu
    /// và Popup cỡ vừa để chọn bàn thi đấu (kèm ảnh chụp mặt bàn từ trên thẳng đứng xuống
    /// và khung hình chữ nhật bo viền xanh nhạt bên ngoài khi chọn).
    /// </summary>
    public class TableSelectorPopupUI : MonoBehaviour
    {
        private class ConfigRowRefs
        {
            public Text labelHeader;
            public Text selectedNameText;
            public Image miniPreviewImage;
            public Text changeBtnText;
        }

        private class TableCardRefs
        {
            public TableTheme theme;
            public Button button;
            public Image cardBg;
            public Image selectionBorder;
            public Image previewImage;
            public Text titleText;
        }

        private readonly List<ConfigRowRefs> registeredRows = new List<ConfigRowRefs>();
        private readonly List<TableCardRefs> tableCards = new List<TableCardRefs>();

        private GameObject modalRoot;
        private Transform modalContainerParent;
        private Text modalTitleText;
        private Button closeButton;
        private Text closeButtonText;

        private static Sprite s_roundedFillSprite;
        private static Sprite s_roundedBorderSprite;

        // Màu xanh nhạt cho viền bo góc bàn đang chọn
        private static readonly Color LightBlueBorderColor = new Color(0.42f, 0.88f, 1.00f, 1.00f);
        private static readonly Color UnselectedBorderColor = new Color(0.32f, 0.45f, 0.62f, 0.42f);
        private static readonly Color SelectedCardBgColor = new Color(0.11f, 0.21f, 0.34f, 0.98f);
        private static readonly Color UnselectedCardBgColor = new Color(0.07f, 0.12f, 0.20f, 0.95f);

        public event Action<TableTheme> OnTableThemeChanged;

        /// <summary>
        /// Tạo dòng "BÀN THI ĐẤU" + khung tên bàn đang chọn + nút "Đổi" bên trong یک Popup cấu hình trận đấu.
        /// </summary>
        public void AttachConfigRow(Transform parentModal, float labelY, float rowY, float width = 520f)
        {
            if (parentModal == null) return;

            if (modalContainerParent == null)
            {
                Canvas parentCanvas = parentModal.GetComponentInParent<Canvas>();
                if (parentModal.parent != null && parentModal.parent.name == "ModalsRoot")
                {
                    modalContainerParent = parentModal.parent;
                }
                else if (parentCanvas != null)
                {
                    modalContainerParent = parentCanvas.rootCanvas != null ? parentCanvas.rootCanvas.transform : parentCanvas.transform;
                }
                else
                {
                    modalContainerParent = parentModal;
                }
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureRoundedSprites();

            // 1. Nhãn tiêu đề mục "BÀN THI ĐẤU"
            GameObject lblGo = new GameObject("TableLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            lblGo.transform.SetParent(parentModal, false);
            RectTransform lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = lblRt.anchorMax = new Vector2(0.5f, 0.5f);
            lblRt.pivot = new Vector2(0.5f, 0.5f);
            lblRt.anchoredPosition = new Vector2(0f, labelY);
            lblRt.sizeDelta = new Vector2(width, 24f);

            Text lblText = lblGo.GetComponent<Text>();
            lblText.font = font;
            lblText.fontSize = 15;
            lblText.fontStyle = FontStyle.Bold;
            lblText.alignment = TextAnchor.MiddleLeft;
            lblText.color = new Color(0.75f, 0.85f, 0.96f, 1f);
            lblText.text = GetHeaderLabelText();

            // 2. Dòng chứa [Ô bàn đang chọn] + [Nút Đổi]
            GameObject rowGo = new GameObject("TableSelectRow", typeof(RectTransform));
            rowGo.transform.SetParent(parentModal, false);
            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, rowY);
            rowRt.sizeDelta = new Vector2(width, 44f);

            float btnWidth = 114f;
            float gap = 10f;
            float boxWidth = width - btnWidth - gap;

            // Ô hiển thị bàn đang chọn bên trái
            GameObject displayBox = new GameObject("SelectedTableBox", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            displayBox.transform.SetParent(rowGo.transform, false);
            RectTransform boxRt = displayBox.GetComponent<RectTransform>();
            boxRt.anchorMin = boxRt.anchorMax = new Vector2(0.5f, 0.5f);
            boxRt.pivot = new Vector2(0.5f, 0.5f);
            boxRt.anchoredPosition = new Vector2(-(btnWidth + gap) * 0.5f, 0f);
            boxRt.sizeDelta = new Vector2(boxWidth, 44f);

            Image boxImg = displayBox.GetComponent<Image>();
            boxImg.sprite = s_roundedFillSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = new Color(0.08f, 0.14f, 0.23f, 0.96f);

            GameObject boxBorderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            boxBorderGo.transform.SetParent(displayBox.transform, false);
            RectTransform boxBorderRt = boxBorderGo.GetComponent<RectTransform>();
            boxBorderRt.anchorMin = Vector2.zero;
            boxBorderRt.anchorMax = Vector2.one;
            boxBorderRt.offsetMin = Vector2.zero;
            boxBorderRt.offsetMax = Vector2.zero;
            Image boxBorderImg = boxBorderGo.GetComponent<Image>();
            boxBorderImg.sprite = s_roundedBorderSprite;
            boxBorderImg.type = Image.Type.Sliced;
            boxBorderImg.color = new Color(0.38f, 0.54f, 0.74f, 0.75f);
            boxBorderImg.raycastTarget = false;

            // Ảnh thu nhỏ mặt bàn chụp từ trên xuống trong ô đang chọn
            GameObject miniImgGo = new GameObject("MiniTableThumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            miniImgGo.transform.SetParent(displayBox.transform, false);
            RectTransform miniRt = miniImgGo.GetComponent<RectTransform>();
            miniRt.anchorMin = miniRt.anchorMax = new Vector2(0f, 0.5f);
            miniRt.pivot = new Vector2(0f, 0.5f);
            miniRt.anchoredPosition = new Vector2(12f, 0f);
            miniRt.sizeDelta = new Vector2(60f, 32f);
            Image miniImg = miniImgGo.GetComponent<Image>();
            miniImg.preserveAspect = true;
            miniImg.raycastTarget = false;

            // Tên bàn đang chọn
            GameObject valTextGo = new GameObject("SelectedTableName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            valTextGo.transform.SetParent(displayBox.transform, false);
            RectTransform valRt = valTextGo.GetComponent<RectTransform>();
            valRt.anchorMin = Vector2.zero;
            valRt.anchorMax = Vector2.one;
            valRt.offsetMin = new Vector2(82f, 0f);
            valRt.offsetMax = new Vector2(-12f, 0f);
            Text valText = valTextGo.GetComponent<Text>();
            valText.font = font;
            valText.fontSize = 16;
            valText.fontStyle = FontStyle.Bold;
            valText.alignment = TextAnchor.MiddleLeft;
            valText.color = new Color(0.95f, 0.98f, 1f, 1f);
            valText.raycastTarget = false;

            Button boxBtn = displayBox.GetComponent<Button>();
            boxBtn.transition = Selectable.Transition.None;
            boxBtn.onClick.AddListener(OpenSelectorModal);

            // Nút "Đổi" bên phải
            GameObject changeBtnGo = new GameObject("BtnChangeTable", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(Button));
            changeBtnGo.transform.SetParent(rowGo.transform, false);
            RectTransform btnRt = changeBtnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2((boxWidth + gap) * 0.5f, 0f);
            btnRt.sizeDelta = new Vector2(btnWidth, 42f);

            SkewedPanelGraphic btnBg = changeBtnGo.GetComponent<SkewedPanelGraphic>();
            btnBg.SetInsets(0f, 12f, 12f, 0f);
            btnBg.color = new Color(0.11f, 0.26f, 0.42f, 0.98f);

            GameObject btnFrameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            btnFrameGo.transform.SetParent(changeBtnGo.transform, false);
            RectTransform btnFrameRt = btnFrameGo.GetComponent<RectTransform>();
            btnFrameRt.anchorMin = Vector2.zero;
            btnFrameRt.anchorMax = Vector2.one;
            btnFrameRt.offsetMin = Vector2.zero;
            btnFrameRt.offsetMax = Vector2.zero;
            BorderProgressGraphic btnFrame = btnFrameGo.GetComponent<BorderProgressGraphic>();
            btnFrame.SetInsets(0f, 12f, 12f, 0f);
            btnFrame.SetBaseColor(new Color(0.42f, 0.85f, 1.0f, 1.0f));
            btnFrame.raycastTarget = false;

            GameObject btnTxtGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            btnTxtGo.transform.SetParent(changeBtnGo.transform, false);
            RectTransform btnTxtRt = btnTxtGo.GetComponent<RectTransform>();
            btnTxtRt.anchorMin = Vector2.zero;
            btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = Vector2.zero;
            btnTxtRt.offsetMax = Vector2.zero;
            Text btnTxt = btnTxtGo.GetComponent<Text>();
            btnTxt.font = font;
            btnTxt.fontSize = 15;
            btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.color = Color.white;
            btnTxt.text = GetChangeButtonText();
            btnTxt.raycastTarget = false;

            Button changeBtn = changeBtnGo.GetComponent<Button>();
            changeBtn.targetGraphic = btnBg;
            changeBtn.transition = Selectable.Transition.None;
            changeBtn.onClick.AddListener(OpenSelectorModal);

            registeredRows.Add(new ConfigRowRefs
            {
                labelHeader = lblText,
                selectedNameText = valText,
                miniPreviewImage = miniImg,
                changeBtnText = btnTxt
            });

            RefreshAllDisplays();
        }

        public void OpenSelectorModal()
        {
            EnsureSelectorModalBuilt();
            if (modalRoot == null) return;

            Transform targetParent = ResolveModalParent();
            if (targetParent != null && modalRoot.transform.parent != targetParent)
            {
                modalRoot.transform.SetParent(targetParent, false);
            }

            RefreshAllDisplays();
            modalRoot.SetActive(true);
            modalRoot.transform.SetAsLastSibling();
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.PlayButtonClick();
            }
        }

        public void CloseSelectorModal()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private Transform ResolveModalParent()
        {
            if (modalContainerParent != null) return modalContainerParent;

            Canvas rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
            {
                rootCanvas = FindObjectOfType<Canvas>();
            }
            if (rootCanvas != null)
            {
                Transform modalsRoot = rootCanvas.transform.Find("ModalsRoot");
                modalContainerParent = modalsRoot != null ? modalsRoot : rootCanvas.transform;
                return modalContainerParent;
            }
            return transform;
        }

        private void EnsureSelectorModalBuilt()
        {
            if (modalRoot != null) return;

            EnsureRoundedSprites();
            Transform modalParent = ResolveModalParent();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Khung gốc Popup Chọn Bàn cỡ vừa (820 x 510)
            modalRoot = new GameObject("TableSelectorModal", typeof(RectTransform));
            modalRoot.transform.SetParent(modalParent, false);
            RectTransform rt = modalRoot.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(820f, 510f);

            // Lớp nền mờ phía sau (click ra ngoài để đóng)
            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            dim.transform.SetParent(modalRoot.transform, false);
            dim.GetComponent<RectTransform>().sizeDelta = new Vector2(6000f, 4000f);
            dim.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.78f);
            Button dimBtn = dim.GetComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(CloseSelectorModal);

            // Thẻ nền chính của Popup
            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            card.transform.SetParent(modalRoot.transform, false);
            RectTransform cr = card.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.offsetMin = Vector2.zero;
            cr.offsetMax = Vector2.zero;
            SkewedPanelGraphic cardG = card.GetComponent<SkewedPanelGraphic>();
            cardG.color = new Color(0.06f, 0.10f, 0.18f, 0.99f);
            cardG.SetInsets(0f, 28f, 28f, 0f);
            cardG.raycastTarget = true;

            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frame.transform.SetParent(modalRoot.transform, false);
            RectTransform fr = frame.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            BorderProgressGraphic frameG = frame.GetComponent<BorderProgressGraphic>();
            frameG.SetInsets(0f, 28f, 28f, 0f);
            frameG.SetBaseColor(new Color(0.42f, 0.75f, 0.96f, 1f));
            frameG.raycastTarget = false;

            // Tiêu đề Popup
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(modalRoot.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -24f);
            titleRt.sizeDelta = new Vector2(0f, 40f);
            modalTitleText = titleGo.GetComponent<Text>();
            modalTitleText.font = font;
            modalTitleText.fontSize = 24;
            modalTitleText.fontStyle = FontStyle.Bold;
            modalTitleText.alignment = TextAnchor.MiddleCenter;
            modalTitleText.color = Color.white;
            modalTitleText.text = GetModalTitleText();

            LobbyModalStyler.StyleModal(modalRoot, LobbyModalStyler.AccentBlue);

            // Tạo 5 thẻ bàn thi đấu (Hàng trên 3 bàn, Hàng dưới 2 bàn căn giữa)
            TableTheme[] themes =
            {
                TableTheme.Blue,
                TableTheme.Purple,
                TableTheme.Green,
                TableTheme.Red,
                TableTheme.Grey
            };

            Vector2 cardSize = new Vector2(236f, 158f);
            float colSpacing = 252f;
            float row1Y = 82f;
            float row2Y = -92f;

            Vector2[] positions =
            {
                new Vector2(-colSpacing, row1Y),
                new Vector2(0f,          row1Y),
                new Vector2(colSpacing,  row1Y),
                new Vector2(-colSpacing * 0.5f, row2Y),
                new Vector2(colSpacing * 0.5f,  row2Y)
            };

            tableCards.Clear();
            for (int i = 0; i < themes.Length; i++)
            {
                TableTheme theme = themes[i];
                TableCardRefs cardRef = CreateTableCard(modalRoot.transform, font, theme, positions[i], cardSize);
                tableCards.Add(cardRef);
            }

            // Nút "XONG" ở dưới cùng
            GameObject closeBtnGo = new GameObject("BtnCloseTableModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(Button));
            closeBtnGo.transform.SetParent(modalRoot.transform, false);
            RectTransform closeRt = closeBtnGo.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(0.5f, 0.5f);
            closeRt.pivot = new Vector2(0.5f, 0.5f);
            closeRt.anchoredPosition = new Vector2(0f, -208f);
            closeRt.sizeDelta = new Vector2(220f, 46f);

            SkewedPanelGraphic closeBg = closeBtnGo.GetComponent<SkewedPanelGraphic>();
            closeBg.SetInsets(0f, 14f, 14f, 0f);
            closeBg.color = new Color(0.08f, 0.38f, 0.24f, 0.98f);

            GameObject closeFrameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            closeFrameGo.transform.SetParent(closeBtnGo.transform, false);
            RectTransform closeFrameRt = closeFrameGo.GetComponent<RectTransform>();
            closeFrameRt.anchorMin = Vector2.zero;
            closeFrameRt.anchorMax = Vector2.one;
            closeFrameRt.offsetMin = Vector2.zero;
            closeFrameRt.offsetMax = Vector2.zero;
            BorderProgressGraphic closeFrame = closeFrameGo.GetComponent<BorderProgressGraphic>();
            closeFrame.SetInsets(0f, 14f, 14f, 0f);
            closeFrame.SetBaseColor(LobbyModalStyler.AccentGreen);
            closeFrame.raycastTarget = false;

            GameObject closeTxtGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            closeTxtGo.transform.SetParent(closeBtnGo.transform, false);
            RectTransform closeTxtRt = closeTxtGo.GetComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.offsetMin = Vector2.zero;
            closeTxtRt.offsetMax = Vector2.zero;
            closeButtonText = closeTxtGo.GetComponent<Text>();
            closeButtonText.font = font;
            closeButtonText.fontSize = 17;
            closeButtonText.fontStyle = FontStyle.Bold;
            closeButtonText.alignment = TextAnchor.MiddleCenter;
            closeButtonText.color = Color.white;
            closeButtonText.text = GetDoneButtonText();
            closeButtonText.raycastTarget = false;

            closeButton = closeBtnGo.GetComponent<Button>();
            closeButton.targetGraphic = closeBg;
            closeButton.transition = Selectable.Transition.None;
            closeButton.onClick.AddListener(CloseSelectorModal);
            UIHoverEffect.Attach(closeBtnGo, true);
        }

        private TableCardRefs CreateTableCard(Transform parent, Font font, TableTheme theme, Vector2 anchoredPos, Vector2 size)
        {
            GameObject cardGo = new GameObject($"TableCard_{theme}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(parent, false);
            RectTransform rt = cardGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image bgImg = cardGo.GetComponent<Image>();
            bgImg.sprite = s_roundedFillSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = UnselectedCardBgColor;

            // Ảnh chụp mặt bàn từ trên thẳng đứng xuống
            GameObject previewGo = new GameObject("TopDownTableImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            previewGo.transform.SetParent(cardGo.transform, false);
            RectTransform previewRt = previewGo.GetComponent<RectTransform>();
            previewRt.anchorMin = previewRt.anchorMax = new Vector2(0.5f, 0.5f);
            previewRt.pivot = new Vector2(0.5f, 0.5f);
            previewRt.anchoredPosition = new Vector2(0f, 14f);
            previewRt.sizeDelta = new Vector2(size.x - 24f, size.y - 50f);

            Image previewImg = previewGo.GetComponent<Image>();
            previewImg.sprite = TableThemeApplier.LoadThemePreviewSprite(theme);
            previewImg.preserveAspect = true;
            previewImg.raycastTarget = false;

            // Nhãn tên bàn phía dưới
            GameObject nameGo = new GameObject("TableName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            nameGo.transform.SetParent(cardGo.transform, false);
            RectTransform nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.anchoredPosition = new Vector2(0f, 8f);
            nameRt.sizeDelta = new Vector2(-12f, 26f);

            Text nameTxt = nameGo.GetComponent<Text>();
            nameTxt.font = font;
            nameTxt.fontSize = 15;
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.text = GetLocalizedTableName(theme);
            nameTxt.raycastTarget = false;

            // Khung hình chữ nhật bo viền xanh nhạt bên ngoài khi được chọn
            GameObject borderGo = new GameObject("RoundedSelectionBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderGo.transform.SetParent(cardGo.transform, false);
            RectTransform borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-4f, -4f);
            borderRt.offsetMax = new Vector2(4f, 4f);

            Image borderImg = borderGo.GetComponent<Image>();
            borderImg.sprite = s_roundedBorderSprite;
            borderImg.type = Image.Type.Sliced;
            borderImg.color = LightBlueBorderColor;
            borderImg.raycastTarget = false;

            Button btn = cardGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            TableTheme capturedTheme = theme;
            btn.onClick.AddListener(() => SelectTableTheme(capturedTheme));
            UIHoverEffect.Attach(cardGo, false, null, null, 0f, 1.03f);

            return new TableCardRefs
            {
                theme = theme,
                button = btn,
                cardBg = bgImg,
                selectionBorder = borderImg,
                previewImage = previewImg,
                titleText = nameTxt
            };
        }

        public void SelectTableTheme(TableTheme theme)
        {
            GameSession.SelectedTableTheme = theme;
            TableThemeApplier.ApplyThemeToCurrentScene(theme);
            RefreshAllDisplays();
            OnTableThemeChanged?.Invoke(theme);
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.PlayButtonClick();
            }
        }

        public void RefreshAllDisplays()
        {
            TableTheme current = GameSession.SelectedTableTheme;
            string currentName = GetLocalizedTableName(current);
            Sprite currentPreview = TableThemeApplier.LoadThemePreviewSprite(current);

            for (int i = 0; i < registeredRows.Count; i++)
            {
                ConfigRowRefs row = registeredRows[i];
                if (row == null) continue;
                if (row.labelHeader != null) row.labelHeader.text = GetHeaderLabelText();
                if (row.selectedNameText != null) row.selectedNameText.text = currentName;
                if (row.miniPreviewImage != null) row.miniPreviewImage.sprite = currentPreview;
                if (row.changeBtnText != null) row.changeBtnText.text = GetChangeButtonText();
            }

            if (modalTitleText != null) modalTitleText.text = GetModalTitleText();
            if (closeButtonText != null) closeButtonText.text = GetDoneButtonText();

            for (int i = 0; i < tableCards.Count; i++)
            {
                TableCardRefs card = tableCards[i];
                if (card == null) continue;

                bool isSelected = card.theme == current;
                if (card.titleText != null)
                {
                    card.titleText.text = GetLocalizedTableName(card.theme);
                    card.titleText.color = isSelected
                        ? new Color(0.55f, 0.92f, 1.00f, 1.00f)
                        : new Color(0.86f, 0.91f, 0.97f, 0.95f);
                }

                if (card.cardBg != null)
                {
                    card.cardBg.color = isSelected ? SelectedCardBgColor : UnselectedCardBgColor;
                }

                if (card.selectionBorder != null)
                {
                    card.selectionBorder.color = isSelected ? LightBlueBorderColor : UnselectedBorderColor;
                    RectTransform brt = card.selectionBorder.rectTransform;
                    brt.offsetMin = isSelected ? new Vector2(-4f, -4f) : Vector2.zero;
                    brt.offsetMax = isSelected ? new Vector2(4f, 4f) : Vector2.zero;
                }
            }
        }

        public static string GetLocalizedTableName(TableTheme theme)
        {
            switch (theme)
            {
                case TableTheme.Purple: return LocalizationManager.Get("table_purple", "Purple Table");
                case TableTheme.Green:  return LocalizationManager.Get("table_green", "Green Table");
                case TableTheme.Red:    return LocalizationManager.Get("table_red", "Red Table");
                case TableTheme.Grey:   return LocalizationManager.Get("table_grey", "Grey Table");
                case TableTheme.Blue:
                default:                return LocalizationManager.Get("table_blue", "Blue Table");
            }
        }

        private static string GetHeaderLabelText()
        {
            return LocalizationManager.Get("table_header", "BILLIARD TABLE");
        }

        private static string GetChangeButtonText()
        {
            return LocalizationManager.Get("table_change", "CHANGE");
        }

        private static string GetModalTitleText()
        {
            return LocalizationManager.Get("table_select_title", "SELECT BILLIARD TABLE");
        }

        private static string GetDoneButtonText()
        {
            return LocalizationManager.Get("table_done", "DONE");
        }

        private static void EnsureRoundedSprites()
        {
            if (s_roundedFillSprite == null)
            {
                s_roundedFillSprite = CreateRoundedSprite(64, 14f, 0f);
            }
            if (s_roundedBorderSprite == null)
            {
                s_roundedBorderSprite = CreateRoundedSprite(64, 14f, 3.8f);
            }
        }

        /// <summary>
        /// Tạo Sprite hình chữ nhật bo góc (9-slice) chống răng cưa mượt mà.
        /// Nếu borderThickness > 0 sẽ chỉ vẽ đường viền bo góc bên ngoài (rỗng ruột).
        /// </summary>
        private static Sprite CreateRoundedSprite(int size, float radius, float borderThickness)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            float half = size * 0.5f;
            Vector2 halfSize = new Vector2(half - 1.5f, half - 1.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - (halfSize - new Vector2(radius, radius));
                    float dist = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;

                    float alpha;
                    if (borderThickness <= 0f)
                    {
                        alpha = Mathf.Clamp01(0.5f - dist);
                    }
                    else
                    {
                        float outerAlpha = Mathf.Clamp01(0.5f - dist);
                        float innerDist = dist + borderThickness;
                        float innerAlpha = Mathf.Clamp01(0.5f - innerDist);
                        alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            float slice = Mathf.Ceil(radius + borderThickness + 2f);
            Vector4 border = new Vector4(slice, slice, slice, slice);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }
    }
}

