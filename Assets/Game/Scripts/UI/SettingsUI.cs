using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    public class SettingsUI : MonoBehaviour
    {
        [Header("Modal Root")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button resumeButton;

        [Header("Volume Sliders")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Match Actions")]
        [SerializeField] private Button surrenderButton;
        [SerializeField] private Button quitMatchButton;
        [SerializeField] private GameObject confirmDialogRoot;
        [SerializeField] private Text confirmMessageText;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private MatchStateManager matchStateManager;
        private Action onQuitAction;
        private Action pendingConfirmAction;

        private void Awake()
        {
            LoadSavedSettings();

            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (resumeButton != null) resumeButton.onClick.AddListener(Hide);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

            if (surrenderButton != null) surrenderButton.onClick.AddListener(OnSurrenderClicked);
            if (quitMatchButton != null) quitMatchButton.onClick.AddListener(OnQuitClicked);

            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(ExecutePendingConfirm);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(CancelConfirmDialog);

            EnsureConfirmDialogStyled();
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
        }

        public void Initialize(MatchStateManager stateMgr, Action quitAction = null)
        {
            matchStateManager = stateMgr;
            onQuitAction = quitAction;
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("settings")) return;
            RefreshLocalizedTexts();
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(true);

            // Tạm dừng hoàn toàn mô phỏng khi ở chế độ Offline theo đặc tả Mục 5
            bool isOnline = (Game.Billiards.Core.GameSession.Instance != null && 
                             Game.Billiards.Core.GameSession.Instance.CurrentGameMode == Game.Billiards.Core.GameMode.OnlinePvP);
            if (!isOnline)
            {
                Time.timeScale = 0f;
            }
        }

        public void Hide()
        {
            HudModalState.Release("settings");
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            if (modalRoot != null) modalRoot.SetActive(false);

            Time.timeScale = 1f;
        }

        private void OnDisable()
        {
            Time.timeScale = 1f;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            HudModalState.Release("settings");
            Time.timeScale = 1f;
        }

        private void RefreshLocalizedTexts()
        {
            if (modalRoot != null)
            {
                Transform title = modalRoot.transform.Find("Title");
                if (title != null && title.GetComponent<Text>() != null) title.GetComponent<Text>().text = LocalizationManager.Get("settings_title");
                Transform masterLbl = modalRoot.transform.Find("LblMaster");
                if (masterLbl != null && masterLbl.GetComponent<Text>() != null) masterLbl.GetComponent<Text>().text = LocalizationManager.Get("settings_master");
                Transform musicLbl = modalRoot.transform.Find("LblMusic");
                if (musicLbl != null && musicLbl.GetComponent<Text>() != null) musicLbl.GetComponent<Text>().text = LocalizationManager.Get("settings_music");
                Transform sfxLbl = modalRoot.transform.Find("LblSFX");
                if (sfxLbl != null && sfxLbl.GetComponent<Text>() != null) sfxLbl.GetComponent<Text>().text = LocalizationManager.Get("settings_sfx");
            }
            SetButtonText(resumeButton, LocalizationManager.Get("hud_resume"));
            SetButtonText(surrenderButton, LocalizationManager.Get("hud_surrender"));
            SetButtonText(quitMatchButton, LocalizationManager.Get("hud_quit_match"));
            if (confirmTitleText != null && !string.IsNullOrEmpty(currentConfirmTitleKey))
                confirmTitleText.text = LocalizationManager.Get(currentConfirmTitleKey);
            if (confirmMessageText != null && !string.IsNullOrEmpty(currentConfirmMessageKey))
                confirmMessageText.text = LocalizationManager.Get(currentConfirmMessageKey);
            SetButtonText(confirmYesButton, LocalizationManager.Get("btn_yes"));
            SetButtonText(confirmNoButton, LocalizationManager.Get("btn_no"));
        }

        private static void SetButtonText(Button b, string val)
        {
            if (b == null || string.IsNullOrEmpty(val)) return;
            Text t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = val;
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Hide();
                else Show();
            }
        }

        private void LoadSavedSettings()
        {
            float master = PlayerPrefs.GetFloat("Volume_Master", 1.0f);
            float music = PlayerPrefs.GetFloat("Volume_Music", 0.8f);
            float sfx = PlayerPrefs.GetFloat("Volume_SFX", 1.0f);

            if (masterVolumeSlider != null) masterVolumeSlider.value = master;
            if (musicVolumeSlider != null) musicVolumeSlider.value = music;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;
        }

        private void OnMasterVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetMasterVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_Master", val);
                AudioListener.volume = val;
            }
        }

        private void OnMusicVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetMusicVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_Music", val);
            }
        }

        private void OnSfxVolumeChanged(float val)
        {
            if (Game.Billiards.Audio.AudioManager.Instance != null)
            {
                Game.Billiards.Audio.AudioManager.Instance.SetSFXVolume(val);
            }
            else
            {
                PlayerPrefs.SetFloat("Volume_SFX", val);
            }
        }

        private void OnSurrenderClicked()
        {
            ShowConfirmDialog("confirm_surrender_title", "confirm_surrender_msg", () =>
            {
                var net = FindObjectOfType<Game.Billiards.Network.NetworkMatchManager>();
                if (net != null && net.IsOnlineMatch)
                {
                    // Online: gửi sự kiện đầu hàng cho đối thủ và ghi điểm đồng bộ trên cả hai máy
                    net.Surrender();
                }
                else if (matchStateManager != null)
                {
                    // Người đầu hàng là người chơi cục bộ (không phụ thuộc lượt hiện tại); đối thủ nhận điểm ván
                    int surrenderingIndex = GetLocalPlayerIndex();
                    int opponentIndex = (surrenderingIndex == 0) ? 1 : 0;
                    matchStateManager.RecordRackWin(opponentIndex);
                }
                Hide();
            });
        }

        /// <summary>Xác định chỉ số người chơi đang dùng máy này: online = Master/Client, offline = người chơi không phải AI.</summary>
        private int GetLocalPlayerIndex()
        {
            var net = FindObjectOfType<Game.Billiards.Network.NetworkMatchManager>();
            if (net != null && net.IsOnlineMatch)
            {
                return net.LocalPlayerIndex;
            }

            bool isOnline = (Game.Billiards.Core.GameSession.Instance != null &&
                             Game.Billiards.Core.GameSession.Instance.CurrentGameMode == Game.Billiards.Core.GameMode.OnlinePvP);
            if (isOnline)
            {
                if (net != null) return net.LocalPlayerIndex;
            }

            if (matchStateManager != null && matchStateManager.State != null)
            {
                var players = matchStateManager.State.players;
                if (players != null)
                {
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (!players[i].isAI) return i;
                    }
                }
            }
            return matchStateManager.CurrentPlayerIndex;
        }

        private void OnQuitClicked()
        {
            ShowConfirmDialog("confirm_quit_title", "confirm_quit_match_msg", () =>
            {
                Hide();
                if (onQuitAction != null)
                {
                    onQuitAction.Invoke();
                }
                else
                {
                    Game.Billiards.Core.GameSession.GetOrCreateInstance().LoadLobbyScene();
                }
            });
        }

        private bool isConfirmStyled = false;
        private Text confirmTitleText;
        private string currentConfirmTitleKey;
        private string currentConfirmMessageKey;

        private void Update()
        {
            if (confirmDialogRoot != null && confirmDialogRoot.activeSelf)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelConfirmDialog();
                }
            }
        }

        private void ShowConfirmDialog(string titleKey, string messageKey, Action onConfirm)
        {
            EnsureConfirmDialogStyled();
            currentConfirmTitleKey = titleKey;
            currentConfirmMessageKey = messageKey;
            pendingConfirmAction = onConfirm;

            if (confirmTitleText != null) confirmTitleText.text = LocalizationManager.Get(titleKey);
            if (confirmMessageText != null) confirmMessageText.text = LocalizationManager.Get(messageKey);
            RefreshLocalizedTexts();

            if (confirmDialogRoot != null)
            {
                confirmDialogRoot.transform.SetAsLastSibling();
                confirmDialogRoot.SetActive(true);
            }
        }

        private void ExecutePendingConfirm()
        {
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            pendingConfirmAction?.Invoke();
            pendingConfirmAction = null;
        }

        private void CancelConfirmDialog()
        {
            if (confirmDialogRoot != null) confirmDialogRoot.SetActive(false);
            pendingConfirmAction = null;
        }

        private void EnsureConfirmDialogStyled()
        {
            if (isConfirmStyled || confirmDialogRoot == null) return;
            if (confirmDialogRoot.transform.Find("Card") != null)
            {
                isConfirmStyled = true;
                return;
            }
            isConfirmStyled = true;

            Font font = null;
            if (confirmMessageText != null) font = confirmMessageText.font;
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. Chuyển confirmDialogRoot lên Canvas để che toàn màn hình chuẩn Modal
            if (modalRoot != null && confirmDialogRoot.transform.parent != modalRoot.transform.parent)
            {
                confirmDialogRoot.transform.SetParent(modalRoot.transform.parent, false);
            }

            // Nền mờ tối toàn màn hình
            Image bgImg = confirmDialogRoot.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.sprite = null;
                bgImg.type = Image.Type.Simple;
                bgImg.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);
                bgImg.raycastTarget = true;
            }

            RectTransform rootRt = confirmDialogRoot.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 2. Card nổi ở giữa theo phong cách HUD vát góc
            GameObject cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            cardGo.transform.SetParent(confirmDialogRoot.transform, false);
            RectTransform cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.sizeDelta = new Vector2(520f, 290f);
            cardRt.anchoredPosition = Vector2.zero;

            SkewedPanelGraphic cardShape = cardGo.GetComponent<SkewedPanelGraphic>();
            cardShape.SetInsets(0f, 16f, 16f, 0f);
            cardShape.color = new Color(0.07f, 0.11f, 0.20f, 0.98f);
            cardShape.raycastTarget = true;

            // Viền sáng cảnh báo
            GameObject frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.transform.SetParent(cardGo.transform, false);
            RectTransform frRt = frameGo.GetComponent<RectTransform>();
            frRt.anchorMin = Vector2.zero;
            frRt.anchorMax = Vector2.one;
            frRt.offsetMin = Vector2.zero;
            frRt.offsetMax = Vector2.zero;

            BorderProgressGraphic frame = frameGo.GetComponent<BorderProgressGraphic>();
            frame.SetInsets(0f, 16f, 16f, 0f);
            frame.SetBaseColor(new Color(0.95f, 0.35f, 0.35f, 0.85f));
            frame.raycastTarget = false;

            // Dải phát sáng vàng cam phía trên
            GameObject stripGo = new GameObject("TopStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            stripGo.transform.SetParent(cardGo.transform, false);
            RectTransform stripRt = stripGo.GetComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0f, 1f);
            stripRt.anchorMax = new Vector2(1f, 1f);
            stripRt.pivot = new Vector2(0.5f, 1f);
            stripRt.anchoredPosition = Vector2.zero;
            stripRt.sizeDelta = new Vector2(-32f, 3.5f);
            Image stripImg = stripGo.GetComponent<Image>();
            stripImg.color = new Color(1f, 0.78f, 0.25f, 1f);
            stripImg.raycastTarget = false;

            // Hiệu ứng mở popup PopIn
            if (cardGo.GetComponent<CanvasGroup>() == null) cardGo.AddComponent<CanvasGroup>();
            if (cardGo.GetComponent<ModalPopIn>() == null) cardGo.AddComponent<ModalPopIn>();

            // 3. Header: Huy hiệu cảnh báo [ ! ] + Tiêu đề XÁC NHẬN
            GameObject badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            badgeGo.transform.SetParent(cardGo.transform, false);
            RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0.5f, 1f);
            badgeRt.anchorMax = new Vector2(0.5f, 1f);
            badgeRt.pivot = new Vector2(0.5f, 1f);
            badgeRt.anchoredPosition = new Vector2(0f, -14f);
            badgeRt.sizeDelta = new Vector2(36f, 26f);

            SkewedPanelGraphic badgeShape = badgeGo.GetComponent<SkewedPanelGraphic>();
            badgeShape.SetInsets(0f, 6f, 6f, 0f);
            badgeShape.color = new Color(0.95f, 0.28f, 0.28f, 0.28f);
            badgeShape.raycastTarget = false;

            GameObject badgeBorder = new GameObject("BadgeBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            badgeBorder.transform.SetParent(badgeGo.transform, false);
            RectTransform bbRt = badgeBorder.GetComponent<RectTransform>();
            bbRt.anchorMin = Vector2.zero;
            bbRt.anchorMax = Vector2.one;
            bbRt.offsetMin = Vector2.zero;
            bbRt.offsetMax = Vector2.zero;
            BorderProgressGraphic bbg = badgeBorder.GetComponent<BorderProgressGraphic>();
            bbg.SetInsets(0f, 6f, 6f, 0f);
            bbg.SetBaseColor(new Color(1f, 0.50f, 0.40f, 0.85f));
            bbg.raycastTarget = false;

            GameObject badgeTxtGo = new GameObject("BadgeText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            badgeTxtGo.transform.SetParent(badgeGo.transform, false);
            RectTransform btRt = badgeTxtGo.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            Text bTxt = badgeTxtGo.GetComponent<Text>();
            bTxt.font = font;
            bTxt.text = "!";
            bTxt.fontSize = 17;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.color = new Color(1f, 0.82f, 0.25f, 1f);
            bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.raycastTarget = false;

            // Tiêu đề
            GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(cardGo.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -44f);
            titleRt.sizeDelta = new Vector2(360f, 28f);
            confirmTitleText = titleGo.GetComponent<Text>();
            confirmTitleText.font = font;
            confirmTitleText.text = LocalizationManager.Get("quit_title");
            confirmTitleText.fontSize = 20;
            confirmTitleText.fontStyle = FontStyle.Bold;
            confirmTitleText.color = Color.white;
            confirmTitleText.alignment = TextAnchor.MiddleCenter;
            confirmTitleText.raycastTarget = false;

            // Gạch chân tiêu đề
            GameObject underline = new GameObject("Underline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            underline.transform.SetParent(titleGo.transform, false);
            RectTransform unRt = underline.GetComponent<RectTransform>();
            unRt.anchorMin = new Vector2(0.5f, 0f);
            unRt.anchorMax = new Vector2(0.5f, 0f);
            unRt.pivot = new Vector2(0.5f, 1f);
            unRt.anchoredPosition = new Vector2(0f, -4f);
            unRt.sizeDelta = new Vector2(80f, 2.5f);
            Image unImg = underline.GetComponent<Image>();
            unImg.color = new Color(0.95f, 0.35f, 0.35f, 0.85f);
            unImg.raycastTarget = false;

            // 4. Nội dung thông báo (confirmMessageText) chuyển vào trong Card
            if (confirmMessageText != null)
            {
                confirmMessageText.transform.SetParent(cardGo.transform, false);
                RectTransform msgRt = confirmMessageText.rectTransform;
                msgRt.anchorMin = new Vector2(0.5f, 0.5f);
                msgRt.anchorMax = new Vector2(0.5f, 0.5f);
                msgRt.pivot = new Vector2(0.5f, 0.5f);
                msgRt.anchoredPosition = new Vector2(0f, 10f);
                msgRt.sizeDelta = new Vector2(460f, 75f);
                confirmMessageText.font = font;
                confirmMessageText.fontSize = 15;
                confirmMessageText.fontStyle = FontStyle.Bold;
                confirmMessageText.color = new Color(0.92f, 0.96f, 1f, 1f);
                confirmMessageText.alignment = TextAnchor.MiddleCenter;
                confirmMessageText.horizontalOverflow = HorizontalWrapMode.Wrap;
                confirmMessageText.verticalOverflow = VerticalWrapMode.Overflow;
                confirmMessageText.lineSpacing = 1.15f;
            }

            // 5. Style 2 nút CÓ và KHÔNG
            if (confirmYesButton != null)
            {
                confirmYesButton.transform.SetParent(cardGo.transform, false);
                StyleActionButton(confirmYesButton, new Vector2(-110f, -85f),
                    new Color(0.85f, 0.22f, 0.22f, 0.98f),
                    new Color(1f, 0.45f, 0.45f, 0.85f),
                    new Color(1f, 0.80f, 0.25f, 1f),
                    new Color(1f, 0.32f, 0.32f, 1f),
                    new Color(1f, 0.55f, 0.55f, 1f));
            }

            if (confirmNoButton != null)
            {
                confirmNoButton.transform.SetParent(cardGo.transform, false);
                StyleActionButton(confirmNoButton, new Vector2(110f, -85f),
                    new Color(0.12f, 0.20f, 0.34f, 0.98f),
                    new Color(0.30f, 0.78f, 0.95f, 0.85f),
                    new Color(0.30f, 0.78f, 0.95f, 1f),
                    new Color(0.20f, 0.40f, 0.65f, 1f),
                    new Color(0.40f, 0.85f, 1f, 1f));
            }
        }

        private static void StyleActionButton(Button b, Vector2 pos, Color bodyColor, Color frameColor, Color stripColor, Color hoverBody, Color hoverFrame)
        {
            RectTransform rt = b.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(175f, 44f);

            // Tắt transition mặc định của Button để không xung đột với màu nền và hiệu ứng hover
            b.transition = Selectable.Transition.None;

            // 1. Tắt hình ảnh của Image cũ nhưng giữ làm vùng nhận raycast
            Image oldImg = b.GetComponent<Image>();
            if (oldImg != null)
            {
                oldImg.sprite = null;
                oldImg.type = Image.Type.Simple;
                oldImg.color = new Color(0, 0, 0, 0);
                oldImg.raycastTarget = true;
            }

            // 2. Thêm con "Shape" bằng SkewedPanelGraphic (không add trực tiếp vào GameObject của Button để tránh xung đột Graphic)
            Transform existShape = b.transform.Find("Shape");
            SkewedPanelGraphic shape = existShape != null ? existShape.GetComponent<SkewedPanelGraphic>() : null;
            if (shape == null)
            {
                GameObject sg = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
                sg.transform.SetParent(b.transform, false);
                RectTransform sRt = sg.GetComponent<RectTransform>();
                sRt.anchorMin = Vector2.zero;
                sRt.anchorMax = Vector2.one;
                sRt.offsetMin = Vector2.zero;
                sRt.offsetMax = Vector2.zero;
                shape = sg.GetComponent<SkewedPanelGraphic>();
                shape.SetInsets(0f, 12f, 12f, 0f);
                shape.color = bodyColor;
                shape.raycastTarget = false;
                sg.transform.SetAsFirstSibling();
            }
            else
            {
                shape.color = bodyColor;
            }
            b.targetGraphic = shape;

            // Frame
            Transform existFr = b.transform.Find("Frame");
            BorderProgressGraphic frame = existFr != null ? existFr.GetComponent<BorderProgressGraphic>() : null;
            if (frame == null)
            {
                GameObject fg = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
                fg.transform.SetParent(b.transform, false);
                RectTransform frRt = fg.GetComponent<RectTransform>();
                frRt.anchorMin = Vector2.zero;
                frRt.anchorMax = Vector2.one;
                frRt.offsetMin = Vector2.zero;
                frRt.offsetMax = Vector2.zero;
                frame = fg.GetComponent<BorderProgressGraphic>();
                frame.SetInsets(0f, 12f, 12f, 0f);
                frame.SetBaseColor(frameColor);
                frame.raycastTarget = false;
            }
            else
            {
                frame.SetBaseColor(frameColor);
            }

            // Dải viền sáng chân nút
            Transform existStrip = b.transform.Find("BottomStrip");
            if (existStrip == null)
            {
                GameObject sg = new GameObject("BottomStrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                sg.transform.SetParent(b.transform, false);
                RectTransform sRt = sg.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0f);
                sRt.anchorMax = new Vector2(1f, 0f);
                sRt.pivot = new Vector2(0.5f, 0f);
                sRt.anchoredPosition = Vector2.zero;
                sRt.sizeDelta = new Vector2(-24f, 3f);
                Image sImg = sg.GetComponent<Image>();
                sImg.color = stripColor;
                sImg.raycastTarget = false;
            }

            // Text
            Text t = b.GetComponentInChildren<Text>(true);
            if (t != null)
            {
                t.fontStyle = FontStyle.Bold;
                t.fontSize = 16;
                t.color = Color.white;
                t.alignment = TextAnchor.MiddleCenter;
                t.raycastTarget = false;
                t.transform.SetAsLastSibling();
            }

            // Gắn UIHoverEffect mượt mà
            UIHoverEffect.Attach(b.gameObject, true, hoverBody, hoverFrame, 0f, 1.05f);
        }
    }
}
