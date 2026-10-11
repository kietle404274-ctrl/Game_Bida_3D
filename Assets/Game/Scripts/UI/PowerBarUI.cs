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

        public void UpdatePower(float power01)
        {
            float clamped = Mathf.Clamp01(power01);

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
