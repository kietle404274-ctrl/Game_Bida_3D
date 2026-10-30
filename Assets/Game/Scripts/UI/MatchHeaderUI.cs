using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class MatchHeaderUI : MonoBehaviour
    {
        [Header("Score & Rack Display")]
        [SerializeField] private Text rackNumberText;
        [SerializeField] private Text scoreRaceText;

        [Header("Shot Timer Elements")]
        [SerializeField] private GameObject timerContainer;
        [SerializeField] private Text timerCountdownText;
        [SerializeField] private Image timerFillBar;
        [SerializeField] private Color normalTimerColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color warningTimerColor = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private float warningThresholdSeconds = 5.0f;

        private int cachedRackNumber = 1;
        private int cachedScoreA = 0;
        private int cachedScoreB = 0;
        private RaceTarget cachedRaceTarget = RaceTarget.RaceTo5;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged()
        {
            UpdateRackAndScore(cachedRackNumber, cachedScoreA, cachedScoreB, cachedRaceTarget);
        }

        public void UpdateRackAndScore(int rackNumber, int playerAWins, int playerBWins, RaceTarget raceTarget)
        {
            cachedRackNumber = rackNumber;
            cachedScoreA = playerAWins;
            cachedScoreB = playerBWins;
            cachedRaceTarget = raceTarget;

            if (rackNumberText != null)
            {
                rackNumberText.text = LocalizationManager.Format("header_race_rack", (int)raceTarget, rackNumber);
            }

            if (scoreRaceText != null)
            {
                scoreRaceText.text = $"{playerAWins} - {playerBWins}";
            }
        }

        public void SetTimerVisible(bool visible)
        {
            if (timerContainer != null)
            {
                timerContainer.SetActive(visible);
            }
        }

        public void UpdateTimer(float remainingSeconds, float totalSeconds)
        {
            if (timerContainer != null && !timerContainer.activeSelf)
            {
                timerContainer.SetActive(true);
            }

            if (timerCountdownText != null)
            {
                timerCountdownText.text = $"{Mathf.CeilToInt(remainingSeconds)}s";
                timerCountdownText.color = (remainingSeconds <= warningThresholdSeconds) ? warningTimerColor : Color.white;
            }

            if (timerFillBar != null)
            {
                float fill = (totalSeconds > 0.001f) ? Mathf.Clamp01(remainingSeconds / totalSeconds) : 0f;
                timerFillBar.fillAmount = fill;
                timerFillBar.color = (remainingSeconds <= warningThresholdSeconds) ? warningTimerColor : normalTimerColor;
            }
        }
    }
}
