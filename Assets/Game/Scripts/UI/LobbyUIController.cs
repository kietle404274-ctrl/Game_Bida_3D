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
            SetupDropdowns();
            EnsureAimGuideToggle();
            StyleModals();
            SetupOnlineConfigPopup();
            StyleQuitButtons();
            SetupButtonListeners();
            ApplyHoverEffects();
            LoadAudioSettings();
            SelectDifficulty(AIDifficulty.Normal);
            HideAllModals();
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
            Button[] quitButtons = { quitGameButton, quitConfirmYesButton };
            foreach (Button b in quitButtons)
            {
                if (b == null) continue;
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
            onlineConfigSummary.text = "CẤU HÌNH TRẬN ĐẤU  •  " + ShortCaption(onlineRaceDropdown) + "  •  " + ShortCaption(onlineTimerDropdown);
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
                    t.rectTransform.offsetMin = Vector2.zero;
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
            const float cut = 36f;

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
            Text title = NewConfigText(popup.transform, "Title", "CẤU HÌNH TRẬN ĐẤU", font);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -25f);
            title.rectTransform.sizeDelta = new Vector2(0f, 40f);
            Text raceLabel = NewConfigText(popup.transform, "RaceLabel", "SỐ VÁN THẮNG MỤC TIÊU", font);
            PlaceCfg(raceLabel.rectTransform, 118f, 24f);
            Text timerLabel = NewConfigText(popup.transform, "TimerLabel", "THỜI GIAN LƯỢT BẮN", font);
            PlaceCfg(timerLabel.rectTransform, 34f, 24f);

            // --- Hai tick cài đặt dùng chung ---
            popupIntroToggle = CreateTickToggle(popup.transform, "PopupIntroToggle", "Hiển thị intro xếp bi đầu trận", GameSession.RackIntroEnabled,
                out popupIntroCheck, out popupIntroOutline, out popupIntroBox);
            popupIntroToggle.onValueChanged.AddListener(v =>
            {
                GameSession.RackIntroEnabled = v;
                SyncTickToggles();
            });
            popupAimToggle = CreateTickToggle(popup.transform, "PopupAimToggle", "Bật thước ngắm (đường ngắm & điểm va chạm)", GameSession.AimGuideEnabled,
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
            ReplaceHudButtonIcon(confirm, "XÁC NHẬN", 18);
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
            ReplaceHudButtonIcon(openBtn, "CẤU HÌNH TRẬN ĐẤU", 16);
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
                aimGuideToggle = CreateTickToggle(difficultyModalRoot.transform, "AimGuideToggle", "Bật thước ngắm (đường ngắm & điểm va chạm)",
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
                rackIntroToggle = CreateTickToggle(difficultyModalRoot.transform, "RackIntroToggle", "Hiển thị intro xếp bi đầu trận",
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

        private void SetupDropdowns()
        {
            if (raceTargetDropdown != null)
            {
                raceTargetDropdown.ClearOptions();
                List<string> options = new List<string> { "Race to 3 (Chạm 3)", "Race to 5 (Chạm 5)", "Race to 7 (Chạm 7)", "Race to 9 (Chạm 9)" };
                raceTargetDropdown.AddOptions(options);
                raceTargetDropdown.value = 1; // Mặc định Race to 5
            }

            if (shotTimerDropdown != null)
            {
                shotTimerDropdown.ClearOptions();
                List<string> options = new List<string> { "Không giới hạn", "30 Giây / Cú đánh", "45 Giây / Cú đánh", "60 Giây / Cú đánh" };
                shotTimerDropdown.AddOptions(options);
                shotTimerDropdown.value = 1; // Mặc định 30 giây
            }

            if (playerNameInput != null && string.IsNullOrEmpty(playerNameInput.text))
            {
                playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Cơ Thủ");
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
                if (onlineStatusText != null) onlineStatusText.text = "Sẵn sàng tạo phòng hoặc tham gia bằng Room ID";
            }
        }

        public void ShowSettingsModal()
        {
            HideAllModals();
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

            string name = (playerNameInput != null && !string.IsNullOrEmpty(playerNameInput.text)) ? playerNameInput.text : "Cơ Thủ";
            PlayerPrefs.SetString("PlayerName", name);

            GameSession session = GameSession.GetOrCreateInstance();
            session.StartOfflineMatch(selectedDifficulty, race, timer, name);
        }

        private void OnCreateRoomClicked()
        {
            if (onlineStatusText != null)
            {
                onlineStatusText.text = "Đang kết nối tới máy chủ Photon PUN 2... (Milestone M8)";
            }
            Debug.Log("[LobbyUI] Tạo phòng Online (chuẩn bị kết nối ở Milestone M8)");
        }

        private void OnJoinRoomClicked()
        {
            string code = (roomCodeInput != null) ? roomCodeInput.text.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                if (onlineStatusText != null)
                {
                    onlineStatusText.text = "<color=red>Vui lòng nhập ID phòng hợp lệ!</color>";
                }
                return;
            }

            if (onlineStatusText != null)
            {
                onlineStatusText.text = $"Đang tìm phòng {code}... (Milestone M8)";
            }
            Debug.Log($"[LobbyUI] Tham gia phòng {code} (chuẩn bị kết nối ở Milestone M8)");
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
        }

        private void OnMusicVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_Music", val);
        }

        private void OnSfxVolumeChanged(float val)
        {
            PlayerPrefs.SetFloat("Volume_SFX", val);
        }

        private void OnConfirmQuitClicked()
        {
            GameSession session = GameSession.GetOrCreateInstance();
            session.QuitGame();
        }
    }
}
