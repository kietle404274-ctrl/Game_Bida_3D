using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hình thẻ liền khối (thẻ lớn + nhánh dẹt nối liền): tô nền bằng các đa giác lồi riêng biệt (không chồng nhau)
    /// và vẽ một đường viền duy nhất bao quanh toàn bộ hình, không có đường ngăn ở chỗ nối.
    /// Toạ độ đỉnh là toạ độ cục bộ của RectTransform (gốc = pivot).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CardShapeGraphic : MaskableGraphic
    {
        private readonly List<Vector2[]> fills = new List<Vector2[]>();
        private Vector2[] loop;
        private Color lineColor = Color.white;
        private float thickness = 2.5f;

        public void Setup(Vector2[] outlineLoop, Vector2[][] fillPolys, Color fill, Color line, float thick)
        {
            raycastTarget = false;
            loop = outlineLoop;
            fills.Clear();
            fills.AddRange(fillPolys);
            color = fill;
            lineColor = line;
            thickness = thick;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int f = 0; f < fills.Count; f++)
            {
                Vector2[] p = fills[f];
                int s = vh.currentVertCount;
                for (int i = 0; i < p.Length; i++) vh.AddVert(p[i], color, Vector2.zero);
                for (int i = 1; i < p.Length - 1; i++) vh.AddTriangle(s, s + i, s + i + 1);
            }
            if (loop == null) return;
            float half = thickness * 0.5f;
            for (int i = 0; i < loop.Length; i++)
            {
                Vector2 a = loop[i], b = loop[(i + 1) % loop.Length];
                Vector2 d = (b - a);
                if (d.sqrMagnitude < 0.0001f) continue;
                d.Normalize();
                Vector2 n = new Vector2(-d.y, d.x) * half;
                Vector2 ext = d * half; // kéo dài nhẹ để các góc khít nhau
                int s = vh.currentVertCount;
                vh.AddVert(a - ext - n, lineColor, Vector2.zero);
                vh.AddVert(a - ext + n, lineColor, Vector2.zero);
                vh.AddVert(b + ext + n, lineColor, Vector2.zero);
                vh.AddVert(b + ext - n, lineColor, Vector2.zero);
                vh.AddTriangle(s, s + 1, s + 2);
                vh.AddTriangle(s, s + 2, s + 3);
            }
        }
    }
}
