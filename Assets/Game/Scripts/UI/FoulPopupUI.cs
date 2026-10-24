using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Popup nhanh báo lỗi (foul): nền đen trong suốt, một dòng ngắn gọn. Tự dựng lúc chạy.
    /// Dùng: FoulPopupUI.Show(FoulType.CueBallScratch) hoặc FoulPopupUI.Show("TÊN LỖI").
    /// </summary>
    public class FoulPopupUI : MonoBehaviour
    {
        private static FoulPopupUI instance;

        private RectTransform panel;
        private CanvasGroup group;
        private Text label;
        private Coroutine routine;

        private const float PanelW = 620f;
        private const float PanelH = 76f;
        private static readonly Vector2 BasePos = new Vector2(0f, 230f);

        public static void Show(FoulType foul)
        {
            Show(GetTitle(foul));
        }

        public static void Show(string title)
        {
            Ensure();
            instance.Display(title, "LỖI", "#FF5A5A", true, 1.8f);
        }

        /// <summary>Banner thắng ván: nền đen trong suốt, nhãn xanh lá.</summary>
        public static void ShowWin(string text, float holdSeconds = 2.0f)
        {
            Ensure();
            instance.Display(text, "THẮNG VÁN", "#5CF08A", false, holdSeconds);
        }

        private static void Ensure()
        {
            if (instance == null)
            {
                var go = new GameObject("FoulPopup");
                instance = go.AddComponent<FoulPopupUI>();
                instance.Build();
            }
        }

        public static string GetTitle(FoulType foul)
        {
            switch (foul)
            {
                case FoulType.CueBallScratch: return "BI CÁI RƠI LỖ";
                case FoulType.WrongBallFirst: return "ĐÁNH SAI BI ĐẦU TIÊN";
                case FoulType.NoRailAfterContact: return "KHÔNG BI NÀO CHẠM BĂNG";
                case FoulType.IllegalBreak: return "KHAI CUỘC KHÔNG HỢP LỆ";
                case FoulType.BallDrivenOffTable: return "BI VĂNG KHỎI BÀN";
                case FoulType.ShotClockExpired: return "HẾT GIỜ";
                case FoulType.BadCueBallPlacement: return "ĐẶT BI CÁI SAI VỊ TRÍ";
                case FoulType.PlayingOutOfTurn: return "ĐÁNH SAI LƯỢT";
                case FoulType.BallsStillMoving: return "ĐÁNH KHI BI CÒN LĂN";
                case FoulType.DoubleHit: return "ĐÁNH HAI LẦN";
                case FoulType.PushShot: return "ĐẨY BI";
                default: return "PHẠM QUY";
            }
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            panel = (RectTransform)panelGo.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelW, PanelH);
            panel.anchoredPosition = BasePos;
            var bg = panelGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.62f);
            bg.raycastTarget = false;
            group = panelGo.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(panel, false);
            label = textGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.supportRichText = true;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 4f);
            rt.offsetMax = new Vector2(-16f, -4f);

            panel.gameObject.SetActive(false);
        }

        private void Display(string title, string tag, string tagColor, bool foulSound, float holdSeconds)
        {
            label.text = "<color=" + tagColor + ">" + tag + "</color>   " + title;
            currentHold = holdSeconds;

            var am = Audio.AudioManager.Instance;
            if (foulSound && am != null) am.PlayFoul();

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Animate());
        }

        private float currentHold = 1.8f;

        private IEnumerator Animate()
        {
            panel.gameObject.SetActive(true);

            float t = 0f;
            const float inDur = 0.18f;
            while (t < inDur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / inDur);
                group.alpha = p;
                panel.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, p);
                yield return null;
            }
            panel.localScale = Vector3.one;
            group.alpha = 1f;

            float hold = 0f;
            while (hold < currentHold)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            t = 0f;
            const float outDur = 0.3f;
            while (t < outDur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / outDur);
                group.alpha = 1f - p;
                panel.anchoredPosition = BasePos + new Vector2(0f, 24f * p);
                yield return null;
            }

            panel.anchoredPosition = BasePos;
            group.alpha = 0f;
            panel.gameObject.SetActive(false);
            routine = null;
        }
    }
}
