using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Tia sét UI: đường gấp khúc răng cưa giữa hai điểm (toạ độ cục bộ), 3 lớp (quầng sáng, thân, lõi trắng),
    /// đổi hình dạng và cường độ ngẫu nhiên liên tục để tạo cảm giác phóng điện.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class LightningGraphic : MaskableGraphic
    {
        private Vector2 from, to;
        private Color tint = Color.white;
        private float amplitude = 20f;
        private int seed = 1;
        private float intensity = 0.6f;
        private float timer;

        public void Setup(Vector2 a, Vector2 b, Color c, float amp)
        {
            raycastTarget = false;
            from = a; to = b; tint = c; amplitude = amp;
            Reroll();
        }

        public void Tick(float dt)
        {
            timer -= dt;
            if (timer <= 0f) Reroll();
        }

        private void Reroll()
        {
            timer = Random.Range(0.05f, 0.14f);
            seed = Random.Range(1, 1000000);
            intensity = Random.value < 0.15f ? 1f : Random.Range(0.35f, 0.8f);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            System.Random rnd = new System.Random(seed);
            DrawBolt(vh, rnd, from, to, amplitude, 14f, 0.18f * intensity, tint);
            rnd = new System.Random(seed);
            DrawBolt(vh, rnd, from, to, amplitude, 6f, 0.35f * intensity, tint);
            rnd = new System.Random(seed);
            DrawBolt(vh, rnd, from, to, amplitude, 2.4f, 0.95f * intensity, Color.Lerp(tint, Color.white, 0.75f));

            // nhánh phụ nhỏ
            System.Random br = new System.Random(seed + 7);
            Vector2 mid = Vector2.Lerp(from, to, 0.35f + (float)br.NextDouble() * 0.3f);
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (br.Next(0, 2) == 0 ? 1f : -1f);
            Vector2 end = mid + (dir * 0.4f + perp) * (amplitude * 2f);
            DrawBolt(vh, br, mid, end, amplitude * 0.5f, 2f, 0.6f * intensity, Color.Lerp(tint, Color.white, 0.5f));
        }

        private static void DrawBolt(VertexHelper vh, System.Random rnd, Vector2 a, Vector2 b, float amp, float thick, float alpha, Color col)
        {
            const int seg = 16;
            Vector2 dir = (b - a);
            float len = dir.magnitude;
            if (len < 1f) return;
            dir /= len;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            col.a = Mathf.Clamp01(alpha);

            Vector2 prev = a;
            for (int i = 1; i <= seg; i++)
            {
                float t = i / (float)seg;
                float jitter = (float)(rnd.NextDouble() * 2.0 - 1.0) * amp * Mathf.Sin(Mathf.PI * t);
                Vector2 p = Vector2.Lerp(a, b, t) + perp * jitter;
                if (i == seg) p = b;
                AddSegment(vh, prev, p, thick * 0.5f, col);
                prev = p;
            }
        }

        private static void AddSegment(VertexHelper vh, Vector2 p0, Vector2 p1, float half, Color col)
        {
            Vector2 d = p1 - p0;
            if (d.sqrMagnitude < 0.0001f) return;
            d.Normalize();
            Vector2 n = new Vector2(-d.y, d.x) * half;
            int s = vh.currentVertCount;
            vh.AddVert(p0 - n, col, Vector2.zero);
            vh.AddVert(p0 + n, col, Vector2.zero);
            vh.AddVert(p1 + n, col, Vector2.zero);
            vh.AddVert(p1 - n, col, Vector2.zero);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }
    }
}
