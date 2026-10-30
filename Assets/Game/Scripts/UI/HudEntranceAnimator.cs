using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Hiệu ứng HUD trượt vào khi bắt đầu trận: thanh tên trượt xuống từ mép trên, thanh lực / tâm bi / 3 nút
    /// trượt vào từ mép màn hình gần nhất. Gọi <see cref="Prepare"/> để đẩy các phần tử ra ngoài màn hình
    /// (lúc HUD còn ẩn), sau đó <see cref="Play"/> để chúng trượt về vị trí gốc. Không sửa scene, chạy hoàn toàn lúc runtime.
    /// </summary>
    public class HudEntranceAnimator : MonoBehaviour
    {
        private enum Edge { Top, Nearest, NearestHorizontal }

        private class Item
        {
            public RectTransform rt;
            public Vector3 restWorld;
            public Vector3 startWorld;
            public float delay;
        }

        private const float Duration = 1.0f;
        private const float EdgeMargin = 30f;

        private readonly List<Item> items = new List<Item>();
        private bool played;
        private bool running;
        private float lastPlayTime = -100f;

        /// <summary>
        /// Dùng cho các ván sau: đẩy HUD ra ngoài rồi trượt vào ngay. Bỏ qua nếu hiệu ứng đang chạy hoặc vừa chạy xong
        /// (tránh chạy hai lần khi sự kiện bắt đầu ván đến sát sau hiệu ứng lúc vào trận).
        /// </summary>
        public static void Replay(GameplayHUD hud)
        {
            if (hud == null || !hud.isActiveAndEnabled) return;
            var existing = hud.GetComponent<HudEntranceAnimator>();
            if (existing != null && (existing.running || Time.unscaledTime - existing.lastPlayTime < 2f)) return;

            var anim = Prepare(hud);
            if (anim != null) anim.Play();
        }

        public static HudEntranceAnimator Prepare(GameplayHUD hud)
        {
            if (hud == null) return null;

            var canvas = hud.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null) return null;
            var canvasRt = canvas.rootCanvas.transform as RectTransform;
            if (canvasRt == null) return null;

            var anim = hud.GetComponent<HudEntranceAnimator>();
            if (anim == null) anim = hud.gameObject.AddComponent<HudEntranceAnimator>();
            if (anim.running) return null;
            anim.items.Clear();
            anim.played = false;

            Canvas.ForceUpdateCanvases();
            CueElevationSelector.EnsureBuilt();
            Canvas.ForceUpdateCanvases();

            // Thanh tên / tỉ số phía trên: trượt xuống
            var top = new List<RectTransform>();
            AddRect(top, hud.MatchHeader);
            AddRect(top, hud.PlayerLeftPanel);
            AddRect(top, hud.PlayerRightPanel);

            // Thanh lực: trượt vào từ mép trái/phải. Panel Điểm cơ (SpinSelectorPanel): từ mép gần nhất.
            // Lấy đúng khung gốc (có AimOnlyHudVisibility) để di chuyển cả panel chứ không phải một phần tử con bên trong.
            var side = new List<RectTransform>();
            var near = new List<RectTransform>();
            foreach (var vis in Object.FindObjectsOfType<AimOnlyHudVisibility>(true))
            {
                var vrt = vis.transform as RectTransform;
                if (vrt == null) continue;
                if (vis.gameObject.name.Contains("Spin")) near.Add(vrt);
                else side.Add(vrt);
            }
            AddRect(side, hud.PowerBar);

            var buttons = new List<RectTransform>();
            foreach (var b in hud.EntranceButtons())
            {
                if (b != null) buttons.Add(ContainerOf(b.transform as RectTransform, canvasRt));
            }

            var taken = new List<RectTransform>();
            anim.Collect(top, Edge.Top, 0f, canvasRt, taken);
            anim.Collect(side, Edge.NearestHorizontal, 0.2f, canvasRt, taken);
            anim.Collect(near, Edge.Nearest, 0.35f, canvasRt, taken);
            float d = 0.5f;
            var uniqueButtons = new List<RectTransform>();
            foreach (var r in buttons) if (r != null && !uniqueButtons.Contains(r)) uniqueButtons.Add(r);
            foreach (var r in uniqueButtons)
            {
                anim.Collect(new List<RectTransform> { r }, Edge.Nearest, d, canvasRt, taken);
                d += 0.12f;
            }

            // Đẩy ra ngoài màn hình ngay
            foreach (var it in anim.items) it.rt.position = it.startWorld;
            return anim;
        }

        private static void AddRect(List<RectTransform> list, Component c)
        {
            if (c == null) return;
            var rt = c.transform as RectTransform;
            if (rt != null) list.Add(rt);
        }

        /// <summary>Nút nằm trong LayoutGroup thì phải di chuyển cả khối chứa, nếu không layout sẽ kéo nút về chỗ cũ.</summary>
        private static RectTransform ContainerOf(RectTransform rt, RectTransform canvasRt)
        {
            if (rt == null) return null;
            var parent = rt.parent as RectTransform;
            if (parent != null && parent != canvasRt && parent.GetComponent<LayoutGroup>() != null) return parent;
            return rt;
        }

        private void Collect(List<RectTransform> source, Edge edge, float delay, RectTransform canvasRt, List<RectTransform> taken)
        {
            foreach (var rt in source)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;

                // Bỏ qua phần tử là con/cháu của phần tử đã được di chuyển (tránh dời hai lần)
                bool skip = false;
                foreach (var t in taken)
                {
                    if (rt == t || rt.IsChildOf(t) || t.IsChildOf(rt)) { skip = true; break; }
                }
                if (skip) continue;
                taken.Add(rt);

                Bounds b = WorldBounds(rt);
                Bounds c = WorldBounds(canvasRt);
                float margin = EdgeMargin * canvasRt.lossyScale.x;

                float dUp = c.max.y - b.max.y;
                float dDown = b.min.y - c.min.y;
                float dLeft = b.min.x - c.min.x;
                float dRight = c.max.x - b.max.x;

                Vector3 offset;
                if (edge == Edge.Top)
                {
                    offset = new Vector3(0f, c.max.y - b.min.y + margin, 0f);
                }
                else
                {
                    float best = Mathf.Min(Mathf.Min(dLeft, dRight), edge == Edge.Nearest ? Mathf.Min(dUp, dDown) : float.MaxValue);
                    if (Mathf.Approximately(best, dLeft)) offset = new Vector3(-(b.max.x - c.min.x + margin), 0f, 0f);
                    else if (Mathf.Approximately(best, dRight)) offset = new Vector3(c.max.x - b.min.x + margin, 0f, 0f);
                    else if (Mathf.Approximately(best, dUp)) offset = new Vector3(0f, c.max.y - b.min.y + margin, 0f);
                    else offset = new Vector3(0f, -(b.max.y - c.min.y + margin), 0f);
                }

                items.Add(new Item { rt = rt, restWorld = rt.position, startWorld = rt.position + offset, delay = delay });
            }
        }

        private static Bounds WorldBounds(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var b = new Bounds(corners[0], Vector3.zero);
            for (int i = 1; i < 4; i++) b.Encapsulate(corners[i]);
            return b;
        }

        public void Play()
        {
            if (played) return;
            played = true;
            running = true;
            lastPlayTime = Time.unscaledTime;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            float clock = 0f;
            float total = Duration;
            foreach (var it in items) total = Mathf.Max(total, it.delay + Duration);

            while (clock < total)
            {
                clock += Time.unscaledDeltaTime;
                foreach (var it in items)
                {
                    if (it.rt == null) continue;
                    float t = Mathf.Clamp01((clock - it.delay) / Duration);
                    it.rt.position = Vector3.LerpUnclamped(it.startWorld, it.restWorld, EaseOutBack(t));
                }
                yield return null;
            }

            foreach (var it in items)
            {
                if (it.rt != null) it.rt.position = it.restWorld;
            }
            items.Clear();
            running = false;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.2f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
