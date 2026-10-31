using System;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Game.Billiards.AI;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    public class PlayerPanelUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text groupText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private GameObject turnHighlightObject;
        [SerializeField] private Graphic panelBackgroundImage;
        [SerializeField] private BallTrackerUI ballTracker;
        [SerializeField] private TurnTimerUI turnTimer;

        [Header("Colors")]
        [SerializeField] private Color activeTurnColor = new Color(0.22f, 0.40f, 0.62f, 0.98f);
        [SerializeField] private Color inactiveTurnColor = new Color(0.13f, 0.21f, 0.35f, 0.96f);

        // Cụm hiển thị trạng thái Mic & Loa trên thẻ người chơi (chế độ Online PvP)
        private RectTransform voiceClusterRt;
        private SkewedPanelGraphic micBadgeBg;
        private BorderProgressGraphic micBadgeBorder;
        private VoiceIconGraphic micIcon;
        private SkewedPanelGraphic speakerBadgeBg;
        private BorderProgressGraphic speakerBadgeBorder;
        private VoiceIconGraphic speakerIcon;
        private bool isLeftCard = true;
        private bool originalTextRectsCaptured;
        private Vector2 origNamePos;
        private Vector2 origNameSize;
        private Vector2 origGroupPos;
        private Vector2 origGroupSize;

        // Hiển thị Avatar thực tế từ AvatarLibrary + Vòng viền sáng (AvatarRing)
        private Image avatarRingImage;
        private int currentAvatarIndex = -1;

        public BallTrackerUI BallTracker => ballTracker;
        public RectTransform VoiceClusterRect => voiceClusterRt;
        public VoiceIconGraphic MicStatusIcon => micIcon;
        public VoiceIconGraphic SpeakerStatusIcon => speakerIcon;
        public int CurrentAvatarIndex => currentAvatarIndex;

        private void Awake()
        {
            DetectCardSide();
            CaptureOriginalTextRects();
            EnsureAvatarVisualsReady();
            RefreshResolvedAvatar();
            EnsureVoiceStatusClusterBuilt();
            RefreshVoiceStatusCluster();
        }

        private void OnEnable()
        {
            DetectCardSide();
            CaptureOriginalTextRects();
            EnsureAvatarVisualsReady();
            RefreshResolvedAvatar();
            EnsureVoiceStatusClusterBuilt();
            RefreshVoiceStatusCluster();
            PlayerProfile.OnChanged -= RefreshResolvedAvatar;
            PlayerProfile.OnChanged += RefreshResolvedAvatar;
        }

        private void OnDisable()
        {
            PlayerProfile.OnChanged -= RefreshResolvedAvatar;
        }

        private void Update()
        {
            RefreshResolvedAvatar();
            RefreshVoiceStatusCluster();
        }

        private void DetectCardSide()
        {
            if (gameObject.name.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isLeftCard = false;
                return;
            }
            if (gameObject.name.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isLeftCard = true;
                return;
            }

            var rt = transform as RectTransform;
            if (rt != null)
            {
                isLeftCard = rt.pivot.x >= 0.5f;
            }
        }

        private void CaptureOriginalTextRects()
        {
            if (originalTextRectsCaptured) return;
            if (playerNameText != null)
            {
                origNamePos = playerNameText.rectTransform.anchoredPosition;
                origNameSize = playerNameText.rectTransform.sizeDelta;
            }
            if (groupText != null)
            {
                origGroupPos = groupText.rectTransform.anchoredPosition;
                origGroupSize = groupText.rectTransform.sizeDelta;
            }
            originalTextRectsCaptured = true;
        }

        private static bool IsOnlineModeActive()
        {
            if (OnlineChatUI.ForceEnableForTest) return true;
            if (PhotonNetwork.InRoom) return true;
            var session = GameSession.Instance;
            if (session != null && session.CurrentGameMode == GameMode.OnlinePvP) return true;
            return false;
        }

        /// <summary>
        /// Tạo cụm 2 huy hiệu [Mic] và [Loa] trên thẻ người chơi trái & phải:
        /// - Hiển thị rõ người chơi đang bật hay tắt Mic/Loa.
        /// - Khi người chơi nói, icon Mic nhấp nháy lên xuống theo biên độ âm thanh thời gian thực.
        /// </summary>
        private void EnsureVoiceStatusClusterBuilt()
        {
            if (voiceClusterRt != null) return;

            Transform existing = transform.Find("VoiceStatusCluster");
            if (existing != null)
            {
                voiceClusterRt = existing as RectTransform;
                micIcon = existing.Find("MicBadge/Icon")?.GetComponent<VoiceIconGraphic>();
                speakerIcon = existing.Find("SpeakerBadge/Icon")?.GetComponent<VoiceIconGraphic>();
                micBadgeBg = existing.Find("MicBadge")?.GetComponent<SkewedPanelGraphic>();
                micBadgeBorder = existing.Find("MicBadge/Border")?.GetComponent<BorderProgressGraphic>();
                speakerBadgeBg = existing.Find("SpeakerBadge")?.GetComponent<SkewedPanelGraphic>();
                speakerBadgeBorder = existing.Find("SpeakerBadge/Border")?.GetComponent<BorderProgressGraphic>();
                return;
            }

            var clusterGo = new GameObject("VoiceStatusCluster", typeof(RectTransform));
            clusterGo.layer = gameObject.layer;
            clusterGo.transform.SetParent(transform, false);
            voiceClusterRt = clusterGo.GetComponent<RectTransform>();
            voiceClusterRt.anchorMin = new Vector2(0.5f, 0.5f);
            voiceClusterRt.anchorMax = new Vector2(0.5f, 0.5f);
            voiceClusterRt.pivot = new Vector2(0.5f, 0.5f);
            // Đặt đối xứng giữa vùng Tên/Nhóm bi và Đồng hồ đếm ngược, không đè lên bất kỳ phần tử nào
            voiceClusterRt.anchoredPosition = new Vector2(isLeftCard ? 78f : -78f, 18f);
            voiceClusterRt.sizeDelta = new Vector2(64f, 28f);

            BuildSingleVoiceBadge(
                voiceClusterRt,
                "MicBadge",
                new Vector2(-17f, 0f),
                VoiceIconGraphic.IconKind.Microphone,
                isLeftCard,
                () => OnlineVoiceChatManager.Ensure().ToggleMic(),
                out micBadgeBg,
                out micBadgeBorder,
                out micIcon);

            BuildSingleVoiceBadge(
                voiceClusterRt,
                "SpeakerBadge",
                new Vector2(17f, 0f),
                VoiceIconGraphic.IconKind.Speaker,
                isLeftCard,
                () => OnlineVoiceChatManager.Ensure().ToggleSpeaker(),
                out speakerBadgeBg,
                out speakerBadgeBorder,
                out speakerIcon);
        }

        private void BuildSingleVoiceBadge(
            RectTransform parent,
            string name,
            Vector2 pos,
            VoiceIconGraphic.IconKind kind,
            bool interactiveClick,
            Action onClickAction,
            out SkewedPanelGraphic bgGraphic,
            out BorderProgressGraphic borderGraphic,
            out VoiceIconGraphic iconGraphic)
        {
            const float w = 30f;
            const float h = 26f;
            const float cut = 5.5f;

            var badgeGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            badgeGo.layer = gameObject.layer;
            badgeGo.transform.SetParent(parent, false);
            var rt = badgeGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(w, h);

            bgGraphic = badgeGo.GetComponent<SkewedPanelGraphic>();
            bgGraphic.color = new Color(0.05f, 0.09f, 0.17f, 0.94f);
            bgGraphic.SetInsets(isLeftCard ? 0f : cut, isLeftCard ? cut : 0f, isLeftCard ? cut : 0f, isLeftCard ? 0f : cut);
            bgGraphic.SetBottomDarken(0.55f);
            bgGraphic.raycastTarget = interactiveClick;

            if (interactiveClick && onClickAction != null)
            {
                var btn = badgeGo.AddComponent<Button>();
                btn.targetGraphic = bgGraphic;
                var nav = btn.navigation;
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClickAction.Invoke());
            }

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            borderGo.layer = gameObject.layer;
            borderGo.transform.SetParent(rt, false);
            var brt = borderGo.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            borderGraphic = borderGo.GetComponent<BorderProgressGraphic>();
            borderGraphic.SetInsets(isLeftCard ? 0f : cut, isLeftCard ? cut : 0f, isLeftCard ? cut : 0f, isLeftCard ? 0f : cut);
            borderGraphic.SetBaseColor(new Color(0.30f, 0.78f, 0.95f, 0.65f));
            borderGraphic.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(VoiceIconGraphic));
            iconGo.layer = gameObject.layer;
            iconGo.transform.SetParent(rt, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(19f, 19f);

            iconGraphic = iconGo.GetComponent<VoiceIconGraphic>();
            iconGraphic.Kind = kind;
            iconGraphic.raycastTarget = false;
        }

        public void RefreshVoiceStatusCluster()
        {
            EnsureVoiceStatusClusterBuilt();
            if (voiceClusterRt == null) return;

            bool online = IsOnlineModeActive();
            if (voiceClusterRt.gameObject.activeSelf != online)
            {
                voiceClusterRt.gameObject.SetActive(online);
            }

            // Thu gọn nhẹ chiều rộng vùng chữ Tên/Nhóm khi hiện cụm icon Mic/Loa để không bao giờ đè lên chữ
            if (originalTextRectsCaptured)
            {
                if (online)
                {
                    const float trimmedW = 224f;
                    float trimmedX = isLeftCard ? -81f : 81f;
                    if (playerNameText != null)
                    {
                        playerNameText.rectTransform.anchoredPosition = new Vector2(trimmedX, origNamePos.y);
                        playerNameText.rectTransform.sizeDelta = new Vector2(trimmedW, origNameSize.y);
                    }
                    if (groupText != null)
                    {
                        groupText.rectTransform.anchoredPosition = new Vector2(trimmedX, origGroupPos.y);
                        groupText.rectTransform.sizeDelta = new Vector2(trimmedW, origGroupSize.y);
                    }
                }
                else
                {
                    if (playerNameText != null)
                    {
                        playerNameText.rectTransform.anchoredPosition = origNamePos;
                        playerNameText.rectTransform.sizeDelta = origNameSize;
                    }
                    if (groupText != null)
                    {
                        groupText.rectTransform.anchoredPosition = origGroupPos;
                        groupText.rectTransform.sizeDelta = origGroupSize;
                    }
                }
            }

            if (!online) return;

            var voice = OnlineVoiceChatManager.Instance != null ? OnlineVoiceChatManager.Instance : OnlineVoiceChatManager.Ensure();
            if (voice == null) return;

            // Thẻ trái luôn là người chơi tại máy này (Local), thẻ phải luôn là đối thủ (Remote) trong chế độ Online
            bool micOn = isLeftCard ? voice.IsMicEnabled : voice.RemoteMicEnabled;
            bool spkOn = isLeftCard ? voice.IsSpeakerEnabled : voice.RemoteSpeakerEnabled;
            float micLevel = isLeftCard ? voice.LocalVoiceLevel01 : voice.RemoteVoiceLevel01;
            float spkLevel = isLeftCard ? voice.RemoteVoiceLevel01 : voice.LocalVoiceLevel01;
            float phase = Time.unscaledTime;

            if (micIcon != null)
            {
                micIcon.SetState(!micOn, micLevel, phase);
            }
            if (speakerIcon != null)
            {
                speakerIcon.SetState(!spkOn, spkLevel, phase);
            }

            Color activeBorder = new Color(0.30f, 0.78f, 0.95f, 0.72f);
            Color speakingBorder = new Color(0.16f, 1.00f, 0.58f, 1.00f);
            Color mutedBorder = new Color(0.92f, 0.26f, 0.30f, 0.75f);

            bool isSpeaking = micOn && micLevel > 0.04f;
            if (micBadgeBorder != null)
            {
                micBadgeBorder.SetBaseColor(!micOn ? mutedBorder : (isSpeaking ? speakingBorder : activeBorder));
            }
            if (micBadgeBg != null)
            {
                micBadgeBg.color = !micOn
                    ? new Color(0.16f, 0.07f, 0.11f, 0.94f)
                    : (isSpeaking ? new Color(0.06f, 0.24f, 0.22f, 0.96f) : new Color(0.05f, 0.09f, 0.17f, 0.94f));
            }

            bool isHearing = spkOn && spkLevel > 0.04f;
            if (speakerBadgeBorder != null)
            {
                speakerBadgeBorder.SetBaseColor(!spkOn ? mutedBorder : (isHearing ? speakingBorder : activeBorder));
            }
            if (speakerBadgeBg != null)
            {
                speakerBadgeBg.color = !spkOn
                    ? new Color(0.16f, 0.07f, 0.11f, 0.94f)
                    : (isHearing ? new Color(0.06f, 0.22f, 0.26f, 0.96f) : new Color(0.05f, 0.09f, 0.17f, 0.94f));
            }

            // Vòng viền Avatar phát sáng xanh ngọc lục bảo khi người chơi đó đang nói
            if (avatarRingImage != null)
            {
                Color baseAccent = AvatarLibrary.AccentColor(currentAvatarIndex >= 0 ? currentAvatarIndex : 0);
                avatarRingImage.color = isSpeaking
                    ? Color.Lerp(baseAccent, speakingBorder, 0.78f)
                    : new Color(baseAccent.r, baseAccent.g, baseAccent.b, 0.92f);
            }
        }

        private void EnsureAvatarVisualsReady()
        {
            if (avatarImage == null)
            {
                Transform avT = transform.Find("Avatar");
                if (avT != null) avatarImage = avT.GetComponent<Image>();
            }
            if (avatarImage == null) return;

            // Đảm bảo màu nền Image là trắng tinh để hiển thị nguyên vẹn màu sắc thật của Sprite AvatarLibrary
            avatarImage.color = Color.white;
            avatarImage.preserveAspect = true;

            if (avatarRingImage == null)
            {
                Transform existingRing = avatarImage.transform.Find("AvatarRing");
                if (existingRing != null)
                {
                    avatarRingImage = existingRing.GetComponent<Image>();
                }
                else
                {
                    var ringGo = new GameObject("AvatarRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    ringGo.layer = gameObject.layer;
                    ringGo.transform.SetParent(avatarImage.transform, false);
                    var rrt = ringGo.GetComponent<RectTransform>();
                    rrt.anchorMin = new Vector2(0.5f, 0.5f);
                    rrt.anchorMax = new Vector2(0.5f, 0.5f);
                    rrt.pivot = new Vector2(0.5f, 0.5f);
                    rrt.anchoredPosition = Vector2.zero;
                    Vector2 baseSize = avatarImage.rectTransform.sizeDelta;
                    if (baseSize.x < 10f) baseSize = new Vector2(58f, 58f);
                    rrt.sizeDelta = baseSize + new Vector2(6f, 6f);

                    avatarRingImage = ringGo.GetComponent<Image>();
                    avatarRingImage.sprite = AvatarLibrary.RingSprite;
                    avatarRingImage.type = Image.Type.Simple;
                    avatarRingImage.raycastTarget = false;
                }
            }
        }

        /// <summary>
        /// Tự động xác định và gán đúng Avatar từ AvatarLibrary cho cơ thủ ở thẻ Trái (Bạn) và thẻ Phải (Đối thủ / AI).
        /// </summary>
        public void RefreshResolvedAvatar()
        {
            EnsureAvatarVisualsReady();
            if (avatarImage == null) return;

            int resolvedIdx = ResolveTargetAvatarIndex();
            if (resolvedIdx != currentAvatarIndex || avatarImage.sprite == null || avatarImage.color != Color.white)
            {
                SetAvatarIndex(resolvedIdx);
            }
        }

        private int ResolveTargetAvatarIndex()
        {
            int localAv = AvatarLibrary.Wrap(PlayerProfile.AvatarIndex);

            // Thẻ bên trái luôn là người chơi tại máy này
            if (isLeftCard)
            {
                return localAv;
            }

            // Thẻ bên phải: Đối thủ Online hoặc AI
            if (IsOnlineModeActive())
            {
                // 1. Đọc từ Photon PlayerList CustomProperties["av"] của đối thủ
                if (PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null)
                {
                    foreach (var p in PhotonNetwork.PlayerList)
                    {
                        if (p == null || p.IsLocal) continue;
                        if (p.CustomProperties != null &&
                            p.CustomProperties.TryGetValue(NetworkManager.PLAYER_PROP_AVATAR, out object val) &&
                            val is int netAv)
                        {
                            return AvatarLibrary.Wrap(netAv);
                        }
                    }
                }

                // 2. Đọc từ gói đồng bộ trạng thái Voice/Avatar thời gian thực
                if (OnlineVoiceChatManager.Instance != null && OnlineVoiceChatManager.Instance.RemoteAvatarIndex >= 0)
                {
                    return AvatarLibrary.Wrap(OnlineVoiceChatManager.Instance.RemoteAvatarIndex);
                }

                // 3. Dự phòng khác với avatar cục bộ
                return AvatarLibrary.Wrap(localAv + 1);
            }

            // Chế độ Đấu với AI: chọn avatar đặc trưng theo độ khó của AI (không trùng với avatar người chơi)
            int aiAv = 5; // Mặc định: Cáo (Fox)
            var session = GameSession.Instance;
            if (session != null)
            {
                switch (session.SelectedAIDifficulty)
                {
                    case AIDifficulty.Easy: aiAv = 4; break;   // Mèo (Cat)
                    case AIDifficulty.Normal: aiAv = 5; break; // Cáo (Fox)
                    case AIDifficulty.Hard: aiAv = 9; break;   // Sư tử (Lion)
                }
            }
            if (aiAv == localAv)
            {
                aiAv = AvatarLibrary.Wrap(aiAv + 2);
            }
            return aiAv;
        }

        public void SetAvatarIndex(int avatarIndex)
        {
            EnsureAvatarVisualsReady();
            currentAvatarIndex = AvatarLibrary.Wrap(avatarIndex);

            if (avatarImage != null)
            {
                avatarImage.sprite = AvatarLibrary.Get(currentAvatarIndex);
                avatarImage.color = Color.white;
            }

            if (avatarRingImage != null)
            {
                Color accent = AvatarLibrary.AccentColor(currentAvatarIndex);
                avatarRingImage.color = new Color(accent.r, accent.g, accent.b, 0.92f);
            }
        }

        public void SetTimer(float remaining, float total)
        {
            if (turnTimer != null) turnTimer.SetTime(remaining, total);
        }

        public void ClearTimer()
        {
            if (turnTimer != null) turnTimer.Hide();
        }

        public void SetPlayerInfo(string playerName, Sprite avatar = null)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }

            EnsureAvatarVisualsReady();
            if (avatarImage != null)
            {
                if (avatar != null)
                {
                    avatarImage.sprite = avatar;
                    avatarImage.color = Color.white;
                }
                else
                {
                    RefreshResolvedAvatar();
                }
            }
        }

        public void SetGroup(PlayerGroup group)
        {
            if (groupText != null)
            {
                switch (group)
                {
                    case PlayerGroup.Open:
                        groupText.text = LocalizationManager.Get("group_open");
                        groupText.color = Color.white;
                        break;
                    case PlayerGroup.Solids:
                        groupText.text = LocalizationManager.Get("group_solids");
                        groupText.color = new Color(1f, 0.8f, 0.2f, 1f);
                        break;
                    case PlayerGroup.Stripes:
                        groupText.text = LocalizationManager.Get("group_stripes");
                        groupText.color = new Color(0.3f, 0.7f, 1f, 1f);
                        break;
                    case PlayerGroup.EightBall:
                        groupText.text = LocalizationManager.Get("group_eight_ball");
                        groupText.color = new Color(1f, 0.84f, 0f, 1f);
                        break;
                }
            }

            if (ballTracker != null)
            {
                ballTracker.SetGroup(group);
            }
        }

        public void SetTurnActive(bool isActive)
        {
            if (turnHighlightObject != null)
            {
                turnHighlightObject.SetActive(isActive);
            }

            if (panelBackgroundImage != null)
            {
                panelBackgroundImage.color = isActive ? activeTurnColor : inactiveTurnColor;
            }
        }

        public void SetScore(int rackWins)
        {
            if (scoreText != null)
            {
                scoreText.text = rackWins.ToString();
            }
        }
    }
}
