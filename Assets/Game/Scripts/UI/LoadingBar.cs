using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Thanh tải kiểu chuyên nghiệp: rãnh mảnh có vạch chia 25/50/75%, phần đã tải tô gradient (xanh ngọc - lục - vàng)
    /// được "lộ ra" từ TRÁI sang PHẢI bằng RectMask2D, vệt sáng quét qua, đầu thanh phát sáng; phần trăm lớn bên phải, trạng thái bên trái.
    /// </summary>
    public class LoadingBar
    {
        private const float TrackHeight = 10f;
        private const float TrackY = -14f;

        public Text Status { get; private set; }
        public Text Percent { get; private set; }

        private RectTransform maskRect;
        private RectTransform shimmer;
        private RectTransform head;
        private Image headGlow;
        private float width;
        private float progress;

        private static Sprite gradientSprite;
        private static Sprite shimmerSprite;

        /// <summary>Dựng thanh. anchor: (0.5,0.5) = giữa màn hình, (0,0.5) = cột trái. pos là toạ độ TÂM thanh so với neo.</summary>
        public static LoadingBar Build(Transform parent, Vector2 anchor, Vector2 pos, float width, TextAnchor statusAlign = TextAnchor.MiddleLeft)
        {
            LoadingBar b = new LoadingBar { width = width };

            GameObject c = new GameObject("LoadingBar", typeof(RectTransform));
            c.transform.SetParent(parent, false);
            RectTransform cr = (RectTransform)c.transform;
            cr.anchorMin = cr.anchorMax = anchor;
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.anchoredPosition = pos;
            cr.sizeDelta = new Vector2(width, 90f);

            // trạng thái (trái) + phần trăm (phải)
            b.Status = LoadingUiKit.NewText(c.transform, "Status", "", 18, new Color(1f, 1f, 1f, 0.85f), statusAlign, FontStyle.Bold);
            RectTransform sr = b.Status.rectTransform;
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0f, 0.5f);
            sr.anchoredPosition = new Vector2(-width * 0.5f, 22f);
            sr.sizeDelta = new Vector2(width * 0.7f, 28f);

            b.Percent = LoadingUiKit.NewText(c.transform, "Percent", "0%", 42, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            RectTransform pr = b.Percent.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(1f, 0.5f);
            pr.anchoredPosition = new Vector2(width * 0.5f, 26f);
            pr.sizeDelta = new Vector2(220f, 52f);
            b.Percent.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

            // rãnh + viền mờ
            Image track = LoadingUiKit.NewImage(c.transform, "Track", null, new Color(1f, 1f, 1f, 0.10f));
            PlaceLeftPivot(track.rectTransform, new Vector2(-width * 0.5f, TrackY), new Vector2(width, TrackHeight));
            Image trackLine = LoadingUiKit.NewImage(c.transform, "TrackLine", null, new Color(1f, 1f, 1f, 0.18f));
            PlaceLeftPivot(trackLine.rectTransform, new Vector2(-width * 0.5f, TrackY - TrackHeight * 0.5f - 1f), new Vector2(width, 1f));

            // vầng sáng dưới phần đã tải
            // (được vẽ bằng ảnh gradient bên trong mặt nạ cùng với phần tô)

            // mặt nạ: chiều rộng = tiến độ, neo mép TRÁI nên thanh luôn chạy từ đầu đến cuối
            GameObject m = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
            m.transform.SetParent(c.transform, false);
            b.maskRect = (RectTransform)m.transform;
            PlaceLeftPivot(b.maskRect, new Vector2(-width * 0.5f, TrackY), new Vector2(0f, TrackHeight));

            Image fill = LoadingUiKit.NewImage(m.transform, "Fill", GradientSprite(), Color.white);
            RectTransform fr = fill.rectTransform;
            fr.anchorMin = fr.anchorMax = new Vector2(0f, 0.5f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.anchoredPosition = Vector2.zero;
            fr.sizeDelta = new Vector2(width, TrackHeight);

            Image sh = LoadingUiKit.NewImage(m.transform, "Shimmer", ShimmerSprite(), new Color(1f, 1f, 1f, 0.75f));
            b.shimmer = sh.rectTransform;
            b.shimmer.anchorMin = b.shimmer.anchorMax = new Vector2(0f, 0.5f);
            b.shimmer.pivot = new Vector2(0.5f, 0.5f);
            b.shimmer.sizeDelta = new Vector2(180f, TrackHeight);

            // vạch chia 25 / 50 / 75 %
            for (int i = 1; i <= 3; i++)
            {
                Image tick = LoadingUiKit.NewImage(c.transform, "Tick" + i, null, new Color(1f, 1f, 1f, 0.28f));
                tick.rectTransform.anchorMin = tick.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                tick.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                tick.rectTransform.anchoredPosition = new Vector2(-width * 0.5f + width * i * 0.25f, TrackY);
                tick.rectTransform.sizeDelta = new Vector2(2f, TrackHeight + 10f);
            }

            // đầu thanh phát sáng
            GameObject h = new GameObject("Head", typeof(RectTransform));
            h.transform.SetParent(c.transform, false);
            b.head = (RectTransform)h.transform;
            b.head.anchorMin = b.head.anchorMax = new Vector2(0.5f, 0.5f);
            b.head.pivot = new Vector2(0.5f, 0.5f);
            b.head.sizeDelta = new Vector2(10f, 10f);
            b.headGlow = LoadingUiKit.NewImage(h.transform, "Glow", AvatarLibrary.DotSprite, new Color(0.55f, 1f, 0.85f, 0.45f));
            LoadingUiKit.PlaceCenter(b.headGlow.rectTransform, Vector2.zero, new Vector2(54f, 54f));
            Image core = LoadingUiKit.NewImage(h.transform, "Core", AvatarLibrary.DotSprite, Color.white);
            LoadingUiKit.PlaceCenter(core.rectTransform, Vector2.zero, new Vector2(16f, 16f));

            b.SetProgress(0f);
            return b;
        }

        private static void PlaceLeftPivot(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        public void SetProgress(float p)
        {
            progress = Mathf.Clamp01(p);
            if (maskRect != null) maskRect.sizeDelta = new Vector2(width * progress, TrackHeight);
            if (head != null)
            {
                head.anchoredPosition = new Vector2(-width * 0.5f + width * progress, TrackY);
                head.gameObject.SetActive(progress > 0.004f && progress < 0.998f);
            }
            if (Percent != null) Percent.text = Mathf.RoundToInt(progress * 100f) + "%";
        }

        /// <summary>Hiệu ứng: vệt sáng quét qua phần đã tải, đầu thanh nhấp nháy.</summary>
        public void Tick(float t)
        {
            if (shimmer != null)
            {
                float k = Mathf.Repeat(t * 0.55f, 1f);
                shimmer.anchoredPosition = new Vector2(Mathf.Lerp(-120f, width + 120f, k), 0f);
            }
            if (head != null)
            {
                head.localScale = Vector3.one * (1f + 0.22f * Mathf.Sin(t * 8f));
                if (headGlow != null)
                {
                    Color c = headGlow.color;
                    c.a = 0.38f + 0.12f * Mathf.Sin(t * 8f);
                    headGlow.color = c;
                }
            }
        }

        private static Sprite GradientSprite()
        {
            if (gradientSprite != null) return gradientSprite;
            const int n = 256;
            Texture2D tex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color a = new Color(0.30f, 0.80f, 1f, 1f);      // xanh ngọc
            Color b = new Color(0.25f, 0.95f, 0.55f, 1f);   // lục
            Color c = new Color(1f, 0.82f, 0.30f, 1f);      // vàng
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                Color col = u < 0.6f ? Color.Lerp(a, b, u / 0.6f) : Color.Lerp(b, c, (u - 0.6f) / 0.4f);
                tex.SetPixel(i, 0, col);
            }
            tex.Apply();
            gradientSprite = Sprite.Create(tex, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 100f);
            return gradientSprite;
        }

        private static Sprite ShimmerSprite()
        {
            if (shimmerSprite != null) return shimmerSprite;
            const int n = 64;
            Texture2D tex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f);
                tex.SetPixel(i, 0, new Color(1f, 1f, 1f, a * a));
            }
            tex.Apply();
            shimmerSprite = Sprite.Create(tex, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 100f);
            return shimmerSprite;
        }
    }

    /// <summary>
    /// Bộ điều tốc tiến độ để thanh tải chạy KHÔNG đều như tải thật: các đoạn chạy nhanh xen kẽ các quãng chững lại.
    /// Tiến độ hiển thị = min(tiến độ thật, đường cong này).
    /// </summary>
    public class ProgressPacer
    {
        private readonly float[] tx;
        private readonly float[] vy;

        public ProgressPacer()
        {
            int interior = 6;
            tx = new float[interior + 2];
            vy = new float[interior + 2];
            tx[0] = 0f; vy[0] = 0f;
            tx[interior + 1] = 1f; vy[interior + 1] = 1f;

            float t = 0f, v = 0f;
            for (int i = 1; i <= interior; i++)
            {
                // thời gian tăng đều theo bước ngẫu nhiên, giá trị tăng bất đều (có đoạn gần như đứng yên)
                t += Random.Range(0.07f, 0.20f);
                bool stall = Random.value < 0.35f;
                v += stall ? Random.Range(0.005f, 0.03f) : Random.Range(0.08f, 0.22f);
                tx[i] = Mathf.Min(t, 0.93f);
                vy[i] = Mathf.Min(v, 0.96f);
            }
            // đảm bảo tăng đơn điệu
            for (int i = 1; i < tx.Length; i++)
            {
                if (tx[i] <= tx[i - 1]) tx[i] = tx[i - 1] + 0.01f;
                if (vy[i] < vy[i - 1]) vy[i] = vy[i - 1];
            }
            tx[tx.Length - 1] = Mathf.Max(tx[tx.Length - 1], tx[tx.Length - 2] + 0.02f);
        }

        /// <summary>x: phần thời gian đã trôi (0..1) -> giá trị tiến độ (0..1).</summary>
        public float Evaluate(float x)
        {
            x = Mathf.Clamp01(x / Mathf.Max(0.0001f, tx[tx.Length - 1]));
            for (int i = 1; i < tx.Length; i++)
            {
                if (x <= tx[i])
                {
                    float k = Mathf.InverseLerp(tx[i - 1], tx[i], x);
                    k = k * k * (3f - 2f * k); // smoothstep: nhanh dần rồi chậm dần trong mỗi đoạn
                    return Mathf.Lerp(vy[i - 1], vy[i], k);
                }
            }
            return 1f;
        }
    }
}
