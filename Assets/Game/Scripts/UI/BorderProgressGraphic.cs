using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Viền bao quanh khung tứ giác xiên (cùng insets với <see cref="SkewedPanelGraphic"/>).
    /// Gồm một viền nền mờ và một đoạn viền sáng chạy từ giữa cạnh trên theo chiều kim đồng hồ,
    /// dài bằng <c>progress × chu vi</c>. Dùng để hiển thị thời gian còn lại của lượt đánh.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class BorderProgressGraphic : MaskableGraphic
    {
        [SerializeField] private float topLeftInset;
        [SerializeField] private float topRightInset;
        [SerializeField] private float bottomLeftInset;
        [SerializeField] private float bottomRightInset;
        [SerializeField] private float thickness = 4f;
        [SerializeField] private Color baseColor = new Color(0.30f, 0.78f, 0.95f, 0.55f);
        [SerializeField, Range(0f, 1f)] private float progress = 1f;
        [SerializeField] private bool showProgress = false;

        public void SetInsets(float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            topLeftInset = topLeft;
            topRightInset = topRight;
            bottomLeftInset = bottomLeft;
            bottomRightInset = bottomRight;
            SetVerticesDirty();
        }

        public Color BaseColor => baseColor;

        public void SetBaseColor(Color c)
        {
            baseColor = c;
            SetVerticesDirty();
        }

        /// <summary>Bật đoạn viền sáng với tỷ lệ còn lại 0..1 và màu <paramref name="arcColor"/>.</summary>
        public void SetProgress(float value01, Color arcColor)
        {
            showProgress = true;
            progress = Mathf.Clamp01(value01);
            color = arcColor;
            SetVerticesDirty();
        }

        /// <summary>Tắt đoạn viền sáng, chỉ còn viền nền mờ.</summary>
        public void ClearProgress()
        {
            if (!showProgress) return;
            showProgress = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float h = thickness * 0.5f;

            Vector2 bl = new Vector2(r.xMin + bottomLeftInset + h, r.yMin + h);
            Vector2 tl = new Vector2(r.xMin + topLeftInset + h, r.yMax - h);
            Vector2 tr = new Vector2(r.xMax - topRightInset - h, r.yMax - h);
            Vector2 br = new Vector2(r.xMax - bottomRightInset - h, r.yMin + h);
            Vector2 mid = (tl + tr) * 0.5f;

            // Đường đi theo chiều kim đồng hồ bắt đầu từ giữa cạnh trên
            // Panel phải (xiên ở góc trên-trái) chạy ngược chiều kim đồng hồ để đối xứng với panel trái
            bool counterClockwise = topLeftInset > topRightInset;
            var path = counterClockwise
                ? new List<Vector2> { mid, tl, bl, br, tr, mid }
                : new List<Vector2> { mid, tr, br, bl, tl, mid };

            AddPath(vh, path, 1f, baseColor, h);
            if (showProgress && progress > 0.0005f)
            {
                AddPath(vh, path, progress, color, h);
            }
        }

        private static void AddPath(VertexHelper vh, List<Vector2> path, float fraction, Color col, float halfThickness)
        {
            float total = 0f;
            for (int i = 0; i < path.Count - 1; i++) total += Vector2.Distance(path[i], path[i + 1]);

            float remaining = total * fraction;
            for (int i = 0; i < path.Count - 1 && remaining > 0.001f; i++)
            {
                Vector2 a = path[i];
                Vector2 b = path[i + 1];
                float len = Vector2.Distance(a, b);
                bool cut = remaining < len;
                if (cut) b = a + (b - a) / len * remaining;
                remaining -= len;

                Vector2 dir = (b - a).normalized;
                // Kéo dài hai đầu đoạn để các góc nối liền (trừ điểm bắt đầu và điểm cắt)
                Vector2 a2 = (i == 0) ? a : a - dir * halfThickness;
                Vector2 b2 = (cut) ? b : b + dir * halfThickness;
                AddQuad(vh, a2, b2, halfThickness, col);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, float halfThickness, Color col)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * halfThickness;

            int idx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = a - n; vh.AddVert(v);
            v.position = a + n; vh.AddVert(v);
            v.position = b + n; vh.AddVert(v);
            v.position = b - n; vh.AddVert(v);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }
    }
}
