using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Màn hình thống kê trận đấu dạng overlay bán trong suốt, bật/tắt bằng phím Tab.
    /// Dựng hoàn toàn bằng code lúc chạy. Hiển thị tên 2 người chơi, tỷ số và các chỉ số kèm thanh so sánh hai chiều
    /// có animation (chạy lần lượt từng dòng khi mở panel / đổi tab).
    /// </summary>
    public class StatsPanelUI : MonoBehaviour
    {
        public const string ModalId = "stats";

        private static readonly Color ColorA = new Color(0.20f, 0.85f, 1.00f, 1f);
        private static readonly Color ColorB = new Color(1.00f, 0.62f, 0.12f, 1f);
        private static readonly Color ColorMuted = new Color(0.62f, 0.70f, 0.86f, 1f);
        private static readonly Color ColorWhite = new Color(0.97f, 0.98f, 1f, 1f);
        private static readonly Color ColorTabIdle = new Color(0.08f, 0.14f, 0.38f, 0.95f);
        private static readonly Color ColorNavyText = new Color(0.02f, 0.06f, 0.20f, 1f);

        private enum Better { High, Low, None }

        private class Row
        {
            public bool isHeader;
            public string labelKey;
            public string label;
            public Color accent;
            public Func<PlayerStats, float> value;
            public Func<PlayerStats, string> text;
            public Func<float, string> anim;
            public bool percent;
            public Better better;

            public Text labelText, leftText, rightText;
            public RectTransform leftFill, rightFill;
            public Image leftImg, rightImg, leftCap, rightCap;
            public float leftCur, rightCur, leftTar, rightTar;
            public float va, vb;
            public string finalL = "0", finalR = "0";
        }

        private const float BarOuterLeft = 0.14f;
        private const float BarInnerLeft = 0.37f;
        private const float BarInnerRight = 0.63f;
        private const float BarOuterRight = 0.86f;
        private const float BarY0 = 0.28f;
        private const float BarY1 = 0.72f;

        private const float RowStagger = 0.035f;
        private const float RowDuration = 0.65f;
        private const float CardCut = 42f;

        private const float Tab0X0 = 0.28f, Tab0X1 = 0.49f;
        private const float Tab1X0 = 0.50f, Tab1X1 = 0.71f;
        private const float TabY0 = 0.742f, TabY1 = 0.790f;

        private MatchStatsTracker tracker;
        private MatchStateManager stateManager;
        private Font font;

        private GameObject root;
        private CanvasGroup canvasGroup;
        private RectTransform panelRt;
        private Text nameLeft, nameRight, scoreText, infoText;
        private Button tabMatchButton, tabRackButton;
        private Text tabMatchLabel, tabRackLabel;
        private RectTransform tabIndicator;
        private float indX0 = Tab0X0, indX1 = Tab0X1;
        private readonly List<Row> rows = new List<Row>();

        private int tab; // 0: cả trận, 1: ván hiện tại
        private bool open;
        private bool closing;
        private float anim; // 0..1
        private float timeRefresh;

        private bool replaying;
        private float animClock;

        // Chế độ màn hình kết thúc trận
        private bool resultMode;
        private int resultWinner;
        private Action onRematch, onLobby;
        private bool resultLobbyOnly, resultRematchEnabled = true;
        private string resultTitleOverride, resultRematchLabel;
        private Text resultTitle;
        private Text btnRematchText, btnLobbyText, tagAText, tagBText, hintText;
        private GameObject tagA, tagB, btnRematchGo, btnLobbyGo, closeGo, hintGo;
        private RectTransform rowsAreaRt;
        private Image dimmerImg;
        private SkewedPanelGraphic cardFrameA, cardFrameB;

        public bool IsOpen => open;

        // ------------------------------------------------------------------ Tạo panel

        public static StatsPanelUI Create(GameplayHUD hud, MatchStatsTracker statsTracker, MatchStateManager stateMgr)
        {
            if (hud == null || statsTracker == null) return null;

            // Dùng đúng Canvas của HUD (qua nút Luật) — tránh nhầm sang Canvas khác trong scene (vd: bảng điểm TV world-space)
            Canvas canvas = hud.RulesButton != null ? hud.RulesButton.GetComponentInParent<Canvas>() : null;
            if (canvas == null) canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[StatsPanelUI] Không tìm thấy Canvas HUD để tạo màn hình thống kê.");
                return null;
            }
            canvas = canvas.rootCanvas;

            var go = new GameObject("StatsPanelUI", typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            go.transform.SetAsLastSibling();
            Debug.Log("[StatsPanelUI] Đã tạo màn hình thống kê trên canvas: " + canvas.name + " (" + canvas.renderMode + ")");
            var panel = go.AddComponent<StatsPanelUI>();
            panel.tracker = statsTracker;
            panel.stateManager = stateMgr;
            panel.Build();

            statsTracker.OnStatsChanged += panel.OnStatsChanged;
            if (stateMgr != null) stateMgr.OnRackScoreChanged += panel.OnScoreChanged;
            LocalizationManager.OnLanguageChanged -= panel.RefreshLocalizedTexts;
            LocalizationManager.OnLanguageChanged += panel.RefreshLocalizedTexts;
            return panel;
        }

        private void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // Lớp phủ làm tối nền + chặn click xuống bàn
            root = MakeImage(transform, "Dimmer", new Color(0.02f, 0.03f, 0.10f, 0.28f), true).gameObject;
            dimmerImg = root.GetComponent<Image>();
            var rootRt = (RectTransform)root.transform;
            Stretch(rootRt);
            canvasGroup = root.AddComponent<CanvasGroup>();

            // Khung ngoài (viền neon) + nền xanh đậm
            var border = MakeImage(root.transform, "Border", new Color(0.35f, 0.80f, 1f, 0.45f), false);
            panelRt = border.rectTransform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(1120f, 1000f);

            var inner = MakeImage(panelRt, "Body", new Color(0.03f, 0.09f, 0.30f, 0.82f), false);
            Stretch(inner.rectTransform, 2f);

            // Dải màu phía trên cho chiều sâu (xanh đậm → trong suốt)
            var topGlow = MakeImage(inner.transform, "TopGlow", new Color(0.12f, 0.35f, 0.85f, 0.30f), false);
            Anchor(topGlow.rectTransform, 0f, 0.80f, 1f, 1f);
            var topLine = MakeImage(inner.transform, "TopLine", new Color(0.45f, 0.85f, 1f, 0.55f), false);
            Anchor(topLine.rectTransform, 0f, 0.996f, 1f, 1f);

            // Thẻ người chơi: trái vát chéo góc trên-trái, phải vát chéo góc trên-phải (hình thang vuông)
            BuildPlayerCard(inner.transform, true, new Vector4(0.025f, 0.835f, 0.350f, 0.955f), ColorA, out nameLeft, out cardFrameA);
            BuildPlayerCard(inner.transform, false, new Vector4(0.650f, 0.835f, 0.975f, 0.955f), ColorB, out nameRight, out cardFrameB);

            // Tỷ số ở giữa: khung hình thang tối + viền sáng
            var scoreFrame = MakeSkewed(inner.transform, "ScoreFrame", new Color(0.45f, 0.85f, 1f, 0.40f), 0f, 0f, 22f, 22f);
            Anchor(scoreFrame.rectTransform, 0.372f, 0.835f, 0.628f, 0.955f);
            var scoreBg = MakeSkewed(scoreFrame.transform, "ScoreBg", new Color(0.01f, 0.03f, 0.14f, 0.88f), 0f, 0f, 22f, 22f);
            Stretch(scoreBg.rectTransform, 2f);

            scoreText = MakeText(scoreBg.transform, "Score", "0 : 0", 74, ColorWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(scoreText.rectTransform);
            scoreText.supportRichText = true;
            var shadow = scoreText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);

            infoText = MakeText(inner.transform, "Info", "", 16, new Color(1f, 0.80f, 0.35f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(infoText.rectTransform, 0.10f, 0.795f, 0.90f, 0.832f);

            // Tab: Cả trận / Ván hiện tại (nút vát chéo ghép thành thanh phân đoạn)
            tabMatchButton = MakeSlantedTab(inner.transform, "TabMatch", LocalizationManager.Get("stats_tab_match"), new Vector4(Tab0X0, TabY0, Tab0X1, TabY1), ColorTabIdle, null, out tabMatchLabel);
            tabRackButton = MakeSlantedTab(inner.transform, "TabRack", LocalizationManager.Get("stats_tab_rack"), new Vector4(Tab1X0, TabY0, Tab1X1, TabY1), ColorTabIdle, null, out tabRackLabel);
            tabMatchButton.onClick.AddListener(() => SetTab(0));
            tabRackButton.onClick.AddListener(() => SetTab(1));

            // Vạch trượt dưới tab đang chọn
            var ind = MakeImage(inner.transform, "TabIndicator", new Color(0.75f, 0.95f, 1f, 0.95f), false);
            tabIndicator = ind.rectTransform;
            ApplyIndicator();

            // Nút đóng
            var close = MakeSquareButton(inner.transform, "Close", "X", new Vector4(0.953f, 0.962f, 0.992f, 0.994f));
            close.onClick.AddListener(() => Hide());
            closeGo = close.gameObject;

            // Danh sách chỉ số
            var rowsArea = new GameObject("Rows", typeof(RectTransform));
            rowsArea.layer = gameObject.layer;
            rowsArea.transform.SetParent(inner.transform, false);
            var areaRt = (RectTransform)rowsArea.transform;
            rowsAreaRt = areaRt;
            Anchor(areaRt, 0.03f, 0.05f, 0.97f, 0.732f);
            BuildRows(areaRt);

            hintText = MakeText(inner.transform, "Hint", LocalizationManager.Get("stats_hint_close"), 16, ColorMuted, TextAnchor.MiddleCenter, FontStyle.Normal);
            Anchor(hintText.rectTransform, 0f, 0.005f, 1f, 0.045f);
            hintGo = hintText.gameObject;

            BuildResultElements(inner.transform);

            SetTab(0, false);
            ApplyMode();
            root.SetActive(false);
        }

        /// <summary>Thẻ người chơi hình thang vuông: vát chéo góc trên phía ngoài, tên lớn, thanh nhấn màu ở chân thẻ.</summary>
        private void BuildPlayerCard(Transform parent, bool leftSide, Vector4 a, Color accent, out Text nameText, out SkewedPanelGraphic frameOut)
        {
            float tl = leftSide ? CardCut : 0f;
            float tr = leftSide ? 0f : CardCut;

            var frame = MakeSkewed(parent, leftSide ? "CardA" : "CardB", new Color(accent.r, accent.g, accent.b, 0.70f), tl, tr, 0f, 0f);
            frameOut = frame;
            Anchor(frame.rectTransform, a.x, a.y, a.z, a.w);
            var bg = MakeSkewed(frame.transform, "Bg", new Color(0.02f, 0.07f, 0.26f, 0.92f), tl, tr, 0f, 0f);
            Stretch(bg.rectTransform, 2f);
            var tint = MakeSkewed(bg.transform, "Tint", new Color(accent.r, accent.g, accent.b, 0.20f), tl, tr, 0f, 0f);
            Stretch(tint.rectTransform);

            // Thanh nhấn màu ở chân thẻ
            var bar = MakeImage(bg.transform, "Accent", accent, false);
            Anchor(bar.rectTransform, 0f, 0f, 1f, 0.055f);

            // Tên người chơi (căn về phía trong, tránh góc vát)
            nameText = MakeText(bg.transform, "Name", "", 34, ColorWhite, leftSide ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, FontStyle.Bold);
            if (leftSide) Anchor(nameText.rectTransform, 0.13f, 0.12f, 0.97f, 0.95f);
            else Anchor(nameText.rectTransform, 0.03f, 0.12f, 0.87f, 0.95f);
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 16;
            nameText.resizeTextMaxSize = 34;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var sh = nameText.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void BuildRows(RectTransform area)
        {
            Color cOverview = new Color(0.20f, 0.85f, 1.00f);
            Color cAccuracy = new Color(0.30f, 0.90f, 0.50f);
            Color cBreak = new Color(0.72f, 0.52f, 1.00f);
            Color cFoul = new Color(1.00f, 0.38f, 0.38f);
            Color cTactic = new Color(1.00f, 0.80f, 0.30f);

            AddHeader("stat_overview", cOverview);
            AddRow("stat_shots", s => s.Get(StatId.Shots), null, false, Better.None);
            AddRow("stat_visits", s => s.Get(StatId.Visits), null, false, Better.None);
            AddRow("stat_balls_pocketed", s => s.Get(StatId.BallsPocketed), null, false, Better.High);
            AddRow("stat_longest_run", s => s.Get(StatId.LongestRun), null, false, Better.High);

            AddHeader("stat_accuracy", cAccuracy);
            AddRow("stat_pot_rate",
                s => PlayerStats.Ratio(s.Get(StatId.PotSuccess), s.Get(StatId.PotAttempts)) * 100f,
                s => Pct(s.Get(StatId.PotSuccess), s.Get(StatId.PotAttempts)), true, Better.High);
            AddRow("stat_called_rate",
                s => PlayerStats.Ratio(s.Get(StatId.CalledSuccess), s.Get(StatId.CalledAttempts)) * 100f,
                s => Pct(s.Get(StatId.CalledSuccess), s.Get(StatId.CalledAttempts)), true, Better.High);
            AddRow("stat_bih_converted",
                s => PlayerStats.Ratio(s.Get(StatId.BihConverted), s.Get(StatId.BihChances)) * 100f,
                s => Pct(s.Get(StatId.BihConverted), s.Get(StatId.BihChances)), true, Better.High);
            AddRow("stat_run_outs", s => s.Get(StatId.RunOuts), null, false, Better.High);

            AddHeader("stat_break", cBreak);
            AddRow("stat_legal_breaks",
                s => PlayerStats.Ratio(s.Get(StatId.BreakLegal), s.Get(StatId.Breaks)) * 100f,
                s => Pct(s.Get(StatId.BreakLegal), s.Get(StatId.Breaks)), true, Better.High);
            AddRow("stat_break_pocketed", s => s.Get(StatId.BreakPocketed), null, false, Better.High);
            AddRow("stat_break_and_runs", s => s.Get(StatId.BreakAndRuns), null, false, Better.High);

            AddHeader("stat_fouls", cFoul);
            AddRow("stat_total_fouls", s => s.Get(StatId.Fouls), null, false, Better.Low);
            AddRow("stat_foul_rate",
                s => PlayerStats.Ratio(s.Get(StatId.Fouls), s.Get(StatId.Shots)) * 100f,
                s => Pct(s.Get(StatId.Fouls), s.Get(StatId.Shots)), true, Better.Low);
            AddRow("stat_scratch_stat", s => s.Get(StatId.FoulScratch), null, false, Better.Low);
            AddRow("stat_wrong_ball_stat", s => s.Get(StatId.FoulWrongBall), null, false, Better.Low);
            AddRow("stat_no_rail_stat", s => s.Get(StatId.FoulNoRail), null, false, Better.Low);
            AddRow("stat_timeout_stat", s => s.Get(StatId.FoulTimeout), null, false, Better.Low);
            AddRow("stat_other_fouls",
                s => s.Get(StatId.FoulOffTable) + s.Get(StatId.FoulIllegalBreak) + s.Get(StatId.FoulOther), null, false, Better.Low);

            AddHeader("stat_tactics", cTactic);
            AddRow("stat_safeties",
                s => PlayerStats.Ratio(s.Get(StatId.SafetiesOk), s.Get(StatId.Safeties)) * 100f,
                s => Pct(s.Get(StatId.SafetiesOk), s.Get(StatId.Safeties)), true, Better.High);
            AddRow("stat_avg_power",
                s => PlayerStats.Ratio(s.Get(StatId.PowerSum), s.Get(StatId.Shots)) * 100f,
                s => Mathf.RoundToInt(PlayerStats.Ratio(s.Get(StatId.PowerSum), s.Get(StatId.Shots)) * 100f) + "%", true, Better.None);
            AddRow("stat_spin_shots", s => s.Get(StatId.SpinShots), null, false, Better.None);
            AddRow("stat_avg_think",
                s => PlayerStats.Ratio(s.Get(StatId.ThinkSum), s.Get(StatId.Shots)),
                s => PlayerStats.Ratio(s.Get(StatId.ThinkSum), s.Get(StatId.Shots)).ToString("0.0") + "s", false, Better.None,
                v => v.ToString("0.0") + "s");

            // Dựng đối tượng UI cho từng dòng theo trọng số chiều cao
            const float headerWeight = 1.05f;
            float totalWeight = 0f;
            foreach (var r in rows) totalWeight += r.isHeader ? headerWeight : 1f;

            float y = 1f;
            int statIndex = 0;
            foreach (var r in rows)
            {
                float h = (r.isHeader ? headerWeight : 1f) / totalWeight;
                float yMin = y - h;

                var rowGo = new GameObject(r.isHeader ? "Header" : "Row", typeof(RectTransform));
                rowGo.layer = gameObject.layer;
                rowGo.transform.SetParent(area, false);
                var rowRt = (RectTransform)rowGo.transform;
                Anchor(rowRt, 0f, yMin, 1f, y);

                if (r.isHeader)
                {
                    statIndex = 0;
                    BuildClusterHeader(rowRt, r);
                }
                else
                {
                    // Nền dòng xen kẽ đậm – nhạt
                    var band = MakeImage(rowRt, "Band", statIndex % 2 == 0
                        ? new Color(0.30f, 0.55f, 1f, 0.20f)
                        : new Color(0.01f, 0.03f, 0.16f, 0.38f), false);
                    Stretch(band.rectTransform);
                    statIndex++;

                    var trackColor = new Color(0.01f, 0.03f, 0.14f, 0.62f);
                    MakeImage(rowRt, "TrackL", trackColor, false).rectTransform
                        .SetAnchors(BarOuterLeft, BarY0, BarInnerLeft, BarY1);
                    MakeImage(rowRt, "TrackR", trackColor, false).rectTransform
                        .SetAnchors(BarInnerRight, BarY0, BarOuterRight, BarY1);

                    r.leftImg = MakeImage(rowRt, "FillL", ColorA, false);
                    r.leftFill = r.leftImg.rectTransform;
                    r.rightImg = MakeImage(rowRt, "FillR", ColorB, false);
                    r.rightFill = r.rightImg.rectTransform;

                    // Vạch sáng ở đầu mỗi thanh
                    r.leftCap = MakeImage(r.leftFill, "Cap", new Color(1f, 1f, 1f, 0.85f), false);
                    r.leftCap.rectTransform.anchorMin = new Vector2(0f, 0f);
                    r.leftCap.rectTransform.anchorMax = new Vector2(0f, 1f);
                    r.leftCap.rectTransform.offsetMin = Vector2.zero;
                    r.leftCap.rectTransform.offsetMax = new Vector2(3f, 0f);
                    r.rightCap = MakeImage(r.rightFill, "Cap", new Color(1f, 1f, 1f, 0.85f), false);
                    r.rightCap.rectTransform.anchorMin = new Vector2(1f, 0f);
                    r.rightCap.rectTransform.anchorMax = new Vector2(1f, 1f);
                    r.rightCap.rectTransform.offsetMin = new Vector2(-3f, 0f);
                    r.rightCap.rectTransform.offsetMax = Vector2.zero;

                    r.labelText = MakeText(rowRt, "Label", r.label, 16, new Color(0.88f, 0.92f, 1f, 1f), TextAnchor.MiddleCenter, FontStyle.Normal);
                    Anchor(r.labelText.rectTransform, BarInnerLeft, 0f, BarInnerRight, 1f);

                    r.leftText = MakeText(rowRt, "ValueL", "0", 20, ColorWhite, TextAnchor.MiddleRight, FontStyle.Bold);
                    Anchor(r.leftText.rectTransform, 0f, 0f, BarOuterLeft - 0.012f, 1f);
                    r.rightText = MakeText(rowRt, "ValueR", "0", 20, ColorWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Anchor(r.rightText.rectTransform, BarOuterRight + 0.012f, 0f, 1f, 1f);

                    ApplyFill(r, 0f, 0f);
                }

                y = yMin;
            }
        }

        /// <summary>Tiêu đề cụm: băng nghiêng nền màu, icon hình thoi, chữ lớn và đường kẻ kéo dài.</summary>
        private void BuildClusterHeader(RectTransform rowRt, Row r)
        {
            Color c = r.accent;

            // Đường kẻ mảnh kéo dài phía sau
            var line = MakeImage(rowRt, "Line", new Color(c.r, c.g, c.b, 0.35f), false);
            Anchor(line.rectTransform, 0.30f, 0.46f, 1f, 0.50f);

            // Băng nghiêng (hình thang): nền màu nhóm
            var bandFrame = MakeSkewed(rowRt, "BandFrame", new Color(c.r, c.g, c.b, 0.95f), 0f, 0f, 0f, 26f);
            Anchor(bandFrame.rectTransform, 0f, 0.08f, 0.34f, 0.98f);
            var bandBg = MakeSkewed(bandFrame.transform, "BandBg", new Color(c.r * 0.28f, c.g * 0.28f, c.b * 0.28f, 0.95f), 0f, 0f, 0f, 26f);
            bandBg.rectTransform.anchorMin = Vector2.zero;
            bandBg.rectTransform.anchorMax = Vector2.one;
            bandBg.rectTransform.offsetMin = new Vector2(5f, 2f);
            bandBg.rectTransform.offsetMax = new Vector2(-2f, -2f);

            // Icon hình thoi
            var diamond = MakeImage(bandBg.transform, "Icon", c, false);
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            diamond.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            diamond.rectTransform.anchoredPosition = new Vector2(20f, 0f);
            diamond.rectTransform.sizeDelta = new Vector2(11f, 11f);
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            r.labelText = MakeText(bandBg.transform, "Label", r.label, 17, new Color(1f, 1f, 1f, 1f), TextAnchor.MiddleLeft, FontStyle.Bold);
            r.labelText.rectTransform.anchorMin = Vector2.zero;
            r.labelText.rectTransform.anchorMax = Vector2.one;
            r.labelText.rectTransform.offsetMin = new Vector2(38f, 0f);
            r.labelText.rectTransform.offsetMax = Vector2.zero;
            r.labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private static string Pct(float num, float den)
        {
            if (den <= 0f) return "—";
            return Mathf.RoundToInt(num / den * 100f) + "% (" + Mathf.RoundToInt(num) + "/" + Mathf.RoundToInt(den) + ")";
        }

        private void AddHeader(string key, Color accent)
        {
            rows.Add(new Row { isHeader = true, labelKey = key, label = LocalizationManager.Get(key), accent = accent });
        }

        private void AddRow(string key, Func<PlayerStats, float> value, Func<PlayerStats, string> text, bool percent, Better better, Func<float, string> anim = null)
        {
            if (anim == null)
            {
                if (percent) anim = v => Mathf.RoundToInt(v) + "%";
                else anim = v => Mathf.RoundToInt(v).ToString();
            }
            rows.Add(new Row { labelKey = key, label = LocalizationManager.Get(key), value = value, text = text, anim = anim, percent = percent, better = better });
        }

        private Button MakeSquareButton(Transform parent, string name, string label, Vector4 anchors)
        {
            var img = MakeImage(parent, name, ColorTabIdle, true);
            Anchor(img.rectTransform, anchors.x, anchors.y, anchors.z, anchors.w);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            var t = MakeText(img.transform, "Label", label, 18, ColorWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform);
            UIHoverEffect.Attach(img.gameObject, true, null, null, 0f, 1.04f);
            return btn;
        }

        /// <summary>Tab dạng hình bình hành vát chéo, có hiệu ứng hover; tab đang chọn giữ màu cam nổi bật.</summary>
        private Button MakeSlantedTab(Transform parent, string name, string label, Vector4 anchors, Color baseColor, Color? hoverColor, out Text labelText)
        {
            var g = MakeSkewed(parent, name, baseColor, 22f, 0f, 0f, 22f);
            g.raycastTarget = true;
            Anchor(g.rectTransform, anchors.x, anchors.y, anchors.z, anchors.w);

            var btn = g.gameObject.AddComponent<Button>();
            btn.targetGraphic = g;
            btn.transition = Selectable.Transition.None;

            labelText = MakeText(g.transform, "Label", label, 19, ColorWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);
            var outline = labelText.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            UIHoverEffect.Attach(g.gameObject, true, hoverColor, null, 0f, 1.04f);
            return btn;
        }

        public void Toggle()
        {
            if (resultMode) return;
            if (open) Hide();
            else Show();
        }

        public void Show()
        {
            if (resultMode) return;
            if (open && !closing) return;
            if (!HudModalState.TryOpen(ModalId))
            {
                Debug.LogWarning("[StatsPanelUI] Không mở được: đang có popup khác mở (" + HudModalState.Current + ").");
                return;
            }

            open = true;
            closing = false;
            root.SetActive(true);

            indX0 = tab == 0 ? Tab0X0 : Tab1X0;
            indX1 = tab == 0 ? Tab0X1 : Tab1X1;
            ApplyIndicator();

            Refresh();
            StartReplay();
        }

        public void Hide()
        {
            if (resultMode) return;
            if (!open) return;
            HudModalState.Release(ModalId);
            open = false;
            closing = true;
        }

        private void SetTab(int index, bool refresh = true)
        {
            bool changed = index != tab;
            tab = index;
            SetSelected(tabMatchButton, tab == 0);
            SetSelected(tabRackButton, tab == 1);
            if (tabMatchLabel != null) tabMatchLabel.color = tab == 0 ? ColorNavyText : ColorWhite;
            if (tabRackLabel != null) tabRackLabel.color = tab == 1 ? ColorNavyText : ColorWhite;

            if (refresh)
            {
                Refresh();
                if (changed) StartReplay();
            }
        }

        private static void SetSelected(Button b, bool selected)
        {
            if (b == null) return;
            var fx = b.GetComponent<UIHoverEffect>();
            if (fx != null) fx.SetSelected(selected);
        }

        private void ApplyIndicator()
        {
            if (tabIndicator == null) return;
            Anchor(tabIndicator, indX0 + 0.006f, TabY0 - 0.010f, indX1 - 0.006f, TabY0 - 0.003f);
        }

        private void OnStatsChanged() { if (open) Refresh(); }
        private void OnScoreChanged(int a, int b) { if (open) Refresh(); }

        /// <summary>Cho mọi thanh về 0 rồi chạy lần lượt từng dòng ra giá trị thật (khi mở panel / đổi tab).</summary>
        private void StartReplay()
        {
            replaying = true;
            animClock = 0f;
            foreach (var r in rows)
            {
                if (r.isHeader) continue;
                r.leftCur = 0f;
                r.rightCur = 0f;
                ApplyFill(r, 0f, 0f);
                r.leftText.text = DisplayText(r, r.va, r.finalL, 0f);
                r.rightText.text = DisplayText(r, r.vb, r.finalR, 0f);
            }
        }

        private static string DisplayText(Row r, float value, string final, float e)
        {
            if (e >= 0.999f || final == "—") return final;
            return r.anim(value * e);
        }

        private void Refresh()
        {
            if (tracker == null) return;

            var stats = tracker.Stats;
            PlayerStats a = tab == 0 ? stats.match[0] : stats.rack[0];
            PlayerStats b = tab == 0 ? stats.match[1] : stats.rack[1];

            MatchState state = stateManager != null ? stateManager.State : null;
            nameLeft.text = PlayerName(state, 0).ToUpperInvariant();
            nameRight.text = PlayerName(state, 1).ToUpperInvariant();

            int winsA = state != null ? state.playerARackWins : 0;
            int winsB = state != null ? state.playerBRackWins : 0;
            scoreText.text = "<color=#33D9FF>" + winsA + "</color><color=#8FA6D6>  :  </color><color=#FF9E1F>" + winsB + "</color>";
            RefreshInfo(state);

            foreach (var r in rows)
            {
                if (r.isHeader) continue;

                float va = r.value(a);
                float vb = r.value(b);
                r.va = va;
                r.vb = vb;
                r.finalL = r.text != null ? r.text(a) : Mathf.RoundToInt(va).ToString();
                r.finalR = r.text != null ? r.text(b) : Mathf.RoundToInt(vb).ToString();
                if (!replaying)
                {
                    r.leftText.text = r.finalL;
                    r.rightText.text = r.finalR;
                }

                float max = r.percent ? 100f : Mathf.Max(va, vb, 1f);
                r.leftTar = Mathf.Clamp01(va / max);
                r.rightTar = Mathf.Clamp01(vb / max);

                bool tie = Mathf.Approximately(va, vb);
                bool aLeads = r.better == Better.None || tie || (r.better == Better.High ? va > vb : va < vb);
                bool bLeads = r.better == Better.None || tie || (r.better == Better.High ? vb > va : vb < va);
                var ca = ColorA; ca.a = aLeads ? 1f : 0.38f;
                var cb = ColorB; cb.a = bLeads ? 1f : 0.38f;
                r.leftImg.color = ca;
                r.rightImg.color = cb;

                // Giá trị của người dẫn trước tô màu theo người chơi, người còn lại mờ hơn
                bool highlight = r.better != Better.None && !tie;
                r.leftText.color = highlight ? (aLeads ? ColorA : ColorMuted) : ColorWhite;
                r.rightText.color = highlight ? (bLeads ? ColorB : ColorMuted) : ColorWhite;
            }
        }

        private void RefreshInfo(MatchState state)
        {
            string race = state != null ? ((int)state.settings.raceTarget).ToString() : "-";
            int rackNo = state != null ? state.rackNumber : 1;
            var stats = tracker.Stats;
            if (resultMode && state != null)
            {
                int totalRacks = state.playerARackWins + state.playerBRackWins;
                infoText.text = LocalizationManager.Format("stats_info_match", race, totalRacks, FormatTime(stats.matchTime));
                return;
            }
            infoText.text = LocalizationManager.Format("stats_info_rack", race, rackNo, FormatTime(stats.matchTime), FormatTime(stats.rackTime));
        }

        // ------------------------------------------------------------------ Màn hình kết thúc trận

        /// <summary>Tạo các thành phần chỉ hiện ở màn hình kết thúc trận: tiêu đề, thẻ "CHIẾN THẮNG" và 2 nút.</summary>
        private void BuildResultElements(Transform inner)
        {
            resultTitle = MakeText(inner, "ResultTitle", LocalizationManager.Get("stats_match_over"), 28, new Color(1f, 0.82f, 0.30f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Anchor(resultTitle.rectTransform, 0.30f, 0.958f, 0.70f, 0.998f);
            var ts = resultTitle.gameObject.AddComponent<Shadow>();
            ts.effectColor = new Color(0f, 0f, 0f, 0.6f);
            ts.effectDistance = new Vector2(2f, -2f);

            tagA = MakeWinnerTag(inner, new Vector4(0.085f, 0.940f, 0.305f, 0.985f), out tagAText);
            tagB = MakeWinnerTag(inner, new Vector4(0.695f, 0.940f, 0.915f, 0.985f), out tagBText);

            var rematch = MakeResultButton(inner, "BtnRematch", LocalizationManager.Get("stats_btn_rematch"), new Vector4(0.22f, 0.024f, 0.49f, 0.112f),
                new Color(0.25f, 0.92f, 0.58f, 1f), 0.45f, true, out btnRematchText);
            var lobby = MakeResultButton(inner, "BtnLobby", LocalizationManager.Get("stats_btn_lobby"), new Vector4(0.51f, 0.024f, 0.78f, 0.112f),
                new Color(1f, 0.38f, 0.36f, 1f), 0.6f, false, out btnLobbyText);
            rematch.onClick.AddListener(() => onRematch?.Invoke());
            lobby.onClick.AddListener(() => onLobby?.Invoke());
            btnRematchGo = rematch.gameObject;
            btnLobbyGo = lobby.gameObject;
        }

        /// <summary>
        /// Nút lớn màn hình kết trận, cùng ngôn ngữ thiết kế với nút HUD trong trận: nền xanh đậm tối dần xuống dưới,
        /// viền cyan, dải màu ở đáy (xanh lá = chơi lại, đỏ = về sảnh), hover chuyển cam. Hiệu ứng do <see cref="ResultButtonFx"/> đảm nhận.
        /// </summary>
        private Button MakeResultButton(Transform parent, string name, string label, Vector4 anchors, Color accentColor, float delay, bool pulse, out Text labelText)
        {
            const float s = 20f;
            Color bodyColor = new Color(0.16f, 0.27f, 0.44f, 0.96f);
            Color borderColor = new Color(0.30f, 0.78f, 0.95f, 0.75f);

            var root = MakeSkewed(parent, name, bodyColor, 0f, s, s, 0f);
            root.SetBottomDarken(0.5f);
            root.raycastTarget = true;
            Anchor(root.rectTransform, anchors.x, anchors.y, anchors.z, anchors.w);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = root;
            btn.transition = Selectable.Transition.None;

            // Viền chạy theo chu vi khung xéo (giống khung của nút HUD)
            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.layer = parent.gameObject.layer;
            frameGo.transform.SetParent(root.transform, false);
            var frame = frameGo.GetComponent<BorderProgressGraphic>();
            frame.raycastTarget = false;
            frame.SetInsets(0f, s, s, 0f);
            frame.SetBaseColor(borderColor);
            Stretch(frame.rectTransform);

            // Dải màu dưới đáy
            const float stripH = 5f;
            var strip = MakeSkewed(root.transform, "Accent", accentColor, s * 0.85f, s * 0.15f, s, 0f);
            var stripRt = strip.rectTransform;
            stripRt.anchorMin = new Vector2(0f, 0f);
            stripRt.anchorMax = new Vector2(1f, 0f);
            stripRt.pivot = new Vector2(0.5f, 0f);
            stripRt.anchoredPosition = Vector2.zero;
            stripRt.sizeDelta = new Vector2(0f, stripH);

            var clipGo = new GameObject("ShineClip", typeof(RectTransform), typeof(RectMask2D));
            clipGo.layer = parent.gameObject.layer;
            clipGo.transform.SetParent(root.transform, false);
            var clipRt = (RectTransform)clipGo.transform;
            Stretch(clipRt, 3f);

            var shine = MakeSkewed(clipGo.transform, "Shine", new Color(1f, 1f, 1f, 0.22f), 0f, 14f, 14f, 0f);
            var shineRt = shine.rectTransform;
            shineRt.anchorMin = new Vector2(0f, 0f);
            shineRt.anchorMax = new Vector2(0f, 1f);
            shineRt.pivot = new Vector2(0.5f, 0.5f);
            shineRt.sizeDelta = new Vector2(46f, 0f);
            shineRt.anchoredPosition = Vector2.zero;
            shineRt.gameObject.SetActive(false);

            labelText = MakeText(root.transform, "Label", label, 24, ColorWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);
            var sh = labelText.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);

            var fx = root.gameObject.AddComponent<ResultButtonFx>();
            fx.frame = frame;
            fx.body = root;
            fx.accent = strip;
            fx.shine = shineRt;
            fx.shineClip = clipRt;
            fx.frameBase = borderColor;
            fx.accentBase = accentColor;
            fx.bodyBase = bodyColor;
            fx.bodyHover = new Color(0.30f, 0.46f, 0.70f, 1f);
            fx.entranceDelay = delay;
            fx.pulse = pulse;

            return btn;
        }

        private GameObject MakeWinnerTag(Transform parent, Vector4 a, out Text labelText)
        {
            var frame = MakeSkewed(parent, "WinnerTag", new Color(1f, 0.90f, 0.45f, 1f), 14f, 0f, 0f, 14f);
            Anchor(frame.rectTransform, a.x, a.y, a.z, a.w);
            var body = MakeSkewed(frame.transform, "Body", new Color(1f, 0.74f, 0.15f, 1f), 14f, 0f, 0f, 14f);
            Stretch(body.rectTransform, 2f);
            labelText = MakeText(body.transform, "Label", LocalizationManager.Get("stats_winner_badge"), 17, ColorNavyText, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(labelText.rectTransform);
            frame.gameObject.SetActive(false);
            return frame.gameObject;
        }

        /// <summary>Áp bố cục theo chế độ: thường (Tab) hay kết thúc trận.</summary>
        private void ApplyMode()
        {
            bool res = resultMode;

            if (resultTitle != null) resultTitle.gameObject.SetActive(res);
            if (btnRematchGo != null) btnRematchGo.SetActive(res && !resultLobbyOnly);
            if (btnLobbyGo != null) btnLobbyGo.SetActive(res);
            if (res) ApplyResultButtons();
            if (closeGo != null) closeGo.SetActive(!res);
            if (hintGo != null) hintGo.SetActive(!res);
            if (tabMatchButton != null) tabMatchButton.gameObject.SetActive(!res);
            if (tabRackButton != null) tabRackButton.gameObject.SetActive(!res);
            if (tabIndicator != null) tabIndicator.gameObject.SetActive(!res);
            if (tagA != null) tagA.SetActive(res && resultWinner == 0);
            if (tagB != null) tagB.SetActive(res && resultWinner == 1);

            if (dimmerImg != null) dimmerImg.color = res ? new Color(0.02f, 0.03f, 0.10f, 0.55f) : new Color(0.02f, 0.03f, 0.10f, 0.28f);

            // Màn hình kết thúc: danh sách chỉ số dài ra để nhường chỗ cho 2 nút ở dưới, bỏ khu vực tab
            if (rowsAreaRt != null)
            {
                if (res) Anchor(rowsAreaRt, 0.03f, 0.135f, 0.97f, 0.790f);
                else Anchor(rowsAreaRt, 0.03f, 0.05f, 0.97f, 0.732f);
            }

            // Viền thẻ: người thắng viền vàng sáng, người thua mờ đi
            if (cardFrameA != null && cardFrameB != null)
            {
                Color gold = new Color(1f, 0.82f, 0.30f, 1f);
                var normalA = new Color(ColorA.r, ColorA.g, ColorA.b, 0.70f);
                var normalB = new Color(ColorB.r, ColorB.g, ColorB.b, 0.70f);
                cardFrameA.color = res ? (resultWinner == 0 ? gold : new Color(normalA.r, normalA.g, normalA.b, 0.30f)) : normalA;
                cardFrameB.color = res ? (resultWinner == 1 ? gold : new Color(normalB.r, normalB.g, normalB.b, 0.30f)) : normalB;
            }
        }

        /// <summary>
        /// Hiện màn hình kết thúc trận: thống kê cả trận, người thắng nổi bật, nút Chơi lại / Về sảnh.
        /// lobbyOnly: chỉ còn nút Về sảnh ở giữa (đối thủ đã rời trận). title: thay tiêu đề mặc định. rematchEnabled/rematchLabel: khóa nút Chơi lại (ví dụ Client chờ chủ phòng).
        /// </summary>
        public void ShowMatchResult(int winnerIndex, Action rematchAction, Action lobbyAction,
            bool lobbyOnly = false, string title = null, bool rematchEnabled = true, string rematchLabel = null)
        {
            resultMode = true;
            resultWinner = winnerIndex;
            onRematch = rematchAction;
            onLobby = lobbyAction;
            resultLobbyOnly = lobbyOnly;
            resultTitleOverride = title;
            resultRematchEnabled = rematchEnabled;
            resultRematchLabel = rematchLabel;

            HudModalState.ForceOpen("result");
            tab = 0;
            SetTab(0, false);
            ApplyMode();

            open = true;
            closing = false;
            root.SetActive(true);

            Refresh();
            StartReplay();
        }

        /// <summary>Đổi sang chế độ chỉ còn nút Về sảnh khi màn hình kết quả đang mở (đối thủ rời phòng sau khi trận đã kết thúc).</summary>
        public void SetLobbyOnly(bool value)
        {
            resultLobbyOnly = value;
            if (resultMode && root != null && root.activeSelf) ApplyMode();
        }

        /// <summary>Đặt nút theo chế độ: chỉ Về sảnh (căn giữa), nút Chơi lại bị khóa/đổi nhãn, tiêu đề tùy chỉnh.</summary>
        private void ApplyResultButtons()
        {
            if (btnLobbyGo != null)
            {
                var rt = (RectTransform)btnLobbyGo.transform;
                if (resultLobbyOnly) Anchor(rt, 0.36f, 0.024f, 0.64f, 0.112f);
                else Anchor(rt, 0.51f, 0.024f, 0.78f, 0.112f);
            }

            if (btnRematchGo != null)
            {
                var b = btnRematchGo.GetComponent<Button>();
                if (b != null) b.interactable = resultRematchEnabled;
                if (btnRematchText != null)
                {
                    btnRematchText.text = string.IsNullOrEmpty(resultRematchLabel) ? LocalizationManager.Get("stats_btn_rematch") : resultRematchLabel;
                    var c = btnRematchText.color; c.a = resultRematchEnabled ? 1f : 0.55f; btnRematchText.color = c;
                }
            }

            if (resultTitle != null)
            {
                string t = resultLobbyOnly && string.IsNullOrEmpty(resultTitleOverride) ? LocalizationManager.Get("stats_opponent_left") : resultTitleOverride;
                resultTitle.text = string.IsNullOrEmpty(t) ? LocalizationManager.Get("stats_match_over") : t;
            }
        }

        /// <summary>Đóng màn hình kết thúc trận (sau khi bấm Chơi lại / Về sảnh).</summary>
        public void CloseResult()
        {
            if (Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.StopMatchResultAudio();
            }
            if (!resultMode) return;
            HudModalState.Release("result");
            open = false;
            closing = true;
        }

        private static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
        }

        private static string PlayerName(MatchState state, int index)
        {
            if (state != null && state.players != null && index < state.players.Length && !string.IsNullOrEmpty(state.players[index].displayName))
                return state.players[index].displayName;
            return LocalizationManager.Format("stats_player_n", index + 1);
        }

        // ------------------------------------------------------------------ Hoạt ảnh

        private void Update()
        {
            if (root == null) return;

            // Popup khác (ví dụ kết quả ván) chiếm quyền → tự đóng bảng thống kê
            if (open && HudModalState.Current != (resultMode ? "result" : ModalId))
            {
                open = false;
                closing = true;
            }

            float dt = Time.unscaledDeltaTime;

            float target = open ? 1f : 0f;
            if (!Mathf.Approximately(anim, target))
            {
                anim = Mathf.MoveTowards(anim, target, dt * 7f);
                float e = anim * anim * (3f - 2f * anim);
                canvasGroup.alpha = e;
                panelRt.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, e);
            }
            else if (closing && !open)
            {
                closing = false;
                root.SetActive(false);
                if (resultMode)
                {
                    resultMode = false;
                    ApplyMode();
                }
                return;
            }

            if (!root.activeSelf) return;

            // Vạch trượt dưới tab đang chọn
            float tx0 = tab == 0 ? Tab0X0 : Tab1X0;
            float tx1 = tab == 0 ? Tab0X1 : Tab1X1;
            float k = 1f - Mathf.Exp(-14f * dt);
            indX0 = Mathf.Lerp(indX0, tx0, k);
            indX1 = Mathf.Lerp(indX1, tx1, k);
            ApplyIndicator();

            if (replaying)
            {
                animClock += dt;
                int idx = 0;
                bool done = true;
                foreach (var r in rows)
                {
                    if (r.isHeader) continue;

                    float t = Mathf.Clamp01((animClock - idx * RowStagger) / RowDuration);
                    idx++;
                    if (t < 1f) done = false;
                    float e = 1f - Mathf.Pow(1f - t, 3f); // ease-out cubic

                    r.leftCur = r.leftTar * e;
                    r.rightCur = r.rightTar * e;
                    ApplyFill(r, r.leftCur, r.rightCur);
                    r.leftText.text = DisplayText(r, r.va, r.finalL, e);
                    r.rightText.text = DisplayText(r, r.vb, r.finalR, e);
                }

                if (done)
                {
                    replaying = false;
                    foreach (var r in rows)
                    {
                        if (r.isHeader) continue;
                        r.leftText.text = r.finalL;
                        r.rightText.text = r.finalR;
                    }
                }
            }
            else
            {
                // Số liệu thay đổi giữa trận: thanh trượt mượt sang giá trị mới
                foreach (var r in rows)
                {
                    if (r.isHeader) continue;
                    float step = dt * 3.5f;
                    r.leftCur = Mathf.MoveTowards(r.leftCur, r.leftTar, step);
                    r.rightCur = Mathf.MoveTowards(r.rightCur, r.rightTar, step);
                    ApplyFill(r, r.leftCur, r.rightCur);
                }
            }

            if (open)
            {
                timeRefresh += dt;
                if (timeRefresh >= 0.5f)
                {
                    timeRefresh = 0f;
                    RefreshInfo(stateManager != null ? stateManager.State : null);
                }
            }
        }

        private static void ApplyFill(Row r, float left, float right)
        {
            float span = BarInnerLeft - BarOuterLeft;
            r.leftFill.SetAnchors(BarInnerLeft - span * left, BarY0, BarInnerLeft, BarY1);
            r.rightFill.SetAnchors(BarInnerRight, BarY0, BarInnerRight + span * right, BarY1);
            if (r.leftCap != null) r.leftCap.enabled = left > 0.015f;
            if (r.rightCap != null) r.rightCap.enabled = right > 0.015f;
        }

        private void RefreshLocalizedTexts()
        {
            if (tabMatchLabel != null) tabMatchLabel.text = LocalizationManager.Get("stats_tab_match");
            if (tabRackLabel != null) tabRackLabel.text = LocalizationManager.Get("stats_tab_rack");
            if (hintText != null) hintText.text = LocalizationManager.Get("stats_hint_close");
            if (resultTitle != null) resultTitle.text = LocalizationManager.Get("stats_match_over");
            if (btnRematchText != null) btnRematchText.text = LocalizationManager.Get("stats_btn_rematch");
            if (btnLobbyText != null) btnLobbyText.text = LocalizationManager.Get("stats_btn_lobby");
            if (tagAText != null) tagAText.text = LocalizationManager.Get("stats_winner_badge");
            if (tagBText != null) tagBText.text = LocalizationManager.Get("stats_winner_badge");

            foreach (var r in rows)
            {
                if (r != null && r.labelText != null && !string.IsNullOrEmpty(r.labelKey))
                {
                    r.label = LocalizationManager.Get(r.labelKey);
                    r.labelText.text = r.label;
                }
            }

            if (open) Refresh();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            HudModalState.Release(ModalId);
            if (tracker != null) tracker.OnStatsChanged -= OnStatsChanged;
            if (stateManager != null) stateManager.OnRackScoreChanged -= OnScoreChanged;
        }

        // ------------------------------------------------------------------ Tiện ích dựng UI

        private Image MakeImage(Transform parent, string name, Color color, bool raycast)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Hình tứ giác có thể vát cạnh (hình thang / bình hành) — dùng SkewedPanelGraphic có sẵn.</summary>
        private SkewedPanelGraphic MakeSkewed(Transform parent, string name, Color color, float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var g = go.GetComponent<SkewedPanelGraphic>();
            g.color = color;
            g.raycastTarget = false;
            g.SetInsets(topLeft, topRight, bottomLeft, bottomRight);
            return g;
        }

        private Text MakeText(Transform parent, string name, string content, int size, Color color, TextAnchor align, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        private static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.SetAnchors(x0, y0, x1, y1);
        }
    }

    internal static class StatsRectExtensions
    {
        public static void SetAnchors(this RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
