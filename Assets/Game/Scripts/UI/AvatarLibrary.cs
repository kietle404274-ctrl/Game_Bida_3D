using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Bộ avatar mặc định (kiểu Discord) được vẽ hoàn toàn bằng code lúc chạy: nền tròn gradient + biểu tượng (trắng / chi tiết tối).
    /// Gồm biểu tượng hình học và các con vật. Không cần file ảnh; sprite tạo một lần rồi lưu cache.
    /// </summary>
    public static class AvatarLibrary
    {
        public const int Count = 27;
        private const int Size = 128;
        private const int Samples = 3;

        private static readonly Sprite[] cache = new Sprite[Count];
        private static Sprite ringSprite;
        private static Sprite dotSprite;

        private static readonly Color[] Top =
        {
            new Color(1.00f, 0.78f, 0.25f), new Color(0.35f, 0.90f, 1.00f), new Color(0.72f, 0.45f, 1.00f),
            new Color(1.00f, 0.45f, 0.55f), new Color(0.35f, 0.95f, 0.80f), new Color(0.50f, 0.60f, 1.00f),
            new Color(0.55f, 0.95f, 0.40f), new Color(0.55f, 0.62f, 0.72f), new Color(1.00f, 0.62f, 0.25f),
            new Color(1.00f, 0.40f, 0.35f), new Color(0.40f, 0.75f, 1.00f), new Color(0.95f, 0.45f, 0.85f)
        };

        private static readonly Color[] Bottom =
        {
            new Color(0.85f, 0.45f, 0.08f), new Color(0.08f, 0.45f, 0.80f), new Color(0.35f, 0.15f, 0.70f),
            new Color(0.70f, 0.10f, 0.25f), new Color(0.05f, 0.55f, 0.50f), new Color(0.18f, 0.20f, 0.65f),
            new Color(0.10f, 0.55f, 0.25f), new Color(0.12f, 0.17f, 0.28f), new Color(0.80f, 0.25f, 0.08f),
            new Color(0.60f, 0.08f, 0.12f), new Color(0.10f, 0.30f, 0.75f), new Color(0.55f, 0.12f, 0.55f)
        };

        private static readonly Color DetailColor = new Color(0.07f, 0.10f, 0.18f, 1f);

        // ---- Đa giác dựng sẵn (toạ độ -1..1, y hướng lên) ----
        private static readonly Vector2[] StarPoly = BuildStar();
        private static readonly Vector2[] BoltPoly =
        {
            new Vector2(0.12f, 0.65f), new Vector2(-0.32f, -0.05f), new Vector2(-0.02f, -0.05f),
            new Vector2(-0.12f, -0.65f), new Vector2(0.32f, 0.12f), new Vector2(0.02f, 0.12f)
        };
        private static readonly Vector2[] CrownPoly =
        {
            new Vector2(-0.6f, -0.4f), new Vector2(-0.6f, 0.25f), new Vector2(-0.3f, -0.05f), new Vector2(0f, 0.45f),
            new Vector2(0.3f, -0.05f), new Vector2(0.6f, 0.25f), new Vector2(0.6f, -0.4f)
        };
        private static readonly Vector2[] TrianglePoly =
        {
            new Vector2(0f, 0.6f), new Vector2(-0.55f, -0.45f), new Vector2(0.55f, -0.45f)
        };
        private static readonly Vector2[] CatEar = { new Vector2(0.5f, 0.12f), new Vector2(0.46f, 0.66f), new Vector2(0.1f, 0.36f) };
        private static readonly Vector2[] CatNose = { new Vector2(-0.05f, -0.1f), new Vector2(0.05f, -0.1f), new Vector2(0f, -0.17f) };
        private static readonly Vector2[] FoxFace =
        {
            new Vector2(0f, 0.15f), new Vector2(0.25f, 0.22f), new Vector2(0.62f, 0.6f), new Vector2(0.55f, -0.08f), new Vector2(0f, -0.6f)
        };
        private static readonly Vector2[] OwlTuft = { new Vector2(0.45f, 0.4f), new Vector2(0.3f, 0.66f), new Vector2(0.12f, 0.46f) };
        private static readonly Vector2[] OwlBeak = { new Vector2(-0.07f, -0.08f), new Vector2(0.07f, -0.08f), new Vector2(0f, -0.26f) };
        private static readonly Vector2[] PenguinBeak = { new Vector2(-0.09f, 0.12f), new Vector2(0.09f, 0.12f), new Vector2(0f, -0.02f) };
        private static readonly Vector2[] LionNose = { new Vector2(-0.07f, -0.08f), new Vector2(0.07f, -0.08f), new Vector2(0f, -0.17f) };
        private static readonly Vector2[] FishTail = { new Vector2(0.3f, 0f), new Vector2(0.68f, 0.3f), new Vector2(0.68f, -0.3f) };
        private static readonly Vector2[] SkullNose = { new Vector2(-0.05f, -0.1f), new Vector2(0.05f, -0.1f), new Vector2(0f, -0.01f) };

        public static int Wrap(int index) => ((index % Count) + Count) % Count;

        /// <summary>Màu nhấn đại diện của avatar (dùng tô viền / chữ).</summary>
        public static Color AccentColor(int index)
        {
            Color top, bottom;
            Palette(Wrap(index), out top, out bottom);
            return top;
        }

        private static void Palette(int id, out Color top, out Color bottom)
        {
            if (id < Top.Length)
            {
                top = Top[id];
                bottom = Bottom[id];
                return;
            }
            float h = (id * 0.137f + 0.05f) % 1f;
            top = Color.HSVToRGB(h, 0.55f, 1f);
            bottom = Color.HSVToRGB(h, 0.85f, 0.62f);
        }

        public static Sprite Get(int index)
        {
            int i = Wrap(index);
            if (cache[i] == null) cache[i] = BuildAvatar(i);
            return cache[i];
        }

        /// <summary>Vòng tròn rỗng (dùng làm viền chọn / viền avatar).</summary>
        public static Sprite RingSprite
        {
            get
            {
                if (ringSprite == null) ringSprite = CreateShapeSprite((u, v) =>
                {
                    float r2 = u * u + v * v;
                    return r2 <= 1f && r2 >= 0.78f * 0.78f;
                });
                return ringSprite;
            }
        }

        /// <summary>Hình tròn đặc màu trắng (dùng làm đèn LED / nền).</summary>
        public static Sprite DotSprite
        {
            get
            {
                if (dotSprite == null) dotSprite = CreateShapeSprite((u, v) => u * u + v * v <= 1f);
                return dotSprite;
            }
        }

        // ------------------------------------------------------------------ dựng sprite

        /// <summary>Vẽ một hình trắng nền trong suốt từ hàm kiểm tra điểm (toạ độ -1..1) với khử răng cưa.</summary>
        public static Sprite CreateShapeSprite(System.Func<float, float, bool> inside)
        {
            Texture2D tex = NewTexture();
            Color32[] px = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float cov = 0f;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / Samples) / Size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / Samples) / Size) * 2f - 1f;
                            if (inside(u, v)) cov += 1f;
                        }
                    }
                    cov /= Samples * Samples;
                    px[y * Size + x] = new Color(1f, 1f, 1f, cov);
                }
            }
            return Finish(tex, px);
        }

        private static Sprite BuildAvatar(int id)
        {
            Texture2D tex = NewTexture();
            Color32[] px = new Color32[Size * Size];
            Color top, bottom;
            Palette(id, out top, out bottom);
            Color glyphColor = new Color(1f, 1f, 1f, 1f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float circle = 0f, white = 0f, dark = 0f;
                    for (int sy = 0; sy < Samples; sy++)
                    {
                        for (int sx = 0; sx < Samples; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / Samples) / Size) * 2f - 1f;
                            float v = ((y + (sy + 0.5f) / Samples) / Size) * 2f - 1f;
                            if (u * u + v * v > 1f) continue;
                            circle += 1f;
                            int g = Glyph(id, u, v);
                            if (g == 1) white += 1f;
                            else if (g == 2) dark += 1f;
                        }
                    }

                    float total = Samples * Samples;
                    float alpha = circle / total;
                    if (alpha <= 0f)
                    {
                        px[y * Size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float cu = (x + 0.5f) / Size * 2f - 1f;
                    float cv = (y + 0.5f) / Size * 2f - 1f;
                    float dist = Mathf.Clamp01(Mathf.Sqrt(cu * cu + cv * cv));
                    Color bg = Color.Lerp(bottom, top, (cv + 1f) * 0.5f);
                    bg = Color.Lerp(bg, Color.white, (1f - dist) * 0.10f);
                    bg = Color.Lerp(bg, Color.black, Mathf.Pow(dist, 4f) * 0.22f);

                    Color c = Color.Lerp(bg, DetailColor, Mathf.Clamp01(dark / circle));
                    c = Color.Lerp(c, glyphColor, Mathf.Clamp01(white / circle));
                    c.a = alpha;
                    px[y * Size + x] = c;
                }
            }
            return Finish(tex, px);
        }

        private static Texture2D NewTexture()
        {
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Sprite Finish(Texture2D tex, Color32[] px)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------ biểu tượng
        // Trả về: 0 = nền, 1 = nét trắng, 2 = chi tiết tối (mắt, mũi...). Lớp ưu tiên cao được kiểm tra trước.

        private static bool Disc(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool Ell(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy <= 1f;
        }

        private static bool Box(float x, float y, float x0, float x1, float y0, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        private static int Glyph(int id, float x, float y)
        {
            float r2 = x * x + y * y;
            float ax = Mathf.Abs(x);
            switch (id)
            {
                case 0: return InPoly(StarPoly, x, y) ? 1 : 0;
                case 1: return InPoly(BoltPoly, x, y) ? 1 : 0;
                case 2: return InPoly(CrownPoly, x, y) ? 1 : 0;
                case 3:
                {
                    float hx = x / 0.55f;
                    float hy = (y + 0.02f) / 0.55f;
                    float a = hx * hx + hy * hy - 1f;
                    return a * a * a - hx * hx * hy * hy * hy <= 0f ? 1 : 0;
                }
                case 4: return ax + Mathf.Abs(y) <= 0.62f ? 1 : 0;
                case 5:
                {
                    float dx = x - 0.25f;
                    float dy = y - 0.12f;
                    return (r2 <= 0.55f * 0.55f && dx * dx + dy * dy > 0.48f * 0.48f) ? 1 : 0;
                }
                case 6:
                {
                    float hexD = Mathf.Max(ax * 0.866f + Mathf.Abs(y) * 0.5f, Mathf.Abs(y));
                    return ((hexD <= 0.6f && hexD >= 0.40f) || r2 <= 0.15f * 0.15f) ? 1 : 0;
                }
                case 7: return (r2 <= 0.6f * 0.6f && r2 >= 0.28f * 0.28f) ? 1 : 0;
                case 8: return InPoly(TrianglePoly, x, y) ? 1 : 0;
                case 9: return ((ax <= 0.18f && Mathf.Abs(y) <= 0.6f) || (Mathf.Abs(y) <= 0.18f && ax <= 0.6f)) ? 1 : 0;
                case 10:
                {
                    float r = Mathf.Sqrt(r2);
                    return ((r <= 0.6f && r >= 0.46f) || (r <= 0.32f && r >= 0.18f) || r <= 0.07f) ? 1 : 0;
                }
                case 11: return (r2 <= 0.6f * 0.6f && Mathf.Abs(y) > 0.18f) ? 1 : 0;

                case 12: // Mèo
                    if (Disc(ax, y, 0.18f, 0.04f, 0.075f)) return 2;
                    if (InPoly(CatNose, x, y)) return 2;
                    if (Ell(x, y, 0f, -0.07f, 0.5f, 0.43f)) return 1;
                    return InPoly(CatEar, ax, y) ? 1 : 0;
                case 13: // Chó
                    if (Disc(ax, y, 0.17f, 0.08f, 0.07f)) return 2;
                    if (Disc(x, y, 0f, -0.15f, 0.1f)) return 2;
                    if (Ell(ax, y, 0.5f, 0f, 0.16f, 0.36f)) return 2;
                    return Disc(x, y, 0f, -0.02f, 0.45f) ? 1 : 0;
                case 14: // Gấu
                    if (Disc(ax, y, 0.17f, 0.05f, 0.06f)) return 2;
                    if (Ell(x, y, 0f, -0.14f, 0.12f, 0.08f)) return 2;
                    if (Disc(x, y, 0f, -0.05f, 0.48f)) return 1;
                    return Disc(ax, y, 0.38f, 0.38f, 0.17f) ? 1 : 0;
                case 15: // Cáo
                    if (Disc(ax, y, 0.2f, 0.05f, 0.06f)) return 2;
                    if (Disc(x, y, 0f, -0.52f, 0.07f)) return 2;
                    return InPoly(FoxFace, ax, y) ? 1 : 0;
                case 16: // Cú
                    if (Disc(ax, y, 0.22f, 0.08f, 0.07f)) return 1;
                    if (Disc(ax, y, 0.22f, 0.08f, 0.17f)) return 2;
                    if (InPoly(OwlBeak, x, y)) return 2;
                    if (Disc(x, y, 0f, -0.02f, 0.55f)) return 1;
                    return InPoly(OwlTuft, ax, y) ? 1 : 0;
                case 17: // Cánh cụt
                    if (Disc(ax, y, 0.14f, 0.22f, 0.06f)) return 1;
                    if (InPoly(PenguinBeak, x, y)) return 1;
                    if (Ell(x, y, 0f, -0.12f, 0.27f, 0.4f)) return 1;
                    return Ell(x, y, 0f, 0f, 0.44f, 0.58f) ? 2 : 0;
                case 18: // Gấu trúc
                    if (Disc(ax, y, 0.2f, 0.02f, 0.04f)) return 1;
                    if (Ell(ax, y, 0.2f, 0f, 0.13f, 0.17f)) return 2;
                    if (Ell(x, y, 0f, -0.2f, 0.08f, 0.05f)) return 2;
                    if (Disc(ax, y, 0.4f, 0.4f, 0.16f)) return 2;
                    return Disc(x, y, 0f, -0.05f, 0.5f) ? 1 : 0;
                case 19: // Ếch
                {
                    if (Disc(ax, y, 0.32f, 0.34f, 0.085f)) return 2;
                    float dm = Mathf.Sqrt(x * x + (y + 0.02f) * (y + 0.02f));
                    if (dm >= 0.4f && dm <= 0.46f && y < -0.12f) return 2;
                    if (Disc(ax, y, 0.32f, 0.34f, 0.19f)) return 1;
                    return Ell(x, y, 0f, -0.1f, 0.6f, 0.4f) ? 1 : 0;
                }
                case 20: // Sư tử
                    if (Disc(ax, y, 0.15f, 0.05f, 0.05f)) return 1;
                    if (InPoly(LionNose, x, y)) return 1;
                    if (Disc(x, y, 0f, -0.03f, 0.36f)) return 2;
                    return Disc(x, y, 0f, 0f, 0.62f) ? 1 : 0;
                case 21: // Cá
                    if (Disc(x, y, -0.3f, 0.07f, 0.05f)) return 2;
                    if (Ell(x, y, -0.1f, 0f, 0.5f, 0.3f)) return 1;
                    return InPoly(FishTail, x, y) ? 1 : 0;
                case 22: // Robot
                    if (Box(ax, y, 0.12f, 0.28f, -0.02f, 0.14f)) return 2;
                    if (Box(ax, y, 0f, 0.22f, -0.27f, -0.18f)) return 2;
                    if (Box(ax, y, 0f, 0.03f, 0.38f, 0.58f)) return 1;
                    if (Disc(x, y, 0f, 0.62f, 0.07f)) return 1;
                    return Box(ax, y, 0f, 0.46f, -0.38f, 0.38f) ? 1 : 0;
                case 23: // Đầu lâu
                    if (Disc(ax, y, 0.18f, 0.1f, 0.11f)) return 2;
                    if (InPoly(SkullNose, x, y)) return 2;
                    if (y >= -0.42f && y <= -0.3f && (ax < 0.015f || Mathf.Abs(ax - 0.12f) < 0.015f)) return 2;
                    if (Disc(x, y, 0f, 0.1f, 0.45f)) return 1;
                    return Box(ax, y, 0f, 0.26f, -0.42f, -0.05f) ? 1 : 0;
                case 24: // Thỏ
                    if (Disc(ax, y, 0.15f, -0.12f, 0.06f)) return 2;
                    if (Disc(x, y, 0f, -0.26f, 0.05f)) return 2;
                    if (Ell(ax, y, 0.18f, 0.42f, 0.04f, 0.24f)) return 2;
                    if (Ell(ax, y, 0.18f, 0.4f, 0.1f, 0.36f)) return 1;
                    return Disc(x, y, 0f, -0.15f, 0.4f) ? 1 : 0;
                case 25: // Người ngoài hành tinh
                    if (Ell(ax, y, 0.2f, 0.08f, 0.14f, 0.08f)) return 2;
                    if (Box(ax, y, 0f, 0.08f, -0.28f, -0.25f)) return 2;
                    return Ell(x, y, 0f, 0.05f, 0.45f, 0.5f) ? 1 : 0;
                default: // Ma
                    if (Disc(ax, y, 0.17f, 0.1f, 0.08f)) return 2;
                    if (Disc(x, y, 0f, -0.1f, 0.06f)) return 2;
                    if (Disc(x, y, 0f, 0.1f, 0.45f)) return 1;
                    return (ax <= 0.45f && y <= 0.1f && y >= -0.42f + 0.08f * Mathf.Sin(x * 14f)) ? 1 : 0;
            }
        }

        private static Vector2[] BuildStar()
        {
            Vector2[] pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float ang = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float rad = (i % 2 == 0) ? 0.64f : 0.26f;
                pts[i] = new Vector2(Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad - 0.04f);
            }
            return pts;
        }

        private static bool InPoly(Vector2[] p, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
            {
                if (((p[i].y > y) != (p[j].y > y)) &&
                    (x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
