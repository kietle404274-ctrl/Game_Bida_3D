using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>Hàm dựng giao diện dùng chung cho màn hình khởi động và màn hình tải (nền ảnh vừa khít, gradient, chữ, ảnh).</summary>
    public static class LoadingUiKit
    {
        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static bool Vi => LocalizationManager.CurrentLanguageIndex == 0;

        public static Image NewImage(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image im = go.GetComponent<Image>();
            im.sprite = sprite;
            im.color = color;
            im.raycastTarget = false;
            return im;
        }

        public static Text NewText(Transform parent, string name, string text, int size, Color color, TextAnchor anchor, FontStyle style)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            Text t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        /// <summary>Đặt phần tử theo neo giữa màn hình (pivot giữa).</summary>
        public static void PlaceCenter(RectTransform r, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        /// <summary>
        /// Nền ảnh: lớp sau phủ kín (làm tối) + lớp trước VỪA KHÍT (hiển thị toàn bộ ảnh, không bị cắt/bóp méo ở mọi tỉ lệ màn hình).
        /// Trả về RectTransform lớp ảnh chính (để làm hiệu ứng trôi nhẹ). Lớp đen nền có raycastTarget để chặn thao tác bên dưới.
        /// </summary>
        public static RectTransform BuildArt(Transform root, Texture2D tex)
        {
            Image black = NewImage(root, "Black", null, new Color(0.02f, 0.03f, 0.05f, 1f));
            Stretch(black.rectTransform);
            black.raycastTarget = true;
            if (tex == null) return null;

            Sprite art = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            float aspect = tex.width / (float)tex.height;

            Image backdrop = NewImage(root, "ArtBackdrop", art, new Color(0.33f, 0.33f, 0.36f, 1f));
            AspectRatioFitter bf = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            bf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            bf.aspectRatio = aspect;
            backdrop.rectTransform.localScale = Vector3.one * 1.12f;

            Image main = NewImage(root, "Art", art, Color.white);
            AspectRatioFitter mf = main.gameObject.AddComponent<AspectRatioFitter>();
            mf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            mf.aspectRatio = aspect;
            return main.rectTransform;
        }

        /// <summary>Gradient trắng chỉ đổi độ trong suốt: đậm nhất ở mép trái (u=0) rồi mờ dần đến u=reach. Dùng tô màu bằng Image.color.</summary>
        public static Sprite MakeAlphaGradient(float reach)
        {
            const int n = 128;
            Texture2D tex = new Texture2D(n, 1, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float a = 1f - Mathf.SmoothStep(0f, 1f, u / Mathf.Max(0.01f, reach));
                tex.SetPixel(i, 0, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, 1), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>Gradient tối: horizontal = true thì đậm ở bên trái, ngược lại đậm ở phía dưới.</summary>
        public static Sprite MakeGradient(bool horizontal, float strength = 0.9f, float reach = 0.62f)
        {
            int n = 128;
            Texture2D tex = horizontal ? new Texture2D(n, 1, TextureFormat.RGBA32, false) : new Texture2D(1, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float a = strength * (1f - Mathf.SmoothStep(0f, 1f, u / reach));
                Color c = new Color(0.02f, 0.03f, 0.06f, Mathf.Clamp01(a));
                if (horizontal) tex.SetPixel(i, 0, c); else tex.SetPixel(0, i, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
