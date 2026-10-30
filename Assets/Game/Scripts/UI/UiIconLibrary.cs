using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>Biểu tượng nút dạng vector vẽ bằng code (trắng, nền trong suốt) - dùng cho các nút ở phòng online.</summary>
    public static class UiIconLibrary
    {
        private static Sprite check, cross, play, exit, copy, gear;

        public static Sprite Check => check != null ? check : (check = AvatarLibrary.CreateShapeSprite((x, y) =>
            SegDist(x, y, -0.5f, 0f, -0.15f, -0.4f) <= 0.14f || SegDist(x, y, -0.15f, -0.4f, 0.55f, 0.42f) <= 0.14f));

        public static Sprite Cross => cross != null ? cross : (cross = AvatarLibrary.CreateShapeSprite((x, y) =>
            SegDist(x, y, -0.45f, -0.45f, 0.45f, 0.45f) <= 0.13f || SegDist(x, y, -0.45f, 0.45f, 0.45f, -0.45f) <= 0.13f));

        private static readonly Vector2[] PlayPoly = { new Vector2(-0.35f, 0.58f), new Vector2(-0.35f, -0.58f), new Vector2(0.62f, 0f) };
        public static Sprite Play => play != null ? play : (play = AvatarLibrary.CreateShapeSprite((x, y) => InPoly(PlayPoly, x, y)));

        private static readonly Vector2[] ArrowHead = { new Vector2(0.22f, 0.32f), new Vector2(0.64f, 0f), new Vector2(0.22f, -0.32f) };
        public static Sprite Exit => exit != null ? exit : (exit = AvatarLibrary.CreateShapeSprite((x, y) =>
            Box(x, y, -0.62f, -0.48f, -0.6f, 0.6f) ||
            Box(x, y, -0.62f, 0.02f, 0.46f, 0.6f) ||
            Box(x, y, -0.62f, 0.02f, -0.6f, -0.46f) ||
            SegDist(x, y, -0.15f, 0f, 0.4f, 0f) <= 0.07f ||
            InPoly(ArrowHead, x, y)));

        public static Sprite Copy => copy != null ? copy : (copy = AvatarLibrary.CreateShapeSprite((x, y) =>
            // thẻ phía trước (khung)
            (Box(x, y, -0.2f, 0.58f, -0.58f, 0.2f) && !Box(x, y, -0.08f, 0.46f, -0.46f, 0.08f)) ||
            // thẻ phía sau (chỉ góc trái - trên)
            Box(x, y, -0.58f, -0.46f, -0.2f, 0.58f) || Box(x, y, -0.58f, 0.1f, 0.46f, 0.58f)));

        public static Sprite Gear => gear != null ? gear : (gear = AvatarLibrary.CreateShapeSprite((x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r < 0.2f) return false;
            if (r <= 0.42f) return true;
            if (r > 0.6f) return false;
            float ang = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            float m = Mathf.Repeat(ang, 45f);
            return m < 11f || m > 34f;
        }));

        private static bool Box(float x, float y, float x0, float x1, float y0, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
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
