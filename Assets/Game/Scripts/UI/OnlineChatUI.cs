using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Giao diện Trò chuyện thời gian thực (Realtime Chat) dành cho chế độ Đấu Trực Tuyến (Online PvP).
    /// - Đặt cố định tại góc dưới-trái (x=30, y=390), ngay bên trên Thanh Lực (PowerMeterPanel).
    /// - Khi thu nhỏ (Collapsed): là một khối Hình Bình Hành nghiêng sắc nét đồng bộ với Thanh Lực & HUD.
    /// - Khi phóng lớn (Expanded): mở rộng thành Hình Bình Hành lớn bên ngoài, bên trong chứa khung Hình Chữ Nhật
    ///   đóng vai trò màn hình chat (gồm vùng cuộn tin nhắn + thanh nhập liệu + nút GỬI liền mạch).
    /// - Nhấn phím [0] để thu/phóng; click ra ngoài KHÔNG làm mất con trỏ chuột; click vào ô nhập luôn hiện dấu nháy (caret).
    /// </summary>
    public class OnlineChatUI : MonoBehaviour
    {
        // Bảng màu đồng bộ với PowerBarUI & CueElevationUI trên Gameplay HUD
        private static readonly Color OuterPanelColor = new Color(0.07f, 0.12f, 0.21f, 0.95f);
        private static readonly Color InnerBoxColor = new Color(0.03f, 0.06f, 0.11f, 0.94f);
        private static readonly Color InputBgColor = new Color(0.02f, 0.04f, 0.08f, 0.98f);
        private static readonly Color SendBtnColor = new Color(0.11f, 0.48f, 0.74f, 1f);
        private static readonly Color Gold = new Color(0.96f, 0.78f, 0.26f, 1f);
        private static readonly Color Cyan = new Color(0.30f, 0.78f, 0.95f, 1f);
        private static readonly Color CyanBright = new Color(0.00f, 0.92f, 1.00f, 1f);
        private static readonly Color OuterBorderBase = new Color(0.30f, 0.78f, 0.95f, 0.65f);
        private static readonly Color OuterBorderActive = new Color(0.18f, 0.90f, 1.00f, 0.96f);
        private static readonly Color InnerBorderColor = new Color(0.28f, 0.78f, 0.96f, 0.52f);

        // Kích thước Hình Bình Hành khi Thu nhỏ (Collapsed) & Phóng lớn (Expanded)
        private const float CollapsedW = 240f;
        private const float CollapsedH = 42f;
        private const float CollapsedCut = 12f;

        private const float ExpandedW = 356f;
        private const float ExpandedH = 244f;
        private const float ExpandedCut = 20f;

        private const float HeaderH = 36f;
        private const float InputBarH = 34f;
        private const float SendBtnW = 66f;
        private const float OuterBorderThickness = 2.2f;
        private const float TopAccentHeight = 3.2f;
        private const float InnerRectBorderThickness = 1.5f;

        // Tọa độ gốc cố định ngay trên đỉnh PowerMeterPanel (x=30, y=30, h=350 -> đỉnh 380 + cách 10px = 390)
        private const float DefaultAnchorX = 30f;
        private const float DefaultAnchorY = 390f;
        private const int MaxHistoryCount = 40;

        public static OnlineChatUI Instance { get; private set; }

        /// <summary>Cờ hỗ trợ kiểm thử tự động trong Editor khi không mở phòng Photon thật.</summary>
        public static bool ForceEnableForTest { get; set; }

        private static int lastTypingFrame = -10;

        /// <summary>True khi khung chat đang hiển thị trong chế độ Online và ở trạng thái mở rộng.</summary>
        public static bool IsChatOpen =>
            Instance != null && Instance.isActiveAndEnabled && Instance.isVisibleOnline && Instance.isExpanded;

        /// <summary>
        /// True khi người chơi đang gõ chữ trong ô InputField hoặc vừa thao tác phím đóng/gửi trong cùng frame.
        /// Dùng để khóa toàn bộ phím tắt gameplay (V, J, K, R, H, Tab, WASD, Esc, 0...).
        /// </summary>
        public static bool IsTyping =>
            (Instance != null && Instance.isActiveAndEnabled && Instance.isVisibleOnline && Instance.inputField != null && Instance.inputField.isFocused)
            || Time.frameCount <= lastTypingFrame + 1;

        /// <summary>
        /// Vẽ khung viền Hình Bình Hành khép kín liền mạch tuyệt đối (exact miter joints tại 4 góc)
        /// kèm dải nhấn vàng (AccentTop) nằm khớp hoàn toàn bên trong viền trên.
        /// Không bị hở góc, không bị lệch điểm giữa như BorderProgressGraphic.
        /// </summary>
        private sealed class ParallelogramFrameGraphic : MaskableGraphic
        {
            private float cut = 12f;
            private float borderThickness = 2.2f;
            private float accentHeight = 3.2f;
            private Color borderColor = OuterBorderBase;
            private Color accentColor = Gold;

            public void Configure(float cutInset, float thickness, float topStripHeight, Color borderCol, Color topStripCol)
            {
                cut = cutInset;
                borderThickness = thickness;
                accentHeight = topStripHeight;
                borderColor = borderCol;
                accentColor = topStripCol;
                SetVerticesDirty();
            }

            public void SetCut(float cutInset)
            {
                if (Mathf.Approximately(cut, cutInset)) return;
                cut = cutInset;
                SetVerticesDirty();
            }

            public void SetBorderColor(Color c)
            {
                borderColor = c;
                SetVerticesDirty();
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = GetPixelAdjustedRect();
                if (r.width <= 4f || r.height <= 4f) return;

                float c = Mathf.Clamp(cut, 0f, r.width * 0.45f);
                float t = Mathf.Clamp(borderThickness, 1f, Mathf.Min(r.width, r.height) * 0.25f);
                float h = r.height;

                // Độ dịch ngang vuông góc với cạnh nghiêng để độ dày viền 2 bên đều nhau hoàn hảo
                float slope = c / Mathf.Max(1f, h);
                float dxPerp = t * Mathf.Sqrt(1f + slope * slope);

                float LeftOuterX(float y) => r.xMin + c * (1f - (y - r.yMin) / h);
                float RightOuterX(float y) => r.xMax - c * ((y - r.yMin) / h);
                float LeftInnerX(float y) => LeftOuterX(y) + dxPerp;
                float RightInnerX(float y) => RightOuterX(y) - dxPerp;

                float yMin = r.yMin;
                float yMax = r.yMax;
                float yBotIn = yMin + t;
                float yTopIn = yMax - t;

                // 4 đỉnh ngoài của hình bình hành (BL, TL, TR, BR)
                Vector2 v0 = new Vector2(LeftOuterX(yMin), yMin);
                Vector2 v1 = new Vector2(LeftOuterX(yMax), yMax);
                Vector2 v2 = new Vector2(RightOuterX(yMax), yMax);
                Vector2 v3 = new Vector2(RightOuterX(yMin), yMin);

                // 4 đỉnh trong của hình bình hành (exact miter joints)
                Vector2 u0 = new Vector2(LeftInnerX(yBotIn), yBotIn);
                Vector2 u1 = new Vector2(LeftInnerX(yTopIn), yTopIn);
                Vector2 u2 = new Vector2(RightInnerX(yTopIn), yTopIn);
                Vector2 u3 = new Vector2(RightInnerX(yBotIn), yBotIn);

                // 1. Vẽ dải nhấn vàng (AccentTop) nằm sát mặt trong cạnh trên [yTopIn - accentHeight .. yTopIn]
                if (accentHeight > 0.1f && yTopIn - accentHeight > yBotIn)
                {
                    float yStripBot = yTopIn - accentHeight;
                    Vector2 s0 = new Vector2(LeftInnerX(yStripBot), yStripBot);
                    Vector2 s1 = u1;
                    Vector2 s2 = u2;
                    Vector2 s3 = new Vector2(RightInnerX(yStripBot), yStripBot);
                    AddQuad(vh, s0, s1, s2, s3, accentColor);
                }

                // 2. Vẽ 4 cạnh viền khép kín chia sẻ đúng các đỉnh góc (0 vết đứt gãy)
                AddQuad(vh, v1, v2, u2, u1, borderColor); // Cạnh trên
                AddQuad(vh, v2, v3, u3, u2, borderColor); // Cạnh phải nghiêng
                AddQuad(vh, v3, v0, u0, u3, borderColor); // Cạnh dưới
                AddQuad(vh, v0, v1, u1, u0, borderColor); // Cạnh trái nghiêng
            }

            private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
            {
                int start = vh.currentVertCount;
                UIVertex v = UIVertex.simpleVert;
                v.color = col;
                v.position = a; vh.AddVert(v);
                v.position = b; vh.AddVert(v);
                v.position = c; vh.AddVert(v);
                v.position = d; vh.AddVert(v);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }

        /// <summary>
        /// Bộ bắt sự kiện click chuột trực tiếp trên vùng nhập chat để luôn khôi phục con trỏ nhập liệu (caret)
        /// ngay cả khi người chơi vừa click chuột ra ngoài bàn bida hoặc vùng khác trên màn hình.
        /// </summary>
        private sealed class ChatInputFocusHelper : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
        {
            public OnlineChatUI Owner;

            public void OnPointerDown(PointerEventData eventData)
            {
                if (Owner != null) Owner.RequestFocusInputField();
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (Owner != null) Owner.RequestFocusInputField();
            }
        }

        private struct ChatEntry
        {
            public int senderIndex;
            public string senderName;
            public string message;
        }

        private readonly List<ChatEntry> entries = new List<ChatEntry>();
        private readonly List<Text> messageRows = new List<Text>();

        private GameplayHUD parentHud;
        private NetworkMatchManager networkMatchManager;
        private Font font;

        private RectTransform rootRt;
        private CanvasGroup rootCanvasGroup;
        private SkewedPanelGraphic outerParallelogramBg;
        private ParallelogramFrameGraphic outerParallelogramFrame;

        // Thành phần Header nằm ở phần đầu của Hình Bình Hành
        private RectTransform headerRt;
        private Button headerToggleBtn;
        private RectTransform badgeRt;
        private Text titleText;
        private Text toggleArrowText;
        private GameObject unreadBadgeObj;
        private Text unreadBadgeText;

        // Thành phần Khung Hình Chữ Nhật bên trong Hình Bình Hành khi Mở rộng (Expanded)
        private RectTransform innerRectBoxRt;
        private CanvasGroup expandedGroup;
        private ScrollRect scrollRect;
        private RectTransform contentRt;
        private RectTransform inputRowRt;
        private RectTransform inputBoxRt;
        private RectTransform sendBtnRt;
        private InputField inputField;
        private Text placeholderText;
        private Button sendButton;
        private Text sendButtonText;

        // Thanh điều khiển Mic [M] & Loa [N] nằm ngay phía trên giao diện Chat
        private RectTransform voiceBarRt;
        private Button micToggleBtn;
        private SkewedPanelGraphic micBtnBg;
        private ParallelogramFrameGraphic micBtnFrame;
        private VoiceIconGraphic micIconGraphic;
        private Text micLabelText;

        private Button speakerToggleBtn;
        private SkewedPanelGraphic speakerBtnBg;
        private ParallelogramFrameGraphic speakerBtnFrame;
        private VoiceIconGraphic speakerIconGraphic;
        private Text speakerLabelText;

        private bool isVisibleOnline;
        private bool isExpanded;
        private int unreadCount;
        private float unreadPulseTimer;
        private Coroutine transitionRoutine;
        private Coroutine focusRoutine;
        private string submittedOnEnterText;

        public bool IsExpandedState => isExpanded;
        public bool IsVisibleOnline => isVisibleOnline;
        public int MessageCount => entries.Count;
        public RectTransform PanelRect => rootRt;
        public RectTransform VoiceBarRect => voiceBarRt;
        public Button MicButton => micToggleBtn;
        public Button SpeakerButton => speakerToggleBtn;
        public InputField ChatInput => inputField;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            ForceEnableForTest = false;
            lastTypingFrame = -10;
        }

        /// <summary>
        /// Đảm bảo OnlineChatUI được tạo và gắn vào đúng GameplayCanvas.
        /// Giữ GameObject luôn active để Update() chạy xuyên suốt từ đầu Ván 1, ẩn/hiện qua CanvasGroup.
        /// </summary>
        public static OnlineChatUI Ensure(GameplayHUD hud)
        {
            if (hud == null) return null;

            Canvas canvas = FindGameplayCanvas(hud);
            if (canvas == null) return null;

            var existing = canvas.GetComponentInChildren<OnlineChatUI>(true);
            if (existing == null) existing = hud.GetComponentInChildren<OnlineChatUI>(true);

            if (existing != null)
            {
                Instance = existing;
                existing.parentHud = hud;
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                OnlineVoiceChatManager.Ensure();
                existing.RefreshModeVisibility();
                return existing;
            }

            if (Instance != null)
            {
                Instance.parentHud = hud;
                if (Instance.transform.parent != canvas.transform)
                {
                    Instance.transform.SetParent(canvas.transform, false);
                }
                if (!Instance.gameObject.activeSelf) Instance.gameObject.SetActive(true);
                OnlineVoiceChatManager.Ensure();
                Instance.RefreshModeVisibility();
                return Instance;
            }

            OnlineVoiceChatManager.Ensure();
            var go = new GameObject("OnlineChatPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(canvas.transform, false);

            var chat = go.AddComponent<OnlineChatUI>();
            Instance = chat;
            chat.Build(hud);
            chat.RefreshModeVisibility();
            return chat;
        }

        private static Canvas FindGameplayCanvas(GameplayHUD hud)
        {
            if (hud != null)
            {
                var c = hud.GetComponentInParent<Canvas>();
                if (c != null && c.renderMode != RenderMode.WorldSpace) return c;

                if (hud.PowerBar != null)
                {
                    c = hud.PowerBar.GetComponentInParent<Canvas>();
                    if (c != null) return c;
                }

                if (hud.PlayerLeftPanel != null)
                {
                    c = hud.PlayerLeftPanel.GetComponentInParent<Canvas>();
                    if (c != null) return c;
                }
            }

            foreach (var c in FindObjectsOfType<Canvas>(true))
            {
                if (c != null && c.name == "GameplayCanvas") return c;
            }

            foreach (var c in FindObjectsOfType<Canvas>(true))
            {
                if (c != null && c.renderMode != RenderMode.WorldSpace) return c;
            }

            return FindObjectOfType<Canvas>();
        }

        private bool IsOnlineModeActive()
        {
            if (ForceEnableForTest) return true;
            if (Photon.Pun.PhotonNetwork.InRoom) return true;
            if (Photon.Pun.PhotonNetwork.IsConnected && Photon.Pun.PhotonNetwork.CurrentRoom != null) return true;

            var session = GameSession.Instance;
            if (session != null && session.CurrentGameMode == GameMode.OnlinePvP) return true;

            if (networkMatchManager == null) networkMatchManager = FindObjectOfType<NetworkMatchManager>();
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch) return true;

            return false;
        }

        public void RefreshModeVisibility()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            bool online = IsOnlineModeActive();
            isVisibleOnline = online;

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = online ? 1f : 0f;
                rootCanvasGroup.interactable = online;
                rootCanvasGroup.blocksRaycasts = online;
            }

            if (online)
            {
                BindNetworkManager();
            }
            else if (isExpanded)
            {
                SetExpanded(false, focusInput: false);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            UnbindNetworkManager();
            if (Instance == this) Instance = null;
        }

        private void BindNetworkManager()
        {
            if (networkMatchManager == null)
            {
                networkMatchManager = FindObjectOfType<NetworkMatchManager>();
            }
            if (networkMatchManager != null)
            {
                networkMatchManager.OnChatMessageReceived -= HandleChatMessageReceived;
                networkMatchManager.OnChatMessageReceived += HandleChatMessageReceived;
            }
        }

        private void UnbindNetworkManager()
        {
            if (networkMatchManager != null)
            {
                networkMatchManager.OnChatMessageReceived -= HandleChatMessageReceived;
            }
        }

        #region UI Construction (Outer Parallelogram + Inner Rectangular Chat Box)

        private void Build(GameplayHUD hud)
        {
            parentHud = hud;
            font = FindHudFont(hud != null ? hud.transform : null);

            rootRt = GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0f, 0f);
            rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.pivot = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(DefaultAnchorX, DefaultAnchorY);
            rootRt.sizeDelta = new Vector2(CollapsedW, CollapsedH);

            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 1. Nền Hình Bình Hành bên ngoài (đồng bộ độ nghiêng với PowerMeterPanel bên dưới)
            outerParallelogramBg = gameObject.AddComponent<SkewedPanelGraphic>();
            outerParallelogramBg.color = OuterPanelColor;
            outerParallelogramBg.SetInsets(0f, CollapsedCut, CollapsedCut, 0f);
            outerParallelogramBg.SetBottomDarken(0.48f);
            outerParallelogramBg.raycastTarget = true;

            // 2. Thanh tiêu đề nằm ở phần đầu của Hình Bình Hành
            BuildHeader();

            // 3. Khung Hình Chữ Nhật nằm gọn bên trong Hình Bình Hành lớn (chứa lịch sử chat & ô nhập)
            BuildInnerRectangularChatBox();

            // 4. Viền Hình Bình Hành khép kín + Dải nhấn vàng trên đỉnh (vẽ trên cùng)
            var frameGo = new GameObject("ParallelogramFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramFrameGraphic));
            frameGo.layer = gameObject.layer;
            frameGo.transform.SetParent(rootRt, false);
            outerParallelogramFrame = frameGo.GetComponent<ParallelogramFrameGraphic>();
            outerParallelogramFrame.raycastTarget = false;
            outerParallelogramFrame.Configure(CollapsedCut, OuterBorderThickness, TopAccentHeight, OuterBorderBase, Gold);
            StretchFull(outerParallelogramFrame.rectTransform);

            // 5. Cụm 2 nút Mic [M] và Loa [N] nằm ngay phía trên giao diện Chat
            BuildVoiceTopBar();

            ApplyGeometry(CollapsedW, CollapsedH, CollapsedCut, 0f);

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;
            RefreshLocalizedTexts();
        }

        private void BuildHeader()
        {
            headerRt = CreateRect("HeaderBar", rootRt);
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -3f);
            headerRt.sizeDelta = new Vector2(0f, CollapsedH - 3f);

            var headerHitImg = headerRt.gameObject.AddComponent<Image>();
            headerHitImg.color = new Color(0f, 0f, 0f, 0.01f);
            headerHitImg.raycastTarget = true;

            headerToggleBtn = headerRt.gameObject.AddComponent<Button>();
            headerToggleBtn.targetGraphic = headerHitImg;
            var nav = headerToggleBtn.navigation;
            nav.mode = Navigation.Mode.None;
            headerToggleBtn.navigation = nav;
            headerToggleBtn.transition = Selectable.Transition.None;
            headerToggleBtn.onClick.AddListener(() => ToggleExpand(focusIfExpanding: true));

            // Huy hiệu phím [0] dạng Hình Bình Hành nhỏ màu vàng đồng bộ phong cách HUD
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            badgeGo.layer = gameObject.layer;
            badgeGo.transform.SetParent(headerRt, false);
            badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(14f, 0f);
            badgeRt.sizeDelta = new Vector2(24f, 20f);

            var badgeGraphic = badgeGo.GetComponent<SkewedPanelGraphic>();
            badgeGraphic.color = Gold;
            badgeGraphic.SetInsets(0f, 4f, 4f, 0f);
            badgeGraphic.SetBottomDarken(0.82f);
            badgeGraphic.raycastTarget = false;

            var badgeTxt = CreateText("KeyText", badgeRt, "0", 13, new Color(0.05f, 0.08f, 0.14f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(badgeTxt.rectTransform);

            // Tiêu đề "TRÒ CHUYỆN" (hoặc xem trước tin nhắn mới nhất khi đang thu nhỏ)
            titleText = CreateText("Title", headerRt, "TRÒ CHUYỆN", 13, Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.supportRichText = true;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Truncate;
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(44f, 2f);
            titleText.rectTransform.offsetMax = new Vector2(-58f, -2f);

            // Huy hiệu số tin nhắn chưa đọc (Unread Badge)
            var unreadGo = new GameObject("UnreadBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            unreadGo.layer = gameObject.layer;
            unreadGo.transform.SetParent(headerRt, false);
            unreadBadgeObj = unreadGo;
            var unreadRt = unreadGo.GetComponent<RectTransform>();
            unreadRt.anchorMin = new Vector2(1f, 0.5f);
            unreadRt.anchorMax = new Vector2(1f, 0.5f);
            unreadRt.pivot = new Vector2(1f, 0.5f);
            unreadRt.anchoredPosition = new Vector2(-38f, 0f);
            unreadRt.sizeDelta = new Vector2(24f, 18f);

            var unreadGraphic = unreadGo.GetComponent<SkewedPanelGraphic>();
            unreadGraphic.color = new Color(0.95f, 0.22f, 0.26f, 1f);
            unreadGraphic.SetInsets(0f, 3.5f, 3.5f, 0f);
            unreadGraphic.raycastTarget = false;

            unreadBadgeText = CreateText("Count", unreadRt, "1", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(unreadBadgeText.rectTransform);
            unreadBadgeObj.SetActive(false);

            // Mũi tên báo hiệu Thu/Phóng bên phải
            toggleArrowText = CreateText("Arrow", headerRt, "▲", 12, Cyan, FontStyle.Bold, TextAnchor.MiddleCenter);
            toggleArrowText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            toggleArrowText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            toggleArrowText.rectTransform.pivot = new Vector2(1f, 0.5f);
            toggleArrowText.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
            toggleArrowText.rectTransform.sizeDelta = new Vector2(20f, 20f);
        }

        /// <summary>
        /// Dựng khung Hình Chữ Nhật nằm gọn bên trong lòng Hình Bình Hành lớn khi phóng to:
        /// - Khoảng lùi hai bên (ExpandedCut + 4px) đảm bảo 4 góc vuông của Hình Chữ Nhật luôn nằm gọn
        ///   bên trong 2 cạnh nghiêng của Hình Bình Hành ngoài, tạo bố cục chuẩn xác và hiện đại.
        /// </summary>
        private void BuildInnerRectangularChatBox()
        {
            innerRectBoxRt = CreateRect("InnerRectangularChatBox", rootRt);
            innerRectBoxRt.anchorMin = new Vector2(0f, 0f);
            innerRectBoxRt.anchorMax = new Vector2(1f, 1f);
            innerRectBoxRt.offsetMin = new Vector2(ExpandedCut + 4f, 10f);
            innerRectBoxRt.offsetMax = new Vector2(-(ExpandedCut + 4f), -(HeaderH + 2f));

            expandedGroup = innerRectBoxRt.gameObject.AddComponent<CanvasGroup>();
            expandedGroup.alpha = 0f;
            expandedGroup.interactable = false;
            expandedGroup.blocksRaycasts = false;

            // Nền Hình Chữ Nhật bên trong (kính tối sâu làm nổi bật chữ chat)
            var innerBg = innerRectBoxRt.gameObject.AddComponent<Image>();
            innerBg.color = InnerBoxColor;
            innerBg.raycastTarget = true;

            // 1. Vùng cuộn lịch sử tin nhắn (MessagesWell) nằm ở phần trên của Hình Chữ Nhật
            var wellGo = new GameObject("MessagesWell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            wellGo.layer = gameObject.layer;
            wellGo.transform.SetParent(innerRectBoxRt, false);
            var wellRt = wellGo.GetComponent<RectTransform>();
            wellRt.anchorMin = new Vector2(0f, 0f);
            wellRt.anchorMax = new Vector2(1f, 1f);
            wellRt.offsetMin = new Vector2(InnerRectBorderThickness, InputBarH + InnerRectBorderThickness);
            wellRt.offsetMax = new Vector2(-InnerRectBorderThickness, -InnerRectBorderThickness);

            var wellImg = wellGo.GetComponent<Image>();
            wellImg.color = new Color(0f, 0f, 0f, 0.01f);
            wellImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.layer = gameObject.layer;
            contentGo.transform.SetParent(wellRt, false);
            contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 8, 8);
            vlg.spacing = 5f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = wellGo.GetComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = wellRt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 22f;

            // Đường kẻ ngang phân cách vùng lịch sử chat và thanh nhập liệu trong Hình Chữ Nhật
            var inputTopDiv = CreateImage("InputTopDivider", innerRectBoxRt, InnerBorderColor);
            inputTopDiv.rectTransform.anchorMin = new Vector2(0f, 0f);
            inputTopDiv.rectTransform.anchorMax = new Vector2(1f, 0f);
            inputTopDiv.rectTransform.pivot = new Vector2(0.5f, 0f);
            inputTopDiv.rectTransform.anchoredPosition = new Vector2(0f, InputBarH);
            inputTopDiv.rectTransform.sizeDelta = new Vector2(0f, InnerRectBorderThickness);

            // 2. Thanh Nhập liệu nằm khít ở đáy Hình Chữ Nhật (InputField + Vách ngăn dọc + Nút GỬI)
            inputRowRt = CreateRect("InputRow", innerRectBoxRt);
            inputRowRt.anchorMin = new Vector2(0f, 0f);
            inputRowRt.anchorMax = new Vector2(1f, 0f);
            inputRowRt.pivot = new Vector2(0.5f, 0f);
            inputRowRt.anchoredPosition = new Vector2(0f, InnerRectBorderThickness);
            inputRowRt.sizeDelta = new Vector2(-InnerRectBorderThickness * 2f, InputBarH - InnerRectBorderThickness);

            // Ô InputField bên trái
            var inputBoxGo = new GameObject("ChatInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
            inputBoxGo.layer = gameObject.layer;
            inputBoxGo.transform.SetParent(inputRowRt, false);
            inputBoxRt = inputBoxGo.GetComponent<RectTransform>();
            inputBoxRt.anchorMin = new Vector2(0f, 0f);
            inputBoxRt.anchorMax = new Vector2(1f, 1f);
            inputBoxRt.offsetMin = Vector2.zero;
            inputBoxRt.offsetMax = new Vector2(-(SendBtnW + InnerRectBorderThickness), 0f);

            var inputBg = inputBoxGo.GetComponent<Image>();
            inputBg.color = InputBgColor;
            inputBg.raycastTarget = true;

            var focusHelper = inputBoxGo.AddComponent<ChatInputFocusHelper>();
            focusHelper.Owner = this;

            placeholderText = CreateText("Placeholder", inputBoxRt, LocalizationManager.Get("chat_placeholder"), 13, new Color(0.55f, 0.68f, 0.82f, 0.68f), FontStyle.Italic, TextAnchor.MiddleLeft);
            StretchFull(placeholderText.rectTransform, 10f, 2f);

            var inputText = CreateText("Text", inputBoxRt, "", 13, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            inputText.supportRichText = false;
            inputText.horizontalOverflow = HorizontalWrapMode.Wrap;
            inputText.verticalOverflow = VerticalWrapMode.Truncate;
            StretchFull(inputText.rectTransform, 10f, 2f);

            inputField = inputBoxGo.GetComponent<InputField>();
            inputField.targetGraphic = inputBg;
            inputField.textComponent = inputText;
            inputField.placeholder = placeholderText;
            inputField.characterLimit = 120;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.selectionColor = new Color(0.15f, 0.65f, 0.95f, 0.45f);
            inputField.customCaretColor = true;
            inputField.caretColor = CyanBright;
            inputField.caretWidth = 2;
            var inputNav = inputField.navigation;
            inputNav.mode = Navigation.Mode.None;
            inputField.navigation = inputNav;
            inputField.onValueChanged.AddListener(OnInputValueChanged);

            // Vách ngăn dọc giữa Ô nhập và Nút GỬI
            var vertDiv = CreateImage("InputVertDivider", inputRowRt, InnerBorderColor);
            vertDiv.rectTransform.anchorMin = new Vector2(1f, 0f);
            vertDiv.rectTransform.anchorMax = new Vector2(1f, 1f);
            vertDiv.rectTransform.pivot = new Vector2(1f, 0.5f);
            vertDiv.rectTransform.anchoredPosition = new Vector2(-SendBtnW, 0f);
            vertDiv.rectTransform.sizeDelta = new Vector2(InnerRectBorderThickness, 0f);

            // Nút GỬI bên phải
            var sendGo = new GameObject("BtnSend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            sendGo.layer = gameObject.layer;
            sendGo.transform.SetParent(inputRowRt, false);
            sendBtnRt = sendGo.GetComponent<RectTransform>();
            sendBtnRt.anchorMin = new Vector2(1f, 0f);
            sendBtnRt.anchorMax = new Vector2(1f, 1f);
            sendBtnRt.pivot = new Vector2(1f, 0.5f);
            sendBtnRt.anchoredPosition = Vector2.zero;
            sendBtnRt.sizeDelta = new Vector2(SendBtnW, 0f);

            var sendBg = sendGo.GetComponent<Image>();
            sendBg.color = SendBtnColor;
            sendBg.raycastTarget = true;

            sendButton = sendGo.GetComponent<Button>();
            sendButton.targetGraphic = sendBg;
            var sendNav = sendButton.navigation;
            sendNav.mode = Navigation.Mode.None;
            sendButton.navigation = sendNav;
            ColorBlock sendCb = sendButton.colors;
            sendCb.normalColor = Color.white;
            sendCb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            sendCb.pressedColor = Gold;
            sendCb.fadeDuration = 0.06f;
            sendButton.colors = sendCb;
            sendButton.onClick.AddListener(() =>
            {
                SubmitCurrentInput();
                RequestFocusInputField();
            });

            sendButtonText = CreateText("Label", sendGo.transform, "GỬI", 12, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(sendButtonText.rectTransform, 4f, 0f);

            // 3. Viền 4 cạnh khép kín của Khung Hình Chữ Nhật bên trong (Inner Rectangular Frame)
            BuildInnerRectBorder(innerRectBoxRt);
        }

        /// <summary>
        /// Dựng cụm điều khiển Mic [M] và Loa [N] nằm ngay phía trên đỉnh của khung Chat (cách 6px).
        /// Tự động dịch chuyển mượt mà theo đỉnh khung Chat khi thu nhỏ / phóng lớn.
        /// </summary>
        private void BuildVoiceTopBar()
        {
            voiceBarRt = CreateRect("VoiceControlBar", rootRt);
            voiceBarRt.anchorMin = new Vector2(0f, 1f);
            voiceBarRt.anchorMax = new Vector2(0f, 1f);
            voiceBarRt.pivot = new Vector2(0f, 0f);
            voiceBarRt.anchoredPosition = new Vector2(0f, 6f);
            voiceBarRt.sizeDelta = new Vector2(CollapsedW, 34f);

            const float btnW = 117f;
            const float btnH = 34f;
            const float gap = 6f;

            micToggleBtn = BuildVoiceControlButton(
                voiceBarRt,
                "BtnVoiceMic",
                new Vector2(0f, 0f),
                new Vector2(btnW, btnH),
                "M",
                VoiceIconGraphic.IconKind.Microphone,
                out micBtnBg,
                out micBtnFrame,
                out micIconGraphic,
                out micLabelText);
            micToggleBtn.onClick.AddListener(() =>
            {
                OnlineVoiceChatManager.Ensure().ToggleMic();
                RefreshVoiceBarVisuals();
            });

            speakerToggleBtn = BuildVoiceControlButton(
                voiceBarRt,
                "BtnVoiceSpeaker",
                new Vector2(btnW + gap, 0f),
                new Vector2(btnW, btnH),
                "N",
                VoiceIconGraphic.IconKind.Speaker,
                out speakerBtnBg,
                out speakerBtnFrame,
                out speakerIconGraphic,
                out speakerLabelText);
            speakerToggleBtn.onClick.AddListener(() =>
            {
                OnlineVoiceChatManager.Ensure().ToggleSpeaker();
                RefreshVoiceBarVisuals();
            });

            RefreshVoiceBarVisuals();
        }

        private Button BuildVoiceControlButton(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            Vector2 size,
            string keyText,
            VoiceIconGraphic.IconKind iconKind,
            out SkewedPanelGraphic bgGraphic,
            out ParallelogramFrameGraphic frameGraphic,
            out VoiceIconGraphic iconGraphic,
            out Text labelText)
        {
            const float cut = 9.5f;
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(Button));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            bgGraphic = go.GetComponent<SkewedPanelGraphic>();
            bgGraphic.color = OuterPanelColor;
            bgGraphic.SetInsets(0f, cut, cut, 0f);
            bgGraphic.SetBottomDarken(0.48f);
            bgGraphic.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bgGraphic;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.transition = Selectable.Transition.None;

            // Viền Hình Bình Hành khép kín
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(ParallelogramFrameGraphic));
            frameGo.layer = gameObject.layer;
            frameGo.transform.SetParent(rt, false);
            frameGraphic = frameGo.GetComponent<ParallelogramFrameGraphic>();
            frameGraphic.raycastTarget = false;
            frameGraphic.Configure(cut, 1.8f, 2.2f, OuterBorderBase, Gold);
            StretchFull(frameGraphic.rectTransform);

            // Huy hiệu phím [M] / [N]
            var badgeGo = new GameObject("KeyBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            badgeGo.layer = gameObject.layer;
            badgeGo.transform.SetParent(rt, false);
            var bRt = badgeGo.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f);
            bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.anchoredPosition = new Vector2(10f, 0f);
            bRt.sizeDelta = new Vector2(20f, 17f);

            var bGraphic = badgeGo.GetComponent<SkewedPanelGraphic>();
            bGraphic.color = Gold;
            bGraphic.SetInsets(0f, 3.2f, 3.2f, 0f);
            bGraphic.SetBottomDarken(0.82f);
            bGraphic.raycastTarget = false;

            var bTxt = CreateText("KeyText", bRt, keyText, 11, new Color(0.05f, 0.08f, 0.14f, 1f), FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(bTxt.rectTransform);

            // Icon Mic / Loa động (nhấp nháy lên xuống khi nói)
            var iconGo = new GameObject("VoiceIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(VoiceIconGraphic));
            iconGo.layer = gameObject.layer;
            iconGo.transform.SetParent(rt, false);
            var iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = new Vector2(0f, 0.5f);
            iRt.anchorMax = new Vector2(0f, 0.5f);
            iRt.pivot = new Vector2(0f, 0.5f);
            iRt.anchoredPosition = new Vector2(33f, 0f);
            iRt.sizeDelta = new Vector2(22f, 22f);

            iconGraphic = iconGo.GetComponent<VoiceIconGraphic>();
            iconGraphic.Kind = iconKind;
            iconGraphic.raycastTarget = false;

            // Nhãn MIC / LOA + trạng thái
            labelText = CreateText("Label", rt, iconKind == VoiceIconGraphic.IconKind.Microphone ? "MIC" : "LOA", 11, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            labelText.supportRichText = true;
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(58f, 2f);
            labelText.rectTransform.offsetMax = new Vector2(-8f, -2f);

            return btn;
        }

        private void RefreshVoiceBarVisuals()
        {
            var voice = OnlineVoiceChatManager.Instance != null ? OnlineVoiceChatManager.Instance : OnlineVoiceChatManager.Ensure();
            if (voice == null) return;

            bool micOn = voice.IsMicEnabled;
            bool spkOn = voice.IsSpeakerEnabled;
            float localLvl = voice.LocalVoiceLevel01;
            float remoteLvl = voice.RemoteVoiceLevel01;
            float phase = Time.unscaledTime;

            if (micIconGraphic != null)
            {
                micIconGraphic.SetState(!micOn, localLvl, phase);
            }
            if (speakerIconGraphic != null)
            {
                speakerIconGraphic.SetState(!spkOn, remoteLvl, phase);
            }

            Color emeraldGlow = new Color(0.16f, 0.96f, 0.58f, 0.98f);
            Color mutedBorder = new Color(0.88f, 0.28f, 0.32f, 0.72f);

            if (micBtnFrame != null)
            {
                Color c = !micOn
                    ? mutedBorder
                    : (localLvl > 0.04f ? Color.Lerp(OuterBorderActive, emeraldGlow, 0.65f) : OuterBorderBase);
                micBtnFrame.SetBorderColor(c);
            }
            if (micBtnBg != null)
            {
                micBtnBg.color = micOn
                    ? (localLvl > 0.04f ? new Color(0.08f, 0.20f, 0.26f, 0.96f) : OuterPanelColor)
                    : new Color(0.14f, 0.09f, 0.14f, 0.94f);
            }
            if (micLabelText != null)
            {
                string micWord = LocalizationManager.Get("voice_mic_short", "MIC");
                string st = micOn
                    ? $"<color=#7CF5FF>{LocalizationManager.Get("voice_on_short", "BẬT")}</color>"
                    : $"<color=#FF6B6B>{LocalizationManager.Get("voice_off_short", "TẮT")}</color>";
                micLabelText.text = $"{micWord} {st}";
            }

            if (speakerBtnFrame != null)
            {
                Color c = !spkOn
                    ? mutedBorder
                    : (remoteLvl > 0.04f ? Color.Lerp(OuterBorderActive, emeraldGlow, 0.65f) : OuterBorderBase);
                speakerBtnFrame.SetBorderColor(c);
            }
            if (speakerBtnBg != null)
            {
                speakerBtnBg.color = spkOn
                    ? (remoteLvl > 0.04f ? new Color(0.08f, 0.20f, 0.26f, 0.96f) : OuterPanelColor)
                    : new Color(0.14f, 0.09f, 0.14f, 0.94f);
            }
            if (speakerLabelText != null)
            {
                string spkWord = LocalizationManager.Get("voice_speaker_short", "LOA");
                string st = spkOn
                    ? $"<color=#7CF5FF>{LocalizationManager.Get("voice_on_short", "BẬT")}</color>"
                    : $"<color=#FF6B6B>{LocalizationManager.Get("voice_off_short", "TẮT")}</color>";
                speakerLabelText.text = $"{spkWord} {st}";
            }
        }

        private void BuildInnerRectBorder(RectTransform targetRect)
        {
            var bTop = CreateImage("RectBorderTop", targetRect, InnerBorderColor);
            bTop.rectTransform.anchorMin = new Vector2(0f, 1f);
            bTop.rectTransform.anchorMax = new Vector2(1f, 1f);
            bTop.rectTransform.pivot = new Vector2(0.5f, 1f);
            bTop.rectTransform.anchoredPosition = Vector2.zero;
            bTop.rectTransform.sizeDelta = new Vector2(0f, InnerRectBorderThickness);

            var bBot = CreateImage("RectBorderBottom", targetRect, InnerBorderColor);
            bBot.rectTransform.anchorMin = new Vector2(0f, 0f);
            bBot.rectTransform.anchorMax = new Vector2(1f, 0f);
            bBot.rectTransform.pivot = new Vector2(0.5f, 0f);
            bBot.rectTransform.anchoredPosition = Vector2.zero;
            bBot.rectTransform.sizeDelta = new Vector2(0f, InnerRectBorderThickness);

            var bLeft = CreateImage("RectBorderLeft", targetRect, InnerBorderColor);
            bLeft.rectTransform.anchorMin = new Vector2(0f, 0f);
            bLeft.rectTransform.anchorMax = new Vector2(0f, 1f);
            bLeft.rectTransform.pivot = new Vector2(0f, 0.5f);
            bLeft.rectTransform.anchoredPosition = Vector2.zero;
            bLeft.rectTransform.sizeDelta = new Vector2(InnerRectBorderThickness, 0f);

            var bRight = CreateImage("RectBorderRight", targetRect, InnerBorderColor);
            bRight.rectTransform.anchorMin = new Vector2(1f, 0f);
            bRight.rectTransform.anchorMax = new Vector2(1f, 1f);
            bRight.rectTransform.pivot = new Vector2(1f, 0.5f);
            bRight.rectTransform.anchoredPosition = Vector2.zero;
            bRight.rectTransform.sizeDelta = new Vector2(InnerRectBorderThickness, 0f);
        }

        private void OnInputValueChanged(string val)
        {
            if (inputField != null && inputField.isFocused)
            {
                lastTypingFrame = Time.frameCount;
                submittedOnEnterText = val;
            }
        }

        #endregion

        #region Expand / Collapse & Focus Management

        public void ToggleExpand(bool focusIfExpanding = true)
        {
            SetExpanded(!isExpanded, focusIfExpanding);
        }

        public void SetExpanded(bool expand, bool focusInput = true)
        {
            if (isExpanded == expand)
            {
                if (expand && focusInput) RequestFocusInputField();
                return;
            }

            isExpanded = expand;
            lastTypingFrame = Time.frameCount;

            if (isExpanded)
            {
                unreadCount = 0;
                UpdateUnreadBadge();
                if (toggleArrowText != null) toggleArrowText.text = "▼";
                if (outerParallelogramFrame != null) outerParallelogramFrame.SetBorderColor(OuterBorderActive);
            }
            else
            {
                UnfocusInputField();
                if (toggleArrowText != null) toggleArrowText.text = "▲";
                if (outerParallelogramFrame != null) outerParallelogramFrame.SetBorderColor(OuterBorderBase);
            }

            UpdateHeaderTitleOrPreview();

            if (transitionRoutine != null) StopCoroutine(transitionRoutine);
            if (gameObject.activeInHierarchy)
            {
                transitionRoutine = StartCoroutine(AnimateExpandRoutine(isExpanded, focusInput));
            }
            else
            {
                ApplyGeometry(
                    isExpanded ? ExpandedW : CollapsedW,
                    isExpanded ? ExpandedH : CollapsedH,
                    isExpanded ? ExpandedCut : CollapsedCut,
                    isExpanded ? 1f : 0f);
            }
        }

        private IEnumerator AnimateExpandRoutine(bool expand, bool focusInput)
        {
            const float duration = 0.17f;
            float elapsed = 0f;

            float startW = rootRt.sizeDelta.x;
            float startH = rootRt.sizeDelta.y;
            float targetW = expand ? ExpandedW : CollapsedW;
            float targetH = expand ? ExpandedH : CollapsedH;

            float startCut = expand ? CollapsedCut : ExpandedCut;
            float targetCut = expand ? ExpandedCut : CollapsedCut;

            float startBlend = expandedGroup != null ? expandedGroup.alpha : (expand ? 0f : 1f);
            float targetBlend = expand ? 1f : 0f;

            if (expand)
            {
                if (expandedGroup != null)
                {
                    expandedGroup.interactable = true;
                    expandedGroup.blocksRaycasts = true;
                }
                if (focusInput) RequestFocusInputField();
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                ApplyGeometry(
                    Mathf.Lerp(startW, targetW, ease),
                    Mathf.Lerp(startH, targetH, ease),
                    Mathf.Lerp(startCut, targetCut, ease),
                    Mathf.Lerp(startBlend, targetBlend, ease));

                yield return null;
            }

            ApplyGeometry(targetW, targetH, targetCut, targetBlend);

            if (expand)
            {
                ScrollToBottomImmediate();
                if (focusInput) RequestFocusInputField();
            }

            transitionRoutine = null;
        }

        private void ApplyGeometry(float w, float h, float cut, float expandedBlend)
        {
            if (rootRt != null) rootRt.sizeDelta = new Vector2(w, h);
            if (outerParallelogramBg != null) outerParallelogramBg.SetInsets(0f, cut, cut, 0f);
            if (outerParallelogramFrame != null) outerParallelogramFrame.SetCut(cut);

            if (headerRt != null)
            {
                float hh = Mathf.Lerp(CollapsedH - 3f, HeaderH, expandedBlend);
                headerRt.sizeDelta = new Vector2(0f, hh);
            }

            // Căn lề trái/phải của Header theo độ vát của Hình Bình Hành để cân đối cả khi thu nhỏ lẫn phóng to
            if (badgeRt != null)
            {
                float leftPad = Mathf.Lerp(14f, cut + 4f, expandedBlend);
                badgeRt.anchoredPosition = new Vector2(leftPad, 0f);
                if (titleText != null)
                {
                    titleText.rectTransform.offsetMin = new Vector2(leftPad + 30f, 2f);
                    titleText.rectTransform.offsetMax = new Vector2(-(cut + 38f), -2f);
                }
            }

            if (toggleArrowText != null)
            {
                float rightPad = Mathf.Lerp(-16f, -(cut + 4f), expandedBlend);
                toggleArrowText.rectTransform.anchoredPosition = new Vector2(rightPad, 0f);
            }

            if (innerRectBoxRt != null)
            {
                float sideInset = cut + 4f;
                innerRectBoxRt.offsetMin = new Vector2(sideInset, 10f);
                innerRectBoxRt.offsetMax = new Vector2(-sideInset, -(HeaderH + 2f));
            }

            if (expandedGroup != null)
            {
                float eAlpha = Mathf.Clamp01((expandedBlend - 0.18f) / 0.82f);
                expandedGroup.alpha = eAlpha;
                bool active = isExpanded || expandedBlend > 0.2f;
                expandedGroup.interactable = active;
                expandedGroup.blocksRaycasts = active;
            }
        }

        /// <summary>
        /// Kích hoạt con trỏ nhập liệu (caret) trong InputField ngay lập tức và xác nhận lại ở frame kế tiếp
        /// để đảm bảo EventSystem của Unity không nuốt sự kiện click khi chuyển từ ngoài vào.
        /// </summary>
        public void RequestFocusInputField()
        {
            FocusInputFieldImmediate();
            if (gameObject.activeInHierarchy)
            {
                if (focusRoutine != null) StopCoroutine(focusRoutine);
                focusRoutine = StartCoroutine(FocusNextFrameRoutine());
            }
        }

        private IEnumerator FocusNextFrameRoutine()
        {
            yield return null;
            FocusInputFieldImmediate();
            focusRoutine = null;
        }

        public void FocusInputFieldImmediate()
        {
            if (inputField == null || !isExpanded) return;
            lastTypingFrame = Time.frameCount;

            if (expandedGroup != null)
            {
                expandedGroup.interactable = true;
                expandedGroup.blocksRaycasts = true;
            }

            if (EventSystem.current != null)
            {
                if (EventSystem.current.currentSelectedGameObject == inputField.gameObject && !inputField.isFocused)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
                EventSystem.current.SetSelectedGameObject(inputField.gameObject);
            }

            inputField.Select();
            inputField.ActivateInputField();
            inputField.MoveTextEnd(false);
        }

        private void UnfocusInputField()
        {
            if (inputField == null) return;
            inputField.DeactivateInputField();
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == inputField.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        #endregion

        #region Update, Mouse Click & Keyboard Shortcuts ([0], [Enter], [Esc])

        private void Update()
        {
            bool focused = inputField != null && inputField.isFocused;

            RefreshModeVisibility();
            if (!isVisibleOnline) return;

            if (networkMatchManager == null) BindNetworkManager();

            if (focused)
            {
                lastTypingFrame = Time.frameCount;
            }

            // Phím tắt nhanh bật/tắt Mic [M] và Loa [N] khi không đang gõ chữ trong ô Chat
            if (!focused && !HudModalState.IsModalOpen)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.M))
                {
                    OnlineVoiceChatManager.Ensure().ToggleMic();
                }
                if (UnityEngine.Input.GetKeyDown(KeyCode.N))
                {
                    OnlineVoiceChatManager.Ensure().ToggleSpeaker();
                }
            }

            // Cập nhật hoạt ảnh nhấp nháy lên xuống của icon Mic & Loa phía trên khung Chat mỗi frame
            RefreshVoiceBarVisuals();

            // Hiệu ứng nhịp thở cho viền Hình Bình Hành khi có tin nhắn chưa đọc lúc đang thu nhỏ
            if (!isExpanded && unreadCount > 0 && outerParallelogramFrame != null)
            {
                unreadPulseTimer += Time.unscaledDeltaTime * 5f;
                float pulse = 0.5f + 0.5f * Mathf.Sin(unreadPulseTimer);
                outerParallelogramFrame.SetBorderColor(Color.Lerp(OuterBorderBase, CyanBright, 0.35f + 0.65f * pulse));
            }

            bool pressedZero = UnityEngine.Input.GetKeyDown(KeyCode.Alpha0) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad0);

            if (isExpanded)
            {
                // Xử lý click chuột trái khi khung chat đang mở:
                // - Click vào vùng ô nhập: kích hoạt lại con trỏ nhập liệu (caret) ngay lập tức.
                // - Click ra ngoài ô nhập (lên bàn bida hoặc vùng lịch sử): chỉ bỏ focus ô nhập,
                //   GIỮ NGUYÊN khung chat mở để KHÔNG bị mất con trỏ chuột (Cursor).
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (IsPointerInsideRect(inputBoxRt))
                    {
                        RequestFocusInputField();
                    }
                    else if (!IsPointerInsideRect(sendBtnRt) && !IsPointerInsideRect(headerRt) && !IsPointerInsideRect(voiceBarRt))
                    {
                        UnfocusInputField();
                    }
                }

                // Nhấn Escape: thu gọn khung chat (ưu tiên trước Pause Menu)
                if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                {
                    lastTypingFrame = Time.frameCount;
                    SetExpanded(false);
                    return;
                }

                // Nhấn Enter: gửi tin nhắn (nếu có chữ) hoặc focus vào ô nhập
                if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    lastTypingFrame = Time.frameCount;
                    string candidate = inputField != null && !string.IsNullOrWhiteSpace(inputField.text)
                        ? inputField.text
                        : submittedOnEnterText;

                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        SubmitText(candidate);
                        RequestFocusInputField();
                    }
                    else if (!focused)
                    {
                        RequestFocusInputField();
                    }
                    return;
                }

                // Nhấn phím [0] khi KHÔNG đang gõ chữ trong InputField -> thu gọn về Hình Bình Hành nhỏ
                if (!focused && pressedZero)
                {
                    lastTypingFrame = Time.frameCount;
                    SetExpanded(false);
                    return;
                }
            }
            else
            {
                // Khi đang thu nhỏ: nhấn phím [0] (nếu không có bảng modal khác đang mở) -> Phóng to & Focus gõ chữ ngay
                if (!HudModalState.IsModalOpen && pressedZero)
                {
                    lastTypingFrame = Time.frameCount;
                    SetExpanded(true, focusInput: true);
                }
            }
        }

        private static bool IsPointerInsideRect(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, UnityEngine.Input.mousePosition, null);
        }

        #endregion

        #region Sending & Receiving Messages

        public void SubmitCurrentInput()
        {
            if (inputField == null) return;
            string text = !string.IsNullOrWhiteSpace(inputField.text) ? inputField.text : submittedOnEnterText;
            SubmitText(text);
        }

        public bool SubmitText(string rawText)
        {
            submittedOnEnterText = string.Empty;
            if (inputField != null)
            {
                inputField.text = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(rawText)) return false;

            BindNetworkManager();
            if (networkMatchManager != null && networkMatchManager.IsOnlineMatch)
            {
                return networkMatchManager.SendChatMessage(rawText);
            }

            // Phòng hờ khi test độc lập trong Editor chưa kết nối phòng Photon
            HandleChatMessageReceived(0, LocalizationManager.Get("chat_you", "Bạn"), rawText.Trim());
            return true;
        }

        private void HandleChatMessageReceived(int senderIndex, string senderName, string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            entries.Add(new ChatEntry
            {
                senderIndex = senderIndex,
                senderName = senderName,
                message = message.Trim()
            });

            while (entries.Count > MaxHistoryCount)
            {
                entries.RemoveAt(0);
            }

            int localIdx = networkMatchManager != null ? networkMatchManager.LocalPlayerIndex : 0;
            bool isFromOpponent = senderIndex >= 0 && senderIndex != localIdx;

            if (!isExpanded && isFromOpponent)
            {
                unreadCount++;
                UpdateUnreadBadge();
            }

            RebuildMessageUI();
        }

        private void UpdateUnreadBadge()
        {
            if (unreadBadgeObj == null || unreadBadgeText == null) return;
            bool show = !isExpanded && unreadCount > 0;
            unreadBadgeObj.SetActive(show);
            if (show)
            {
                unreadBadgeText.text = unreadCount > 9 ? "9+" : unreadCount.ToString();
            }
        }

        private void RebuildMessageUI()
        {
            if (contentRt == null) return;

            while (messageRows.Count < entries.Count)
            {
                var rowGo = new GameObject("Msg_" + messageRows.Count, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                rowGo.layer = gameObject.layer;
                rowGo.transform.SetParent(contentRt, false);
                var t = rowGo.GetComponent<Text>();
                t.font = font;
                t.fontSize = 13;
                t.fontStyle = FontStyle.Normal;
                t.alignment = TextAnchor.MiddleLeft;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.supportRichText = true;
                t.lineSpacing = 1.15f;
                t.raycastTarget = false;
                messageRows.Add(t);
            }

            int localIdx = networkMatchManager != null ? networkMatchManager.LocalPlayerIndex : 0;

            for (int i = 0; i < messageRows.Count; i++)
            {
                if (i >= entries.Count)
                {
                    messageRows[i].gameObject.SetActive(false);
                    continue;
                }

                messageRows[i].gameObject.SetActive(true);
                ChatEntry e = entries[i];

                bool isMe = e.senderIndex == localIdx;
                string tag = isMe
                    ? $"<color=#FFC933><b>[{EscapeRichText(LocalizationManager.Get("chat_you", "Bạn"))}]:</b></color>"
                    : $"<color=#00E5FF><b>{EscapeRichText(e.senderName)}:</b></color>";
                string bodyColor = isMe ? "#F5FAFF" : "#E2F4FF";
                messageRows[i].text = $"{tag} <color={bodyColor}>{EscapeRichText(e.message)}</color>";
            }

            UpdateHeaderTitleOrPreview();
            ScrollToBottomImmediate();
        }

        private void UpdateHeaderTitleOrPreview()
        {
            if (titleText == null) return;

            if (!isExpanded && entries.Count > 0)
            {
                ChatEntry last = entries[entries.Count - 1];
                int localIdx = networkMatchManager != null ? networkMatchManager.LocalPlayerIndex : 0;
                bool isMe = last.senderIndex == localIdx;
                string prefix = isMe
                    ? $"<color=#FFC933><b>{EscapeRichText(LocalizationManager.Get("chat_you", "Bạn"))}:</b></color>"
                    : $"<color=#00E5FF><b>{EscapeRichText(last.senderName)}:</b></color>";
                titleText.text = $"{prefix} <color=#F0F6FF>{EscapeRichText(last.message)}</color>";
                return;
            }

            titleText.text = LocalizationManager.Get("chat_title");
        }

        private void ScrollToBottomImmediate()
        {
            if (scrollRect == null || contentRt == null) return;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            scrollRect.verticalNormalizedPosition = 0f;
        }

        private void RefreshLocalizedTexts()
        {
            if (placeholderText != null) placeholderText.text = LocalizationManager.Get("chat_placeholder");
            if (sendButtonText != null) sendButtonText.text = LocalizationManager.Get("chat_send");
            RefreshVoiceBarVisuals();
            RebuildMessageUI();
        }

        private static string EscapeRichText(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("<", "‹").Replace(">", "›");
        }

        #endregion

        #region UI Helpers

        private static Font FindHudFont(Transform root)
        {
            if (root != null)
            {
                var existing = root.GetComponentInChildren<Text>(true);
                if (existing != null && existing.font != null) return existing.font;
            }
            var anyText = FindObjectOfType<Text>(true);
            if (anyText != null && anyText.font != null) return anyText.font;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private Image CreateImage(string name, Transform parent, Color color)
        {
            var rt = CreateRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private Text CreateText(string name, Transform parent, string text, int fontSize, Color color, FontStyle style, TextAnchor align)
        {
            var rt = CreateRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void StretchFull(RectTransform rt, float insetX = 0f, float insetY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(insetX, insetY);
            rt.offsetMax = new Vector2(-insetX, -insetY);
        }

        #endregion
    }
}
