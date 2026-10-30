using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.AI;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    public class LobbyUIController : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button playVsAIButton;
        [SerializeField] private Button playOnlineButton;
        [SerializeField] private Button rulesButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitGameButton;

        [Header("Offline Difficulty Modal")]
        [SerializeField] private GameObject difficultyModalRoot;
        [SerializeField] private Button diffEasyButton;
        [SerializeField] private Button diffNormalButton;
        [SerializeField] private Button diffHardButton;
        [SerializeField] private Image diffEasyHighlight;
        [SerializeField] private Image diffNormalHighlight;
        [SerializeField] private Image diffHardHighlight;
        [SerializeField] private Dropdown raceTargetDropdown;
        [SerializeField] private Dropdown shotTimerDropdown;
        [SerializeField] private InputField playerNameInput;
        [SerializeField] private Button startOfflineMatchButton;
        [SerializeField] private Button closeDifficultyModalButton;
        [Tooltip("Công tắc bật/tắt thước ngắm. Nếu để trống, sẽ tự tạo trong bảng cấu hình khi chạy.")]
        [SerializeField] private Toggle aimGuideToggle;

        [Header("Online 1v1 Modal")]
        [SerializeField] private GameObject onlineModalRoot;
        [SerializeField] private OnlineRoomUI onlineRoomUI;
        [SerializeField] private Button createRoomButton;
        [SerializeField] private InputField roomCodeInput;
        [SerializeField] private Button joinRoomButton;
        [SerializeField] private Button closeOnlineModalButton;
        [SerializeField] private Text onlineStatusText;

        [Header("Settings Modal")]
        [SerializeField] private GameObject settingsModalRoot;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Button closeSettingsModalButton;

        [Header("Rules Modal")]
        [SerializeField] private GameObject rulesModalRoot;
        [SerializeField] private Button closeRulesModalButton;

        [Header("Quit Confirm Modal")]
        [SerializeField] private GameObject quitConfirmModalRoot;
        [SerializeField] private Button quitConfirmYesButton;
        [SerializeField] private Button quitConfirmNoButton;

        private AIDifficulty selectedDifficulty = AIDifficulty.Normal;
        private readonly RaceTarget[] raceOptions = new[] { RaceTarget.RaceTo3, RaceTarget.RaceTo5, RaceTarget.RaceTo7, RaceTarget.RaceTo9 };
        private readonly ShotTimerOption[] timerOptions = new[] { ShotTimerOption.Unlimited, ShotTimerOption.Seconds30, ShotTimerOption.Seconds45, ShotTimerOption.Seconds60 };

        public AIDifficulty SelectedDifficulty => selectedDifficulty;

        private void Start()
        {
            Time.timeScale = 1f;
            HudModalState.Reset();
            CursorToggle.SetCursorToggled(false);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            EnsureLanguageDropdown();
            EnsureProfileSettings();
            SetupDropdowns();
            EnsureAimGuideToggle();
            StyleModals();
            SetupOnlineConfigPopup();
            if (onlineRoomUI != null) onlineRoomUI.SetupRoomView(roomConfigButton);
            StyleQuitButtons();
            SetupButtonListeners();
            ApplyHoverEffects();
            LoadAudioSettings();
            SelectDifficulty(AIDifficulty.Normal);
            HideAllModals();

            LocalizationManager.OnLanguageChanged -= RefreshAllLocalizedTexts;
            LocalizationManager.OnLanguageChanged += RefreshAllLocalizedTexts;
            RefreshAllLocalizedTexts();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshAllLocalizedTexts;
            PlayerProfile.OnChanged -= RefreshProfileViews;
        }

        private DifficultyCardUI[] difficultyCards;

        private void StyleModals()
        {
            difficultyCards = LobbyModalStyler.StyleDifficultyModal(difficultyModalRoot, diffEasyButton, diffNormalButton, diffHardButton);
            LobbyModalStyler.StyleModal(onlineModalRoot, LobbyModalStyler.AccentBlue);
            LobbyModalStyler.StyleModal(settingsModalRoot, LobbyModalStyler.Accent);
            LobbyModalStyler.StyleModal(rulesModalRoot, new Color(0.96f, 0.62f, 0.04f, 1f));
            LobbyModalStyler.StyleModal(quitConfirmModalRoot, LobbyModalStyler.AccentRed);
        }

        // ---- Hiệu ứng UI nâng cấp: nút Thoát đỏ + hover ----

        private static readonly Color QuitBase = new Color(0.62f, 0.13f, 0.15f, 0.96f);
        private static readonly Color QuitFrame = new Color(1f, 0.45f, 0.42f, 1f);
        private static readonly Color QuitHover = new Color(0.96f, 0.24f, 0.22f, 1f);
        private static readonly Color QuitHoverFrame = new Color(1f, 0.65f, 0.6f, 1f);

        private void StyleQuitButtons()
        {
            Button[] quitButtons = { quitGameButton, quitConfirmYesButton, quitConfirmNoButton };
            foreach (Button b in quitButtons)
            {
                if (b == null) continue;
                b.transition = Selectable.Transition.None;
                if (b == quitConfirmNoButton) continue;
                if (b.targetGraphic != null) b.targetGraphic.color = QuitBase;
                BorderProgressGraphic frame = b.GetComponentInChildren<BorderProgressGraphic>(true);
                if (frame != null) frame.SetBaseColor(QuitFrame);
            }
        }

        private void ApplyHoverEffects()
        {
            foreach (Button b in FindObjectsOfType<Button>(true))
            {
                if (b == null) continue;

                if (b == diffEasyButton || b == diffNormalButton || b == diffHardButton)
                {
                    continue; // thẻ độ khó tự xử lý hover/chọn (DifficultyCardUI)
                }

                if (b == quitGameButton || b == quitConfirmYesButton)
                {
                    UIHoverEffect.Attach(b.gameObject, true, QuitHover, QuitHoverFrame);
                }
                else if (b == quitConfirmNoButton)
                {
                    UIHoverEffect.Attach(b.gameObject, true, new Color(0.20f, 0.40f, 0.65f, 1f), new Color(0.40f, 0.85f, 1f, 1f));
                }
                else if (b.targetGraphic is SkewedPanelGraphic)
                {
                    // Nút kiểu HUD vát góc: phóng to, dịch phải, chuyển vàng cam
                    UIHoverEffect.Attach(b.gameObject, true);
                }
                else
                {
                    // Các nút khác (thẻ độ khó...): chỉ phóng to nhẹ để không xung đột màu chọn
                    UIHoverEffect.Attach(b.gameObject, false, null, null, 0f, 1.05f);
                }
            }

            if (aimGuideToggle != null)
            {
                UIHoverEffect.Attach(aimGuideToggle.gameObject, false, null, null, 6f, 1.03f);
            }
        }

        // ---- Công tắc thước ngắm ----

        private GameObject aimGuideCheckRoot;
        private Outline aimGuideOutline;
        private Image aimGuideBox;

        private static readonly Color TickGreen = new Color(0.20f, 0.90f, 0.42f, 1f);
        private static readonly Color BoxOff = new Color(0.10f, 0.15f, 0.24f, 1f);
        private static readonly Color BoxOn = new Color(0.07f, 0.22f, 0.17f, 1f);
        private static readonly Color OutlineOff = new Color(0.42f, 0.56f, 0.74f, 0.9f);

        private Toggle rackIntroToggle;
        private GameObject introCheckRoot;
        private Outline introOutline;
        private Image introBox;

        private GameObject onlineConfigPopup;
        private Text onlineConfigSummary;
        private Dropdown onlineRaceDropdown;
        private Dropdown onlineTimerDropdown;
        private Toggle popupIntroToggle;
        private Toggle popupAimToggle;
        private GameObject popupIntroCheck, popupAimCheck;
        private Outline popupIntroOutline, popupAimOutline;
        private Image popupIntroBox, popupAimBox;

        /// <summary>Đồng bộ các ô tick với PlayerPrefs (vì cùng một cài đặt xuất hiện ở 2 popup).</summary>
        private void SyncTickToggles()
        {
            bool aim = GameSession.AimGuideEnabled;
            bool intro = GameSession.RackIntroEnabled;
            if (aimGuideToggle != null) { aimGuideToggle.SetIsOnWithoutNotify(aim); RefreshTickVisual(aimGuideCheckRoot, aimGuideOutline, aimGuideBox, aim); }
            if (rackIntroToggle != null) { rackIntroToggle.SetIsOnWithoutNotify(intro); RefreshTickVisual(introCheckRoot, introOutline, introBox, intro); }
            if (popupAimToggle != null) { popupAimToggle.SetIsOnWithoutNotify(aim); RefreshTickVisual(popupAimCheck, popupAimOutline, popupAimBox, aim); }
            if (popupIntroToggle != null) { popupIntroToggle.SetIsOnWithoutNotify(intro); RefreshTickVisual(popupIntroCheck, popupIntroOutline, popupIntroBox, intro); }
        }

        private static string ShortCaption(Dropdown d)
        {
            if (d == null || d.captionText == null) return "";
            string s = d.captionText.text ?? "";
            int cut = s.IndexOfAny(new[] { '(', '/' });
            return (cut > 0 ? s.Substring(0, cut) : s).Trim();
        }

        private void RefreshOnlineConfigSummary()
        {
            if (onlineConfigSummary == null) return;
            onlineConfigSummary.text = LocalizationManager.Get("online_config_summary") + "  •  " + ShortCaption(onlineRaceDropdown) + "  •  " + ShortCaption(onlineTimerDropdown);
        }

        private static void ReplaceHudButtonIcon(Button b, string label, int fontSize)
        {
            foreach (Transform child in b.transform)
            {
                if (child.name == "Icon") child.gameObject.SetActive(false);
            }
            foreach (Text t in b.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == "Label")
                {
                    t.text = label;
                    t.fontSize = fontSize;
                    t.rectTransform.offsetMin = b.transform.Find("Glyph") != null ? new Vector2(44f, t.rectTransform.offsetMin.y) : Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Phòng online: thay 2 dropdown Race/Timer bằng nút "Cấu hình trận đấu" mở popup cùng phong cách popup cấu hình AI.
        /// Hai dropdown được chuyển vào popup nên OnlineRoomUI vẫn đọc đúng giá trị khi tạo phòng.
        /// </summary>
        private void SetupOnlineConfigPopup()
        {
            if (onlineModalRoot == null || startOfflineMatchButton == null) return;
            Transform lobbySub = onlineModalRoot.transform.Find("LobbySubPanel");
            if (lobbySub == null) return;
            onlineRaceDropdown = lobbySub.Find("OnlineRaceDrop")?.GetComponent<Dropdown>();
            onlineTimerDropdown = lobbySub.Find("OnlineTimerDrop")?.GetComponent<Dropdown>();
            Button source = onlineModalRoot.transform.Find("RoomSubPanel/BtnToggleReady")?.GetComponent<Button>();
            if (onlineRaceDropdown == null || onlineTimerDropdown == null || source == null) return;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // --- Khung popup (nền mờ + thẻ vát góc + viền) ---
            GameObject popup = new GameObject("OnlineConfigModal", typeof(RectTransform));
            popup.transform.SetParent(onlineModalRoot.transform, false);
            RectTransform pr = popup.GetComponent<RectTransform>();
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(580f, 440f);

            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(popup.transform, false);
            dim.GetComponent<RectTransform>().sizeDelta = new Vector2(6000f, 4000f);
            dim.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.78f);

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            card.transform.SetParent(popup.transform, false);
            RectTransform cr = card.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            SkewedPanelGraphic cardG = card.GetComponent<SkewedPanelGraphic>();
            cardG.color = new Color(0.06f, 0.10f, 0.18f, 0.99f);
            cardG.SetInsets(0f, 30f, 30f, 0f);
            cardG.raycastTarget = true;

            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frame.transform.SetParent(popup.transform, false);
            RectTransform fr = frame.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            BorderProgressGraphic frameG = frame.GetComponent<BorderProgressGraphic>();
            frameG.SetInsets(0f, 30f, 30f, 0f);
            frameG.SetBaseColor(new Color(0.42f, 0.56f, 0.74f, 1f));
            frameG.raycastTarget = false;

            // --- Chữ: tiêu đề + nhãn (tên đặt để LobbyModalStyler tự nhận diện) ---
            Text title = NewConfigText(popup.transform, "Title", LocalizationManager.Get("online_config_title"), font);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -25f);
            title.rectTransform.sizeDelta = new Vector2(0f, 40f);
            Text raceLabel = NewConfigText(popup.transform, "RaceLabel", LocalizationManager.Get("diff_race"), font);
            PlaceCfg(raceLabel.rectTransform, 118f, 24f);
            Text timerLabel = NewConfigText(popup.transform, "TimerLabel", LocalizationManager.Get("diff_timer"), font);
            PlaceCfg(timerLabel.rectTransform, 34f, 24f);

            // --- Hai tick cài đặt dùng chung ---
            popupIntroToggle = CreateTickToggle(popup.transform, "PopupIntroToggle", LocalizationManager.Get("diff_rack_intro"), GameSession.RackIntroEnabled,
                out popupIntroCheck, out popupIntroOutline, out popupIntroBox);
            popupIntroToggle.onValueChanged.AddListener(v =>
            {
                GameSession.RackIntroEnabled = v;
                SyncTickToggles();
            });
            popupAimToggle = CreateTickToggle(popup.transform, "PopupAimToggle", LocalizationManager.Get("diff_aim_guide"), GameSession.AimGuideEnabled,
                out popupAimCheck, out popupAimOutline, out popupAimBox);
            popupAimToggle.onValueChanged.AddListener(v =>
            {
                GameSession.AimGuideEnabled = v;
                SyncTickToggles();
            });
            PlaceCfg((RectTransform)popupIntroToggle.transform, -56f, 32f);
            PlaceCfg((RectTransform)popupAimToggle.transform, -92f, 32f);

            // --- Nút Xác nhận (nhân bản từ nút Bắt đầu của popup AI để giữ đúng phong cách xanh lá) ---
            Button confirm = Instantiate(startOfflineMatchButton, popup.transform);
            confirm.name = "BtnConfirmConfig";
            confirm.onClick.RemoveAllListeners();
            ReplaceHudButtonIcon(confirm, LocalizationManager.Get("btn_confirm"), 18);
            PlaceCfg((RectTransform)confirm.transform, -156f, 54f);

            // Style chung (tiêu đề, nhãn mục, hiệu ứng mở) khi chưa có dropdown
            LobbyModalStyler.StyleModal(popup, LobbyModalStyler.AccentBlue);

            // --- Chuyển 2 dropdown vào popup ---
            MoveDropdownInto(onlineRaceDropdown, popup.transform, 84f);
            MoveDropdownInto(onlineTimerDropdown, popup.transform, 0f);

            // --- Nút mở popup nằm trong màn hình PHÒNG (chỉ chủ phòng thấy) ---
            Transform roomSub = onlineModalRoot.transform.Find("RoomSubPanel");
            Button openBtn = Instantiate(source, roomSub);
            openBtn.name = "BtnOnlineConfig";
            openBtn.onClick.RemoveAllListeners();
            ReplaceHudButtonIcon(openBtn, LocalizationManager.Get("online_config_summary"), 16);
            roomConfigButton = openBtn;

            // Sắp lại các nút trong phòng để có chỗ cho nút cấu hình
            SetRoomRow(roomSub, "BtnToggleReady", -32f, 44f);
            SetRoomRow(roomSub, "BtnOnlineConfig", -84f, 44f);
            SetRoomRow(roomSub, "BtnStartMatch", -136f, 44f);
            SetRoomRow(roomSub, "BtnLeaveRoom", -188f, 42f);
            SetRoomRow(roomSub, "RoomStatusMsg", -234f, 25f);

            onlineConfigPopup = popup;
            popup.SetActive(false);
            popup.transform.SetAsLastSibling();

            openBtn.onClick.AddListener(() =>
            {
                // Đồng bộ dropdown theo thuộc tính phòng hiện tại trước khi mở
                if (NetworkManager.Instance != null && NetworkManager.Instance.IsInRoom)
                {
                    MatchSettings current = NetworkManager.Instance.ExtractSettingsFromRoomProperties();
                    int ri = Array.IndexOf(raceOptions, current.raceTarget);
                    int ti = Array.IndexOf(timerOptions, current.shotTimer);
                    if (ri >= 0) onlineRaceDropdown.SetValueWithoutNotify(ri);
                    if (ti >= 0) onlineTimerDropdown.SetValueWithoutNotify(ti);
                    onlineRaceDropdown.RefreshShownValue();
                    onlineTimerDropdown.RefreshShownValue();
                }
                SyncTickToggles();
                popup.SetActive(true);
                popup.transform.SetAsLastSibling();
            });
            confirm.onClick.AddListener(() =>
            {
                popup.SetActive(false);
                ApplyOnlineRoomSettings();
            });
        }

        private Button roomConfigButton;

        private static void SetRoomRow(Transform roomSub, string childName, float y, float height)
        {
            Transform t = roomSub != null ? roomSub.Find(childName) : null;
            if (t == null) return;
            RectTransform rt = (RectTransform)t;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>Chủ phòng áp dụng Race/Timer đã chọn trong popup lên phòng (khách nhận qua thuộc tính phòng).</summary>
        private void ApplyOnlineRoomSettings()
        {
            if (NetworkManager.Instance == null || !NetworkManager.Instance.IsInRoom || !NetworkManager.Instance.IsMaster) return;

            MatchSettings s = NetworkManager.Instance.ExtractSettingsFromRoomProperties();
            if (onlineRaceDropdown.value < raceOptions.Length) s.raceTarget = raceOptions[onlineRaceDropdown.value];
            if (onlineTimerDropdown.value < timerOptions.Length) s.shotTimer = timerOptions[onlineTimerDropdown.value];
            NetworkManager.Instance.UpdateRoomSettings(s);
        }

        private void Update()
        {
            // Nút cấu hình chỉ hiện cho chủ phòng khi đang ở trong phòng
            if (roomConfigButton != null && NetworkManager.Instance != null)
            {
                bool show = NetworkManager.Instance.IsInRoom && NetworkManager.Instance.IsMaster;
                if (roomConfigButton.gameObject.activeSelf != show) roomConfigButton.gameObject.SetActive(show);
            }
        }

        private static Text NewConfigText(Transform parent, string objName, string content, Font font)
        {
            GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = 14;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (objName == "Title") t.alignment = TextAnchor.MiddleCenter;
            return t;
        }

        private static void PlaceCfg(RectTransform rt, float y, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private static void MoveDropdownInto(Dropdown d, Transform parent, float y)
        {
            d.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)d.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(500f, 44f);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private void EnsureAimGuideToggle()
        {
            if (aimGuideToggle == null && difficultyModalRoot != null)
            {
                aimGuideToggle = CreateTickToggle(difficultyModalRoot.transform, "AimGuideToggle", LocalizationManager.Get("diff_aim_guide"),
                    GameSession.AimGuideEnabled, out aimGuideCheckRoot, out aimGuideOutline, out aimGuideBox);
            }

            if (aimGuideToggle != null)
            {
                aimGuideToggle.SetIsOnWithoutNotify(GameSession.AimGuideEnabled);
                RefreshTickVisual(aimGuideCheckRoot, aimGuideOutline, aimGuideBox, GameSession.AimGuideEnabled);
                aimGuideToggle.onValueChanged.AddListener(value =>
                {
                    GameSession.AimGuideEnabled = value;
                    RefreshTickVisual(aimGuideCheckRoot, aimGuideOutline, aimGuideBox, value);
                });
            }

            if (rackIntroToggle == null && difficultyModalRoot != null)
            {
                rackIntroToggle = CreateTickToggle(difficultyModalRoot.transform, "RackIntroToggle", LocalizationManager.Get("diff_rack_intro"),
                    GameSession.RackIntroEnabled, out introCheckRoot, out introOutline, out introBox);
                rackIntroToggle.onValueChanged.AddListener(value =>
                {
                    GameSession.RackIntroEnabled = value;
                    SyncTickToggles();
                });
                UIHoverEffect.Attach(rackIntroToggle.gameObject, false, null, null, 6f, 1.03f);
            }
        }

        private static void RefreshTickVisual(GameObject checkRoot, Outline outline, Image box, bool on)
        {
            if (checkRoot != null) checkRoot.SetActive(on);
            if (outline != null) outline.effectColor = on ? TickGreen : OutlineOff;
            if (box != null) box.color = on ? BoxOn : BoxOff;
        }

        private Image CreateStroke(Transform parent, string strokeName, Vector2 pos, Vector2 size, float angle)
        {
            GameObject go = new GameObject(strokeName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            r.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image img = go.GetComponent<Image>();
            img.color = TickGreen;
            img.raycastTarget = false;
            return img;
        }

        private Toggle CreateTickToggle(Transform parent, string objName, string labelString, bool initial, out GameObject checkRoot, out Outline outline, out Image box)
        {
            Text sampleText = parent.GetComponentInChildren<Text>(true);
            Font font = sampleText != null ? sampleText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject root = new GameObject(objName, typeof(RectTransform), typeof(Toggle));
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.5f);
            rootRt.anchorMax = new Vector2(0.5f, 0.5f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.anchoredPosition = new Vector2(0f, -122f);
            rootRt.sizeDelta = new Vector2(480f, 32f);

            // Ô vuông (nền tối + viền đổi màu theo trạng thái)
            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(Outline));
            bg.transform.SetParent(root.transform, false);
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.anchoredPosition = new Vector2(2f, 0f);
            bgRt.sizeDelta = new Vector2(28f, 28f);

            Image boxImg = bg.GetComponent<Image>();
            boxImg.color = initial ? BoxOn : BoxOff;
            Outline boxOutline = bg.GetComponent<Outline>();
            boxOutline.effectDistance = new Vector2(2f, -2f);
            boxOutline.effectColor = initial ? TickGreen : OutlineOff;
            box = boxImg;
            outline = boxOutline;

            // Dấu tick ✓ vẽ bằng 2 nét xoay: nét ngắn đi xuống, nét dài đi lên
            GameObject check = new GameObject("Checkmark", typeof(RectTransform));
            check.transform.SetParent(bg.transform, false);
            checkRoot = check;
            RectTransform ckRt = check.GetComponent<RectTransform>();
            ckRt.anchorMin = Vector2.zero;
            ckRt.anchorMax = Vector2.one;
            ckRt.offsetMin = Vector2.zero;
            ckRt.offsetMax = Vector2.zero;
            CreateStroke(check.transform, "ShortStroke", new Vector2(-5.5f, -1.5f), new Vector2(10f, 4.5f), -45f);
            CreateStroke(check.transform, "LongStroke", new Vector2(3f, 1.5f), new Vector2(19f, 4.5f), 52f);
            check.SetActive(initial);

            GameObject label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            RectTransform labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(42f, 0f);
            labelRt.offsetMax = Vector2.zero;
            Text labelText = label.GetComponent<Text>();
            labelText.font = font;
            labelText.fontSize = 15;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.85f, 0.90f, 0.97f, 1f);
            labelText.text = labelString;
            labelText.raycastTarget = true; // bấm vào chữ cũng bật/tắt được

            Toggle toggle = root.GetComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.targetGraphic = boxImg;
            toggle.graphic = null; // Dấu tick được ẩn/hiện bằng RefreshTickVisual
            toggle.SetIsOnWithoutNotify(initial);
            return toggle;
        }

        private Dropdown languageDropdown;
        private Text languageLabel;

        private void EnsureLanguageDropdown()
        {
            if (settingsModalRoot == null) return;

            // Mở rộng chiều cao SettingsModal từ 520 lên 600
            RectTransform modalRt = settingsModalRoot.GetComponent<RectTransform>();
            if (modalRt != null && modalRt.sizeDelta.y < 580f)
            {
                modalRt.sizeDelta = new Vector2(modalRt.sizeDelta.x, 600f);
            }

            // Dịch nút Đóng (BtnCloseSettings) xuống dưới cùng
            if (closeSettingsModalButton != null)
            {
                RectTransform closeRt = closeSettingsModalButton.GetComponent<RectTransform>();
                if (closeRt != null)
                {
                    closeRt.anchoredPosition = new Vector2(0f, -225f);
                }
            }

            // Kiểm tra hoặc tạo nhãn LblLanguage
            Transform existingLbl = settingsModalRoot.transform.Find("LblLanguage");
            if (existingLbl == null)
            {
                GameObject lblGo = new GameObject("LblLanguage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                lblGo.transform.SetParent(settingsModalRoot.transform, false);
                RectTransform rt = lblGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -112f);
                rt.sizeDelta = new Vector2(440f, 25f);

                Text txt = lblGo.GetComponent<Text>();
                Text sample = settingsModalRoot.GetComponentInChildren<Text>(true);
                txt.font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                txt.fontSize = 13;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleLeft;
                txt.color = new Color(0.58f, 0.72f, 0.92f, 1f);
                txt.text = LocalizationManager.Get("settings_language");
                languageLabel = txt;

                LobbyModalStyler.StyleSectionLabel(txt);
            }
            else
            {
                languageLabel = existingLbl.GetComponent<Text>();
            }

            // Kiểm tra hoặc tạo LanguageDropdown
            Transform existingDrop = settingsModalRoot.transform.Find("LanguageDropdown");
            if (existingDrop == null)
            {
                if (raceTargetDropdown != null)
                {
                    GameObject dropGo = Instantiate(raceTargetDropdown.gameObject, settingsModalRoot.transform);
                    dropGo.name = "LanguageDropdown";

                    RectTransform dropRt = dropGo.GetComponent<RectTransform>();
                    dropRt.anchorMin = dropRt.anchorMax = new Vector2(0.5f, 0.5f);
                    dropRt.pivot = new Vector2(0.5f, 0.5f);
                    dropRt.anchoredPosition = new Vector2(0f, -155f);
                    dropRt.sizeDelta = new Vector2(440f, 42f);

                    languageDropdown = dropGo.GetComponent<Dropdown>();
                    languageDropdown.ClearOptions();
                    languageDropdown.AddOptions(new List<string>(LocalizationManager.LanguageNames));
                    languageDropdown.SetValueWithoutNotify(LocalizationManager.CurrentLanguageIndex);
                    if (languageDropdown.captionText != null)
                    {
                        languageDropdown.captionText.text = LocalizationManager.LanguageNames[LocalizationManager.CurrentLanguageIndex];
                    }

                    languageDropdown.onValueChanged.RemoveAllListeners();
                    languageDropdown.onValueChanged.AddListener((int index) =>
                    {
                        LocalizationManager.SetLanguageByIndex(index);
                        if (languageDropdown.captionText != null)
                        {
                            languageDropdown.captionText.text = LocalizationManager.LanguageNames[index];
                        }
                        if (Audio.AudioManager.Instance != null)
                        {
                            Audio.AudioManager.Instance.PlayButtonClick();
                        }
                    });
                }
            }
            else
            {
                languageDropdown = existingDrop.GetComponent<Dropdown>();
            }
        }

        // ---- Hồ sơ người chơi (tên + avatar) trong Cài đặt ----

        private InputField profileNameInput;
        private Text profileNameLabel;
        private Image avatarPreview;
        private Button changeAvatarButton;
        private Button closePickerButton;
        private Text avatarPickerTitle;
        private GameObject avatarPicker;
        private Image[] avatarImages;
        private Image[] avatarRings;

        private static readonly Color AvatarRingColor = new Color(0.25f, 0.95f, 0.50f, 1f);

        private static void SetAnchoredY(Transform parent, string childName, float y)
        {
            Transform t = parent.Find(childName);
            if (t == null) return;
            RectTransform rt = (RectTransform)t;
            Vector2 p = rt.anchoredPosition;
            p.y = y;
            rt.anchoredPosition = p;
        }

        private static void PlaceCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private Text EnsureSettingsLabel(Transform root, string objName, float y, string locKey)
        {
            Transform existing = root.Find(objName);
            if (existing != null) return existing.GetComponent<Text>();

            GameObject go = new GameObject(objName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(root, false);
            PlaceCenter(go.GetComponent<RectTransform>(), new Vector2(0f, y), new Vector2(440f, 25f));

            Text txt = go.GetComponent<Text>();
            Text sample = root.GetComponentInChildren<Text>(true);
            txt.font = sample != null ? sample.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 13;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = new Color(0.58f, 0.72f, 0.92f, 1f);
            txt.text = LocalizationManager.Get(locKey);
            txt.raycastTarget = false;
            LobbyModalStyler.StyleSectionLabel(txt);
            return txt;
        }

        /// <summary>
        /// Thêm khu vực Hồ sơ vào modal Cài đặt: ô nhập tên (ngắn) + ảnh đại diện đang chọn + nút "ĐỔI ẢNH" mở popup chọn avatar.
        /// Đây là nơi duy nhất để đổi tên/avatar; các ô tên ở modal độ khó và sảnh online chỉ hiển thị.
        /// </summary>
        private void EnsureProfileSettings()
        {
            if (settingsModalRoot == null) return;
            Transform root = settingsModalRoot.transform;

            RectTransform modalRt = settingsModalRoot.GetComponent<RectTransform>();
            if (modalRt != null) modalRt.sizeDelta = new Vector2(modalRt.sizeDelta.x, 640f);

            // Sắp lại các mục cũ để nhường chỗ cho khu vực Hồ sơ ở trên cùng
            SetAnchoredY(root, "Title", 275f);
            SetAnchoredY(root, "LblMaster", 100f);
            SetAnchoredY(root, "SldMaster", 65f);
            SetAnchoredY(root, "LblMusic", 22f);
            SetAnchoredY(root, "SldMusic", -13f);
            SetAnchoredY(root, "LblSFX", -58f);
            SetAnchoredY(root, "SldSFX", -93f);
            SetAnchoredY(root, "LblLanguage", -138f);
            SetAnchoredY(root, "LanguageDropdown", -181f);
            SetAnchoredY(root, "BtnCloseSettings", -258f);

            profileNameLabel = EnsureSettingsLabel(root, "LblPlayerName", 215f, "settings_player_name");

            // Ô nhập tên (ngắn): nhân bản từ ô tên chưa được làm đẹp ở modal độ khó (được style cùng các popup sau đó)
            Transform existingInput = root.Find("ProfileNameInput");
            if (existingInput != null)
            {
                profileNameInput = existingInput.GetComponent<InputField>();
            }
            else if (playerNameInput != null)
            {
                GameObject go = Instantiate(playerNameInput.gameObject, root);
                go.name = "ProfileNameInput";
                PlaceCenter(go.GetComponent<RectTransform>(), new Vector2(-110f, 160f), new Vector2(220f, 44f));

                profileNameInput = go.GetComponent<InputField>();
                profileNameInput.interactable = true;
                profileNameInput.characterLimit = PlayerProfile.MaxNameLength;
                profileNameInput.onValueChanged.RemoveAllListeners();
                profileNameInput.onEndEdit.RemoveAllListeners();
                profileNameInput.onEndEdit.AddListener(OnProfileNameEdited);
                if (profileNameInput.placeholder is Text ph) ph.text = LocalizationManager.Get("settings_player_name_hint");
            }

            // Ảnh đại diện đang chọn (bấm vào cũng mở popup)
            if (root.Find("AvatarPreview") == null)
            {
                GameObject pv = new GameObject("AvatarPreview", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                pv.transform.SetParent(root, false);
                PlaceCenter(pv.GetComponent<RectTransform>(), new Vector2(66f, 160f), new Vector2(84f, 84f));
                avatarPreview = pv.GetComponent<Image>();
                avatarPreview.preserveAspect = true;
                avatarPreview.sprite = AvatarLibrary.Get(PlayerProfile.AvatarIndex);
                avatarPreview.raycastTarget = false; // chỉ hiển thị, không bấm được

                GameObject ringGo = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ringGo.transform.SetParent(pv.transform, false);
                RectTransform rr = ringGo.GetComponent<RectTransform>();
                rr.anchorMin = Vector2.zero;
                rr.anchorMax = Vector2.one;
                rr.offsetMin = new Vector2(-7f, -7f);
                rr.offsetMax = new Vector2(7f, 7f);
                Image ring = ringGo.GetComponent<Image>();
                ring.sprite = AvatarLibrary.RingSprite;
                ring.color = new Color(0.40f, 0.85f, 1f, 1f);
                ring.raycastTarget = false;

                Button pb = pv.GetComponent<Button>();
                pb.interactable = false;
                pb.transition = Selectable.Transition.None;
                pb.navigation = new Navigation { mode = Navigation.Mode.None };
            }
            else
            {
                avatarPreview = root.Find("AvatarPreview").GetComponent<Image>();
            }

            // Nút ĐỔI ẢNH (nhân bản từ nút Đóng để giữ đúng phong cách HUD)
            if (closeSettingsModalButton != null && root.Find("BtnChangeAvatar") == null)
            {
                changeAvatarButton = Instantiate(closeSettingsModalButton, root);
                changeAvatarButton.name = "BtnChangeAvatar";
                changeAvatarButton.onClick.RemoveAllListeners();
                PlaceCenter((RectTransform)changeAvatarButton.transform, new Vector2(170f, 160f), new Vector2(92f, 34f));
                ReplaceHudButtonIcon(changeAvatarButton, LocalizationManager.Get("settings_change_avatar"), 12);
                changeAvatarButton.onClick.AddListener(OpenAvatarPicker);
            }

            BuildAvatarPicker(root);

            PlayerProfile.OnChanged -= RefreshProfileViews;
            PlayerProfile.OnChanged += RefreshProfileViews;
            RefreshProfileViews();
        }

        /// <summary>Khung popup kiểu HUD (nền mờ + thẻ vát góc + viền) dùng chung cho các popup con.</summary>
        private static GameObject CreatePopupFrame(Transform parent, string objName, Vector2 size)
        {
            GameObject popup = new GameObject(objName, typeof(RectTransform));
            popup.transform.SetParent(parent, false);
            PlaceCenter(popup.GetComponent<RectTransform>(), Vector2.zero, size);

            GameObject dim = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dim.transform.SetParent(popup.transform, false);
            dim.GetComponent<RectTransform>().sizeDelta = new Vector2(6000f, 4000f);
            dim.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.78f);

            GameObject card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            card.transform.SetParent(popup.transform, false);
            RectTransform cr = card.GetComponent<RectTransform>();
            cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
            SkewedPanelGraphic cardG = card.GetComponent<SkewedPanelGraphic>();
            cardG.color = new Color(0.06f, 0.10f, 0.18f, 0.99f);
            cardG.SetInsets(0f, 30f, 30f, 0f);
            cardG.raycastTarget = true;

            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frame.transform.SetParent(popup.transform, false);
            RectTransform fr = frame.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = Vector2.zero; fr.offsetMax = Vector2.zero;
            BorderProgressGraphic frameG = frame.GetComponent<BorderProgressGraphic>();
            frameG.SetInsets(0f, 30f, 30f, 0f);
            frameG.SetBaseColor(new Color(0.42f, 0.56f, 0.74f, 1f));
            frameG.raycastTarget = false;
            return popup;
        }

        /// <summary>Popup chọn ảnh đại diện (lưới avatar 9 cột x 3 hàng) cùng phong cách các popup ở sảnh.</summary>
        private void BuildAvatarPicker(Transform settingsRoot)
        {
            if (settingsRoot.Find("AvatarPickerModal") != null)
            {
                avatarPicker = settingsRoot.Find("AvatarPickerModal").gameObject;
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject popup = CreatePopupFrame(settingsRoot, "AvatarPickerModal", new Vector2(720f, 460f));

            avatarPickerTitle = NewConfigText(popup.transform, "Title", LocalizationManager.Get("avatar_picker_title"), font);
            avatarPickerTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            avatarPickerTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            avatarPickerTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            avatarPickerTitle.rectTransform.anchoredPosition = new Vector2(0f, -25f);
            avatarPickerTitle.rectTransform.sizeDelta = new Vector2(0f, 40f);

            avatarImages = new Image[AvatarLibrary.Count];
            avatarRings = new Image[AvatarLibrary.Count];
            for (int i = 0; i < AvatarLibrary.Count; i++)
            {
                int row = i / 9;
                int col = i % 9;
                CreateAvatarButton(popup.transform, i, new Vector2((col - 4) * 74f, 65f - row * 84f));
            }

            if (closeSettingsModalButton != null)
            {
                closePickerButton = Instantiate(closeSettingsModalButton, popup.transform);
                closePickerButton.name = "BtnClosePicker";
                closePickerButton.onClick.RemoveAllListeners();
                PlaceCenter((RectTransform)closePickerButton.transform, new Vector2(0f, -178f), new Vector2(260f, 46f));
                ReplaceHudButtonIcon(closePickerButton, LocalizationManager.Get("settings_close"), 15);
                foreach (Transform ch in closePickerButton.transform)
                {
                    if (ch.name == "Icon" || ch.name == "Q" || ch.name == "Glyph") ch.gameObject.SetActive(false); // ẩn icon/phím tắt thừa lòi ra ngoài
                }
                closePickerButton.onClick.AddListener(CloseAvatarPicker);
            }

            LobbyModalStyler.StyleModal(popup, LobbyModalStyler.AccentBlue);
            avatarPicker = popup;
            popup.SetActive(false);
        }

        private void OpenAvatarPicker()
        {
            if (avatarPicker == null) return;
            RefreshProfileViews();
            avatarPicker.SetActive(true);
            avatarPicker.transform.SetAsLastSibling();
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayButtonClick();
        }

        private void CloseAvatarPicker()
        {
            if (avatarPicker != null) avatarPicker.SetActive(false);
        }

        private void CreateAvatarButton(Transform parent, int index, Vector2 pos)
        {
            GameObject go = new GameObject("Avatar_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            PlaceCenter(go.GetComponent<RectTransform>(), pos, new Vector2(60f, 60f));

            Image img = go.GetComponent<Image>();
            img.sprite = AvatarLibrary.Get(index);
            img.preserveAspect = true;
            avatarImages[index] = img;

            GameObject ringGo = new GameObject("Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringGo.transform.SetParent(go.transform, false);
            RectTransform rr = ringGo.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero;
            rr.anchorMax = Vector2.one;
            rr.offsetMin = new Vector2(-8f, -8f);
            rr.offsetMax = new Vector2(8f, 8f);
            Image ring = ringGo.GetComponent<Image>();
            ring.sprite = AvatarLibrary.RingSprite;
            ring.color = AvatarRingColor;
            ring.raycastTarget = false;
            avatarRings[index] = ring;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            int captured = index;
            btn.onClick.AddListener(() => OnAvatarPicked(captured));
        }

        private void OnProfileNameEdited(string value)
        {
            PlayerProfile.SetName(value);
            if (profileNameInput != null) profileNameInput.SetTextWithoutNotify(PlayerProfile.Name);
        }

        private void OnAvatarPicked(int index)
        {
            PlayerProfile.SetAvatar(index);
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayButtonClick();
        }

        /// <summary>Đồng bộ mọi nơi hiển thị tên/avatar (Cài đặt, popup chọn, modal độ khó) theo PlayerProfile.</summary>
        private void RefreshProfileViews()
        {
            string n = PlayerProfile.Name;
            if (profileNameInput != null && !profileNameInput.isFocused) profileNameInput.SetTextWithoutNotify(n);
            if (playerNameInput != null)
            {
                playerNameInput.SetTextWithoutNotify(n);
                playerNameInput.interactable = false; // chỉ hiển thị; đổi tên trong Cài đặt
            }

            int selected = AvatarLibrary.Wrap(PlayerProfile.AvatarIndex);
            if (avatarPreview != null) avatarPreview.sprite = AvatarLibrary.Get(selected);

            if (avatarImages == null || avatarRings == null) return;
            for (int i = 0; i < avatarImages.Length; i++)
            {
                if (avatarImages[i] == null) continue;
                bool on = i == selected;
                avatarImages[i].color = on ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
                if (avatarRings[i] != null) avatarRings[i].gameObject.SetActive(on);
            }
        }

        private void SetupDropdowns()
        {
            PopulateDropdownsLocalized();
            if (raceTargetDropdown != null) raceTargetDropdown.value = 1; // Mặc định Race to 5
            if (shotTimerDropdown != null) shotTimerDropdown.value = 1; // Mặc định 30 giây

            RefreshProfileViews();
        }

        private void PopulateDropdownsLocalized()
        {
            if (raceTargetDropdown != null)
            {
                int cur = raceTargetDropdown.value;
                raceTargetDropdown.ClearOptions();
                raceTargetDropdown.AddOptions(new List<string> {
                    LocalizationManager.Get("opt_race_3"),
                    LocalizationManager.Get("opt_race_5"),
                    LocalizationManager.Get("opt_race_7"),
                    LocalizationManager.Get("opt_race_9")
                });
                raceTargetDropdown.value = Mathf.Clamp(cur, 0, 3);
                raceTargetDropdown.RefreshShownValue();
            }

            if (shotTimerDropdown != null)
            {
                int cur = shotTimerDropdown.value;
                shotTimerDropdown.ClearOptions();
                shotTimerDropdown.AddOptions(new List<string> {
                    LocalizationManager.Get("opt_timer_unlimited"),
                    LocalizationManager.Get("opt_timer_30"),
                    LocalizationManager.Get("opt_timer_45"),
                    LocalizationManager.Get("opt_timer_60")
                });
                shotTimerDropdown.value = Mathf.Clamp(cur, 0, 3);
                shotTimerDropdown.RefreshShownValue();
            }
        }

        private void RefreshAllLocalizedTexts()
        {
            // Menu chính
            SetButtonText(playVsAIButton, LocalizationManager.Get("menu_play_ai"));
            SetButtonText(playOnlineButton, LocalizationManager.Get("menu_play_online"));
            SetButtonText(rulesButton, LocalizationManager.Get("menu_rules"));
            SetButtonText(settingsButton, LocalizationManager.Get("menu_settings"));
            SetButtonText(quitGameButton, LocalizationManager.Get("menu_quit"));

            // Settings Modal
            if (settingsModalRoot != null)
            {
                SetChildText(settingsModalRoot.transform, "Title", LocalizationManager.Get("settings_title"));
                SetChildText(settingsModalRoot.transform, "LblMaster", LocalizationManager.Get("settings_master"));
                SetChildText(settingsModalRoot.transform, "LblMusic", LocalizationManager.Get("settings_music"));
                SetChildText(settingsModalRoot.transform, "LblSFX", LocalizationManager.Get("settings_sfx"));
                if (languageLabel != null) languageLabel.text = LocalizationManager.Get("settings_language");
                if (profileNameLabel != null) profileNameLabel.text = LocalizationManager.Get("settings_player_name");
                if (changeAvatarButton != null) ReplaceHudButtonIcon(changeAvatarButton, LocalizationManager.Get("settings_change_avatar"), 13);
                if (avatarPickerTitle != null) avatarPickerTitle.text = LocalizationManager.Get("avatar_picker_title");
                if (closePickerButton != null) SetButtonText(closePickerButton, LocalizationManager.Get("settings_close"));
                if (profileNameInput != null && profileNameInput.placeholder is Text namePh) namePh.text = LocalizationManager.Get("settings_player_name_hint");
                SetButtonText(closeSettingsModalButton, LocalizationManager.Get("settings_close"));
                if (languageDropdown != null)
                {
                    languageDropdown.SetValueWithoutNotify(LocalizationManager.CurrentLanguageIndex);
                    if (languageDropdown.captionText != null)
                    {
                        languageDropdown.captionText.text = LocalizationManager.LanguageNames[LocalizationManager.CurrentLanguageIndex];
                    }
                }
            }

            // Difficulty Modal
            if (difficultyModalRoot != null)
            {
                SetChildText(difficultyModalRoot.transform, "Title", LocalizationManager.Get("diff_title"));
                SetButtonText(diffEasyButton, LocalizationManager.Get("diff_easy"));
                SetButtonText(diffNormalButton, LocalizationManager.Get("diff_normal"));
                SetButtonText(diffHardButton, LocalizationManager.Get("diff_hard"));
                SetChildText(difficultyModalRoot.transform, "LblRace", LocalizationManager.Get("diff_race"));
                SetChildText(difficultyModalRoot.transform, "LblTimer", LocalizationManager.Get("diff_timer"));
                SetChildText(difficultyModalRoot.transform, "LblPlayerName", LocalizationManager.Get("diff_player_name"));
                SetButtonText(startOfflineMatchButton, LocalizationManager.Get("diff_start"));
                SetButtonText(closeDifficultyModalButton, LocalizationManager.Get("settings_close"));

                Transform aimToggleTrans = difficultyModalRoot.transform.Find("AimGuideToggle");
                if (aimToggleTrans != null) SetChildText(aimToggleTrans, "Label", LocalizationManager.Get("diff_aim_guide"));

                Transform introToggleTrans = difficultyModalRoot.transform.Find("RackIntroToggle");
                if (introToggleTrans != null) SetChildText(introToggleTrans, "Label", LocalizationManager.Get("diff_rack_intro"));
            }

            // Quit Confirm Modal
            if (quitConfirmModalRoot != null)
            {
                SetChildText(quitConfirmModalRoot.transform, "Title", LocalizationManager.Get("quit_title"));
                SetChildText(quitConfirmModalRoot.transform, "Message", LocalizationManager.Get("quit_msg"));
                SetButtonText(quitConfirmYesButton, LocalizationManager.Get("btn_yes"));
                SetButtonText(quitConfirmNoButton, LocalizationManager.Get("btn_no"));
            }

            // Rules Modal
            if (rulesModalRoot != null)
            {
                SetChildText(rulesModalRoot.transform, "Title", LocalizationManager.Get("rules_title"));
                SetButtonText(closeRulesModalButton, LocalizationManager.Get("settings_close"));
            }

            // Cập nhật Difficulty Cards nếu đã tạo
            if (difficultyCards != null && difficultyCards.Length == 3)
            {
                if (difficultyCards[0] != null) difficultyCards[0].SetTitle(LocalizationManager.Get("diff_easy"));
                if (difficultyCards[1] != null) difficultyCards[1].SetTitle(LocalizationManager.Get("diff_normal"));
                if (difficultyCards[2] != null) difficultyCards[2].SetTitle(LocalizationManager.Get("diff_hard"));
            }

            PopulateDropdownsLocalized();
            RefreshOnlineConfigSummary();
            RefreshProfileViews();

            if (onlineConfigPopup != null)
            {
                SetChildText(onlineConfigPopup.transform, "Title", LocalizationManager.Get("online_config_title"));
                SetChildText(onlineConfigPopup.transform, "RaceLabel", LocalizationManager.Get("diff_race"));
                SetChildText(onlineConfigPopup.transform, "TimerLabel", LocalizationManager.Get("diff_timer"));
                Transform intro = onlineConfigPopup.transform.Find("PopupIntroToggle");
                if (intro != null) SetChildText(intro, "Label", LocalizationManager.Get("diff_rack_intro"));
                Transform aim = onlineConfigPopup.transform.Find("PopupAimToggle");
                if (aim != null) SetChildText(aim, "Label", LocalizationManager.Get("diff_aim_guide"));
                Button conf = onlineConfigPopup.transform.Find("BtnConfirmConfig")?.GetComponent<Button>();
                if (conf != null) ReplaceHudButtonIcon(conf, LocalizationManager.Get("btn_confirm"), 18);
            }
            if (roomConfigButton != null)
            {
                ReplaceHudButtonIcon(roomConfigButton, LocalizationManager.Get("online_config_summary"), 16);
            }
        }

        private static void SetButtonText(Button b, string val)
        {
            if (b == null || string.IsNullOrEmpty(val)) return;
            Transform lbl = b.transform.Find("Label");
            Text t = lbl != null ? lbl.GetComponent<Text>() : null;
            if (t == null)
            {
                foreach (var candidate in b.GetComponentsInChildren<Text>(true))
                {
                    if (candidate != null && candidate.gameObject.name != "Q")
                    {
                        t = candidate;
                        break;
                    }
                }
            }
            if (t == null) t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = val;
        }

        private static void SetChildText(Transform parent, string childName, string val)
        {
            if (parent == null || string.IsNullOrEmpty(childName) || string.IsNullOrEmpty(val)) return;
            Transform c = parent.Find(childName);
            if (c != null)
            {
                Text t = c.GetComponent<Text>();
                if (t != null) t.text = val;
            }
        }

        private void SetupButtonListeners()
        {
            // Main menu
            if (playVsAIButton != null) playVsAIButton.onClick.AddListener(ShowDifficultyModal);
            if (playOnlineButton != null) playOnlineButton.onClick.AddListener(ShowOnlineModal);
            if (settingsButton != null) settingsButton.onClick.AddListener(ShowSettingsModal);
            if (rulesButton != null) rulesButton.onClick.AddListener(ShowRulesModal);
            if (quitGameButton != null) quitGameButton.onClick.AddListener(ShowQuitModal);

            // Difficulty buttons
            if (diffEasyButton != null) diffEasyButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Easy));
            if (diffNormalButton != null) diffNormalButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Normal));
            if (diffHardButton != null) diffHardButton.onClick.AddListener(() => SelectDifficulty(AIDifficulty.Hard));
            if (startOfflineMatchButton != null) startOfflineMatchButton.onClick.AddListener(OnStartOfflineMatchClicked);
            if (closeDifficultyModalButton != null) closeDifficultyModalButton.onClick.AddListener(HideAllModals);

            // Online modal
            if (createRoomButton != null) createRoomButton.onClick.AddListener(OnCreateRoomClicked);
            if (joinRoomButton != null) joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
            if (closeOnlineModalButton != null) closeOnlineModalButton.onClick.AddListener(HideAllModals);

            // Settings modal
            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (closeSettingsModalButton != null) closeSettingsModalButton.onClick.AddListener(HideAllModals);

            // Rules modal
            if (closeRulesModalButton != null) closeRulesModalButton.onClick.AddListener(HideAllModals);

            // Quit modal
            if (quitConfirmYesButton != null) quitConfirmYesButton.onClick.AddListener(OnConfirmQuitClicked);
            if (quitConfirmNoButton != null) quitConfirmNoButton.onClick.AddListener(HideAllModals);
        }

        public void SelectDifficulty(AIDifficulty diff)
        {
            selectedDifficulty = diff;
            Debug.Log($"[LobbyUI] Người chơi chọn độ khó: {diff}");

            if (difficultyCards != null && difficultyCards.Length == 3)
            {
                if (difficultyCards[0] != null) difficultyCards[0].SetSelected(diff == AIDifficulty.Easy);
                if (difficultyCards[1] != null) difficultyCards[1].SetSelected(diff == AIDifficulty.Normal);
                if (difficultyCards[2] != null) difficultyCards[2].SetSelected(diff == AIDifficulty.Hard);
                return;
            }

            // Phương án dự phòng khi chưa dựng thẻ: đổi màu nền nút như cũ
            Color activeColor = new Color(0.2f, 0.8f, 0.4f, 1f);
            Color inactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            if (diffEasyHighlight != null) diffEasyHighlight.color = (diff == AIDifficulty.Easy) ? activeColor : inactiveColor;
            if (diffNormalHighlight != null) diffNormalHighlight.color = (diff == AIDifficulty.Normal) ? activeColor : inactiveColor;
            if (diffHardHighlight != null) diffHardHighlight.color = (diff == AIDifficulty.Hard) ? activeColor : inactiveColor;
        }

        public void ShowDifficultyModal()
        {
            HideAllModals();
            SyncTickToggles();
            RefreshProfileViews();
            if (difficultyModalRoot != null) difficultyModalRoot.SetActive(true);
        }

        public void ShowOnlineModal()
        {
            HideAllModals();
            RefreshOnlineConfigSummary();
            if (onlineRoomUI != null)
            {
                onlineRoomUI.OpenOnlinePanel();
            }
            else
            {
                if (onlineModalRoot != null) onlineModalRoot.SetActive(true);
                if (onlineStatusText != null) onlineStatusText.text = LocalizationManager.Get("online_status_ready");
            }
        }

        public void ShowSettingsModal()
        {
            HideAllModals();
            CloseAvatarPicker();
            RefreshProfileViews();
            if (settingsModalRoot != null) settingsModalRoot.SetActive(true);
        }

        public void ShowRulesModal()
        {
            HideAllModals();
            if (rulesModalRoot != null) rulesModalRoot.SetActive(true);
        }

        public void ShowQuitModal()
        {
            HideAllModals();
            if (quitConfirmModalRoot != null) quitConfirmModalRoot.SetActive(true);
        }

        public void HideAllModals()
        {
            if (onlineConfigPopup != null) onlineConfigPopup.SetActive(false);
            if (difficultyModalRoot != null) difficultyModalRoot.SetActive(false);
            if (onlineRoomUI != null) onlineRoomUI.CloseOnlinePanel();
            if (onlineModalRoot != null) onlineModalRoot.SetActive(false);
            if (settingsModalRoot != null) settingsModalRoot.SetActive(false);
            if (rulesModalRoot != null) rulesModalRoot.SetActive(false);
            if (quitConfirmModalRoot != null) quitConfirmModalRoot.SetActive(false);
        }

        private void OnStartOfflineMatchClicked()
        {
            RaceTarget race = RaceTarget.RaceTo5;
            if (raceTargetDropdown != null && raceTargetDropdown.value < raceOptions.Length)
            {
                race = raceOptions[raceTargetDropdown.value];
            }

            ShotTimerOption timer = ShotTimerOption.Seconds30;
            if (shotTimerDropdown != null && shotTimerDropdown.value < timerOptions.Length)
            {
                timer = timerOptions[shotTimerDropdown.value];
            }

            string name = PlayerProfile.Name;

            GameSession session = GameSession.GetOrCreateInstance();
            session.StartOfflineMatch(selectedDifficulty, race, timer, name);
        }

        private void OnCreateRoomClicked()
        {
            if (onlineStatusText != null)
            {
                onlineStatusText.text = LocalizationManager.Get("online_connecting");
            }
            Debug.Log("[LobbyUI] Tạo phòng Online (chuẩn bị kết nối)");
        }

        private void OnJoinRoomClicked()
        {
            string code = (roomCodeInput != null) ? roomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                if (onlineStatusText != null)
                {
                    onlineStatusText.text = $"<color=red>{LocalizationManager.Get("online_id_invalid")}</color>";
                }
                return;
            }

            if (onlineStatusText != null)
            {
                onlineStatusText.text = LocalizationManager.Format("online_searching_room", code);
            }
            Debug.Log($"[LobbyUI] Tham gia phòng {code}");
        }

        private void LoadAudioSettings()
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
            PlayerPrefs.SetFloat("Volume_Master", val);
            AudioListener.volume = val;
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.SetMasterVolume(val);
        }

        private void OnMusicVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_Music", val);
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.SetMusicVolume(val);
        }

        private void OnSfxVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_SFX", val);
            if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.SetSFXVolume(val);
        }

        private void OnConfirmQuitClicked()
        {
            GameSession session = GameSession.GetOrCreateInstance();
            session.QuitGame();
        }
    }
}
