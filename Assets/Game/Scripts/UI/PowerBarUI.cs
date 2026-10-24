using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Input;

namespace Game.Billiards.UI
{
    public class PowerBarUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image powerFillImage;
        [SerializeField] private Slider powerSlider;
        [SerializeField] private Text powerPercentText;

        [Header("Visual Colors")]
        [SerializeField] private Color lowPowerColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color midPowerColor = new Color(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color highPowerColor = new Color(0.95f, 0.2f, 0.2f, 1f);

        [Header("Pro HUD")]
        [SerializeField] private RectTransform trackRect;
        [SerializeField] private SlantedBarGraphic slantedBar;
        [SerializeField] private RectTransform peakMarker;
        [SerializeField] private BorderProgressGraphic frameBorder;
        [SerializeField] private Color frameBaseColor = new Color(0.30f, 0.78f, 0.95f, 0.55f);
        [SerializeField] private float dangerThreshold = 0.9f;

        private float currentPower;
        private float peakPower;
        private bool frameDanger;

        private PlayerInputController inputController;

        public void Initialize(PlayerInputController input)
        {
            inputController = input;
            if (inputController != null)
            {
                inputController.OnPowerChanged -= UpdatePower;
                inputController.OnPowerChanged += UpdatePower;
                UpdatePower(inputController.CurrentPower01);
            }
        }

        private void OnDestroy()
        {
            if (inputController != null)
            {
                inputController.OnPowerChanged -= UpdatePower;
            }
        }

        private void Awake()
        {
            if (slantedBar != null) slantedBar.SetPalette(lowPowerColor, midPowerColor, highPowerColor);
        }

        private void Update()
        {
            // Vạch đỉnh giữ mức lực cao nhất rồi từ từ trượt xuống theo lực hiện tại
            peakPower = Mathf.Max(currentPower, peakPower - Time.unscaledDeltaTime * 0.35f);
            if (peakMarker != null && trackRect != null)
            {
                peakMarker.gameObject.SetActive(peakPower > 0.02f);
                // Thanh nghiêng: tâm tại độ cao f lệch slant * (0.5 - f) so với tâm hình chữ nhật
                float slant = slantedBar != null ? slantedBar.Slant : 0f;
                peakMarker.anchoredPosition = new Vector2(slant * (0.5f - peakPower), peakPower * trackRect.rect.height);
            }

            if (frameBorder != null)
            {
                if (currentPower >= dangerThreshold)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);
                    Color c = Color.Lerp(frameBaseColor, new Color(1f, 0.2f, 0.18f, 1f), 0.5f + 0.5f * pulse);
                    frameBorder.SetBaseColor(c);
                    frameDanger = true;
                }
                else if (frameDanger)
                {
                    frameBorder.SetBaseColor(frameBaseColor);
                    frameDanger = false;
                }
            }
        }

        public void UpdatePower(float power01)
        {
            float clamped = Mathf.Clamp01(power01);
            currentPower = clamped;
            if (slantedBar != null) slantedBar.SetFill(clamped);

            if (powerSlider != null)
            {
                powerSlider.value = clamped;
            }

            if (powerFillImage != null)
            {
                powerFillImage.fillAmount = clamped;
                powerFillImage.color = EvaluatePowerColor(clamped);
            }

            if (powerPercentText != null)
            {
                powerPercentText.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
            }
        }

        private Color EvaluatePowerColor(float t)
        {
            if (t < 0.5f)
            {
                return Color.Lerp(lowPowerColor, midPowerColor, t * 2f);
            }
            else
            {
                return Color.Lerp(midPowerColor, highPowerColor, (t - 0.5f) * 2f);
            }
        }
    }
}
