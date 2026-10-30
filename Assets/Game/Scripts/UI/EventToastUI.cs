using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class EventToastUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Configuration")]
        [SerializeField] private float displayDuration = 2.5f;
        [SerializeField] private float fadeDuration = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.1f, 0.4f, 0.8f, 0.9f);
        [SerializeField] private Color foulColor = new Color(0.85f, 0.2f, 0.2f, 0.95f);
        [SerializeField] private Color winColor = new Color(0.2f, 0.75f, 0.3f, 0.95f);
        [SerializeField] private Color infoColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        private Coroutine activeToastCoroutine;

        private void Awake()
        {
            if (canvasGroup == null && toastRoot != null)
            {
                canvasGroup = toastRoot.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = toastRoot.AddComponent<CanvasGroup>();
                }
            }

            if (toastRoot != null)
            {
                toastRoot.SetActive(false);
            }
        }

        public void ShowToast(string title, string message, Color? customBgColor = null, float duration = -1f)
        {
            if (toastRoot == null || !gameObject.activeInHierarchy) return;

            if (activeToastCoroutine != null)
            {
                StopCoroutine(activeToastCoroutine);
            }

            activeToastCoroutine = StartCoroutine(ToastRoutine(title, message, customBgColor ?? infoColor, duration > 0 ? duration : displayDuration));
        }

        public void ShowFoulToast(string foulName, string description)
        {
            ShowToast($"{LocalizationManager.Get("foul_badge")}: {foulName}", $"{description}\n{LocalizationManager.Get("foul_sub")}", foulColor, 3.0f);
        }

        public void ShowTurnToast(string playerName)
        {
            ShowToast(LocalizationManager.Get("turn_new_turn"), LocalizationManager.Format("turn_playing_now", playerName), normalColor, 2.0f);
        }

        public void ShowGroupAssignedToast(string playerName, string groupName)
        {
            ShowToast(LocalizationManager.Get("group_open"), LocalizationManager.Format("turn_group_assigned", playerName, groupName), normalColor, 3.0f);
        }

        public void ShowWinToast(string winnerName, string details)
        {
            ShowToast(LocalizationManager.Get("win_badge"), $"{winnerName} - {details}", winColor, 4.0f);
        }

        private IEnumerator ToastRoutine(string title, string message, Color bgColor, float duration)
        {
            toastRoot.SetActive(true);

            if (titleText != null) titleText.text = title;
            if (messageText != null) messageText.text = message;
            if (backgroundImage != null) backgroundImage.color = bgColor;

            // Fade in
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 1f;

            // Wait
            yield return new WaitForSeconds(duration);

            // Fade out
            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (canvasGroup != null) canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
            toastRoot.SetActive(false);
            activeToastCoroutine = null;
        }
    }
}
