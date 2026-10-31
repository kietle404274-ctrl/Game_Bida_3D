using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Đồng hồ đếm ngược trong bảng tên cơ thủ: số giây + viền chạy đồng bộ.
    /// Đổi màu theo thời gian còn lại và có tiếng tích-tắc ở 5 giây cuối.
    /// </summary>
    public class TurnTimerUI : MonoBehaviour
    {
        [SerializeField] private Text numberText;
        [SerializeField] private BorderProgressGraphic border;

        [Header("Màu theo thời gian còn lại")]
        [SerializeField] private Color normalColor = new Color(0.20f, 0.85f, 0.50f, 1f);
        [SerializeField] private Color warnColor = new Color(1f, 0.80f, 0.15f, 1f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.25f, 0.20f, 1f);
        [SerializeField] private float warnThreshold = 10f;
        [SerializeField] private float dangerThreshold = 5f;

        private int lastWholeSecond = -1;
        private float pulse;
        private Vector3 baseScale = Vector3.one;

        private void Awake()
        {
            if (numberText != null) baseScale = numberText.rectTransform.localScale;
            Hide();
        }

        private void Update()
        {
            if (numberText == null || pulse <= 0.001f) return;
            pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 3.5f);
            numberText.rectTransform.localScale = baseScale * (1f + 0.30f * pulse);
        }

        public void SetTime(float remaining, float total)
        {
            if (total <= 0.001f) { Hide(); return; }

            Color c = remaining <= dangerThreshold ? dangerColor : (remaining <= warnThreshold ? warnColor : normalColor);
            int whole = Mathf.CeilToInt(Mathf.Max(0f, remaining));

            if (numberText != null)
            {
                numberText.enabled = true;
                numberText.color = c;
                numberText.text = whole + "<size=18>s</size>";
            }

            if (border != null)
            {
                border.SetProgress(Mathf.Clamp01(remaining / total), dangerColor);
            }

            if (whole != lastWholeSecond)
            {
                lastWholeSecond = whole;
                if (remaining > 0f && remaining <= dangerThreshold)
                {
                    pulse = 1f;
                    if (Audio.AudioManager.Instance != null) Audio.AudioManager.Instance.PlayTimerTick();
                }
            }
        }

        public void Hide()
        {
            if (lastWholeSecond != -1 && Audio.AudioManager.Instance != null)
            {
                Audio.AudioManager.Instance.StopTimerTick();
            }
            lastWholeSecond = -1;
            pulse = 0f;
            if (numberText != null)
            {
                numberText.enabled = false;
                numberText.rectTransform.localScale = baseScale;
            }
            if (border != null) border.ClearProgress();
        }
    }
}
