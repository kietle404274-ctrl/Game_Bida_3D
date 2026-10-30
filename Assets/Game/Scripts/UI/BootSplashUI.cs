using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Màn hình khởi động (Boot) dựng hoàn toàn bằng code: ảnh nghệ thuật vừa khít toàn màn hình, logo bi số 8,
    /// thanh tải chạy từ trái sang phải với tốc độ không đều theo tiến độ tải scene thật, bokeh bay.
    /// Canvas được giữ qua scene Lobby rồi mờ dần để không bị giật hình khi chuyển cảnh.
    /// </summary>
    public class BootSplashUI : MonoBehaviour
    {
        private static readonly Color Gold = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color Cyan = new Color(0.40f, 0.85f, 1f, 1f);

        private CanvasGroup group;
        private RectTransform bgRect;
        private LoadingBar bar;
        private ProgressPacer pacer; // tạo trong Build() vì Random.Range không được gọi trong khởi tạo trường của MonoBehaviour
        private Image ballRing;
        private RectTransform[] bokeh;
        private Image[] bokehImg;
        private float[] bokehSpeed;
        private float[] bokehPhase;

        private float progress;
        private float startTime;
        private float fade = 0f;
        private bool fadingOut;

        private static readonly string[] StatusVi =
        {
            "Đang khởi tạo hệ thống",
            "Nạp bàn bi và vật lý",
            "Chuẩn bị âm thanh và giao diện",
            "Sắp xong",
            "Sẵn sàng"
        };

        private static readonly string[] StatusEn =
        {
            "Initializing systems",
            "Loading table and physics",
            "Preparing audio and interface",
            "Almost there",
            "Ready"
        };

        // ------------------------------------------------------------------ tạo

        public static BootSplashUI Create()
        {
            GameObject go = new GameObject("BootSplashCanvas");
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<CanvasGroup>();
            DontDestroyOnLoad(go);

            BootSplashUI ui = go.AddComponent<BootSplashUI>();
            ui.Build();
            return ui;
        }

        private static bool Vi => LoadingUiKit.Vi;

        private void Build()
        {
            group = GetComponent<CanvasGroup>();
            pacer = new ProgressPacer();
            group.alpha = 0f;
            group.blocksRaycasts = true;
            startTime = Time.unscaledTime;
            Transform root = transform;

            // ảnh nghệ thuật: vừa khít (thấy toàn bộ ảnh, không cắt/bóp tỉ lệ) + nền mờ phía sau lấp kín đối với mọi tỉ lệ màn hình
            bgRect = LoadingUiKit.BuildArt(root, Resources.Load<Texture2D>("Boot/boot_splash_art"));

            // lớp tối bên trái để chữ nổi bật + lớp tối phía dưới
            Image leftShade = LoadingUiKit.NewImage(root, "LeftShade", LoadingUiKit.MakeGradient(true), Color.white);
            LoadingUiKit.Stretch(leftShade.rectTransform);
            Image bottomShade = LoadingUiKit.NewImage(root, "BottomShade", LoadingUiKit.MakeGradient(false, 0.85f, 0.42f), Color.white);
            LoadingUiKit.Stretch(bottomShade.rectTransform);

            BuildBokeh(root);
            BuildLogo(root);

            // thanh tải ở cột trái, dưới logo (tâm thanh = 150 + 380)
            bar = LoadingBar.Build(root, new Vector2(0f, 0.5f), new Vector2(530f, -150f), 760f);
            SetProgress(0f);
        }

        private void BuildLogo(Transform root)
        {
            // Bi số 8 + vòng sáng
            RectTransform ball = NewAnchored(root, "Ball", new Vector2(150f, 270f), new Vector2(130f, 130f));
            Image b = ball.gameObject.AddComponent<Image>();
            b.sprite = AvatarLibrary.DotSprite;
            b.color = new Color(0.04f, 0.04f, 0.06f, 1f);
            b.raycastTarget = false;
            Image w = LoadingUiKit.NewImage(ball, "White", AvatarLibrary.DotSprite, Color.white);
            w.rectTransform.anchorMin = w.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            w.rectTransform.sizeDelta = new Vector2(62f, 62f);
            Text eight = LoadingUiKit.NewText(ball, "Eight", "8", 46, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            eight.rectTransform.anchorMin = eight.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            eight.rectTransform.sizeDelta = new Vector2(62f, 62f);

            ballRing = LoadingUiKit.NewImage(ball, "Ring", AvatarLibrary.RingSprite, Cyan);
            ballRing.rectTransform.anchorMin = ballRing.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ballRing.rectTransform.sizeDelta = new Vector2(170f, 170f);

            Text t1 = LoadingUiKit.NewText(root, "Title1", "8-BALL", 124, Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceLeft(t1.rectTransform, new Vector2(150f, 100f), new Vector2(1000f, 140f));
            t1.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4f, -4f);

            Text t2 = LoadingUiKit.NewText(root, "Title2", "BILLIARDS 3D", 60, Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            PlaceLeft(t2.rectTransform, new Vector2(156f, 0f), new Vector2(1000f, 80f));
            t2.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);

            Image line = LoadingUiKit.NewImage(root, "GoldLine", null, Gold);
            PlaceLeft(line.rectTransform, new Vector2(158f, -52f), new Vector2(160f, 4f));
        }

        private void BuildBokeh(Transform root)
        {
            const int n = 22;
            bokeh = new RectTransform[n];
            bokehImg = new Image[n];
            bokehSpeed = new float[n];
            bokehPhase = new float[n];
            Color[] cols = { Cyan, Gold, new Color(1f, 1f, 1f, 1f) };
            for (int i = 0; i < n; i++)
            {
                Image im = LoadingUiKit.NewImage(root, "Bokeh" + i, AvatarLibrary.DotSprite, cols[i % cols.Length]);
                RectTransform r = im.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                float s = Random.Range(8f, 34f);
                r.sizeDelta = new Vector2(s, s);
                r.anchoredPosition = new Vector2(Random.Range(-960f, 960f), Random.Range(-540f, 540f));
                bokeh[i] = r;
                bokehImg[i] = im;
                bokehSpeed[i] = Random.Range(8f, 28f);
                bokehPhase[i] = Random.Range(0f, 6.28f);
            }
        }

        // ------------------------------------------------------------------ chạy

        /// <summary>Bắt đầu tải; coroutine chạy trên chính canvas (DontDestroyOnLoad) nên không bị dừng khi scene Boot bị hủy.</summary>
        public void Begin(string sceneName, float minSeconds)
        {
            StartCoroutine(Run(sceneName, minSeconds));
        }

        /// <summary>Tải scene đích bất đồng bộ; thanh tiến trình bám theo tiến độ thật nhưng chạy không đều (nhanh - chững xen kẽ).</summary>
        private IEnumerator Run(string sceneName, float minSeconds)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null) { Destroy(gameObject); yield break; }
            op.allowSceneActivation = false;

            float elapsed = 0f;
            float shown = 0f;
            while (true)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                float real = Mathf.Clamp01(op.progress / 0.9f);
                float paced = pacer.Evaluate(elapsed / Mathf.Max(0.1f, minSeconds));
                shown = Mathf.MoveTowards(shown, Mathf.Min(real, paced), dt * 2.5f);
                SetProgress(shown);
                if (shown >= 0.999f && real >= 1f) break;
                yield return null;
            }

            SetProgress(1f);
            yield return new WaitForSecondsRealtime(0.45f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            fadingOut = true;
            while (group.alpha > 0.01f) yield return null;
            Destroy(gameObject);
        }

        private void SetProgress(float p)
        {
            progress = Mathf.Clamp01(p);
            if (bar != null) bar.SetProgress(progress);
        }

        private void Update()
        {
            float t = Time.unscaledTime - startTime;

            // mờ vào / mờ ra
            if (fadingOut) fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.2f);
            else fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 2.0f);
            if (group != null) group.alpha = fade;

            if (bgRect != null) bgRect.localPosition = new Vector3(Mathf.Sin(t * 0.4f) * 8f, Mathf.Cos(t * 0.3f) * 5f, 0f); // trôi nhẹ, không phóng to nên không mất chi tiết

            if (ballRing != null)
            {
                float pulse = 1f + 0.06f * Mathf.Sin(t * 3f);
                ballRing.rectTransform.localScale = Vector3.one * pulse;
                Color c = Cyan; c.a = 0.55f + 0.4f * Mathf.Sin(t * 3f);
                ballRing.color = c;
            }

            if (bar != null)
            {
                bar.Tick(t);
                string[] st = Vi ? StatusVi : StatusEn;
                int idx = progress >= 0.999f ? 4 : Mathf.Min(3, (int)(progress * 4f));
                string dots = idx == 4 ? "" : new string('.', 1 + (int)(t * 2.5f) % 3);
                bar.Status.text = st[idx] + dots;
            }

            // bokeh bay lên + nhấp nháy
            if (bokeh != null)
            {
                for (int i = 0; i < bokeh.Length; i++)
                {
                    Vector2 p = bokeh[i].anchoredPosition;
                    p.y += bokehSpeed[i] * Time.unscaledDeltaTime;
                    p.x += Mathf.Sin(t * 0.6f + bokehPhase[i]) * 6f * Time.unscaledDeltaTime;
                    if (p.y > 580f) { p.y = -580f; p.x = Random.Range(-960f, 960f); }
                    bokeh[i].anchoredPosition = p;
                    Color c = bokehImg[i].color;
                    c.a = 0.05f + 0.16f * (0.5f + 0.5f * Mathf.Sin(t * 1.4f + bokehPhase[i]));
                    bokehImg[i].color = c;
                }
            }
        }

        // ------------------------------------------------------------------ tiện ích

        private static RectTransform NewAnchored(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        /// <summary>Đặt phần tử vào cột bên trái: neo trái - giữa, pivot trái - giữa.</summary>
        private static void PlaceLeft(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }
    }
}
