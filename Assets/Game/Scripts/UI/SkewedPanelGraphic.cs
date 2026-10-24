using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Khung tứ giác có thể lệch cạnh: dùng cho hình bình hành (panel cơ thủ) và hình thang cân ngược (khung tỷ số).
    /// Mỗi inset là khoảng thu vào (pixel) của từng góc theo trục X; dương = thu vào trong.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SkewedPanelGraphic : MaskableGraphic
    {
        [SerializeField] private float topLeftInset;
        [SerializeField] private float topRightInset;
        [SerializeField] private float bottomLeftInset;
        [SerializeField] private float bottomRightInset;
        [Tooltip("Hệ số độ sáng ở cạnh dưới so với cạnh trên (1 = không gradient, 0.5 = tối dần xuống dưới)")]
        [SerializeField] private float bottomDarken = 1f;

        public void SetBottomDarken(float value)
        {
            bottomDarken = value;
            SetVerticesDirty();
        }

        public void SetInsets(float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            topLeftInset = topLeft;
            topRightInset = topRight;
            bottomLeftInset = bottomLeft;
            bottomRightInset = bottomRight;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            UIVertex v = UIVertex.simpleVert;
            v.color = color;

            Color32 topCol = color;
            Color32 botCol = new Color(color.r * bottomDarken, color.g * bottomDarken, color.b * bottomDarken, color.a);

            v.color = botCol;
            v.position = new Vector3(r.xMin + bottomLeftInset, r.yMin); vh.AddVert(v);
            v.color = topCol;
            v.position = new Vector3(r.xMin + topLeftInset, r.yMax); vh.AddVert(v);
            v.position = new Vector3(r.xMax - topRightInset, r.yMax); vh.AddVert(v);
            v.color = botCol;
            v.position = new Vector3(r.xMax - bottomRightInset, r.yMin); vh.AddVert(v);

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
        }
    }
}
