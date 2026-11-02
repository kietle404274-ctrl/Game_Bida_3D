using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Photon.Pun;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Màn hình tải dùng khi vào trận (đấu AI / đấu online) và khi thoát về sảnh.
    /// - Nền ảnh vừa khít toàn bộ (không cắt/bóp tỉ lệ), thanh tải chạy từ trái sang phải với tốc độ không đều như tải thật.
    /// - Màn vào trận có 2 thẻ người chơi (avatar + tên) và huy hiệu VS.
    /// - Online: sau khi tải xong scene, mỗi máy báo "đã tải xong" qua Photon và chờ máy kia (tối đa 30 giây) rồi mới mờ dần.
    /// Canvas được giữ qua việc đổi scene (DontDestroyOnLoad) và tự hủy sau khi mờ.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public const string PlayerPropLoaded = "ld";
        private const float OnlineWaitTimeout = 30f;

        private enum Kind { MatchAI, MatchOnline, Lobby }

        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Cyan = new Color(0.40f, 0.85f, 1f, 1f);

        private static LoadingScreen current;

        /// <summary>True khi màn hình tải đang hiển thị và chờ người chơi (chưa bắt đầu mờ dần).</summary>
        public static bool IsActive => current != null && !current.fadingOut;

        private Kind kind;
        private CanvasGroup group;
        private RectTransform artRect;
        private LoadingBar bar;
        private ProgressPacer pacer; // tạo trong Build() vì Random.Range không được gọi trong khởi tạo trường của MonoBehaviour
        private RectTransform leftCard, rightCard, vsBadge;
        private CanvasGroup leftGroup, rightGroup, vsGroup, embersGroup;
        private Image vsPulse;
        private LightningGraphic[] bolts;
        private System.Collections.Generic.List<Ember> embers;
        private StripBar leftStrip, rightStrip;
        private float lastSentProgress = -1f;
        public const string PlayerPropProgress = "ldp";

        private float progress;
        private float startTime;
        private float fade;
        private bool fadingOut;
        private string statusOverride;

        // Hệ thống âm thanh dòng điện điện ảnh (VS Charge & Clash + Plasma Hum + Synchronized Electric Zaps)
        private static AudioClip s_vsChargeAndClashClip;
        private static AudioClip s_electricHumLoopClip;
        private static AudioClip[] s_electricZapClips;
        private AudioSource electricOneShotSource;
        private AudioSource electricHumSource;
        private bool playedChargeAndClash;
        private bool startedHumLoop;
        private float nextZapAllowedTime;
        private int lastZapVariant = -1;

        private static readonly string[] StatusVi =
        {
            "Đang chuẩn bị bàn bida",
            "Nạp bi và vật lý",
            "Thiết lập luật chơi",
            "Sắp xong",
            "Sẵn sàng"
        };

        private static readonly string[] StatusEn =
        {
            "Preparing the table",
            "Loading balls and physics",
            "Setting up the rules",
            "Almost there",
            "Ready"
        };

        // ------------------------------------------------------------------ API

        /// <summary>Vào trận đấu với máy: tải scene Gameplay bất đồng bộ.</summary>
        public static LoadingScreen ShowForOfflineMatch()
        {
            LoadingScreen s = Create(Kind.MatchAI);
            s.StartCoroutine(s.RunAsync("Gameplay", 3.2f));
            return s;
        }

        /// <summary>Vào trận online: scene được Photon tải đồng bộ; màn hình này theo dõi tiến độ và chờ cả hai máy.</summary>
        public static LoadingScreen ShowForOnlineMatch()
        {
            LoadingScreen s = Create(Kind.MatchOnline);
            s.StartCoroutine(s.RunOnline(3.0f));
            return s;
        }

        /// <summary>Thoát về sảnh: tải scene Lobby bất đồng bộ.</summary>
        public static LoadingScreen ShowForLobby()
        {
            LoadingScreen s = Create(Kind.Lobby);
            s.StartCoroutine(s.RunAsync("Lobby", 1.8f));
            return s;
        }

        private static LoadingScreen Create(Kind kind)
        {
            if (current != null) Destroy(current.gameObject);

            GameObject go = new GameObject("LoadingScreenCanvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4900;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>(); // chặn thao tác phía dưới trong lúc tải
            go.AddComponent<CanvasGroup>();
            DontDestroyOnLoad(go);

            LoadingScreen ui = go.AddComponent<LoadingScreen>();
            ui.kind = kind;
            ui.Build();
            current = ui;
            return ui;
        }

        private void OnDestroy()
        {
            if (electricHumSource != null && electricHumSource.isPlaying) electricHumSource.Stop();
            if (electricOneShotSource != null && electricOneShotSource.isPlaying) electricOneShotSource.Stop();
            if (current == this) current = null;
        }

        // ------------------------------------------------------------------ dựng giao diện

        private void Build()
        {
            group = GetComponent<CanvasGroup>();
            pacer = new ProgressPacer();
            group.alpha = 0f;
            startTime = Time.unscaledTime;
            Transform root = transform;

            string artPath = kind == Kind.MatchOnline ? "Loading/loading_online_duel" : "Loading/loading_ai_table";
            artRect = LoadingUiKit.BuildArt(root, Resources.Load<Texture2D>(artPath));

            // lớp tối giúp chữ và thẻ nổi bật
            Image bottomShade = LoadingUiKit.NewImage(root, "BottomShade", LoadingUiKit.MakeGradient(false, 0.88f, 0.48f), Color.white);
            LoadingUiKit.Stretch(bottomShade.rectTransform);
            Image centerShade = LoadingUiKit.NewImage(root, "CenterShade", null, new Color(0.02f, 0.03f, 0.06f, kind == Kind.Lobby ? 0.35f : 0.45f));
            LoadingUiKit.Stretch(centerShade.rectTransform);

            bool vi = LoadingUiKit.Vi;
            if (kind == Kind.Lobby) BuildLobbyHeader(root, vi);
            else
            {
                BuildMatchHeaderAndCards(root, vi);
                SetupElectricAudio();
            }

            bar = LoadingBar.Build(root, new Vector2(0.5f, 0.5f), new Vector2(0f, -360f), 1000f);
            SetProgress(0f);
        }

        private void BuildLobbyHeader(Transform root, bool vi)
        {
            Text t = LoadingUiKit.NewText(root, "Title", vi ? "ĐANG VỀ SẢNH" : "RETURNING TO LOBBY", 64, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            LoadingUiKit.PlaceCenter(t.rectTransform, new Vector2(0f, 60f), new Vector2(1400f, 90f));
            t.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            Image line = LoadingUiKit.NewImage(root, "GoldLine", null, Gold);
            LoadingUiKit.PlaceCenter(line.rectTransform, new Vector2(0f, 10f), new Vector2(180f, 4f));
        }

        private void BuildMatchHeaderAndCards(Transform root, bool vi)
        {
            GameSession session = GameSession.Instance;
            bool online = kind == Kind.MatchOnline;

            Text mode = LoadingUiKit.NewText(root, "Mode", online ? (vi ? "ĐẤU ONLINE 1v1" : "ONLINE 1v1 DUEL") : (vi ? "ĐẤU VỚI MÁY" : "VS COMPUTER"),
                54, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            LoadingUiKit.PlaceCenter(mode.rectTransform, new Vector2(0f, 360f), new Vector2(1400f, 80f));
            mode.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
            Image line = LoadingUiKit.NewImage(root, "GoldLine", null, Gold);
            LoadingUiKit.PlaceCenter(line.rectTransform, new Vector2(0f, 318f), new Vector2(180f, 4f));
            Text info = LoadingUiKit.NewText(root, "Info", BuildInfoLine(session), 22, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            LoadingUiKit.PlaceCenter(info.rectTransform, new Vector2(0f, 282f), new Vector2(1400f, 34f));

            string n1, n2, s1, s2;
            int a1, a2;
            if (online)
            {
                bool iAmMaster = PhotonNetwork.IsMasterClient;
                n1 = session != null ? session.PlayerAName : "Player 1";
                n2 = session != null ? session.PlayerBName : "Player 2";
                NetworkManager nm = NetworkManager.Instance;
                a1 = nm != null ? Mathf.Max(0, nm.GetPlayerAvatar(true)) : 0;
                a2 = nm != null ? Mathf.Max(0, nm.GetPlayerAvatar(false)) : 1;
                string you = vi ? " (BẠN)" : " (YOU)";
                s1 = (vi ? "CHỦ PHÒNG" : "HOST") + (iAmMaster ? you : "");
                s2 = (vi ? "KHÁCH" : "GUEST") + (iAmMaster ? "" : you);
            }
            else
            {
                n1 = session != null ? session.PlayerAName : PlayerProfile.Name;
                n2 = session != null ? session.PlayerBName : "AI";
                a1 = PlayerProfile.AvatarIndex;
                a2 = 22; // robot
                s1 = vi ? "BẠN" : "YOU";
                s2 = vi ? "MÁY" : "COMPUTER";
            }

            Color accL = AvatarLibrary.AccentColor(a1);
            Color accR = AvatarLibrary.AccentColor(a2);

            // hiệu ứng nền phía sau thẻ: tia sét + tàn lửa 2 bên
            Color boltL = new Color(0.45f, 0.80f, 1f, 1f);
            Color boltR = new Color(1f, 0.45f, 0.30f, 1f);
            bolts = new LightningGraphic[4];
            bolts[0] = BuildBolt(root, new Vector2(-104f, 10f), new Vector2(-350f, 14f), boltL, 26f);
            bolts[1] = BuildBolt(root, new Vector2(104f, 10f), new Vector2(350f, 6f), boltR, 26f);
            bolts[2] = BuildBolt(root, new Vector2(40f, 176f), new Vector2(0f, 108f), boltL, 14f);
            bolts[3] = BuildBolt(root, new Vector2(-40f, -156f), new Vector2(0f, -88f), boltR, 14f);
            BuildEmbers(root, boltL, boltR);

            leftCard = BuildCard(root, "CardLeft", n1, s1, a1, true, out leftGroup);
            rightCard = BuildCard(root, "CardRight", n2, s2, a2, false, out rightGroup);

            GameObject vs = new GameObject("VS", typeof(RectTransform), typeof(CanvasGroup));
            vs.transform.SetParent(root, false);
            vsBadge = vs.GetComponent<RectTransform>();
            vsGroup = vs.GetComponent<CanvasGroup>();
            LoadingUiKit.PlaceCenter(vsBadge, new Vector2(0f, 10f), new Vector2(200f, 200f));
            Image vsGlow = LoadingUiKit.NewImage(vs.transform, "Glow", AvatarLibrary.DotSprite, new Color(Gold.r, Gold.g, Gold.b, 0.20f));
            LoadingUiKit.PlaceCenter(vsGlow.rectTransform, Vector2.zero, new Vector2(290f, 290f));
            vsPulse = LoadingUiKit.NewImage(vs.transform, "Pulse", AvatarLibrary.RingSprite, new Color(Gold.r, Gold.g, Gold.b, 0.5f));
            LoadingUiKit.PlaceCenter(vsPulse.rectTransform, Vector2.zero, new Vector2(232f, 232f));
            Image ring = LoadingUiKit.NewImage(vs.transform, "Ring", AvatarLibrary.DotSprite, Gold);
            LoadingUiKit.PlaceCenter(ring.rectTransform, Vector2.zero, new Vector2(200f, 200f));
            Image inner = LoadingUiKit.NewImage(vs.transform, "Inner", AvatarLibrary.DotSprite, new Color(0.04f, 0.06f, 0.11f, 1f));
            LoadingUiKit.PlaceCenter(inner.rectTransform, Vector2.zero, new Vector2(184f, 184f));
            Text vsText = LoadingUiKit.NewText(vs.transform, "Text", "VS", 76, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            LoadingUiKit.PlaceCenter(vsText.rectTransform, Vector2.zero, new Vector2(184f, 184f));
            vsText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
        }

        private static void PlaceAt(RectTransform r, float pivotX, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(pivotX, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static SkewedPanelGraphic NewSkew(Transform parent, string name, Color c, float cx, float cy, float w, float h, float tl, float tr, float bl, float br)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.transform.SetParent(parent, false);
            PlaceAt((RectTransform)go.transform, 0.5f, cx, cy, w, h);
            SkewedPanelGraphic g = go.GetComponent<SkewedPanelGraphic>();
            g.color = c;
            g.SetInsets(tl, tr, bl, br);
            g.raycastTarget = false;
            return g;
        }

        private static LightningGraphic BuildBolt(Transform root, Vector2 a, Vector2 b, Color col, float amp)
        {
            GameObject go = new GameObject("Bolt", typeof(RectTransform), typeof(CanvasRenderer), typeof(LightningGraphic));
            go.transform.SetParent(root, false);
            LoadingUiKit.PlaceCenter((RectTransform)go.transform, Vector2.zero, new Vector2(1920f, 1080f));
            LightningGraphic lg = go.GetComponent<LightningGraphic>();
            lg.Setup(a, b, col, amp);
            return lg;
        }

        private class Ember { public RectTransform rt; public Image img; public float x0, y, speed, phase, size; public float side; }

        private void BuildEmbers(Transform root, Color left, Color right)
        {
            GameObject holder = new GameObject("Embers", typeof(RectTransform), typeof(CanvasGroup));
            holder.transform.SetParent(root, false);
            LoadingUiKit.PlaceCenter((RectTransform)holder.transform, Vector2.zero, new Vector2(1920f, 1080f));
            embersGroup = holder.GetComponent<CanvasGroup>();
            embersGroup.alpha = 0f;
            embersGroup.blocksRaycasts = false;
            embers = new System.Collections.Generic.List<Ember>();
            for (int i = 0; i < 48; i++)
            {
                bool l = i % 2 == 0;
                Ember e = new Ember();
                e.side = l ? -1f : 1f;
                Color c = l ? left : right;
                if (!l && Random.value < 0.4f) c = new Color(1f, 0.75f, 0.2f, 1f);
                e.img = LoadingUiKit.NewImage(holder.transform, "E", AvatarLibrary.DotSprite, c);
                e.rt = e.img.rectTransform;
                e.size = Random.Range(3f, 8f);
                e.rt.sizeDelta = new Vector2(e.size, e.size);
                e.rt.anchorMin = e.rt.anchorMax = new Vector2(0.5f, 0.5f);
                RespawnEmber(e, true);
                embers.Add(e);
            }
        }

        private static void RespawnEmber(Ember e, bool anywhere)
        {
            e.x0 = e.side * Random.Range(100f, 360f);
            e.y = anywhere ? Random.Range(-170f, 90f) : Random.Range(-170f, -110f);
            e.speed = Random.Range(40f, 90f);
            e.phase = Random.Range(0f, 6.28f);
        }

        private void UpdateEmbers(float t, float dt)
        {
            if (embers == null) return;
            for (int i = 0; i < embers.Count; i++)
            {
                Ember e = embers[i];
                e.y += e.speed * dt;
                if (e.y > 100f) RespawnEmber(e, false);
                float life = Mathf.InverseLerp(100f, -170f, e.y); // 0 trên cao, 1 dưới thấp
                float a = Mathf.Clamp01(life * 1.4f) * (0.6f + 0.4f * Mathf.Sin(t * 6f + e.phase));
                Color c = e.img.color; c.a = a; e.img.color = c;
                e.rt.anchoredPosition = new Vector2(e.x0 + Mathf.Sin(t * 2f + e.phase) * 10f, e.y);
            }
        }

        /// <summary>
        /// Thẻ người chơi dạng hình bình hành nghiêng "/" (theo bản phác thảo): avatar bên trong, tên + nhãn vai trò,
        /// dải màu nhấn nghiêng ở mép ngoài và một thanh mảnh kéo dài về phía huy hiệu VS ở giữa.
        /// </summary>
        private RectTransform BuildCard(Transform root, string name, string playerName, string subtitle, int avatar, bool left, out CanvasGroup cg)
        {
            const float w = 600f, h = 190f, slant = 60f;
            Color accent = AvatarLibrary.AccentColor(avatar);
            float side = left ? -1f : 1f;

            GameObject card = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            card.transform.SetParent(root, false);
            RectTransform rt = card.GetComponent<RectTransform>();
            cg = card.GetComponent<CanvasGroup>();
            LoadingUiKit.PlaceCenter(rt, CardPos(left, 1f), new Vector2(w, h));

            // thanh tải dài 3D chạy sát mép (trái: mép trên thẻ, phải: mép dưới thẻ), từ góc ngoài thẻ tới đầu nhánh dẹt
            StripBar sb = BuildStripBar(card.transform, left);
            if (left) leftStrip = sb; else rightStrip = sb;

            // hình thẻ liền khối: thẻ lớn + nhánh dẹt nối liền (một viền duy nhất)
            BuildCardShape(card.transform, left, accent);

            // ánh màu nhấn từ mép ngoài vào trong
            NewSkew(card.transform, "Wash", new Color(accent.r, accent.g, accent.b, 0.14f), left ? -170f : 110f, 0f, 260f, h, slant, -slant, 0f, 0f);

            // chữ P1 / P2 mờ
            Text mark = LoadingUiKit.NewText(card.transform, "Mark", left ? "P1" : "P2", 120, new Color(1f, 1f, 1f, 0.06f), left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceAt(mark.rectTransform, left ? 1f : 0f, -side * 250f, -4f, 200f, 150f);


            // dải nhấn dày nghiêng ở mép ngoài
            NewSkew(card.transform, "AccentBar", accent, left ? -293f : 233f, 0f, 14f, h, slant, -slant, 0f, 0f);

            // avatar
            float ax = side * 168f;
            Image glow = LoadingUiKit.NewImage(card.transform, "AvatarGlow", AvatarLibrary.DotSprite, new Color(accent.r, accent.g, accent.b, 0.20f));
            PlaceAt(glow.rectTransform, 0.5f, ax, 0f, 200f, 200f);
            Image back = LoadingUiKit.NewImage(card.transform, "AvatarBack", AvatarLibrary.DotSprite, new Color(0.03f, 0.05f, 0.09f, 1f));
            PlaceAt(back.rectTransform, 0.5f, ax, 0f, 146f, 146f);
            Image av = LoadingUiKit.NewImage(card.transform, "Avatar", AvatarLibrary.Get(avatar), Color.white);
            av.preserveAspect = true;
            PlaceAt(av.rectTransform, 0.5f, ax, 0f, 136f, 136f);
            Image ringImg = LoadingUiKit.NewImage(card.transform, "AvatarRing", AvatarLibrary.RingSprite, accent);
            PlaceAt(ringImg.rectTransform, 0.5f, ax, 0f, 164f, 164f);

            // cột chữ
            float colStart = left ? -60f : 60f;
            const float colWidth = 316f;
            float pivot = left ? 0f : 1f;
            TextAnchor anchor = left ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            Text nameT = LoadingUiKit.NewText(card.transform, "Name", playerName, 44, Color.white, anchor, FontStyle.Bold);
            nameT.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameT.resizeTextForBestFit = true;
            nameT.resizeTextMinSize = 22;
            nameT.resizeTextMaxSize = 44;
            PlaceAt(nameT.rectTransform, pivot, colStart, 26f, colWidth, 60f);
            nameT.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

            Image accentLine = LoadingUiKit.NewImage(card.transform, "AccentLine", null, accent);
            PlaceAt(accentLine.rectTransform, pivot, colStart, -8f, 110f, 3f);

            Text subT = LoadingUiKit.NewText(card.transform, "Sub", subtitle, 19, accent, anchor, FontStyle.Bold);
            PlaceAt(subT.rectTransform, pivot, colStart, -36f, colWidth, 28f);
            return rt;
        }

        private class StripBar
        {
            public RectTransform mask, head;
            public Image glow, core, flareH, flareV;
            public bool left;
            public float Width = 905f, Height = 10f;

            public void Apply(float v, float t, float phase)
            {
                v = Mathf.Clamp01(v);
                mask.sizeDelta = new Vector2(Width * v, Height);
                float x = left ? -Width * 0.5f + Width * v : Width * 0.5f - Width * v;
                head.anchoredPosition = new Vector2(x, 0f);
                float pulse = 1f + 0.25f * Mathf.Sin(t * 9f + phase);
                float vis = Mathf.Clamp01(v * 20f);
                head.localScale = Vector3.one * pulse;
                SetA(glow, 0.55f * vis); SetA(core, vis); SetA(flareH, 0.9f * vis); SetA(flareV, 0.8f * vis);
            }

            private static void SetA(Image i, float a) { Color c = i.color; c.a = a; i.color = c; }
        }

        /// <summary>
        /// Hình thẻ liền khối theo bản phác thảo: thẻ lớn (hình bình hành "/") + nhánh dẹt ở mép trên (thẻ trái) hoặc mép dưới (thẻ phải),
        /// nhánh có mép trên liền mạch với mép thẻ, mép trong trùng đúng cạnh nghiêng của thẻ, đầu nhánh vát.
        /// Thẻ phải là thẻ trái xoay 180 độ.
        /// </summary>
        private static void BuildCardShape(Transform card, bool left, Color accent)
        {
            float s = left ? 1f : -1f;
            Vector2 BL = new Vector2(-300f, -95f) * s, TL = new Vector2(-240f, 95f) * s;
            Vector2 TR = new Vector2(300f, 95f) * s, BR = new Vector2(240f, -95f) * s;
            Vector2 stripTR = new Vector2(645f, 95f) * s, stripBR = new Vector2(625f, 67f) * s, join = new Vector2(291.16f, 67f) * s;

            GameObject go = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(CardShapeGraphic));
            go.transform.SetParent(card, false);
            LoadingUiKit.PlaceCenter((RectTransform)go.transform, Vector2.zero, new Vector2(600f, 190f));
            go.GetComponent<CardShapeGraphic>().Setup(
                new[] { BL, TL, stripTR, stripBR, join, BR },
                new[] { new[] { BL, TL, TR, BR }, new[] { TR, stripTR, stripBR, join } },
                new Color(0.04f, 0.07f, 0.13f, 0.94f),
                new Color(accent.r, accent.g, accent.b, 0.95f), 2.5f);
        }

        /// <summary>
        /// Thanh tải nổi 3D (xanh lam) chạy suốt chiều dài mép thẻ + nhánh dẹt: nền tối viền sáng, dải màu có mặt sáng phía trên
        /// và mặt tối phía dưới, điểm sáng chói (quầng + lõi trắng + tia chữ thập) ở đầu tiến trình. Chạy từ góc ngoài thẻ về phía VS.
        /// </summary>
        private static StripBar BuildStripBar(Transform card, bool left)
        {
            const float sw = 905f, sh = 10f, sl = 4f;
            float side = left ? -1f : 1f;
            StripBar b = new StripBar { left = left, Width = sw, Height = sh };

            GameObject root = new GameObject("LongBar", typeof(RectTransform));
            root.transform.SetParent(card, false);
            PlaceAt((RectTransform)root.transform, 0.5f, -side * 187.5f, -side * 108f, sw, sh);

            NewSkew(root.transform, "Track", new Color(0.02f, 0.06f, 0.14f, 0.96f), 0f, 0f, sw, sh, sl, 0f, 0f, sl);

            GameObject maskGo = new GameObject("Mask", typeof(RectTransform), typeof(RectMask2D));
            maskGo.transform.SetParent(root.transform, false);
            b.mask = (RectTransform)maskGo.transform;
            b.mask.anchorMin = b.mask.anchorMax = new Vector2(0.5f, 0.5f);
            b.mask.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            b.mask.anchoredPosition = new Vector2(left ? -sw * 0.5f : sw * 0.5f, 0f);
            b.mask.sizeDelta = new Vector2(0f, sh);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(maskGo.transform, false);
            RectTransform fr = (RectTransform)fillGo.transform;
            fr.anchorMin = fr.anchorMax = new Vector2(left ? 0f : 1f, 0.5f);
            fr.pivot = new Vector2(left ? 0f : 1f, 0.5f);
            fr.anchoredPosition = Vector2.zero;
            fr.sizeDelta = new Vector2(sw, sh);

            NewSkew(fr, "Body", new Color(0.10f, 0.45f, 0.95f, 1f), 0f, 0f, sw, sh, sl, 0f, 0f, sl);
            NewSkew(fr, "Top", new Color(0.60f, 0.90f, 1f, 0.70f), 0.8f, 2.5f, sw - 2f, 4.5f, sl * 0.45f, 0f, 0f, sl * 0.45f);
            NewSkew(fr, "Bottom", new Color(0.00f, 0.10f, 0.45f, 0.60f), -1f, -3.2f, sw - 2f, 3.2f, sl * 0.32f, 0f, 0f, sl * 0.32f);

            GameObject headGo = new GameObject("Head", typeof(RectTransform));
            headGo.transform.SetParent(root.transform, false);
            b.head = (RectTransform)headGo.transform;
            b.head.anchorMin = b.head.anchorMax = new Vector2(0.5f, 0.5f);
            b.head.sizeDelta = Vector2.zero;
            b.glow = LoadingUiKit.NewImage(headGo.transform, "Glow", AvatarLibrary.DotSprite, new Color(0.40f, 0.80f, 1f, 0.55f));
            LoadingUiKit.PlaceCenter(b.glow.rectTransform, Vector2.zero, new Vector2(70f, 70f));
            b.flareH = LoadingUiKit.NewImage(headGo.transform, "FlareH", null, new Color(0.85f, 0.95f, 1f, 0.9f));
            LoadingUiKit.PlaceCenter(b.flareH.rectTransform, Vector2.zero, new Vector2(110f, 2.5f));
            b.flareV = LoadingUiKit.NewImage(headGo.transform, "FlareV", null, new Color(0.85f, 0.95f, 1f, 0.8f));
            LoadingUiKit.PlaceCenter(b.flareV.rectTransform, Vector2.zero, new Vector2(2.5f, 40f));
            b.core = LoadingUiKit.NewImage(headGo.transform, "Core", AvatarLibrary.DotSprite, Color.white);
            LoadingUiKit.PlaceCenter(b.core.rectTransform, Vector2.zero, new Vector2(18f, 18f));
            b.Apply(0f, 0f, 0f);
            return b;
        }

        private static Vector2 CardPos(bool left, float e)
        {
            float off = 1000f * (1f - e);
            return left ? new Vector2(-600f - off, 105f) : new Vector2(600f + off, -85f);
        }

        private static string BuildInfoLine(GameSession s)
        {
            if (s == null) return "8-BALL";
            string timer;
            switch (s.CurrentSettings.shotTimer)
            {
                case ShotTimerOption.Seconds30: timer = LocalizationManager.Get("opt_timer_30"); break;
                case ShotTimerOption.Seconds45: timer = LocalizationManager.Get("opt_timer_45"); break;
                case ShotTimerOption.Seconds60: timer = LocalizationManager.Get("opt_timer_60"); break;
                default: timer = LocalizationManager.Get("opt_timer_unlimited"); break;
            }
            return "RACE TO " + (int)s.CurrentSettings.raceTarget + "   •   " + timer.ToUpperInvariant();
        }

        // ------------------------------------------------------------------ luồng tải

        /// <summary>
        /// Tiến độ hiển thị: bám sát tiến độ thật (không còn thời gian tối thiểu hay đường cong giả), chỉ làm mượt
        /// để thanh không giật khi tiến độ thật nhảy bậc. Tham số elapsed/minSeconds giữ lại cho tương thích.
        /// </summary>
        private float NextShown(float shown, float real, float elapsed, float minSeconds, float dt)
        {
            if (real <= shown) return shown;
            float next = shown + (real - shown) * (1f - Mathf.Exp(-dt * 7f));
            next = Mathf.Max(next, Mathf.MoveTowards(shown, real, dt * 0.3f));
            if (real >= 1f && next > 0.995f) next = 1f;
            return Mathf.Min(next, real);
        }

        private IEnumerator RunAsync(string sceneName, float minSeconds)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null) { Destroy(gameObject); yield break; }
            op.allowSceneActivation = false;

            float elapsed = 0f, shown = 0f;
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                float real = Mathf.Clamp01(op.progress / 0.9f);
                shown = NextShown(shown, real, elapsed, minSeconds, dt);
                SetProgress(shown);
                if (shown >= 0.999f && real >= 1f) break;
                yield return null;
            }

            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.4f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;
            yield return FadeOutAndDestroy();
        }

        private IEnumerator RunOnline(float minSeconds)
        {
            ResetLoadedFlag();
            const string scene = "Gameplay";
            float elapsed = 0f, shown = 0f;
            bool fallbackStarted = false;
            bool loaded = false;

            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                loaded = SceneManager.GetActiveScene().name == scene;
                float real = loaded ? 1f : Mathf.Clamp01(PhotonNetwork.LevelLoadingProgress);

                // Dự phòng: nếu sau 3 giây Photon vẫn chưa bắt đầu tải scene thì tự tải
                if (!loaded && !fallbackStarted && elapsed > 3f && PhotonNetwork.LevelLoadingProgress <= 0f)
                {
                    fallbackStarted = true;
                    SceneManager.LoadSceneAsync(scene);
                }

                shown = NextShown(shown, real, elapsed, minSeconds, dt);
                SetProgress(shown);
                if (shown >= 0.999f && loaded) break;
                if (elapsed > 60f) break; // tránh kẹt vô hạn
                yield return null;
            }

            SetProgress(1f);
            yield return null;
            yield return null;
            SetLoadedFlag();

            // Chờ máy còn lại tải xong
            float wait = 0f;
            statusOverride = LoadingUiKit.Vi ? "Đang chờ đối thủ" : "Waiting for opponent";
            while (wait < OnlineWaitTimeout)
            {
                if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount < 2) break; // đối thủ thoát / mất mạng
                if (AllPlayersLoaded()) break;
                wait += Time.unscaledDeltaTime;
                yield return null;
            }
            statusOverride = null;
            yield return new WaitForSecondsRealtime(0.25f);
            yield return FadeOutAndDestroy();
        }

        private static void ResetLoadedFlag()
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { PlayerPropLoaded, false }, { PlayerPropProgress, 0f } });
        }

        private static void SetLoadedFlag()
        {
            if (!PhotonNetwork.InRoom) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { PlayerPropLoaded, true } });
        }

        private static bool AllPlayersLoaded()
        {
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (!(p.CustomProperties.TryGetValue(PlayerPropLoaded, out object v) && v is bool b && b)) return false;
            }
            return true;
        }

        private IEnumerator FadeOutAndDestroy()
        {
            fadingOut = true;
            group.blocksRaycasts = false;
            while (group.alpha > 0.01f) yield return null;
            Destroy(gameObject);
        }

        // ------------------------------------------------------------------ cập nhật

        private void SetProgress(float p)
        {
            progress = Mathf.Clamp01(p);
            if (bar != null) bar.SetProgress(progress);
            PublishProgress();
        }

        /// <summary>Online: gửi tiến độ của máy mình cho đối thủ (giới hạn tần suất) để họ vẽ thanh tải của mình.</summary>
        private void PublishProgress()
        {
            if (kind != Kind.MatchOnline || !PhotonNetwork.InRoom) return;
            if (progress < 0.999f && progress - lastSentProgress < 0.05f) return;
            if (progress >= 0.999f && lastSentProgress >= 0.999f) return;
            lastSentProgress = progress;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { PlayerPropProgress, progress } });
        }

        private static float OpponentProgress()
        {
            if (!PhotonNetwork.InRoom) return 0f;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.IsLocal) continue;
                if (p.CustomProperties.TryGetValue(PlayerPropLoaded, out object ld) && ld is bool b && b) return 1f;
                if (p.CustomProperties.TryGetValue(PlayerPropProgress, out object v) && v is float f) return Mathf.Clamp01(f);
            }
            return 0f;
        }

        private float stripLeftVal, stripRightVal;

        private void UpdateStrips(float t, float dt, float vis)
        {
            if (leftStrip == null || rightStrip == null) return;
            float mine = progress;
            float other = kind == Kind.MatchOnline ? OpponentProgress() : progress;
            bool iAmLeft = kind != Kind.MatchOnline || PhotonNetwork.IsMasterClient;
            float tl = iAmLeft ? mine : other;
            float tr = iAmLeft ? other : mine;
            stripLeftVal = Mathf.MoveTowards(stripLeftVal, tl, dt * (iAmLeft ? 4f : 1.2f));
            stripRightVal = Mathf.MoveTowards(stripRightVal, tr, dt * (iAmLeft ? 1.2f : 4f));
            leftStrip.Apply(stripLeftVal, t, 0f);
            rightStrip.Apply(stripRightVal, t, 1.7f);
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float t = x - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        private void Update()
        {
            float t = Time.unscaledTime - startTime;
            float dt = Time.unscaledDeltaTime;

            fade = Mathf.MoveTowards(fade, fadingOut ? 0f : 1f, dt * (fadingOut ? 2.2f : 3f));
            if (group != null) group.alpha = fade;

            if (artRect != null) artRect.localPosition = new Vector3(Mathf.Sin(t * 0.4f) * 8f, Mathf.Cos(t * 0.3f) * 5f, 0f);
            if (bar != null) bar.Tick(t);

            // thẻ người chơi trượt vào, VS bật lên
            if (leftCard != null)
            {
                float k = Mathf.Clamp01((t - 0.1f) / 0.55f);
                float e = EaseOutBack(k);
                leftCard.anchoredPosition = CardPos(true, e);
                rightCard.anchoredPosition = CardPos(false, e);
                leftGroup.alpha = rightGroup.alpha = Mathf.Clamp01(k * 1.6f);

                float v = Mathf.Clamp01((t - 0.5f) / 0.45f);
                vsGroup.alpha = v;
                float pulse = 1f + 0.06f * Mathf.Sin(t * 3.2f);
                vsBadge.localScale = Vector3.one * (EaseOutBack(v) * pulse);

                if (vsPulse != null)
                {
                    float ph = (t * 0.9f) % 1f;
                    vsPulse.rectTransform.localScale = Vector3.one * (1f + ph * 0.45f);
                    Color pc = vsPulse.color; pc.a = 0.5f * (1f - ph); vsPulse.color = pc;
                }
                bool anyBoltSurged = false;
                if (bolts != null)
                {
                    for (int i = 0; i < bolts.Length; i++)
                    {
                        if (bolts[i].Tick(dt)) anyBoltSurged = true;
                        bolts[i].canvasRenderer.SetAlpha(v);
                    }
                }
                if (embersGroup != null) embersGroup.alpha = v;
                UpdateEmbers(t, dt);
                UpdateStrips(t, dt, v);
                UpdateElectricAudio(t, v, anyBoltSurged);
            }

            if (bar != null && bar.Status != null)
            {
                string[] st = LoadingUiKit.Vi ? StatusVi : StatusEn;
                string dots = new string('.', 1 + (int)(t * 2.5f) % 3);
                if (!string.IsNullOrEmpty(statusOverride)) bar.Status.text = statusOverride + dots;
                else
                {
                    int idx = progress >= 0.999f ? 4 : Mathf.Min(3, (int)(progress * 4f));
                    bar.Status.text = st[idx] + (idx == 4 ? "" : dots);
                }
            }
        }

        // ------------------------------------------------------------------ âm thanh dòng điện điện ảnh

        private void SetupElectricAudio()
        {
            EnsureElectricClipsSynthesized();

            electricOneShotSource = gameObject.AddComponent<AudioSource>();
            electricOneShotSource.playOnAwake = false;
            electricOneShotSource.loop = false;
            electricOneShotSource.spatialBlend = 0f;
            electricOneShotSource.priority = 40;

            electricHumSource = gameObject.AddComponent<AudioSource>();
            electricHumSource.playOnAwake = false;
            electricHumSource.loop = true;
            electricHumSource.spatialBlend = 0f;
            electricHumSource.priority = 56;
            electricHumSource.clip = s_electricHumLoopClip;
            electricHumSource.volume = 0f;

            playedChargeAndClash = false;
            startedHumLoop = false;
            nextZapAllowedTime = 0.62f;
        }

        private static float GetEffectiveSfxVolume()
        {
            // Tránh phát trùng âm thanh trên cửa sổ phụ khi test 2 cửa sổ Online trên cùng 1 máy
            if (Application.isPlaying && !Application.isFocused && PhotonNetwork.InRoom)
                return 0f;

            var am = Audio.AudioManager.Instance;
            if (am != null)
            {
                return Mathf.Clamp01(am.MasterVolume * am.SFXVolume);
            }

            float master = PlayerPrefs.GetFloat("Volume_Master", 1.0f);
            float sfx = PlayerPrefs.GetFloat("Volume_SFX", 1.0f);
            return Mathf.Clamp01(master * sfx);
        }

        private void UpdateElectricAudio(float t, float vsVisibility, bool anyBoltSurged)
        {
            if (electricOneShotSource == null || electricHumSource == null) return;

            float sfxVol = GetEffectiveSfxVolume();
            float fadeFactor = Mathf.Clamp01(fade);

            // Pha 1 & 2: Phát tiếng tụ điện khi 2 thẻ cơ thủ trượt vào (t = 0.08s) + tiếng nổ hồ quang khi chữ VS đập xuống (t = 0.50s)
            if (!playedChargeAndClash && t >= 0.08f)
            {
                playedChargeAndClash = true;
                if (s_vsChargeAndClashClip != null && sfxVol > 0.001f)
                {
                    electricOneShotSource.pitch = 1.0f;
                    electricOneShotSource.volume = 1.0f;
                    electricOneShotSource.PlayOneShot(s_vsChargeAndClashClip, sfxVol * 0.88f);
                }
            }

            // Pha 3a: Duy trì âm nền dòng điện cao thế (Plasma Hum + Micro-Crackle) từ lúc chữ VS và 4 tia sét xuất hiện
            if (!startedHumLoop && t >= 0.48f && !fadingOut)
            {
                startedHumLoop = true;
                if (s_electricHumLoopClip != null)
                {
                    electricHumSource.volume = fadeFactor * vsVisibility * sfxVol * 0.34f;
                    electricHumSource.Play();
                }
            }

            if (startedHumLoop && electricHumSource != null)
            {
                if (fadingOut && fadeFactor <= 0.02f)
                {
                    if (electricHumSource.isPlaying) electricHumSource.Stop();
                }
                else
                {
                    // Điều biến nhẹ âm lượng hum theo xung nhịp chữ VS để dòng điện nghe "sống"
                    float humPulse = 0.88f + 0.12f * Mathf.Sin(t * 14f);
                    electricHumSource.volume = fadeFactor * vsVisibility * sfxVol * 0.34f * humPulse;
                }
            }

            // Pha 3b: Đồng bộ trực tiếp với nhịp phóng hồ quang cực đại (intensity == 1.0f) của 4 tia sét LightningGraphic
            if (!fadingOut && vsVisibility > 0.65f && anyBoltSurged && t >= nextZapAllowedTime && sfxVol > 0.001f)
            {
                if (s_electricZapClips != null && s_electricZapClips.Length > 0)
                {
                    int idx = Random.Range(0, s_electricZapClips.Length);
                    if (s_electricZapClips.Length > 1 && idx == lastZapVariant)
                    {
                        idx = (idx + 1) % s_electricZapClips.Length;
                    }
                    lastZapVariant = idx;

                    electricOneShotSource.pitch = Random.Range(0.88f, 1.16f);
                    float zapVol = fadeFactor * sfxVol * Random.Range(0.48f, 0.72f);
                    electricOneShotSource.PlayOneShot(s_electricZapClips[idx], zapVol);

                    // Giãn cách tự nhiên giữa các cú sẹt lớn để tiếng điện giật nghe đanh, sắc nét như phim điện ảnh
                    nextZapAllowedTime = t + Random.Range(0.18f, 0.34f);
                }
            }
        }

        private static void EnsureElectricClipsSynthesized()
        {
            if (s_vsChargeAndClashClip != null && s_electricHumLoopClip != null && s_electricZapClips != null)
                return;

            const int sampleRate = 44100;

            // 1. Tổng hợp VS Charge-Up + Thunder Arc Clash (~0.96s)
            {
                int samples = (int)(sampleRate * 0.96f);
                float[] data = new float[samples];
                System.Random rnd = new System.Random(20261102);
                float phaseSweep = 0f;
                float phaseImpact = 0f;
                float phaseBuzz = 0f;
                float lpNoise = 0f;
                float bpState1 = 0f;
                float bpState2 = 0f;

                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)sampleRate;
                    float sample = 0f;

                    // [0.00s .. 0.42s] Hai thẻ cơ thủ trượt vào: Whoosh tụ điện trầm vút lên
                    if (t < 0.44f)
                    {
                        float u = Mathf.Clamp01(t / 0.42f);
                        float env = Mathf.Sin(u * Mathf.PI) * Mathf.Pow(u, 0.65f);
                        float freq = Mathf.Lerp(58f, 235f, u * u);
                        phaseSweep += 2f * Mathf.PI * freq / sampleRate;

                        float hum = Mathf.Sin(phaseSweep) * 0.55f
                                  + Mathf.Sin(phaseSweep * 2f) * 0.28f
                                  + Mathf.Sin(phaseSweep * 3f) * 0.17f;

                        float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                        lpNoise += (white - lpNoise) * (0.04f + 0.14f * u);
                        sample += (hum * 0.48f + lpNoise * 0.35f) * env * 0.65f;
                    }

                    // [0.42s .. 0.96s] Huy hiệu VS đập xuống (tương ứng t = 0.50s trên màn hình) & Bùng nổ 4 tia sét!
                    if (t >= 0.40f)
                    {
                        float tc = t - 0.40f;
                        // Cú nổ trầm điện từ (Sub-bass impact)
                        float impactFreq = 38f + 78f * Mathf.Exp(-tc * 22f);
                        phaseImpact += 2f * Mathf.PI * impactFreq / sampleRate;
                        float impactEnv = Mathf.Exp(-tc * 8.5f) * Mathf.Clamp01(tc / 0.005f);
                        float subImpact = Mathf.Sin(phaseImpact) * impactEnv * 0.78f;

                        // Chùm tia hồ quang cao thế (High-voltage electric arc burst)
                        float arcEnv = Mathf.Exp(-tc * 5.2f) * Mathf.Clamp01(tc / 0.003f);
                        float buzzFreq = 118f + 22f * Mathf.Sin(tc * 55f);
                        phaseBuzz += buzzFreq / sampleRate;
                        phaseBuzz -= Mathf.Floor(phaseBuzz);
                        float saw = (phaseBuzz * 2f - 1f);

                        // Tạo các gai phóng điện răng cưa ngẫu nhiên (Spark crackle)
                        float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                        bpState1 += (white - bpState1) * 0.32f;
                        bpState2 += (bpState1 - bpState2) * 0.08f;
                        float highCrackle = bpState1 - bpState2;

                        // Điều biến biên độ dạng chớp hồ quang (Gated plasma sputter)
                        float gate = 0.55f + 0.45f * Mathf.Sin(tc * 95f) * Mathf.Cos(tc * 143f);
                        float rawArc = (saw * 0.38f + highCrackle * 1.35f) * gate * arcEnv;

                        sample += subImpact + SoftClip(rawArc * 0.90f);
                    }

                    data[i] = SoftClip(sample);
                }

                s_vsChargeAndClashClip = AudioClip.Create("VS_ElectricChargeClash", samples, 1, sampleRate, false);
                s_vsChargeAndClashClip.SetData(data, 0);
            }

            // 2. Tổng hợp Plasma Hum + Micro-Crackle Loop (1.60s liền mạch không có tiếng khấc ở điểm nối)
            {
                const float duration = 1.60f;
                int samples = (int)(sampleRate * duration);
                float[] data = new float[samples];
                System.Random rnd = new System.Random(88421);
                float lp1 = 0f, lp2 = 0f;
                float sparkEnv = 0f;

                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)sampleRate;
                    // Các tần số là bội số nguyên của 1 / 1.60s (0.625 Hz) để nối vòng 100% trùng pha
                    float f60 = 2f * Mathf.PI * 60f * t;
                    float f120 = 2f * Mathf.PI * 120f * t;
                    float f180 = 2f * Mathf.PI * 180f * t;
                    float f240 = 2f * Mathf.PI * 240f * t;
                    float f360 = 2f * Mathf.PI * 360f * t;

                    float modSlow = 0.78f + 0.22f * Mathf.Sin(2f * Mathf.PI * 2.5f * t);
                    float modFast = 0.85f + 0.15f * Mathf.Cos(2f * Mathf.PI * 7.5f * t);

                    float transformerHum =
                        (Mathf.Sin(f60) * 0.32f +
                         Mathf.Sin(f120) * 0.38f +
                         Mathf.Sin(f180 + 0.6f) * 0.16f +
                         Mathf.Sin(f240 + 1.2f) * 0.12f +
                         Mathf.Sin(f360) * 0.08f) * modSlow * modFast;

                    // Tiếng lách tách hồ quang vi mô (Corona micro-sparks)
                    if (rnd.NextDouble() < 0.0045)
                    {
                        sparkEnv = 0.45f + (float)rnd.NextDouble() * 0.55f;
                    }
                    sparkEnv *= 0.965f;

                    float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    lp1 += (white - lp1) * 0.28f;
                    lp2 += (lp1 - lp2) * 0.06f;
                    float bandSpark = (lp1 - lp2) * sparkEnv * 0.65f;

                    // Làm mượt 15ms ở 2 đầu biên để vòng lặp tuyệt đối êm
                    float edgeFade = 1f;
                    int edgeSamples = (int)(sampleRate * 0.015f);
                    if (i < edgeSamples) edgeFade = i / (float)edgeSamples;
                    else if (i > samples - edgeSamples) edgeFade = (samples - i) / (float)edgeSamples;

                    data[i] = SoftClip((transformerHum * 0.48f + bandSpark * edgeFade) * 0.75f);
                }

                s_electricHumLoopClip = AudioClip.Create("VS_ElectricHumLoop", samples, 1, sampleRate, false);
                s_electricHumLoopClip.SetData(data, 0);
            }

            // 3. Tổng hợp 3 biến thể tiếng Sẹt Điện (Crispy Electric Arc Zap, ~0.15s) khớp nhịp giật của tia sét
            s_electricZapClips = new AudioClip[3];
            float[] baseFreqs = { 125f, 148f, 108f };
            float[] reStrikeTimes = { 0.048f, 0.062f, 0.039f };

            for (int v = 0; v < 3; v++)
            {
                int samples = (int)(sampleRate * 0.155f);
                float[] data = new float[samples];
                System.Random rnd = new System.Random(55100 + v * 997);
                float phase = 0f;
                float bpFast = 0f;
                float bpSlow = 0f;
                float baseF = baseFreqs[v];
                float reStrikeT = reStrikeTimes[v];

                for (int i = 0; i < samples; i++)
                {
                    float t = i / (float)sampleRate;

                    // Xung phóng điện chính (Main snap) + Xung nhánh phụ (Secondary branch arc)
                    float envMain = Mathf.Exp(-t * 28f) * Mathf.Clamp01(t / 0.0015f);
                    float envBranch = t >= reStrikeT
                        ? Mathf.Exp(-(t - reStrikeT) * 34f) * Mathf.Clamp01((t - reStrikeT) / 0.0015f) * 0.72f
                        : 0f;
                    float env = Mathf.Max(envMain, envBranch);

                    // Sóng mang răng cưa biến thiên tần số tạo âm "rè sẹt" đặc trưng của tia hồ quang
                    float f = baseF * (1f + 0.35f * Mathf.Sin(t * 210f));
                    phase += f / sampleRate;
                    phase -= Mathf.Floor(phase);
                    float buzz = (phase * 2f - 1f);

                    // Nhiễu dải thông (Band-passed noise 1.2kHz - 3.8kHz) tạo tiếng "tách/xẹt" sắc nét mà không chói
                    float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    bpFast += (white - bpFast) * 0.36f;
                    bpSlow += (bpFast - bpSlow) * 0.09f;
                    float crackle = bpFast - bpSlow;

                    // Cú nhấp đầu tia lửa (Initial spark transient < 8ms)
                    float snap = t < 0.008f ? white * Mathf.Exp(-t * 320f) * 0.9f : 0f;

                    float flutter = 0.60f + 0.40f * Mathf.Sin(t * 165f + v * 1.3f);
                    float raw = (buzz * 0.42f + crackle * 1.45f) * env * flutter + snap;
                    data[i] = SoftClip(raw * 0.92f);
                }

                var clip = AudioClip.Create($"VS_ElectricZap_{v}", samples, 1, sampleRate, false);
                clip.SetData(data, 0);
                s_electricZapClips[v] = clip;
            }
        }

        private static float SoftClip(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }
    }
}
