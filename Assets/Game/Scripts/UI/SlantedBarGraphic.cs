using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Thanh lực dạng hình bình hành nghiêng, chia thành các ô LED (xanh → vàng → đỏ) chạy từ dưới lên.
    /// Đỉnh lệch sang trái <c>slant</c> pixel so với đáy, khớp với khung vát bên ngoài.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SlantedBarGraphic : MaskableGraphic
    {
        [SerializeField] private float slant = 17f;
        [SerializeField] private int segments = 10;
        [SerializeField] private float gap = 3f;
        [SerializeField, Range(0f, 1f)] private float fill = 0.5f;
        [SerializeField] private Color lowColor = new Color(0.2f, 0.85f, 0.45f, 1f);
        [SerializeField] private Color midColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color highColor = new Color(0.95f, 0.2f, 0.2f, 1f);

        public float Slant => slant;

        public void SetSlant(float value)
        {
            slant = value;
            SetVerticesDirty();
        }

        public void SetFill(float value01)
        {
            float v = Mathf.Clamp01(value01);
            if (Mathf.Approximately(v, fill)) return;
            fill = v;
            SetVerticesDirty();
        }

        public void SetPalette(Color low, Color mid, Color high)
        {
            lowColor = low;
            midColor = mid;
            highColor = high;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            int n = Mathf.Max(1, segments);
            float gapFrac = (r.height > 1f) ? gap / r.height : 0f;

            for (int i = 0; i < n; i++)
            {
                float f0 = (float)i / n + gapFrac * 0.5f;
                float f1 = (float)(i + 1) / n - gapFrac * 0.5f;
                if (fill <= f0) break;
                f1 = Mathf.Min(f1, fill);
                if (f1 <= f0) continue;

                float t = (i + 0.5f) / n;
                Color col = t < 0.5f ? Color.Lerp(lowColor, midColor, t * 2f) : Color.Lerp(midColor, highColor, (t - 0.5f) * 2f);
                AddSlice(vh, r, f0, f1, col);
            }
        }

        private void AddSlice(VertexHelper vh, Rect r, float f0, float f1, Color col)
        {
            float y0 = r.yMin + r.height * f0;
            float y1 = r.yMin + r.height * f1;
            float l0 = r.xMin + slant * (1f - f0), rr0 = r.xMax - slant * f0;
            float l1 = r.xMin + slant * (1f - f1), rr1 = r.xMax - slant * f1;

            int idx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = new Vector2(l0, y0); vh.AddVert(v);
            v.position = new Vector2(l1, y1); vh.AddVert(v);
            v.position = new Vector2(rr1, y1); vh.AddVert(v);
            v.position = new Vector2(rr0, y0); vh.AddVert(v);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx + 2, idx + 3, idx);
        }
    }
}
