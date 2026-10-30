using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class RulesUI : MonoBehaviour
    {
        [Header("Modal Containers")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text ruleTitleText;
        [SerializeField] private Text ruleContentText;

        [Header("Tab Buttons")]
        [SerializeField] private Button breakTabButton;
        [SerializeField] private Button openTableTabButton;
        [SerializeField] private Button foulTabButton;
        [SerializeField] private Button eightBallTabButton;
        [SerializeField] private Button winLoseTabButton;

        private int currentTabIndex = 0;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            if (breakTabButton != null)
                breakTabButton.onClick.AddListener(() => ShowTab(0));
            if (openTableTabButton != null)
                openTableTabButton.onClick.AddListener(() => ShowTab(1));
            if (foulTabButton != null)
                foulTabButton.onClick.AddListener(() => ShowTab(2));
            if (eightBallTabButton != null)
                eightBallTabButton.onClick.AddListener(() => ShowTab(3));
            if (winLoseTabButton != null)
                winLoseTabButton.onClick.AddListener(() => ShowTab(4));

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;

            RefreshLocalizedTexts();
            ShowTab(0);
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("rules")) return;
            RefreshLocalizedTexts();
            if (modalRoot != null) modalRoot.SetActive(true);
            ShowTab(currentTabIndex);
        }

        public void Hide()
        {
            HudModalState.Release("rules");
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            HudModalState.Release("rules");
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Hide();
                else Show();
            }
        }

        private void RefreshLocalizedTexts()
        {
            if (modalRoot != null)
            {
                Transform title = modalRoot.transform.Find("Title");
                if (title != null && title.GetComponent<Text>() != null)
                    title.GetComponent<Text>().text = LocalizationManager.Get("rules_title");

                Transform head = modalRoot.transform.Find("RuleHeadText");
                if (head != null && head.GetComponent<Text>() != null)
                    head.GetComponent<Text>().text = LocalizationManager.Get("rules_head_detail");
            }

            SetButtonText(breakTabButton, LocalizationManager.Get("rules_tab_1_btn"));
            SetButtonText(openTableTabButton, LocalizationManager.Get("rules_tab_2_btn"));
            SetButtonText(foulTabButton, LocalizationManager.Get("rules_tab_3_btn"));
            SetButtonText(eightBallTabButton, LocalizationManager.Get("rules_tab_4_btn"));
            SetButtonText(winLoseTabButton, LocalizationManager.Get("rules_tab_5_btn"));
            SetButtonText(closeButton, LocalizationManager.Get("rules_close_btn"));

            ShowTab(currentTabIndex);
        }

        private static void SetButtonText(Button b, string val)
        {
            if (b == null || string.IsNullOrEmpty(val)) return;
            Text t = b.GetComponentInChildren<Text>(true);
            if (t != null) t.text = val;
        }

        private void UpdateTabSelection(int tabIndex)
        {
            Button[] tabs = { breakTabButton, openTableTabButton, foulTabButton, eightBallTabButton, winLoseTabButton };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] == null) continue;
                var fx = tabs[i].GetComponent<UIHoverEffect>();
                if (fx == null) fx = UIHoverEffect.Attach(tabs[i].gameObject, tabs[i].targetGraphic is SkewedPanelGraphic);
                fx.SetSelected(i == tabIndex);
            }
        }

        public void ShowTab(int tabIndex)
        {
            currentTabIndex = tabIndex;
            UpdateTabSelection(tabIndex);
            switch (tabIndex)
            {
                case 0:
                    SetContent(
                        LocalizationManager.Get("rules_tab_1_title"),
                        LocalizationManager.Get("rules_tab_1_desc")
                    );
                    break;

                case 1:
                    SetContent(
                        LocalizationManager.Get("rules_tab_2_title"),
                        LocalizationManager.Get("rules_tab_2_desc")
                    );
                    break;

                case 2:
                    SetContent(
                        LocalizationManager.Get("rules_tab_3_title"),
                        LocalizationManager.Get("rules_tab_3_desc")
                    );
                    break;

                case 3:
                    SetContent(
                        LocalizationManager.Get("rules_tab_4_title"),
                        LocalizationManager.Get("rules_tab_4_desc")
                    );
                    break;

                case 4:
                    SetContent(
                        LocalizationManager.Get("rules_tab_5_title"),
                        LocalizationManager.Get("rules_tab_5_desc")
                    );
                    break;
            }
        }

        private void SetContent(string title, string content)
        {
            if (ruleTitleText != null) ruleTitleText.text = title;
            if (ruleContentText != null) ruleContentText.text = content;
        }
    }
}
