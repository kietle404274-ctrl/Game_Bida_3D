using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game.Billiards.Audio;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Bộ chọn nhạc nền kiểu Carousel có 2 nút mũi tên trái/phải (< Nhạc nền 1 >)
    /// hỗ trợ cả bấm mũi tên lẫn kéo/lướt ngang để chuyển bài với hiệu ứng trượt ngang mượt mà.
    /// </summary>
    public class MusicTrackCarouselUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int TrackCount = 2;
        private const float SlideDuration = 0.22f;

        private RectTransform viewportRt;
        private Text activeText;
        private Text incomingText;
        private CanvasGroup activeCg;
        private CanvasGroup incomingCg;
        private BorderProgressGraphic frameGraphic;

        private int currentTrackIndex;
        private bool isAnimating;
        private float animTimer;
        private int animDirection; // +1: lướt sang trái (bấm >), -1: lướt sang phải (bấm <)
        private float slideWidth = 104f;

        private bool isDragging;
        private Vector2 dragStartPos;
        private float currentDragOffsetX;

        public static MusicTrackCarouselUI Create(Transform parent, string name, Font font, Vector2 anchoredPos, Vector2 size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                var existingComp = existing.GetComponent<MusicTrackCarouselUI>();
                if (existingComp != null)
                {
                    existingComp.SyncFromSettings(false);
                    return existingComp;
                }
                Destroy(existing.gameObject);
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            const float cut = 8f;
            var rootGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(MusicTrackCarouselUI));
            rootGo.layer = parent.gameObject.layer;
            rootGo.transform.SetParent(parent, false);

            var rt = rootGo.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var bg = rootGo.GetComponent<SkewedPanelGraphic>();
            bg.color = new Color(0.06f, 0.10f, 0.18f, 0.98f);
            bg.SetInsets(0f, cut, cut, 0f);
            bg.SetBottomDarken(0.42f);
            bg.raycastTarget = true;

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            frameGo.layer = rootGo.layer;
            frameGo.transform.SetParent(rt, false);
            var frt = frameGo.GetComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            var frame = frameGo.GetComponent<BorderProgressGraphic>();
            frame.SetInsets(0f, cut, cut, 0f);
            frame.SetBaseColor(new Color(0.30f, 0.72f, 0.95f, 0.85f));
            frame.raycastTarget = false;

            // Vùng hiển thị chữ ở giữa có cắt khung (RectMask2D) để chữ trượt ngang vào/ra
            var vpGo = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
            vpGo.layer = rootGo.layer;
            vpGo.transform.SetParent(rt, false);
            var vpRt = vpGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = new Vector2(26f, 2f);
            vpRt.offsetMax = new Vector2(-26f, -2f);

            var vpHitImg = vpGo.GetComponent<Image>();
            vpHitImg.color = new Color(0f, 0f, 0f, 0f);
            vpHitImg.raycastTarget = true;

            Text txtA = CreateSlideText(vpRt, "TrackTextA", font, out CanvasGroup cgA);
            Text txtB = CreateSlideText(vpRt, "TrackTextB", font, out CanvasGroup cgB);
            txtB.gameObject.SetActive(false);

            var comp = rootGo.GetComponent<MusicTrackCarouselUI>();
            comp.viewportRt = vpRt;
            comp.activeText = txtA;
            comp.incomingText = txtB;
            comp.activeCg = cgA;
            comp.incomingCg = cgB;
            comp.frameGraphic = frame;
            comp.slideWidth = Mathf.Max(88f, size.x - 48f);

            // Nút mũi tên Trái (<) và Phải (>)
            Button btnPrev = CreateArrowButton(rt, "BtnPrev", -1);
            btnPrev.onClick.AddListener(() => comp.StepTrack(-1));

            Button btnNext = CreateArrowButton(rt, "BtnNext", +1);
            btnNext.onClick.AddListener(() => comp.StepTrack(+1));

            comp.SyncFromSettings(false);
            return comp;
        }

        private static Text CreateSlideText(RectTransform parent, string name, Font font, out CanvasGroup cg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(CanvasGroup));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.color = new Color(0.92f, 0.97f, 1f, 1f);
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            cg = go.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
            return t;
        }

        private static Button CreateArrowButton(RectTransform parent, string name, int direction)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(ArrowHoverFeedback));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            if (direction < 0)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(2f, 0f);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-2f, 0f);
            }
            rt.sizeDelta = new Vector2(28f, 0f);

            var hitImg = go.GetComponent<Image>();
            hitImg.color = new Color(1f, 1f, 1f, 0.002f);
            hitImg.raycastTarget = true;

            var chevronGo = new GameObject("Chevron", typeof(RectTransform), typeof(CanvasRenderer), typeof(ArrowChevronGraphic));
            chevronGo.layer = parent.gameObject.layer;
            chevronGo.transform.SetParent(rt, false);
            var crt = chevronGo.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(11f, 14f);

            var chevron = chevronGo.GetComponent<ArrowChevronGraphic>();
            chevron.Direction = direction;
            chevron.color = new Color(0.42f, 0.86f, 1f, 0.95f);
            chevron.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = chevron;
            btn.transition = Selectable.Transition.None;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var hover = go.GetComponent<ArrowHoverFeedback>();
            hover.Init(chevron, crt);

            return btn;
        }

        private static string GetTrackTitle(int index)
        {
            return index == 1
                ? LocalizationManager.Get("music_track_2", "Nhạc nền 2")
                : LocalizationManager.Get("music_track_1", "Nhạc nền 1");
        }

        public void SyncFromSettings(bool animateIfChanged = false)
        {
            int saved = AudioManager.Instance != null
                ? AudioManager.Instance.SelectedMusicTrack
                : Mathf.Clamp(PlayerPrefs.GetInt("Music_Track", 0), 0, TrackCount - 1);

            if (animateIfChanged && saved != currentTrackIndex && gameObject.activeInHierarchy)
            {
                int dir = saved > currentTrackIndex ? +1 : -1;
                StartSlideTo(saved, dir);
                return;
            }

            currentTrackIndex = saved;
            isAnimating = false;
            isDragging = false;
            currentDragOffsetX = 0f;

            if (activeText != null)
            {
                activeText.text = GetTrackTitle(currentTrackIndex);
                activeText.rectTransform.anchoredPosition = Vector2.zero;
                if (activeCg != null) activeCg.alpha = 1f;
            }
            if (incomingText != null)
            {
                incomingText.gameObject.SetActive(false);
            }
        }

        public void RefreshLocalization()
        {
            if (activeText != null)
            {
                activeText.text = GetTrackTitle(currentTrackIndex);
            }
        }

        public void StepTrack(int direction)
        {
            int dir = direction >= 0 ? +1 : -1;
            int nextIndex = (currentTrackIndex + dir + TrackCount) % TrackCount;

            PlayerPrefs.SetInt("Music_Track", nextIndex);
            PlayerPrefs.Save();

            var audio = AudioManager.GetOrCreateInstance();
            if (audio != null)
            {
                audio.SetMusicTrack(nextIndex);
                audio.PlayButtonClick();
            }

            StartSlideTo(nextIndex, dir);
        }

        private void StartSlideTo(int newTrackIndex, int dir)
        {
            if (activeText == null || incomingText == null)
            {
                currentTrackIndex = newTrackIndex;
                return;
            }

            // Nếu đang trượt dở thì chốt ngay trạng thái trước đó
            if (isAnimating)
            {
                SwapBuffers();
            }

            currentTrackIndex = newTrackIndex;
            animDirection = dir;
            animTimer = 0f;
            isAnimating = true;
            isDragging = false;
            currentDragOffsetX = 0f;

            incomingText.text = GetTrackTitle(currentTrackIndex);
            incomingText.gameObject.SetActive(true);
            incomingText.rectTransform.anchoredPosition = new Vector2(dir * slideWidth, 0f);
            if (incomingCg != null) incomingCg.alpha = 0f;
        }

        private void SwapBuffers()
        {
            var tmpT = activeText;
            activeText = incomingText;
            incomingText = tmpT;

            var tmpCg = activeCg;
            activeCg = incomingCg;
            incomingCg = tmpCg;

            if (activeText != null)
            {
                activeText.rectTransform.anchoredPosition = Vector2.zero;
                if (activeCg != null) activeCg.alpha = 1f;
            }
            if (incomingText != null)
            {
                incomingText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!isAnimating) return;

            animTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(animTimer / SlideDuration);
            // EaseOutCubic cho chuyển động trượt ngang mượt mà
            float ease = 1f - Mathf.Pow(1f - t, 3f);

            float outX = Mathf.Lerp(currentDragOffsetX, -animDirection * slideWidth, ease);
            float inX = Mathf.Lerp(animDirection * slideWidth, 0f, ease);

            if (activeText != null) activeText.rectTransform.anchoredPosition = new Vector2(outX, 0f);
            if (incomingText != null) incomingText.rectTransform.anchoredPosition = new Vector2(inX, 0f);

            if (activeCg != null) activeCg.alpha = 1f - ease;
            if (incomingCg != null) incomingCg.alpha = ease;

            if (t >= 1f)
            {
                isAnimating = false;
                currentDragOffsetX = 0f;
                SwapBuffers();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isAnimating) return;
            isDragging = true;
            dragStartPos = eventData.position;
            currentDragOffsetX = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging || isAnimating || activeText == null) return;
            float deltaX = eventData.position.x - dragStartPos.x;
            currentDragOffsetX = Mathf.Clamp(deltaX * 0.65f, -slideWidth * 0.6f, slideWidth * 0.6f);
            activeText.rectTransform.anchoredPosition = new Vector2(currentDragOffsetX, 0f);
            if (activeCg != null)
            {
                activeCg.alpha = Mathf.Lerp(1f, 0.35f, Mathf.Abs(currentDragOffsetX) / (slideWidth * 0.6f));
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;
            isDragging = false;
            float deltaX = eventData.position.x - dragStartPos.x;

            if (Mathf.Abs(deltaX) >= 14f)
            {
                // Kéo sang trái (deltaX < 0) -> bài tiếp (+1), kéo sang phải (deltaX > 0) -> bài trước (-1)
                StepTrack(deltaX < 0f ? +1 : -1);
            }
            else
            {
                currentDragOffsetX = 0f;
                if (activeText != null) activeText.rectTransform.anchoredPosition = Vector2.zero;
                if (activeCg != null) activeCg.alpha = 1f;
            }
        }

        /// <summary>
        /// Vẽ mũi tên Chevron (< hoặc >) sắc nét bằng vector.
        /// </summary>
        private class ArrowChevronGraphic : MaskableGraphic
        {
            public int Direction = 1; // -1: Trái (<), +1: Phải (>)

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Rect r = GetPixelAdjustedRect();
                float w = r.width * 0.5f;
                float h = r.height * 0.5f;
                float thickness = 2.4f;

                Vector2 tip = new Vector2(Direction < 0 ? -w * 0.55f : w * 0.55f, 0f);
                Vector2 top = new Vector2(Direction < 0 ? w * 0.45f : -w * 0.45f, h * 0.85f);
                Vector2 bot = new Vector2(Direction < 0 ? w * 0.45f : -w * 0.45f, -h * 0.85f);

                AddSegment(vh, top, tip, thickness, color);
                AddSegment(vh, tip, bot, thickness, color);
            }

            private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color32 col)
            {
                Vector2 dir = (b - a).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * (thickness * 0.5f);

                int idx = vh.currentVertCount;
                vh.AddVert(a - n, col, Vector2.zero);
                vh.AddVert(a + n, col, Vector2.zero);
                vh.AddVert(b + n, col, Vector2.zero);
                vh.AddVert(b - n, col, Vector2.zero);

                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }
        }

        private class ArrowHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private Graphic target;
            private RectTransform targetRt;
            private static readonly Color NormalCol = new Color(0.42f, 0.86f, 1f, 0.95f);
            private static readonly Color HoverCol = new Color(1f, 0.84f, 0.28f, 1f);
            private static readonly Color PressCol = new Color(0.20f, 0.95f, 0.65f, 1f);

            public void Init(Graphic g, RectTransform rt)
            {
                target = g;
                targetRt = rt;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (target != null) target.color = HoverCol;
                if (targetRt != null) targetRt.localScale = new Vector3(1.18f, 1.18f, 1f);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (target != null) target.color = NormalCol;
                if (targetRt != null) targetRt.localScale = Vector3.one;
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                if (target != null) target.color = PressCol;
                if (targetRt != null) targetRt.localScale = new Vector3(0.92f, 0.92f, 1f);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (target != null) target.color = HoverCol;
                if (targetRt != null) targetRt.localScale = new Vector3(1.18f, 1.18f, 1f);
            }

            private void OnDisable()
            {
                if (target != null) target.color = NormalCol;
                if (targetRt != null) targetRt.localScale = Vector3.one;
            }
        }
    }
}

