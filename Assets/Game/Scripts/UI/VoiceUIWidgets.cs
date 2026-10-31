using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Đồ họa Vector thủ tục (Procedural UI Graphic) vẽ Icon Microphone và Icon Loa (Speaker) sắc nét
    /// với hiệu ứng nhấp nháy lên xuống theo biên độ giọng nói thời gian thực (VU Meter + Equalizer Bars)
    /// giống các tựa game eSports thực tế (Discord / Valorant / PUBG), hoặc gạch chéo đỏ khi Tắt (Muted).
    /// </summary>
    public class VoiceIconGraphic : MaskableGraphic
    {
        public enum IconKind
        {
            Microphone = 0,
            Speaker = 1
        }

        [SerializeField] private IconKind kind = IconKind.Microphone;
        [SerializeField] private bool isMuted = false;
        [SerializeField] private float voiceLevel01 = 0f;
        [SerializeField] private float animPhase = 0f;

        private static readonly Color ColorActiveIdle = new Color(0.78f, 0.94f, 1.00f, 1f);
        private static readonly Color ColorSpeakingMain = new Color(0.16f, 1.00f, 0.58f, 1f);
        private static readonly Color ColorSpeakingFill = new Color(0.00f, 0.95f, 1.00f, 1f);
        private static readonly Color ColorMutedBody = new Color(0.56f, 0.62f, 0.72f, 0.85f);
        private static readonly Color ColorMuteSlash = new Color(1.00f, 0.24f, 0.28f, 1f);
        private static readonly Color ColorSlashShadow = new Color(0.04f, 0.07f, 0.14f, 0.95f);

        public IconKind Kind
        {
            get => kind;
            set
            {
                if (kind == value) return;
                kind = value;
                SetVerticesDirty();
            }
        }

        public bool IsMuted => isMuted;
        public float VoiceLevel01 => voiceLevel01;

        public void SetState(bool muted, float level01, float timePhase)
        {
            float clamped = muted ? 0f : Mathf.Clamp01(level01);
            bool dirty = (isMuted != muted)
                         || Mathf.Abs(voiceLevel01 - clamped) > 0.004f
                         || (clamped > 0.02f && Mathf.Abs(animPhase - timePhase) > 0.01f);

            isMuted = muted;
            voiceLevel01 = clamped;
            animPhase = timePhase;

            if (dirty)
            {
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            if (r.width <= 4f || r.height <= 4f) return;

            if (kind == IconKind.Microphone)
            {
                DrawMicrophone(vh, r);
            }
            else
            {
                DrawSpeaker(vh, r);
            }
        }

        private void DrawMicrophone(VertexHelper vh, Rect r)
        {
            float cx = r.center.x;
            float cy = r.center.y;
            float s = Mathf.Min(r.width, r.height) * 0.5f;

            bool speaking = !isMuted && voiceLevel01 > 0.03f;
            float bounce01 = speaking
                ? Mathf.Clamp01(voiceLevel01 * (0.78f + 0.22f * Mathf.Sin(animPhase * 22f)))
                : 0f;

            // Khi đang nói, thân mic nhấp nhô lên xuống nhẹ nhàng theo nhịp giọng nói
            float verticalBob = speaking
                ? Mathf.Sin(animPhase * 18f) * s * 0.07f * Mathf.Clamp01(voiceLevel01 * 1.4f)
                : 0f;

            // Dịch nhẹ tâm mic sang trái một chút khi bật để nhường chỗ cho 3 cột sóng âm nhấp nháy bên phải
            float micCenterX = cx - s * 0.14f;
            float micCenterY = cy + verticalBob;

            Color bodyCol = isMuted
                ? ColorMutedBody
                : Color.Lerp(ColorActiveIdle, ColorSpeakingMain, Mathf.Clamp01(voiceLevel01 * 1.5f));

            // 1. Thân viên nang Mic (Capsule)
            float capW = s * 0.52f;
            float capH = s * 0.92f;
            float capCenterY = micCenterY + s * 0.16f;
            DrawRoundedVerticalCapsule(vh, new Vector2(micCenterX, capCenterY), capW, capH, bodyCol);

            // Lõi tối bên trong viên nang Mic để làm nổi cột sóng xanh nhấp nháy lên xuống
            if (!isMuted)
            {
                Color wellCol = new Color(0.05f, 0.10f, 0.18f, 0.92f);
                DrawRoundedVerticalCapsule(vh, new Vector2(micCenterX, capCenterY), capW * 0.68f, capH * 0.78f, wellCol);

                // Thanh mức âm lượng (VU Meter) bên trong thân Mic dâng lên hạ xuống theo giọng nói thật
                float fillRatio = Mathf.Clamp01(0.18f + bounce01 * 0.82f);
                float innerH = capH * 0.74f;
                float fillH = innerH * fillRatio;
                float fillBottomY = capCenterY - innerH * 0.5f;
                float fillCenterY = fillBottomY + fillH * 0.5f;
                Color meterCol = speaking
                    ? Color.Lerp(ColorSpeakingFill, ColorSpeakingMain, bounce01)
                    : new Color(0.25f, 0.78f, 0.96f, 0.55f);

                AddRect(vh, new Vector2(micCenterX, fillCenterY), new Vector2(capW * 0.58f, fillH), meterCol);
            }

            // 2. Giá đỡ chữ U (Cradle) ôm quanh nửa dưới viên nang
            float uRadiusX = capW * 0.82f;
            float uRadiusY = capH * 0.52f;
            float uThickness = Mathf.Max(1.6f, s * 0.13f);
            DrawBottomArc(vh, new Vector2(micCenterX, capCenterY - capH * 0.04f), uRadiusX, uRadiusY, uThickness, bodyCol);

            // 3. Chân trụ dọc + Đế ngang của Mic
            float cradleBottomY = (capCenterY - capH * 0.04f) - uRadiusY;
            float baseY = micCenterY - s * 0.76f;
            float stemH = Mathf.Max(2f, cradleBottomY - baseY);
            AddRect(vh, new Vector2(micCenterX, baseY + stemH * 0.5f), new Vector2(uThickness, stemH), bodyCol);
            AddRect(vh, new Vector2(micCenterX, baseY), new Vector2(capW * 1.15f, uThickness), bodyCol);

            // 4. Ba cột sóng âm (Equalizer Bars) bên phải thân Mic nhấp nháy lên xuống theo giọng nói
            if (!isMuted)
            {
                float eqBaseX = micCenterX + uRadiusX + s * 0.22f;
                float eqStepX = s * 0.20f;
                float barW = Mathf.Max(1.6f, s * 0.125f);
                float maxBarH = s * 1.18f;
                float minBarH = s * 0.20f;

                for (int i = 0; i < 3; i++)
                {
                    float wave = speaking
                        ? 0.5f + 0.5f * Mathf.Sin(animPhase * (19f + i * 5.5f) + i * 1.35f)
                        : 0f;
                    float levelFactor = speaking
                        ? Mathf.Clamp01(voiceLevel01 * (0.55f + 0.65f * wave))
                        : 0.12f;

                    float bh = Mathf.Lerp(minBarH, maxBarH * (i == 1 ? 1.0f : 0.78f), levelFactor);
                    float bx = eqBaseX + i * eqStepX;
                    Color barCol = speaking
                        ? Color.Lerp(ColorSpeakingFill, ColorSpeakingMain, wave)
                        : new Color(0.30f, 0.78f, 0.95f, 0.45f);

                    AddRect(vh, new Vector2(bx, cy), new Vector2(barW, bh), barCol);
                }
            }
            else
            {
                // Khi tắt Mic: vẽ đường gạch chéo đỏ sắc nét có viền tối
                DrawMuteSlash(vh, new Vector2(cx, cy), s);
            }
        }

        private void DrawSpeaker(VertexHelper vh, Rect r)
        {
            float cx = r.center.x;
            float cy = r.center.y;
            float s = Mathf.Min(r.width, r.height) * 0.5f;

            bool activeWave = !isMuted && voiceLevel01 > 0.03f;
            Color bodyCol = isMuted
                ? ColorMutedBody
                : Color.Lerp(ColorActiveIdle, ColorSpeakingFill, Mathf.Clamp01(voiceLevel01 * 1.4f));

            // 1. Hộp loa bên trái + Phễu loa hình thang loe sang phải
            float spkX = cx - s * 0.26f;
            float boxW = s * 0.34f;
            float boxH = s * 0.52f;
            AddRect(vh, new Vector2(spkX - boxW * 0.5f, cy), new Vector2(boxW, boxH), bodyCol);

            Vector2 c0 = new Vector2(spkX - boxW * 0.1f, cy - boxH * 0.5f);
            Vector2 c1 = new Vector2(spkX - boxW * 0.1f, cy + boxH * 0.5f);
            Vector2 c2 = new Vector2(spkX + s * 0.36f, cy + s * 0.66f);
            Vector2 c3 = new Vector2(spkX + s * 0.36f, cy - s * 0.66f);
            AddQuad(vh, c0, c1, c2, c3, bodyCol);

            // 2. Hai vòm sóng âm bên phải loa (nhấp nháy theo âm thanh phát ra)
            if (!isMuted)
            {
                float wave1Pulse = activeWave ? (0.55f + 0.45f * Mathf.Sin(animPhase * 20f)) : 0.75f;
                float wave2Pulse = activeWave ? (0.55f + 0.45f * Mathf.Sin(animPhase * 20f - 1.2f)) : 0.45f;

                Color w1Col = activeWave
                    ? Color.Lerp(ColorActiveIdle, ColorSpeakingMain, wave1Pulse)
                    : new Color(0.30f, 0.78f, 0.95f, 0.80f);
                Color w2Col = activeWave
                    ? Color.Lerp(ColorActiveIdle, ColorSpeakingFill, wave2Pulse)
                    : new Color(0.30f, 0.78f, 0.95f, 0.45f);

                float arcThick = Mathf.Max(1.5f, s * 0.12f);
                float scale1 = activeWave ? (1f + 0.14f * voiceLevel01 * wave1Pulse) : 1f;
                float scale2 = activeWave ? (1f + 0.20f * voiceLevel01 * wave2Pulse) : 1f;

                DrawRightArc(vh, new Vector2(spkX + s * 0.38f, cy), s * 0.26f * scale1, s * 0.42f * scale1, arcThick, w1Col);
                DrawRightArc(vh, new Vector2(spkX + s * 0.42f, cy), s * 0.48f * scale2, s * 0.68f * scale2, arcThick, w2Col);
            }
            else
            {
                DrawMuteSlash(vh, new Vector2(cx, cy), s);
            }
        }

        private static void DrawRoundedVerticalCapsule(VertexHelper vh, Vector2 center, float w, float h, Color32 col)
        {
            float radius = w * 0.5f;
            float bodyH = Mathf.Max(0f, h - w);

            if (bodyH > 0.01f)
            {
                AddRect(vh, center, new Vector2(w, bodyH), col);
            }

            // Nửa tròn trên & dưới (6 đoạn mỗi đầu)
            const int segs = 6;
            Vector2 topCenter = new Vector2(center.x, center.y + bodyH * 0.5f);
            Vector2 botCenter = new Vector2(center.x, center.y - bodyH * 0.5f);

            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.PI * i / segs;
                float a1 = Mathf.PI * (i + 1) / segs;

                Vector2 t0 = topCenter + new Vector2(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius);
                Vector2 t1 = topCenter + new Vector2(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius);
                AddTriangle(vh, topCenter, t1, t0, col);

                Vector2 b0 = botCenter + new Vector2(Mathf.Cos(a0) * radius, -Mathf.Sin(a0) * radius);
                Vector2 b1 = botCenter + new Vector2(Mathf.Cos(a1) * radius, -Mathf.Sin(a1) * radius);
                AddTriangle(vh, botCenter, b0, b1, col);
            }
        }

        private static void DrawBottomArc(VertexHelper vh, Vector2 center, float rx, float ry, float thickness, Color32 col)
        {
            const int segs = 8;
            float rxIn = Mathf.Max(1f, rx - thickness);
            float ryIn = Mathf.Max(1f, ry - thickness);

            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.PI + Mathf.PI * i / segs;
                float a1 = Mathf.PI + Mathf.PI * (i + 1) / segs;

                Vector2 p0Out = center + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry);
                Vector2 p1Out = center + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry);
                Vector2 p1In = center + new Vector2(Mathf.Cos(a1) * rxIn, Mathf.Sin(a1) * ryIn);
                Vector2 p0In = center + new Vector2(Mathf.Cos(a0) * rxIn, Mathf.Sin(a0) * ryIn);

                AddQuad(vh, p0Out, p1Out, p1In, p0In, col);
            }
        }

        private static void DrawRightArc(VertexHelper vh, Vector2 center, float rx, float ry, float thickness, Color32 col)
        {
            const int segs = 6;
            float rxIn = Mathf.Max(0.5f, rx - thickness);
            float ryIn = Mathf.Max(0.5f, ry - thickness);
            float startAngle = -Mathf.PI * 0.36f;
            float endAngle = Mathf.PI * 0.36f;

            for (int i = 0; i < segs; i++)
            {
                float a0 = Mathf.Lerp(startAngle, endAngle, (float)i / segs);
                float a1 = Mathf.Lerp(startAngle, endAngle, (float)(i + 1) / segs);

                Vector2 p0Out = center + new Vector2(Mathf.Cos(a0) * rx, Mathf.Sin(a0) * ry);
                Vector2 p1Out = center + new Vector2(Mathf.Cos(a1) * rx, Mathf.Sin(a1) * ry);
                Vector2 p1In = center + new Vector2(Mathf.Cos(a1) * rxIn, Mathf.Sin(a1) * ryIn);
                Vector2 p0In = center + new Vector2(Mathf.Cos(a0) * rxIn, Mathf.Sin(a0) * ryIn);

                AddQuad(vh, p0Out, p1Out, p1In, p0In, col);
            }
        }

        private static void DrawMuteSlash(VertexHelper vh, Vector2 center, float radius)
        {
            Vector2 a = center + new Vector2(-radius * 0.72f, radius * 0.72f);
            Vector2 b = center + new Vector2(radius * 0.72f, -radius * 0.72f);
            float thick = Mathf.Max(2.0f, radius * 0.22f);

            DrawLineSegment(vh, a, b, thick + 1.8f, ColorSlashShadow);
            DrawLineSegment(vh, a, b, thick, ColorMuteSlash);
        }

        private static void DrawLineSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);
            AddQuad(vh, a - n, a + n, b + n, b - n, col);
        }

        private static void AddRect(VertexHelper vh, Vector2 center, Vector2 size, Color32 col)
        {
            float hx = size.x * 0.5f;
            float hy = size.y * 0.5f;
            AddQuad(
                vh,
                new Vector2(center.x - hx, center.y - hy),
                new Vector2(center.x - hx, center.y + hy),
                new Vector2(center.x + hx, center.y + hy),
                new Vector2(center.x + hx, center.y - hy),
                col);
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            v.position = d; vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }

        private static void AddTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 col)
        {
            int start = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
        }
    }
}

