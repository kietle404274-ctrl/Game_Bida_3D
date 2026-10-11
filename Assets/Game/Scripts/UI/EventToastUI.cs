using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
            if (toastRoot == null) return;

            if (activeToastCoroutine != null)
            {
                StopCoroutine(activeToastCoroutine);
            }

            activeToastCoroutine = StartCoroutine(ToastRoutine(title, message, customBgColor ?? infoColor, duration > 0 ? duration : displayDuration));
        }

        public void ShowFoulToast(string foulName, string description)
        {
            ShowToast($"PHẠM LỖI: {foulName}", $"{description}\nĐối thủ nhận quyền Ball-in-Hand!", foulColor, 3.0f);
        }

        public void ShowTurnToast(string playerName)
        {
            ShowToast("LƯỢT ĐÁNH MỚI", $"Đến lượt thi đấu của: {playerName}", normalColor, 2.0f);
        }

        public void ShowGroupAssignedToast(string playerName, string groupName)
        {
            ShowToast("XÁC ĐỊNH NHÓM BI", $"{playerName} đã chọn nhóm: {groupName}", normalColor, 3.0f);
        }

        public void ShowWinToast(string winnerName, string details)
        {
            ShowToast("CHIẾN THẮNG!", $"{winnerName} - {details}", winColor, 4.0f);
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
