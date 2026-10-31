using System;
using System.Collections.Generic;
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

        // Điều khiển Âm lượng & Bật/Tắt Mic / Loa trong phần Cài đặt trận đấu
        private bool isVoiceSettingsBuilt;
        private MusicTrackCarouselUI musicTrackCarousel;
        private Text micVolumeLabel;
        private Slider micVolumeSlider;
        private Button micToggleButton;
        private SkewedPanelGraphic micToggleBg;
        private BorderProgressGraphic micToggleFrame;
        private VoiceIconGraphic micToggleIcon;
        private Text micToggleLabel;

        private Text speakerVolumeLabel;
        private Slider speakerVolumeSlider;
        private Button speakerToggleButton;
        private SkewedPanelGraphic speakerToggleBg;
        private BorderProgressGraphic speakerToggleFrame;
        private VoiceIconGraphic speakerToggleIcon;
        private Text speakerToggleLabel;

        public Slider MicVolumeSlider => micVolumeSlider;
        public Slider SpeakerVolumeSlider => speakerVolumeSlider;
        public Button MicToggleButton => micToggleButton;
        public Button SpeakerToggleButton => speakerToggleButton;

        private void Awake()
        {
            EnsureVoiceSettingsBuilt();
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
            EnsureVoiceSettingsBuilt();
            LoadSavedSettings();
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
            if (micVolumeLabel != null) micVolumeLabel.text = LocalizationManager.Get("settings_mic_vol", "ÂM LƯỢNG MIC");
            if (speakerVolumeLabel != null) speakerVolumeLabel.text = LocalizationManager.Get("settings_speaker_vol", "ÂM LƯỢNG LOA");
            RefreshMusicTrackDropdownOptions();
            RefreshVoiceSettingsVisuals();

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

            RefreshMusicTrackDropdownOptions();

            var voice = Game.Billiards.Network.OnlineVoiceChatManager.Ensure();
            if (voice != null)
            {
                if (micVolumeSlider != null) micVolumeSlider.SetValueWithoutNotify(voice.MicVolume);
                if (speakerVolumeSlider != null) speakerVolumeSlider.SetValueWithoutNotify(voice.SpeakerVolume);
            }
            RefreshVoiceSettingsVisuals();
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
            if (modalRoot != null && modalRoot.activeSelf)
            {
                RefreshVoiceSettingsVisuals();
            }

            if (confirmDialogRoot != null && confirmDialogRoot.activeSelf)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelConfirmDialog();
                }
            }
        }

        /// <summary>
        /// Dựng thêm 2 cụm điều khiển [Âm lượng Mic + Nút Bật/Tắt Mic] và [Âm lượng Loa + Nút Bật/Tắt Loa]
        /// bên trong bảng Cài đặt trận đấu (SettingsModal), đồng thời căn chỉnh lại bố cục gọn gàng, liền mạch.
        /// </summary>
        private void EnsureVoiceSettingsBuilt()
        {
            if (isVoiceSettingsBuilt || modalRoot == null || sfxVolumeSlider == null) return;
            isVoiceSettingsBuilt = true;

            RectTransform modalRt = modalRoot.GetComponent<RectTransform>();
            if (modalRt != null)
            {
                modalRt.sizeDelta = new Vector2(580f, 660f);
            }

            Transform accentTop = modalRoot.transform.Find("AccentTop");
            if (accentTop != null)
            {
                var atRt = accentTop.GetComponent<RectTransform>();
                if (atRt != null) atRt.anchoredPosition = new Vector2(0f, (660f - 4f) * 0.5f);
            }

            Transform titleT = modalRoot.transform.Find("Title");
            if (titleT != null)
            {
                titleT.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 276f);
            }

            Transform lblMasterT = modalRoot.transform.Find("LblMaster");
            if (lblMasterT != null) lblMasterT.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 232f);
            if (masterVolumeSlider != null) masterVolumeSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 206f);

            Transform lblMusicT = modalRoot.transform.Find("LblMusic");
            if (lblMusicT != null) lblMusicT.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 174f);
            if (musicVolumeSlider != null)
            {
                var musicRt = musicVolumeSlider.GetComponent<RectTransform>();
                musicRt.anchoredPosition = new Vector2(-82f, 148f);
                musicRt.sizeDelta = new Vector2(296f, 25f);
            }

            Transform lblSfxT = modalRoot.transform.Find("LblSFX");
            if (lblSfxT != null) lblSfxT.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 116f);
            if (sfxVolumeSlider != null) sfxVolumeSlider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 90f);

            Font font = lblSfxT != null && lblSfxT.GetComponent<Text>() != null
                ? lblSfxT.GetComponent<Text>().font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color lblCol = lblSfxT != null && lblSfxT.GetComponent<Text>() != null
                ? lblSfxT.GetComponent<Text>().color
                : new Color(0.68f, 0.78f, 0.90f, 1f);

            // Bộ chọn Nhạc nền 1 / Nhạc nền 2 (< Nhạc nền 1 >) nằm kế bên thanh âm lượng nhạc
            musicTrackCarousel = MusicTrackCarouselUI.Create(modalRoot.transform, "MusicTrackDropdown", font, new Vector2(154f, 150f), new Vector2(152f, 34f));

            // 1. Dòng Âm lượng Mic + Nút Bật/Tắt Mic [M]
            micVolumeLabel = CreateSettingsLabel("LblMic", LocalizationManager.Get("settings_mic_vol", "ÂM LƯỢNG MIC"), font, lblCol, new Vector2(-66f, 58f), new Vector2(328f, 22f));
            micVolumeSlider = CloneSettingsSlider("SldMic", new Vector2(-66f, 32f), new Vector2(328f, 25f));
            micVolumeSlider.onValueChanged.AddListener(val =>
            {
                Game.Billiards.Network.OnlineVoiceChatManager.Ensure().SetMicVolume(val);
            });

            micToggleButton = CreateVoiceToggleButton(
                "BtnToggleMic",
                new Vector2(170f, 38f),
                new Vector2(120f, 34f),
                VoiceIconGraphic.IconKind.Microphone,
                font,
                out micToggleBg,
                out micToggleFrame,
                out micToggleIcon,
                out micToggleLabel);
            micToggleButton.onClick.AddListener(() =>
            {
                Game.Billiards.Network.OnlineVoiceChatManager.Ensure().ToggleMic();
                RefreshVoiceSettingsVisuals();
            });

            // 2. Dòng Âm lượng Loa + Nút Bật/Tắt Loa [N]
            speakerVolumeLabel = CreateSettingsLabel("LblSpeaker", LocalizationManager.Get("settings_speaker_vol", "ÂM LƯỢNG LOA"), font, lblCol, new Vector2(-66f, 0f), new Vector2(328f, 22f));
            speakerVolumeSlider = CloneSettingsSlider("SldSpeaker", new Vector2(-66f, -26f), new Vector2(328f, 25f));
            speakerVolumeSlider.onValueChanged.AddListener(val =>
            {
                Game.Billiards.Network.OnlineVoiceChatManager.Ensure().SetSpeakerVolume(val);
            });

            speakerToggleButton = CreateVoiceToggleButton(
                "BtnToggleSpeaker",
                new Vector2(170f, -20f),
                new Vector2(120f, 34f),
                VoiceIconGraphic.IconKind.Speaker,
                font,
                out speakerToggleBg,
                out speakerToggleFrame,
                out speakerToggleIcon,
                out speakerToggleLabel);
            speakerToggleButton.onClick.AddListener(() =>
            {
                Game.Billiards.Network.OnlineVoiceChatManager.Ensure().ToggleSpeaker();
                RefreshVoiceSettingsVisuals();
            });

            // 3. Căn chỉnh lại 3 nút hành động phía dưới
            if (resumeButton != null) resumeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -92f);
            if (surrenderButton != null) surrenderButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -148f);
            if (quitMatchButton != null) quitMatchButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -204f);

            RefreshVoiceSettingsVisuals();
        }

        private Text CreateSettingsLabel(string name, string text, Font font, Color color, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = modalRoot.layer;
            go.transform.SetParent(modalRoot.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = 14;
            t.fontStyle = FontStyle.Normal;
            t.color = color;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
            return t;
        }

        private Slider CloneSettingsSlider(string name, Vector2 pos, Vector2 size)
        {
            GameObject clone = Instantiate(sfxVolumeSlider.gameObject, modalRoot.transform, false);
            clone.name = name;
            var rt = clone.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var sld = clone.GetComponent<Slider>();
            sld.onValueChanged = new Slider.SliderEvent();
            sld.minValue = 0f;
            sld.maxValue = 1f;
            return sld;
        }

        private void RefreshMusicTrackDropdownOptions()
        {
            if (musicTrackCarousel == null) return;
            musicTrackCarousel.RefreshLocalization();
            musicTrackCarousel.SyncFromSettings(false);
        }

        private Button CreateVoiceToggleButton(
            string name,
            Vector2 pos,
            Vector2 size,
            VoiceIconGraphic.IconKind kind,
            Font font,
            out SkewedPanelGraphic bgGraphic,
            out BorderProgressGraphic frameGraphic,
            out VoiceIconGraphic iconGraphic,
            out Text labelText)
        {
            const float cut = 9.5f;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(Button));
            go.layer = modalRoot.layer;
            go.transform.SetParent(modalRoot.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            bgGraphic = go.GetComponent<SkewedPanelGraphic>();
            bgGraphic.color = new Color(0.11f, 0.19f, 0.32f, 0.96f);
            bgGraphic.SetInsets(0f, cut, cut, 0f);
            bgGraphic.SetBottomDarken(0.48f);
            bgGraphic.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bgGraphic;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.transition = Selectable.Transition.None;

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.layer = modalRoot.layer;
            frameGo.transform.SetParent(rt, false);
            var frt = frameGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            frameGraphic = frameGo.GetComponent<BorderProgressGraphic>();
            frameGraphic.SetInsets(0f, cut, cut, 0f);
            frameGraphic.SetBaseColor(new Color(0.30f, 0.78f, 0.95f, 0.75f));
            frameGraphic.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(VoiceIconGraphic));
            iconGo.layer = modalRoot.layer;
            iconGo.transform.SetParent(rt, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0f, 0.5f);
            irt.anchorMax = new Vector2(0f, 0.5f);
            irt.pivot = new Vector2(0f, 0.5f);
            irt.anchoredPosition = new Vector2(14f, 0f);
            irt.sizeDelta = new Vector2(21f, 21f);

            iconGraphic = iconGo.GetComponent<VoiceIconGraphic>();
            iconGraphic.Kind = kind;
            iconGraphic.raycastTarget = false;

            var txtGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtGo.layer = modalRoot.layer;
            txtGo.transform.SetParent(rt, false);
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(38f, 2f);
            trt.offsetMax = new Vector2(-8f, -2f);

            labelText = txtGo.GetComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 12;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.supportRichText = true;
            labelText.raycastTarget = false;

            return btn;
        }

        private void RefreshVoiceSettingsVisuals()
        {
            var voice = Game.Billiards.Network.OnlineVoiceChatManager.Instance != null
                ? Game.Billiards.Network.OnlineVoiceChatManager.Instance
                : Game.Billiards.Network.OnlineVoiceChatManager.Ensure();
            if (voice == null) return;

            bool micOn = voice.IsMicEnabled;
            bool spkOn = voice.IsSpeakerEnabled;
            float phase = Time.unscaledTime;

            if (micVolumeSlider != null && !Mathf.Approximately(micVolumeSlider.value, voice.MicVolume))
            {
                micVolumeSlider.SetValueWithoutNotify(voice.MicVolume);
            }
            if (speakerVolumeSlider != null && !Mathf.Approximately(speakerVolumeSlider.value, voice.SpeakerVolume))
            {
                speakerVolumeSlider.SetValueWithoutNotify(voice.SpeakerVolume);
            }

            if (micToggleIcon != null)
            {
                micToggleIcon.SetState(!micOn, voice.LocalVoiceLevel01, phase);
            }
            if (speakerToggleIcon != null)
            {
                speakerToggleIcon.SetState(!spkOn, voice.RemoteVoiceLevel01, phase);
            }

            Color onBorder = new Color(0.18f, 0.90f, 0.65f, 0.92f);
            Color offBorder = new Color(0.95f, 0.30f, 0.34f, 0.85f);

            if (micToggleFrame != null) micToggleFrame.SetBaseColor(micOn ? onBorder : offBorder);
            if (micToggleBg != null)
            {
                micToggleBg.color = micOn
                    ? new Color(0.08f, 0.24f, 0.28f, 0.96f)
                    : new Color(0.22f, 0.09f, 0.14f, 0.96f);
            }
            if (micToggleLabel != null)
            {
                micToggleLabel.text = micOn
                    ? $"<color=#64FFDA>{LocalizationManager.Get("voice_on_short", "BẬT")} [M]</color>"
                    : $"<color=#FF6E6E>{LocalizationManager.Get("voice_off_short", "TẮT")} [M]</color>";
            }

            if (speakerToggleFrame != null) speakerToggleFrame.SetBaseColor(spkOn ? onBorder : offBorder);
            if (speakerToggleBg != null)
            {
                speakerToggleBg.color = spkOn
                    ? new Color(0.08f, 0.24f, 0.28f, 0.96f)
                    : new Color(0.22f, 0.09f, 0.14f, 0.96f);
            }
            if (speakerToggleLabel != null)
            {
                speakerToggleLabel.text = spkOn
                    ? $"<color=#64FFDA>{LocalizationManager.Get("voice_on_short", "BẬT")} [N]</color>"
                    : $"<color=#FF6E6E>{LocalizationManager.Get("voice_off_short", "TẮT")} [N]</color>";
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
