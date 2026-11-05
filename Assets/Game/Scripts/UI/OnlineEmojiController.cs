using System;
using System.IO;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hệ thống Thả Emoji Động Cỡ Lớn (Phiên bản V2 - 24 FPS + Squash & Stretch 60 FPS)
    /// dành riêng cho chế độ Đấu Trực Tuyến (Online PvP):
    /// - Nút mở khay Emoji là Nút Icon Mặt Cười Tối Giản Phát Sáng (Minimalist Glowing Smiley Medallion [E])
    ///   đặt ngay phía trên nút [M] MIC & [N] LOA (tự động trượt lên/xuống theo khung Chat [C]).
    /// - Khung hiển thị dưới Thẻ Tên Người Chơi (PlayerLeftPanel & PlayerRightPanel) theo Mẫu C:
    ///   Huy Hiệu Khiên Tròn Vàng Hoàng Kim Đúp (Circular Championship Medallion) + Băng Chữ Bo Tròn (Capsule Ribbon),
    ///   loại bỏ hoàn toàn khung hình bình hành cũ.
    /// </summary>
    public class OnlineEmojiController : MonoBehaviour, IOnEventCallback
    {
        public const int EmojiCount = 10;
        private const int SheetCols = 6;
        private const int SheetRows = 4;
        private const int FramesPerEmoji = SheetCols * SheetRows; // 24 frames mượt mà
        private const float FrameRate = 24f;

        private const float MedallionSize = 152f;
        private const float BubbleTotalWidth = 156f;
        private const float BubbleTotalHeight = 176f;
        private const float BubbleDisplayDuration = 3.2f;
        private const float SendCooldownSeconds = 0.9f;

        private static readonly string[] KeyHints = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0" };

        private static readonly KeyCode[] AlphaKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0
        };

        private static readonly KeyCode[] KeypadKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5,
            KeyCode.Keypad6, KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0
        };

        private static readonly Color GoldRingColor = new Color(1.00f, 0.81f, 0.22f, 1f);
        private static readonly Color MedallionFillColor = new Color(0.055f, 0.095f, 0.175f, 0.96f);
        private static readonly Color CapsuleFillColor = new Color(0.06f, 0.09f, 0.15f, 0.98f);

        private static readonly Color[] AccentColors =
        {
            new Color(0.16f, 0.96f, 0.59f, 1f), // 0 [1]: LIKE (TUYỆT!)
            new Color(1.00f, 0.27f, 0.35f, 1f), // 1 [2]: DISLIKE (QUÁ GÀ!)
            new Color(1.00f, 0.84f, 0.20f, 1f), // 2 [3]: HAHA (HAHA!)
            new Color(0.00f, 0.92f, 1.00f, 1f), // 3 [4]: COOL (EASY!)
            new Color(1.00f, 0.61f, 0.16f, 1f), // 4 [5]: SHOCK (TOANG!)
            new Color(1.00f, 0.22f, 0.22f, 1f), // 5 [6]: ANGRY (CAY THẾ!)
            new Color(0.24f, 0.98f, 0.65f, 1f), // 6 [7]: CLAP (HAY QUÁ!)
            new Color(0.25f, 0.75f, 1.00f, 1f), // 7 [8]: CRY (HUHU!)
            new Color(1.00f, 0.49f, 0.10f, 1f), // 8 [9]: FIRE (CHÁY QUÁ!)
            new Color(0.76f, 0.43f, 1.00f, 1f)  // 9 [0]: THINK (200 IQ)
        };

        public static OnlineEmojiController Instance { get; private set; }

        private static int lastInteractionFrame = -10;

        /// <summary>
        /// True khi Khay chọn Emoji đang mở hoặc vừa mới thao tác đóng/chọn trong 2 frame gần nhất
        /// (dùng để khóa hoàn toàn thao tác ngắm/bắn/xoay camera/đặt bi của cơ thủ).
        /// </summary>
        public static bool IsPickerOpen =>
            (Instance != null && Instance.isActiveAndEnabled && Instance.isVisibleOnline && Instance.isTrayOpen)
            || Time.frameCount <= lastInteractionFrame + 2;

        private static Sprite[][] cachedFrames;
        private static Sprite cachedLauncherSprite;
        private static AudioClip[] cachedSfxClips;

        private GameplayHUD parentHud;
        private Font uiFont;

        // Nút Icon Emoji Tối Giản [E] gắn ngay phía trên nút [M] MIC & [N] LOA
        private RectTransform launcherRootRt;
        private CanvasGroup launcherCanvasGroup;
        private Button emojiToggleButton;
        private RectTransform emojiBtnIconRt;
        private Image emojiBtnIconImage;
        private EmojiMedallionGraphic emojiBtnGlowRing;

        // Khay chọn 10 Emoji (5 cột x 2 hàng, bo tròn hiện đại) bung ngay phía trên nút Icon [E]
        private RectTransform trayRt;
        private CanvasGroup trayCanvasGroup;
        private Image[] trayIconImages = new Image[EmojiCount];
        private Text[] trayTagTexts = new Text[EmojiCount];

        // 2 Khung Huy Hiệu Khiên Tròn Vàng Gold (Mẫu C) gắn dưới PlayerLeftPanel & PlayerRightPanel
        private EmojiBubbleRuntime leftBubble;
        private EmojiBubbleRuntime rightBubble;

        private AudioSource sfxSource;
        private bool isVisibleOnline;
        private bool isTrayOpen;
        private float trayAnim01;
        private float lastSendTime = -10f;

        public RectTransform TopLeftBarRect => null;
        public Button EmojiButton => emojiToggleButton;

        /// <summary>
        /// Đồ họa Vector đa năng vẽ Khung Huy Hiệu Khiên Tròn Vàng Gold Đúp (Mẫu C),
        /// Băng Chữ Bo Tròn (Capsule Ribbon), Mũi Tên Tam Giác Vàng và Hào Quang Starburst.
        /// Không sử dụng bất kỳ hình bình hành nào.
        /// </summary>
        private sealed class EmojiMedallionGraphic : MaskableGraphic
        {
            public enum ShapeKind
            {
                DoubleRingMedallion,
                CapsuleRibbon,
                RoundedBox,
                PointerArrow,
                StarburstAura
            }

            public ShapeKind Kind = ShapeKind.DoubleRingMedallion;
            public Color OuterRingColor = GoldRingColor;
            public Color InnerRingColor = new Color(0f, 0.9f, 1f, 0.9f);
            public float OuterThickness = 4.2f;
            public float InnerThickness = 2.2f;
            public float CornerRadius = 16f;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = GetPixelAdjustedRect();
                if (r.width <= 2f || r.height <= 2f) return;

                switch (Kind)
                {
                    case ShapeKind.PointerArrow:
                        DrawPointerArrow(vh, r);
                        break;
                    case ShapeKind.StarburstAura:
                        DrawStarburst(vh, r);
                        break;
                    case ShapeKind.CapsuleRibbon:
                        DrawCapsuleWithBorder(vh, r);
                        break;
                    case ShapeKind.RoundedBox:
                        DrawRoundedRectWithBorder(vh, r, CornerRadius, OuterThickness, color, OuterRingColor);
                        break;
                    default:
                        DrawMedallion(vh, r);
                        break;
                }
            }

            private void DrawPointerArrow(VertexHelper vh, Rect r)
            {
                Vector2 top = new Vector2(r.center.x, r.yMax);
                Vector2 bl = new Vector2(r.xMin, r.yMin);
                Vector2 br = new Vector2(r.xMax, r.yMin);
                UIVertex v = UIVertex.simpleVert;
                v.color = color;
                v.position = bl; vh.AddVert(v);
                v.position = top; vh.AddVert(v);
                v.position = br; vh.AddVert(v);
                vh.AddTriangle(0, 1, 2);
            }

            private void DrawMedallion(VertexHelper vh, Rect r)
            {
                Vector2 c = r.center;
                float maxR = Mathf.Min(r.width, r.height) * 0.5f;
                const int segs = 64;

                float rOuterEdge = maxR;
                float rOuterIn = Mathf.Max(2f, rOuterEdge - OuterThickness);
                float rGapIn = Mathf.Max(1f, rOuterIn - 2.2f);
                float rInnerIn = Mathf.Max(1f, rGapIn - InnerThickness);

                // 1. Nền tròn xanh đen sâu bên trong Huy Hiệu
                AddFilledDisc(vh, c, rOuterIn, segs, color);

                // 2. Vòng nhẫn Vàng Hoàng Kim bên ngoài (Outer Gold Ring)
                AddRing(vh, c, rOuterIn, rOuterEdge, segs, OuterRingColor);

                // 3. Vòng nhẫn phát sáng màu cảm xúc bên trong (Inner Accent Ring)
                if (InnerThickness > 0.2f)
                {
                    AddRing(vh, c, rInnerIn, rGapIn, segs, InnerRingColor);
                }
            }

            private void DrawCapsuleWithBorder(VertexHelper vh, Rect r)
            {
                float rad = Mathf.Min(r.width, r.height) * 0.5f;
                DrawRoundedRectWithBorder(vh, r, rad, OuterThickness, color, OuterRingColor);
            }

            private static void DrawRoundedRectWithBorder(
                VertexHelper vh,
                Rect r,
                float radius,
                float borderThick,
                Color32 fillCol,
                Color32 borderCol)
            {
                float maxRad = Mathf.Min(r.width, r.height) * 0.5f;
                float radOut = Mathf.Clamp(radius, 2f, maxRad);
                float t = Mathf.Clamp(borderThick, 1f, maxRad * 0.6f);
                float radIn = Mathf.Max(1f, radOut - t);

                Rect rIn = new Rect(r.xMin + t, r.yMin + t, Mathf.Max(2f, r.width - t * 2f), Mathf.Max(2f, r.height - t * 2f));

                const int cornerSteps = 10;
                const int totalPts = cornerSteps * 4;

                Vector2[] outerPts = new Vector2[totalPts];
                Vector2[] innerPts = new Vector2[totalPts];

                FillRoundedRectPerimeter(r, radOut, cornerSteps, outerPts);
                FillRoundedRectPerimeter(rIn, radIn, cornerSteps, innerPts);

                // 1. Nền bên trong
                int centerIdx = vh.currentVertCount;
                UIVertex vc = UIVertex.simpleVert;
                vc.position = r.center;
                vc.color = fillCol;
                vh.AddVert(vc);

                int innerStart = vh.currentVertCount;
                for (int i = 0; i < totalPts; i++)
                {
                    UIVertex v = UIVertex.simpleVert;
                    v.position = innerPts[i];
                    v.color = fillCol;
                    vh.AddVert(v);
                }
                for (int i = 0; i < totalPts; i++)
                {
                    int next = (i + 1) % totalPts;
                    vh.AddTriangle(centerIdx, innerStart + i, innerStart + next);
                }

                // 2. Viền bo tròn khép kín
                int ringStart = vh.currentVertCount;
                for (int i = 0; i < totalPts; i++)
                {
                    UIVertex vIn = UIVertex.simpleVert;
                    vIn.position = innerPts[i];
                    vIn.color = borderCol;
                    vh.AddVert(vIn);

                    UIVertex vOut = UIVertex.simpleVert;
                    vOut.position = outerPts[i];
                    vOut.color = borderCol;
                    vh.AddVert(vOut);
                }
                for (int i = 0; i < totalPts; i++)
                {
                    int next = (i + 1) % totalPts;
                    int i0 = ringStart + i * 2;
                    int i1 = ringStart + i * 2 + 1;
                    int n0 = ringStart + next * 2;
                    int n1 = ringStart + next * 2 + 1;
                    vh.AddTriangle(i0, i1, n1);
                    vh.AddTriangle(i0, n1, n0);
                }
            }

            private static void FillRoundedRectPerimeter(Rect r, float rad, int stepsPerCorner, Vector2[] outPts)
            {
                Vector2[] centers =
                {
                    new Vector2(r.xMax - rad, r.yMax - rad), // Top-Right (0..90 deg)
                    new Vector2(r.xMin + rad, r.yMax - rad), // Top-Left (90..180 deg)
                    new Vector2(r.xMin + rad, r.yMin + rad), // Bottom-Left (180..270 deg)
                    new Vector2(r.xMax - rad, r.yMin + rad)  // Bottom-Right (270..360 deg)
                };

                int idx = 0;
                for (int c = 0; c < 4; c++)
                {
                    float startAng = c * Mathf.PI * 0.5f;
                    for (int s = 0; s < stepsPerCorner; s++)
                    {
                        float t = s / (float)(stepsPerCorner - 1);
                        float ang = startAng + t * (Mathf.PI * 0.5f);
                        outPts[idx++] = new Vector2(
                            centers[c].x + Mathf.Cos(ang) * rad,
                            centers[c].y + Mathf.Sin(ang) * rad);
                    }
                }
            }

            private static void AddFilledDisc(VertexHelper vh, Vector2 c, float radius, int segs, Color32 col)
            {
                int centerIdx = vh.currentVertCount;
                UIVertex vc = UIVertex.simpleVert;
                vc.position = c;
                vc.color = col;
                vh.AddVert(vc);

                int rimStart = vh.currentVertCount;
                for (int i = 0; i < segs; i++)
                {
                    float ang = (i / (float)segs) * Mathf.PI * 2f;
                    UIVertex vp = UIVertex.simpleVert;
                    vp.position = new Vector2(c.x + Mathf.Cos(ang) * radius, c.y + Mathf.Sin(ang) * radius);
                    vp.color = col;
                    vh.AddVert(vp);
                }
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    vh.AddTriangle(centerIdx, rimStart + i, rimStart + next);
                }
            }

            private static void AddRing(VertexHelper vh, Vector2 c, float rIn, float rOut, int segs, Color32 col)
            {
                int start = vh.currentVertCount;
                for (int i = 0; i < segs; i++)
                {
                    float ang = (i / (float)segs) * Mathf.PI * 2f;
                    float cs = Mathf.Cos(ang);
                    float sn = Mathf.Sin(ang);

                    UIVertex vIn = UIVertex.simpleVert;
                    vIn.position = new Vector2(c.x + cs * rIn, c.y + sn * rIn);
                    vIn.color = col;
                    vh.AddVert(vIn);

                    UIVertex vOut = UIVertex.simpleVert;
                    vOut.position = new Vector2(c.x + cs * rOut, c.y + sn * rOut);
                    vOut.color = col;
                    vh.AddVert(vOut);
                }
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int i0 = start + i * 2;
                    int i1 = start + i * 2 + 1;
                    int n0 = start + next * 2;
                    int n1 = start + next * 2 + 1;
                    vh.AddTriangle(i0, i1, n1);
                    vh.AddTriangle(i0, n1, n0);
                }
            }

            private void DrawStarburst(VertexHelper vh, Rect r)
            {
                Vector2 c = r.center;
                float maxR = Mathf.Min(r.width, r.height) * 0.5f;
                float innerR = maxR * 0.62f;
                const int points = 24;

                int cIdx = vh.currentVertCount;
                UIVertex vc = UIVertex.simpleVert;
                vc.position = c;
                vc.color = new Color32(
                    (byte)(color.r * 255f),
                    (byte)(color.g * 255f),
                    (byte)(color.b * 255f),
                    (byte)(color.a * 170f));
                vh.AddVert(vc);

                Color32 edgeCol = new Color32(
                    (byte)(color.r * 255f),
                    (byte)(color.g * 255f),
                    (byte)(color.b * 255f),
                    0);
                Color32 tipCol = new Color32(
                    (byte)(color.r * 255f),
                    (byte)(color.g * 255f),
                    (byte)(color.b * 255f),
                    (byte)(color.a * 95f));

                int ringStart = vh.currentVertCount;
                for (int i = 0; i < points; i++)
                {
                    float ang = (i / (float)points) * Mathf.PI * 2f;
                    bool isTip = (i % 2 == 0);
                    float rad = isTip ? maxR : innerR;
                    UIVertex vp = UIVertex.simpleVert;
                    vp.position = new Vector2(c.x + Mathf.Cos(ang) * rad, c.y + Mathf.Sin(ang) * rad);
                    vp.color = isTip ? tipCol : edgeCol;
                    vh.AddVert(vp);
                }

                for (int i = 0; i < points; i++)
                {
                    int next = (i + 1) % points;
                    vh.AddTriangle(cIdx, ringStart + i, ringStart + next);
                }
            }
        }

        private sealed class EmojiBubbleRuntime
        {
            public RectTransform RootRt;
            public CanvasGroup Group;
            public EmojiMedallionGraphic PointerArrow;
            public EmojiMedallionGraphic MedallionGraphic;
            public EmojiMedallionGraphic AuraGraphic;
            public RectTransform AuraRt;
            public Image IconImage;
            public RectTransform IconRt;
            public EmojiMedallionGraphic TagRibbonBg;
            public Text TagText;

            public bool Active;
            public int EmojiId;
            public float Elapsed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            cachedFrames = null;
            cachedLauncherSprite = null;
            lastInteractionFrame = -10;
        }

        public static OnlineEmojiController Ensure(GameplayHUD hud)
        {
            if (hud == null) return null;

            Canvas canvas = FindGameplayCanvas(hud);
            if (canvas == null) return null;

            var existing = canvas.GetComponentInChildren<OnlineEmojiController>(true);
            if (existing == null) existing = hud.GetComponentInChildren<OnlineEmojiController>(true);

            if (existing != null)
            {
                Instance = existing;
                existing.parentHud = hud;
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                existing.EnsureLauncherAttachedToVoiceBar();
                existing.EnsureBubblesAttached();
                existing.RefreshModeVisibility();
                return existing;
            }

            var go = new GameObject("OnlineEmojiSystem", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);

            var ctrl = go.AddComponent<OnlineEmojiController>();
            Instance = ctrl;
            ctrl.parentHud = hud;
            ctrl.BuildUI();
            ctrl.RefreshModeVisibility();
            return ctrl;
        }

        private static Canvas FindGameplayCanvas(GameplayHUD hud)
        {
            if (hud != null)
            {
                var c = hud.GetComponentInParent<Canvas>();
                if (c != null) return c.rootCanvas;
            }
            foreach (var c in FindObjectsOfType<Canvas>(true))
            {
                if (c != null && c.name == "GameplayCanvas") return c;
            }
            return FindObjectOfType<Canvas>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureSpritesLoaded();
            EnsureAudioReady();
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            HandleLanguageChanged();
        }

        private void OnDisable()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            if (launcherRootRt != null) Destroy(launcherRootRt.gameObject);
            if (Instance == this) Instance = null;
        }

        private static Texture2D LoadTextureWithFallback(string resourceSubPath, string diskRelativePath)
        {
            Texture2D tex = Resources.Load<Texture2D>(resourceSubPath);
            if (tex == null)
            {
                string diskPath = Path.Combine(Application.dataPath, diskRelativePath);
                if (File.Exists(diskPath))
                {
                    byte[] raw = File.ReadAllBytes(diskPath);
                    var runtimeTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    runtimeTex.LoadImage(raw);
                    runtimeTex.filterMode = FilterMode.Bilinear;
                    runtimeTex.wrapMode = TextureWrapMode.Clamp;
                    tex = runtimeTex;
                }
            }
            return tex;
        }

        private static void EnsureSpritesLoaded()
        {
            if (cachedLauncherSprite == null)
            {
                Texture2D launcherTex = LoadTextureWithFallback("Emojis/EmojiLauncherIcon", "Resources/Emojis/EmojiLauncherIcon.png");
                if (launcherTex != null)
                {
                    cachedLauncherSprite = Sprite.Create(
                        launcherTex,
                        new Rect(0f, 0f, launcherTex.width, launcherTex.height),
                        new Vector2(0.5f, 0.5f),
                        100f);
                }
            }

            if (cachedFrames != null && cachedFrames.Length == EmojiCount && cachedFrames[0] != null && cachedFrames[0].Length == FramesPerEmoji)
                return;

            cachedFrames = new Sprite[EmojiCount][];
            for (int e = 0; e < EmojiCount; e++)
            {
                Texture2D sheetTex = LoadTextureWithFallback($"Emojis/EmojiSheet_{e}", $"Resources/Emojis/EmojiSheet_{e}.png");
                cachedFrames[e] = new Sprite[FramesPerEmoji];
                if (sheetTex == null) continue;

                int cellW = sheetTex.width / SheetCols;
                int cellH = sheetTex.height / SheetRows;
                for (int f = 0; f < FramesPerEmoji; f++)
                {
                    int col = f % SheetCols;
                    int row = f / SheetCols;
                    int yBottom = sheetTex.height - (row + 1) * cellH;
                    Rect rect = new Rect(col * cellW, yBottom, cellW, cellH);
                    cachedFrames[e][f] = Sprite.Create(sheetTex, rect, new Vector2(0.5f, 0.5f), 100f);
                }
            }
        }

        private void EnsureAudioReady()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.GetComponent<AudioSource>();
                if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
                sfxSource.volume = 0.55f;
            }

            if (cachedSfxClips != null && cachedSfxClips.Length == EmojiCount && cachedSfxClips[0] != null)
                return;

            cachedSfxClips = new AudioClip[EmojiCount];
            for (int i = 0; i < EmojiCount; i++)
            {
                cachedSfxClips[i] = SynthesizeEmojiSfx(i);
            }
        }

        private static AudioClip SynthesizeEmojiSfx(int emojiId)
        {
            const int sampleRate = 22050;
            float duration = 0.34f;
            int count = Mathf.CeilToInt(sampleRate * duration);
            float[] data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)sampleRate;
                float p = t / duration;
                float env = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * (1f - p * 0.45f);
                float sample;

                switch (emojiId)
                {
                    case 0: // LIKE: Hợp âm 2 nốt tăng tươi sáng (C5 -> G5)
                        {
                            float freq = (p < 0.45f) ? 523.25f : 783.99f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f
                                   + Mathf.Sin(4f * Mathf.PI * freq * t) * 0.25f;
                            break;
                        }
                    case 1: // DISLIKE: Nốt trầm đi xuống (E4 -> Bb3)
                        {
                            float freq = Mathf.Lerp(330f, 220f, p);
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f
                                   + Mathf.Sin(6f * Mathf.PI * freq * t) * 0.2f;
                            break;
                        }
                    case 2: // HAHA: 3 nhịp nảy vui nhộn
                        {
                            float step = Mathf.Floor(p * 3f);
                            float freq = 580f + step * 110f + Mathf.Sin(p * 36f) * 45f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
                            break;
                        }
                    case 3: // COOL: Giai điệu lấp lánh kính râm (Arpeggio A5 -> C#6 -> E6)
                        {
                            float freq = (p < 0.33f) ? 659.25f : ((p < 0.66f) ? 830.6f : 987.77f);
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
                            break;
                        }
                    case 4: // SHOCK: Rung cao độ bất ngờ
                        {
                            float freq = Mathf.Lerp(420f, 880f, Mathf.PingPong(p * 2f, 1f));
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;
                            break;
                        }
                    case 5: // ANGRY: Âm trầm gầm nhẹ + hơi khói
                        {
                            float freq = 165f + Mathf.Sin(p * 50f) * 25f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
                            break;
                        }
                    case 6: // CLAP: 2 nhịp vỗ + chuông khen thưởng
                        {
                            float freq = (p < 0.5f) ? 698.46f : 1046.5f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.65f;
                            break;
                        }
                    case 7: // CRY: Giai điệu uốn lượn đi xuống
                        {
                            float freq = Mathf.Lerp(620f, 340f, p) + Mathf.Sin(p * 28f) * 22f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
                            break;
                        }
                    case 8: // FIRE: Âm bùng năng lượng đi lên
                        {
                            float freq = Mathf.Lerp(280f, 920f, p * p);
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f;
                            break;
                        }
                    default: // THINK: Sonar ping 200 IQ
                        {
                            float freq = 880f;
                            sample = Mathf.Sin(2f * Mathf.PI * freq * t) * Mathf.Exp(-p * 3.5f) * 0.8f;
                            break;
                        }
                }

                data[i] = Mathf.Clamp(sample * env * 0.45f, -0.9f, 0.9f);
            }

            AudioClip clip = AudioClip.Create($"EmojiSfx_{emojiId}", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void BuildUI()
        {
            EnsureSpritesLoaded();
            uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (uiFont == null) uiFont = Font.CreateDynamicFontFromOSFont("Segoe UI", 14);

            var selfRt = (RectTransform)transform;
            selfRt.anchorMin = Vector2.zero;
            selfRt.anchorMax = Vector2.one;
            selfRt.offsetMin = Vector2.zero;
            selfRt.offsetMax = Vector2.zero;

            BuildMinimalistLauncherAndTray();
            EnsureLauncherAttachedToVoiceBar();
            EnsureBubblesAttached();
            HandleLanguageChanged();
        }

        /// <summary>
        /// Dựng Nút Icon Emoji Tối Giản [E] (46x46 px) và Khay Chọn 10 Emoji (5x2 bo góc)
        /// nằm ngay phía trên nút [M] MIC & [N] LOA của OnlineChatUI.
        /// </summary>
        private void BuildMinimalistLauncherAndTray()
        {
            var rootGo = new GameObject("EmojiVoiceTopLauncher", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.layer = gameObject.layer;
            rootGo.transform.SetParent(transform, false);
            launcherRootRt = (RectTransform)rootGo.transform;
            launcherRootRt.anchorMin = new Vector2(0f, 1f);
            launcherRootRt.anchorMax = new Vector2(0f, 1f);
            launcherRootRt.pivot = new Vector2(0f, 0f);
            launcherRootRt.anchoredPosition = new Vector2(0f, 6f);
            launcherRootRt.sizeDelta = new Vector2(46f, 46f);

            launcherCanvasGroup = rootGo.GetComponent<CanvasGroup>();

            // 1. Vòng hào quang nhịp thở nhẹ phía sau nút Icon Emoji tối giản
            var glowGo = new GameObject("PulseRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            glowGo.layer = gameObject.layer;
            glowGo.transform.SetParent(launcherRootRt, false);
            var glowRt = (RectTransform)glowGo.transform;
            glowRt.anchorMin = new Vector2(0.5f, 0.5f);
            glowRt.anchorMax = new Vector2(0.5f, 0.5f);
            glowRt.pivot = new Vector2(0.5f, 0.5f);
            glowRt.anchoredPosition = Vector2.zero;
            glowRt.sizeDelta = new Vector2(48f, 48f);

            emojiBtnGlowRing = glowGo.GetComponent<EmojiMedallionGraphic>();
            emojiBtnGlowRing.Kind = EmojiMedallionGraphic.ShapeKind.DoubleRingMedallion;
            emojiBtnGlowRing.color = new Color(0.05f, 0.10f, 0.20f, 0.01f);
            emojiBtnGlowRing.OuterRingColor = new Color(1f, 0.84f, 0.25f, 0.45f);
            emojiBtnGlowRing.InnerRingColor = new Color(0f, 0.92f, 1f, 0.35f);
            emojiBtnGlowRing.OuterThickness = 2.0f;
            emojiBtnGlowRing.InnerThickness = 1.2f;
            emojiBtnGlowRing.raycastTarget = false;

            // 2. Nút Icon Emoji Tối Giản (Minimalist Glowing Smiley-Wink Medallion)
            var btnGo = new GameObject("BtnEmojiMinimal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.layer = gameObject.layer;
            btnGo.transform.SetParent(launcherRootRt, false);
            emojiBtnIconRt = (RectTransform)btnGo.transform;
            emojiBtnIconRt.anchorMin = new Vector2(0.5f, 0.5f);
            emojiBtnIconRt.anchorMax = new Vector2(0.5f, 0.5f);
            emojiBtnIconRt.pivot = new Vector2(0.5f, 0.5f);
            emojiBtnIconRt.anchoredPosition = Vector2.zero;
            emojiBtnIconRt.sizeDelta = new Vector2(46f, 46f);

            emojiBtnIconImage = btnGo.GetComponent<Image>();
            emojiBtnIconImage.sprite = cachedLauncherSprite;
            emojiBtnIconImage.preserveAspect = true;
            emojiBtnIconImage.raycastTarget = true;

            emojiToggleButton = btnGo.GetComponent<Button>();
            emojiToggleButton.targetGraphic = emojiBtnIconImage;
            var nav = emojiToggleButton.navigation;
            nav.mode = Navigation.Mode.None;
            emojiToggleButton.navigation = nav;
            var cb = emojiToggleButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            cb.pressedColor = new Color(0.92f, 0.88f, 0.72f, 1f);
            cb.fadeDuration = 0.06f;
            emojiToggleButton.colors = cb;
            emojiToggleButton.onClick.AddListener(ToggleTray);

            // 3. Huy hiệu phím tắt [E] nhỏ gọn đính ở góc dưới-phải của nút Icon
            var badgeGo = new GameObject("KeyBadgeE", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            badgeGo.layer = gameObject.layer;
            badgeGo.transform.SetParent(emojiBtnIconRt, false);
            var bRt = (RectTransform)badgeGo.transform;
            bRt.anchorMin = new Vector2(1f, 0f);
            bRt.anchorMax = new Vector2(1f, 0f);
            bRt.pivot = new Vector2(1f, 0f);
            bRt.anchoredPosition = new Vector2(2f, -1f);
            bRt.sizeDelta = new Vector2(19f, 16f);

            var bGraphic = badgeGo.GetComponent<EmojiMedallionGraphic>();
            bGraphic.Kind = EmojiMedallionGraphic.ShapeKind.RoundedBox;
            bGraphic.CornerRadius = 5f;
            bGraphic.OuterThickness = 1.5f;
            bGraphic.color = GoldRingColor;
            bGraphic.OuterRingColor = new Color(0.05f, 0.08f, 0.14f, 1f);
            bGraphic.raycastTarget = false;

            var bTxtGo = new GameObject("KeyText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            bTxtGo.layer = gameObject.layer;
            bTxtGo.transform.SetParent(bRt, false);
            var bTxtRt = (RectTransform)bTxtGo.transform;
            bTxtRt.anchorMin = Vector2.zero;
            bTxtRt.anchorMax = Vector2.one;
            bTxtRt.offsetMin = Vector2.zero;
            bTxtRt.offsetMax = Vector2.zero;

            var bTxt = bTxtGo.GetComponent<Text>();
            bTxt.font = uiFont;
            bTxt.text = "E";
            bTxt.fontSize = 11;
            bTxt.fontStyle = FontStyle.Bold;
            bTxt.alignment = TextAnchor.MiddleCenter;
            bTxt.color = new Color(0.05f, 0.08f, 0.14f, 1f);
            bTxt.raycastTarget = false;

            // 4. Khay Chọn 10 Emoji (5x2 bo tròn hiện đại) bung ngay phía trên nút Icon [E]
            BuildEmojiPickerTray();
        }

        private void BuildEmojiPickerTray()
        {
            const float trayW = 416f;
            const float trayH = 184f;

            var trayGo = new GameObject("EmojiPickerTray", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic), typeof(CanvasGroup));
            trayGo.layer = gameObject.layer;
            trayGo.transform.SetParent(launcherRootRt, false);
            trayRt = (RectTransform)trayGo.transform;
            trayRt.anchorMin = new Vector2(0f, 1f);
            trayRt.anchorMax = new Vector2(0f, 1f);
            trayRt.pivot = new Vector2(0f, 0f);
            trayRt.anchoredPosition = new Vector2(0f, 8f);
            trayRt.sizeDelta = new Vector2(trayW, trayH);

            trayCanvasGroup = trayGo.GetComponent<CanvasGroup>();
            trayCanvasGroup.alpha = 0f;
            trayCanvasGroup.interactable = false;
            trayCanvasGroup.blocksRaycasts = false;

            var trayBg = trayGo.GetComponent<EmojiMedallionGraphic>();
            trayBg.Kind = EmojiMedallionGraphic.ShapeKind.RoundedBox;
            trayBg.CornerRadius = 18f;
            trayBg.OuterThickness = 2.4f;
            trayBg.color = new Color(0.05f, 0.10f, 0.19f, 0.96f);
            trayBg.OuterRingColor = new Color(0.00f, 0.88f, 1.00f, 0.85f);
            trayBg.raycastTarget = true;

            const int cols = 5;
            const float cellW = 74f;
            const float cellH = 78f;
            const float gapX = 6f;
            const float gapY = 8f;
            const float startX = 11f;
            const float startY = -10f;

            for (int i = 0; i < EmojiCount; i++)
            {
                int emojiIndex = i;
                int c = i % cols;
                int r = i / cols;

                var cellGo = new GameObject($"EmojiCell_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic), typeof(Button));
                cellGo.layer = gameObject.layer;
                cellGo.transform.SetParent(trayRt, false);
                var crt = (RectTransform)cellGo.transform;
                crt.anchorMin = new Vector2(0f, 1f);
                crt.anchorMax = new Vector2(0f, 1f);
                crt.pivot = new Vector2(0f, 1f);
                crt.anchoredPosition = new Vector2(startX + c * (cellW + gapX), startY - r * (cellH + gapY));
                crt.sizeDelta = new Vector2(cellW, cellH);

                var cellGraphic = cellGo.GetComponent<EmojiMedallionGraphic>();
                cellGraphic.Kind = EmojiMedallionGraphic.ShapeKind.RoundedBox;
                cellGraphic.CornerRadius = 13f;
                cellGraphic.OuterThickness = 1.8f;
                cellGraphic.color = new Color(0.09f, 0.16f, 0.28f, 0.96f);
                cellGraphic.OuterRingColor = new Color(AccentColors[i].r, AccentColors[i].g, AccentColors[i].b, 0.72f);
                cellGraphic.raycastTarget = true;

                var btn = cellGo.GetComponent<Button>();
                btn.targetGraphic = cellGraphic;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1.22f, 1.22f, 1.22f, 1f);
                colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
                btn.colors = colors;
                btn.onClick.AddListener(() => TriggerEmojiFromUser(emojiIndex));

                // Hình Linh Vật Bi Số 8 V2 ở giữa ô
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.layer = gameObject.layer;
                iconGo.transform.SetParent(crt, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.anchoredPosition = new Vector2(0f, 6f);
                irt.sizeDelta = new Vector2(54f, 54f);

                var iconImg = iconGo.GetComponent<Image>();
                iconImg.preserveAspect = false;
                iconImg.raycastTarget = false;
                if (cachedFrames != null && cachedFrames[i] != null && cachedFrames[i].Length > 6)
                {
                    iconImg.sprite = cachedFrames[i][6];
                }
                trayIconImages[i] = iconImg;

                // Huy hiệu phím tắt [1]..[0] ở góc trên-trái của ô
                var keyGo = new GameObject("KeyHint", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                keyGo.layer = gameObject.layer;
                keyGo.transform.SetParent(crt, false);
                var krt = (RectTransform)keyGo.transform;
                krt.anchorMin = new Vector2(0f, 1f);
                krt.anchorMax = new Vector2(0f, 1f);
                krt.pivot = new Vector2(0f, 1f);
                krt.anchoredPosition = new Vector2(5f, -3f);
                krt.sizeDelta = new Vector2(22f, 16f);

                var keyTxt = keyGo.GetComponent<Text>();
                keyTxt.font = uiFont;
                keyTxt.fontSize = 10;
                keyTxt.fontStyle = FontStyle.Bold;
                keyTxt.alignment = TextAnchor.UpperLeft;
                keyTxt.color = new Color(1f, 0.86f, 0.28f, 0.95f);
                keyTxt.text = KeyHints[i];
                keyTxt.raycastTarget = false;

                // Nhãn cảm xúc bên dưới ô
                var tagGo = new GameObject("Tag", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                tagGo.layer = gameObject.layer;
                tagGo.transform.SetParent(crt, false);
                var trt = (RectTransform)tagGo.transform;
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, 3f);
                trt.sizeDelta = new Vector2(-6f, 15f);

                var tagTxt = tagGo.GetComponent<Text>();
                tagTxt.font = uiFont;
                tagTxt.fontSize = 10;
                tagTxt.fontStyle = FontStyle.Bold;
                tagTxt.alignment = TextAnchor.MiddleCenter;
                tagTxt.color = AccentColors[i];
                tagTxt.resizeTextForBestFit = true;
                tagTxt.resizeTextMinSize = 8;
                tagTxt.resizeTextMaxSize = 10;
                tagTxt.raycastTarget = false;
                trayTagTexts[i] = tagTxt;
            }
        }

        /// <summary>
        /// Gắn nút Icon Emoji Tối Giản [E] trực tiếp lên phía trên thanh VoiceControlBar ([M] MIC & [N] LOA)
        /// để nút luôn nằm ngay trên nút Mic/Loa và tự động trượt lên/xuống khi khung Chat [C] đóng/mở.
        /// </summary>
        private void EnsureLauncherAttachedToVoiceBar()
        {
            if (launcherRootRt == null) return;

            var chat = OnlineChatUI.Instance;
            if (chat == null && parentHud != null)
            {
                chat = OnlineChatUI.Ensure(parentHud);
            }

            if (chat != null && chat.VoiceBarRect != null)
            {
                if (launcherRootRt.parent != chat.VoiceBarRect)
                {
                    launcherRootRt.SetParent(chat.VoiceBarRect, false);
                    launcherRootRt.anchorMin = new Vector2(0f, 1f);
                    launcherRootRt.anchorMax = new Vector2(0f, 1f);
                    launcherRootRt.pivot = new Vector2(0f, 0f);
                    launcherRootRt.anchoredPosition = new Vector2(2f, 6f);
                    launcherRootRt.sizeDelta = new Vector2(46f, 46f);
                    launcherRootRt.localScale = Vector3.one;
                }
            }
        }

        private void EnsureBubblesAttached()
        {
            if (parentHud == null) return;

            if (leftBubble == null && parentHud.PlayerLeftPanel != null)
            {
                leftBubble = CreateMedallionBubbleUnderCard(parentHud.PlayerLeftPanel.transform as RectTransform, true);
            }
            if (rightBubble == null && parentHud.PlayerRightPanel != null)
            {
                rightBubble = CreateMedallionBubbleUnderCard(parentHud.PlayerRightPanel.transform as RectTransform, false);
            }
        }

        /// <summary>
        /// Tạo Khung Hiển Thị Emoji Dưới Thẻ Tên theo Mẫu C:
        /// Huy Hiệu Khiên Tròn Vàng Hoàng Kim Đúp (Circular Championship Medallion) + Băng Chữ Bo Tròn (Capsule Ribbon).
        /// </summary>
        private EmojiBubbleRuntime CreateMedallionBubbleUnderCard(RectTransform cardRt, bool isLeftCard)
        {
            if (cardRt == null) return null;

            Transform existing = cardRt.Find("EmojiReactionBubble");
            if (existing != null) Destroy(existing.gameObject);

            var bubble = new EmojiBubbleRuntime();

            var rootGo = new GameObject("EmojiReactionBubble", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.layer = cardRt.gameObject.layer;
            rootGo.transform.SetParent(cardRt, false);

            var rt = (RectTransform)rootGo.transform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(isLeftCard ? -54f : 54f, -6f);
            rt.sizeDelta = new Vector2(BubbleTotalWidth, BubbleTotalHeight);
            rt.localScale = Vector3.zero;

            bubble.RootRt = rt;
            bubble.Group = rootGo.GetComponent<CanvasGroup>();
            bubble.Group.alpha = 0f;
            bubble.Group.blocksRaycasts = false;
            bubble.Group.interactable = false;

            // 1. Mũi tên tam giác Vàng Hoàng Kim trên đỉnh trỏ lên Thẻ Tên Người Chơi
            var arrowGo = new GameObject("GoldPointerArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            arrowGo.layer = rootGo.layer;
            arrowGo.transform.SetParent(rt, false);
            var art = (RectTransform)arrowGo.transform;
            art.anchorMin = new Vector2(0.5f, 1f);
            art.anchorMax = new Vector2(0.5f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = new Vector2(0f, 0f);
            art.sizeDelta = new Vector2(22f, 12f);

            bubble.PointerArrow = arrowGo.GetComponent<EmojiMedallionGraphic>();
            bubble.PointerArrow.Kind = EmojiMedallionGraphic.ShapeKind.PointerArrow;
            bubble.PointerArrow.color = GoldRingColor;
            bubble.PointerArrow.raycastTarget = false;

            // 2. Khung Huy Hiệu Khiên Tròn Vàng Gold Đúp (Mẫu C - Double Ring Circular Medallion)
            var medGo = new GameObject("ChampionshipMedallion", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            medGo.layer = rootGo.layer;
            medGo.transform.SetParent(rt, false);
            var medRt = (RectTransform)medGo.transform;
            medRt.anchorMin = new Vector2(0.5f, 1f);
            medRt.anchorMax = new Vector2(0.5f, 1f);
            medRt.pivot = new Vector2(0.5f, 1f);
            medRt.anchoredPosition = new Vector2(0f, -11f);
            medRt.sizeDelta = new Vector2(MedallionSize, MedallionSize);

            bubble.MedallionGraphic = medGo.GetComponent<EmojiMedallionGraphic>();
            bubble.MedallionGraphic.Kind = EmojiMedallionGraphic.ShapeKind.DoubleRingMedallion;
            bubble.MedallionGraphic.color = MedallionFillColor;
            bubble.MedallionGraphic.OuterRingColor = GoldRingColor;
            bubble.MedallionGraphic.OuterThickness = 4.2f;
            bubble.MedallionGraphic.InnerThickness = 2.4f;
            bubble.MedallionGraphic.raycastTarget = false;

            // 3. Hào quang 12 cánh xoay tròn mềm mại bên trong lòng Khiên Tròn
            var auraGo = new GameObject("StarburstAura", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            auraGo.layer = rootGo.layer;
            auraGo.transform.SetParent(medRt, false);
            var auraRt = (RectTransform)auraGo.transform;
            auraRt.anchorMin = new Vector2(0.5f, 0.5f);
            auraRt.anchorMax = new Vector2(0.5f, 0.5f);
            auraRt.pivot = new Vector2(0.5f, 0.5f);
            auraRt.anchoredPosition = new Vector2(0f, 2f);
            auraRt.sizeDelta = new Vector2(130f, 130f);

            bubble.AuraRt = auraRt;
            bubble.AuraGraphic = auraGo.GetComponent<EmojiMedallionGraphic>();
            bubble.AuraGraphic.Kind = EmojiMedallionGraphic.ShapeKind.StarburstAura;
            bubble.AuraGraphic.raycastTarget = false;

            // 4. Hình Linh Vật Bi Số 8 Động V2 Cỡ Lớn (138x138 px)
            var iconGo = new GameObject("MascotIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGo.layer = rootGo.layer;
            iconGo.transform.SetParent(medRt, false);
            var irt = (RectTransform)iconGo.transform;
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(0f, 4f);
            irt.sizeDelta = new Vector2(138f, 138f);

            bubble.IconRt = irt;
            bubble.IconImage = iconGo.GetComponent<Image>();
            bubble.IconImage.preserveAspect = false;
            bubble.IconImage.raycastTarget = false;

            // 5. Băng Chữ Bo Tròn (Capsule Ribbon) viền Vàng Gold nằm ôm dưới đáy Huy Hiệu Tròn
            var pillGo = new GameObject("CapsuleTagRibbon", typeof(RectTransform), typeof(CanvasRenderer), typeof(EmojiMedallionGraphic));
            pillGo.layer = rootGo.layer;
            pillGo.transform.SetParent(rt, false);
            var prt = (RectTransform)pillGo.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 2f);
            prt.sizeDelta = new Vector2(132f, 30f);

            bubble.TagRibbonBg = pillGo.GetComponent<EmojiMedallionGraphic>();
            bubble.TagRibbonBg.Kind = EmojiMedallionGraphic.ShapeKind.CapsuleRibbon;
            bubble.TagRibbonBg.color = CapsuleFillColor;
            bubble.TagRibbonBg.OuterRingColor = GoldRingColor;
            bubble.TagRibbonBg.OuterThickness = 2.2f;
            bubble.TagRibbonBg.raycastTarget = false;

            var txtGo = new GameObject("TagText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Shadow));
            txtGo.layer = rootGo.layer;
            txtGo.transform.SetParent(prt, false);
            var trt = (RectTransform)txtGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 1f);
            trt.offsetMax = new Vector2(-8f, -1f);

            bubble.TagText = txtGo.GetComponent<Text>();
            bubble.TagText.font = uiFont;
            bubble.TagText.fontSize = 14;
            bubble.TagText.fontStyle = FontStyle.Bold;
            bubble.TagText.alignment = TextAnchor.MiddleCenter;
            bubble.TagText.resizeTextForBestFit = true;
            bubble.TagText.resizeTextMinSize = 10;
            bubble.TagText.resizeTextMaxSize = 14;
            bubble.TagText.raycastTarget = false;

            var shadow = txtGo.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);

            return bubble;
        }

        public void HandleLanguageChanged()
        {
            for (int i = 0; i < EmojiCount; i++)
            {
                if (trayTagTexts[i] != null)
                {
                    trayTagTexts[i].text = LocalizationManager.Get($"emoji_tag_{i}");
                }
            }

            if (leftBubble != null && leftBubble.Active && leftBubble.TagText != null)
            {
                leftBubble.TagText.text = LocalizationManager.Get($"emoji_tag_{leftBubble.EmojiId}");
            }
            if (rightBubble != null && rightBubble.Active && rightBubble.TagText != null)
            {
                rightBubble.TagText.text = LocalizationManager.Get($"emoji_tag_{rightBubble.EmojiId}");
            }
        }

        public void RefreshModeVisibility()
        {
            bool online = (GameSession.Instance != null && GameSession.Instance.CurrentGameMode == GameMode.OnlinePvP)
                          || PhotonNetwork.InRoom
                          || OnlineChatUI.ForceEnableForTest;

            isVisibleOnline = online;
            if (launcherCanvasGroup != null)
            {
                launcherCanvasGroup.alpha = online ? 1f : 0f;
                launcherCanvasGroup.interactable = online;
                launcherCanvasGroup.blocksRaycasts = online;
            }

            if (!online && isTrayOpen)
            {
                SetTrayOpen(false);
            }
        }

        public void ToggleTray()
        {
            if (!isVisibleOnline) return;
            lastInteractionFrame = Time.frameCount;
            SetTrayOpen(!isTrayOpen);
        }

        public void SetTrayOpen(bool open)
        {
            if (isTrayOpen != open)
            {
                lastInteractionFrame = Time.frameCount;
            }
            isTrayOpen = open;
            if (trayCanvasGroup != null)
            {
                trayCanvasGroup.interactable = open;
                trayCanvasGroup.blocksRaycasts = open;
            }
        }

        private static bool IsPointerInsideRect(RectTransform rt)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, UnityEngine.Input.mousePosition, null);
        }

        private void Update()
        {
            RefreshModeVisibility();
            if (!isVisibleOnline) return;

            EnsureLauncherAttachedToVoiceBar();
            EnsureBubblesAttached();

            if (isTrayOpen)
            {
                lastInteractionFrame = Time.frameCount;

                // Nếu người chơi click chuột ra ngoài Khay Emoji và ngoài nút Icon [E] -> đóng khay và chặn bắn ở frame này
                if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetMouseButtonDown(1))
                {
                    if (!IsPointerInsideRect(trayRt) && !IsPointerInsideRect(emojiBtnIconRt))
                    {
                        SetTrayOpen(false);
                        return;
                    }
                }
            }

            // 1. Phím tắt [E] bật/tắt Khay Emoji và các phím [1]..[9], [0] khi Khay đang mở
            if (!OnlineChatUI.IsTyping && !HudModalState.IsModalOpen)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.E))
                {
                    ToggleTray();
                }
                else if (isTrayOpen)
                {
                    for (int i = 0; i < EmojiCount; i++)
                    {
                        if (UnityEngine.Input.GetKeyDown(AlphaKeys[i]) || UnityEngine.Input.GetKeyDown(KeypadKeys[i]))
                        {
                            TriggerEmojiFromUser(i);
                            break;
                        }
                    }

                    if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    {
                        SetTrayOpen(false);
                    }
                }
            }

            // 2. Hiệu ứng nhịp thở nhẹ nhàng cho Nút Icon Emoji Tối Giản [E]
            if (emojiBtnGlowRing != null)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.8f);
                float ringScale = isTrayOpen ? 1.12f : Mathf.Lerp(1.00f, 1.09f, pulse);
                emojiBtnGlowRing.rectTransform.localScale = new Vector3(ringScale, ringScale, 1f);
                emojiBtnGlowRing.OuterRingColor = isTrayOpen
                    ? new Color(0f, 0.95f, 1f, 0.95f)
                    : new Color(1f, 0.84f, 0.25f, Mathf.Lerp(0.32f, 0.78f, pulse));
                emojiBtnGlowRing.SetVerticesDirty();
            }

            // 3. Hiệu ứng đóng/mở mượt mà cho Khay Chọn 10 Emoji + preview động 24 FPS trong khay
            float targetTray = isTrayOpen ? 1f : 0f;
            trayAnim01 = Mathf.MoveTowards(trayAnim01, targetTray, Time.unscaledDeltaTime * 8.0f);
            if (trayCanvasGroup != null)
            {
                trayCanvasGroup.alpha = trayAnim01;
            }
            if (trayRt != null)
            {
                float s = Mathf.Lerp(0.85f, 1f, Mathf.SmoothStep(0f, 1f, trayAnim01));
                trayRt.localScale = new Vector3(s, s, 1f);
            }

            if (trayAnim01 > 0.01f && cachedFrames != null)
            {
                int previewFrame = Mathf.FloorToInt(Time.unscaledTime * 18f) % FramesPerEmoji;
                for (int i = 0; i < EmojiCount; i++)
                {
                    if (trayIconImages[i] != null && cachedFrames[i] != null && cachedFrames[i][previewFrame] != null)
                    {
                        trayIconImages[i].sprite = cachedFrames[i][previewFrame];
                    }
                }
            }

            // 4. Cập nhật hoạt hình mượt 24-FPS + Squash & Stretch 60-FPS cho 2 Huy Hiệu dưới Thẻ Tên
            UpdateBubbleAnimation(leftBubble);
            UpdateBubbleAnimation(rightBubble);
        }

        private void UpdateBubbleAnimation(EmojiBubbleRuntime b)
        {
            if (b == null || !b.Active || b.RootRt == null) return;

            b.Elapsed += Time.unscaledDeltaTime;
            float t = b.Elapsed;

            if (t >= BubbleDisplayDuration)
            {
                b.Active = false;
                b.Group.alpha = 0f;
                b.RootRt.localScale = Vector3.zero;
                return;
            }

            // Giai đoạn 1 (0..0.26s): Elastic Pop-in mượt mà
            // Giai đoạn 2 (0.26..2.90s): Duy trì hiển thị ổn định
            // Giai đoạn 3 (2.90..3.20s): Thu nhỏ mượt mà
            float rootScale;
            float alpha;
            if (t < 0.26f)
            {
                float p = t / 0.26f;
                float easeOutBack = 1f + 2.2f * Mathf.Pow(p - 1f, 3f) + 1.2f * Mathf.Pow(p - 1f, 2f);
                rootScale = Mathf.Clamp(easeOutBack, 0f, 1.14f);
                alpha = Mathf.Clamp01(p * 1.8f);
            }
            else if (t > BubbleDisplayDuration - 0.28f)
            {
                float p = Mathf.Clamp01((BubbleDisplayDuration - t) / 0.28f);
                rootScale = Mathf.SmoothStep(0f, 1f, p);
                alpha = p;
            }
            else
            {
                rootScale = 1f;
                alpha = 1f;
            }

            b.Group.alpha = alpha;
            b.RootRt.localScale = new Vector3(rootScale, rootScale, 1f);

            // Chuyển frame 24 FPS của Sprite Sheet V2 (24 frames/chu kỳ)
            int eid = Mathf.Clamp(b.EmojiId, 0, EmojiCount - 1);
            if (cachedFrames != null && cachedFrames[eid] != null)
            {
                int frameIdx = Mathf.FloorToInt(t * FrameRate) % FramesPerEmoji;
                if (cachedFrames[eid][frameIdx] != null)
                {
                    b.IconImage.sprite = cachedFrames[eid][frameIdx];
                }
            }

            // Xoay hào quang Starburst mượt 60 FPS
            if (b.AuraRt != null)
            {
                b.AuraRt.localEulerAngles = new Vector3(0f, 0f, -t * 48f);
                float auraPulse = 1f + Mathf.Sin(t * 7f) * 0.06f;
                b.AuraRt.localScale = new Vector3(auraPulse, auraPulse, 1f);
            }

            // Hiệu ứng Squash & Stretch đàn hồi 60 FPS (bảo toàn thể tích scaleX * scaleY) + nhún nhịp nhàng
            if (b.IconRt != null)
            {
                float cycleRad = t * Mathf.PI * 2f; // Chu kỳ 1 giây khớp với 24 frames ở 24 FPS
                float squash = Mathf.Sin(cycleRad * 2f) * 0.055f;
                float sx = 1f - squash;
                float sy = 1f + squash;
                float bounceY = 4f + Mathf.Abs(Mathf.Sin(cycleRad)) * 5.5f;

                b.IconRt.localScale = new Vector3(sx, sy, 1f);
                b.IconRt.anchoredPosition = new Vector2(0f, bounceY);
                b.IconRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(cycleRad) * 3.2f);
            }
        }

        public void TriggerEmojiFromUser(int emojiId)
        {
            if (!isVisibleOnline) return;
            lastInteractionFrame = Time.frameCount;
            emojiId = Mathf.Clamp(emojiId, 0, EmojiCount - 1);

            if (Time.unscaledTime - lastSendTime < SendCooldownSeconds)
            {
                SetTrayOpen(false);
                return;
            }

            lastSendTime = Time.unscaledTime;
            SetTrayOpen(false);

            // Hiển thị ngay dưới Thẻ Tên Bên Trái (luôn là Bạn trong chế độ Online PvP)
            ShowEmojiOnCard(true, emojiId);

            // Đồng bộ qua mạng Photon tới đối thủ
            if (PhotonNetwork.InRoom)
            {
                int actorNr = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 1;
                object[] payload = { actorNr, emojiId };
                RaiseEventOptions opts = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
                PhotonNetwork.RaiseEvent(NetworkConstants.EVENT_EMOJI_REACTION, payload, opts, SendOptions.SendReliable);
            }
        }

        public void ShowEmojiOnCard(bool isLocalLeftCard, int emojiId)
        {
            EnsureBubblesAttached();
            EnsureSpritesLoaded();
            EnsureAudioReady();

            emojiId = Mathf.Clamp(emojiId, 0, EmojiCount - 1);
            EmojiBubbleRuntime target = isLocalLeftCard ? leftBubble : rightBubble;
            if (target == null) return;

            Color accent = AccentColors[emojiId];
            target.EmojiId = emojiId;
            target.Elapsed = 0f;
            target.Active = true;

            if (target.MedallionGraphic != null)
            {
                target.MedallionGraphic.InnerRingColor = accent;
                target.MedallionGraphic.SetVerticesDirty();
            }
            if (target.PointerArrow != null)
            {
                target.PointerArrow.color = GoldRingColor;
                target.PointerArrow.SetVerticesDirty();
            }
            if (target.AuraGraphic != null)
            {
                target.AuraGraphic.color = new Color(accent.r, accent.g, accent.b, 0.52f);
                target.AuraGraphic.SetVerticesDirty();
            }
            if (target.TagText != null)
            {
                target.TagText.text = LocalizationManager.Get($"emoji_tag_{emojiId}");
                target.TagText.color = accent;
            }
            if (target.IconImage != null && cachedFrames != null && cachedFrames[emojiId] != null)
            {
                target.IconImage.sprite = cachedFrames[emojiId][0];
            }

            // Phát âm thanh biểu cảm vui nhộn
            if (sfxSource != null && cachedSfxClips != null && cachedSfxClips[emojiId] != null)
            {
                float sfxVol = PlayerPrefs.GetFloat("SFXVolume", 0.8f) * PlayerPrefs.GetFloat("MasterVolume", 1f);
                if (sfxVol > 0.01f)
                {
                    sfxSource.PlayOneShot(cachedSfxClips[emojiId], Mathf.Clamp01(sfxVol * 0.65f));
                }
            }
        }

        public void OnEvent(EventData photonEvent)
        {
            if (photonEvent == null || photonEvent.Code != NetworkConstants.EVENT_EMOJI_REACTION)
                return;

            if (photonEvent.CustomData is object[] data && data.Length >= 2)
            {
                int senderActor = Convert.ToInt32(data[0]);
                int emojiId = Convert.ToInt32(data[1]);
                int localActor = PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : -1;

                bool isLocal = (senderActor == localActor);
                ShowEmojiOnCard(isLocal, emojiId);
            }
        }
    }
}
