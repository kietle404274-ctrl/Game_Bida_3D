using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hiển thị tỷ số trận đấu lên chiếc TV trong phòng (tự tìm renderer có material "TV Screen").
    /// Dựng Canvas World Space khớp kích thước màn hình TV, không cần chỉnh scene.
    /// </summary>
    public class TvScoreboardDisplay : MonoBehaviour
    {
        private const string ScreenMaterialName = "TV Screen";
        private const float RefHeight = 900f;

        private MatchStateManager msm;
        private Text nameA, nameB, scoreA, scoreB, info;
        private GameObject root;
        private float nextPoll;
        private string lastKey;

        private void Start()
        {
            Build();
        }

        private void Update()
        {
            if (root == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.25f;
            Refresh();
        }

        private void OnDestroy()
        {
            if (root != null) Destroy(root);
        }

        private void Build()
        {
            Renderer screen = null;
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && m.name.StartsWith(ScreenMaterialName)) { screen = r; break; }
                }
                if (screen != null) break;
            }
            if (screen == null) { Debug.LogWarning("[TV] Không tìm thấy màn hình TV."); enabled = false; return; }

            Transform t = screen.transform;
            Bounds lb = screen.localBounds;
            Vector3 s = t.lossyScale;
            Vector3 size = new Vector3(Mathf.Abs(lb.size.x * s.x), Mathf.Abs(lb.size.y * s.y), Mathf.Abs(lb.size.z * s.z));
            Vector3[] axes = { t.right, t.up, t.forward };
            float[] dims = { size.x, size.y, size.z };

            int thin = 0;
            for (int i = 1; i < 3; i++) if (dims[i] < dims[thin]) thin = i;

            // Hai trục còn lại: trục nằm ngang hơn là chiều rộng
            int a = (thin + 1) % 3, b = (thin + 2) % 3;
            int wi = Mathf.Abs(axes[a].y) < Mathf.Abs(axes[b].y) ? a : b;
            int hi = wi == a ? b : a;
            float w = dims[wi] * 0.92f, h = dims[hi] * 0.86f;

            Vector3 normal = axes[thin];
            Vector3 center = t.TransformPoint(lb.center);
            // Hướng pháp tuyến về phía bàn bi
            var table = FindObjectOfType<Game.Billiards.Physics.TableWrapper>();
            Vector3 target = table != null ? table.transform.position : Vector3.zero;
            if (Vector3.Dot(normal, target - center) < 0f) normal = -normal;

            root = new GameObject("TV Scoreboard Canvas");
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

            var rt = root.GetComponent<RectTransform>();
            float aspect = w / Mathf.Max(0.0001f, h);
            rt.sizeDelta = new Vector2(RefHeight * aspect, RefHeight);
            root.transform.localScale = Vector3.one * (h / RefHeight);
            Vector3 up = axes[hi];
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            root.transform.rotation = Quaternion.LookRotation(-normal, up);
            root.transform.position = center + normal * (dims[thin] * 0.5f + 0.003f) + up * (dims[hi] * 0.02f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            float W = rt.sizeDelta.x;

            // Nền màn hình
            var bg = Panel("BG", rt, new Color(0.02f, 0.03f, 0.05f, 1f));
            Stretch(bg.rectTransform);
            var band = Panel("Band", rt, new Color(0.10f, 0.14f, 0.22f, 1f));
            Anchor(band.rectTransform, 0.5f, 0.5f, W * 0.94f, RefHeight * 0.52f, 0, 20);
            var line = Panel("Line", rt, new Color(0.55f, 0.70f, 0.90f, 1f));
            Anchor(line.rectTransform, 0.5f, 0.5f, W * 0.94f, 5f, 0, 20 - RefHeight * 0.26f);

            info = Label("Info", rt, font, 44, FontStyle.Bold, new Color(0.7f, 0.8f, 0.95f), TextAnchor.MiddleCenter);
            Anchor(info.rectTransform, 0.5f, 0.5f, W * 0.9f, 80, 0, RefHeight * 0.36f);
            var tag = Label("Tag", rt, font, 36, FontStyle.Bold, new Color(0.9f, 0.3f, 0.3f), TextAnchor.MiddleLeft);
            tag.text = "● LIVE";
            Anchor(tag.rectTransform, 0, 1, 300, 70, W * 0.04f + 150, -RefHeight * 0.07f);

            nameA = Label("NameA", rt, font, 60, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(nameA.rectTransform, 0.5f, 0.5f, W * 0.34f, 120, -W * 0.30f, 20);
            nameB = Label("NameB", rt, font, 60, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(nameB.rectTransform, 0.5f, 0.5f, W * 0.34f, 120, W * 0.30f, 20);
            scoreA = Label("ScoreA", rt, font, 260, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(scoreA.rectTransform, 0.5f, 0.5f, W * 0.16f, 300, -W * 0.09f, 20);
            scoreB = Label("ScoreB", rt, font, 260, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Anchor(scoreB.rectTransform, 0.5f, 0.5f, W * 0.16f, 300, W * 0.09f, 20);
            var dash = Label("Dash", rt, font, 200, FontStyle.Bold, new Color(0.6f, 0.7f, 0.85f), TextAnchor.MiddleCenter);
            dash.text = "-";
            Anchor(dash.rectTransform, 0.5f, 0.5f, 120, 300, 0, 20);

            Refresh();
        }

        private static Image Panel(string n, Transform parent, Color c)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        private static Text Label(string n, Transform parent, Font f, int size, FontStyle st, Color c, TextAnchor al)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var tx = go.GetComponent<Text>();
            tx.font = f; tx.fontSize = size; tx.fontStyle = st; tx.color = c; tx.alignment = al;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.verticalOverflow = VerticalWrapMode.Overflow;
            tx.raycastTarget = false;
            return tx;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform r, float ax, float ay, float w, float h, float x, float y)
        {
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(x, y);
        }

        private void Refresh()
        {
            if (msm == null) msm = FindObjectOfType<MatchStateManager>();
            if (msm == null || msm.State == null || msm.State.players == null) return;
            var st = msm.State;

            string na = st.players != null && st.players.Length > 0 ? st.players[0].displayName : "Player A";
            string nb = st.players != null && st.players.Length > 1 ? st.players[1].displayName : "Player B";
            string key = na + "|" + nb + "|" + st.playerARackWins + "|" + st.playerBRackWins + "|" + st.rackNumber;
            if (key == lastKey) return;
            lastKey = key;

            nameA.text = na.ToUpperInvariant();
            nameB.text = nb.ToUpperInvariant();
            scoreA.text = st.playerARackWins.ToString();
            scoreB.text = st.playerBRackWins.ToString();
            int race = 0;
            int.TryParse(st.settings.raceTarget.ToString().Replace("RaceTo", ""), out race);
            info.text = "8-BALL  •  VÁN " + st.rackNumber + (race > 0 ? "  •  RACE TO " + race : "");
        }
    }
}
