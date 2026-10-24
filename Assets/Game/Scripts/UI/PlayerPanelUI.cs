using System;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;

namespace Game.Billiards.UI
{
    public class PlayerPanelUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Text playerNameText;
        [SerializeField] private Text groupText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Image avatarImage;
        [SerializeField] private GameObject turnHighlightObject;
        [SerializeField] private Graphic panelBackgroundImage;
        [SerializeField] private BallTrackerUI ballTracker;
        [SerializeField] private TurnTimerUI turnTimer;

        [Header("Colors")]
        [SerializeField] private Color activeTurnColor = new Color(0.22f, 0.40f, 0.62f, 0.98f);
        [SerializeField] private Color inactiveTurnColor = new Color(0.13f, 0.21f, 0.35f, 0.96f);

        public BallTrackerUI BallTracker => ballTracker;

        public void SetTimer(float remaining, float total)
        {
            if (turnTimer != null) turnTimer.SetTime(remaining, total);
        }

        public void ClearTimer()
        {
            if (turnTimer != null) turnTimer.Hide();
        }

        public void SetPlayerInfo(string playerName, Sprite avatar = null)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }

            if (avatarImage != null && avatar != null)
            {
                avatarImage.sprite = avatar;
            }
        }

        public void SetGroup(PlayerGroup group)
        {
            if (groupText != null)
            {
                switch (group)
                {
                    case PlayerGroup.Open:
                        groupText.text = "BÀN MỞ (OPEN)";
                        groupText.color = Color.white;
                        break;
                    case PlayerGroup.Solids:
                        groupText.text = "BI TRƠN (SOLIDS 1-7)";
                        groupText.color = new Color(1f, 0.8f, 0.2f, 1f);
                        break;
                    case PlayerGroup.Stripes:
                        groupText.text = "BI SỌC (STRIPES 9-15)";
                        groupText.color = new Color(0.3f, 0.7f, 1f, 1f);
                        break;
                    case PlayerGroup.EightBall:
                        groupText.text = "BI SỐ 8 (EIGHT BALL)";
                        groupText.color = new Color(1f, 0.84f, 0f, 1f);
                        break;
                }
            }

            if (ballTracker != null)
            {
                ballTracker.SetGroup(group);
            }
        }

        public void SetTurnActive(bool isActive)
        {
            if (turnHighlightObject != null)
            {
                turnHighlightObject.SetActive(isActive);
            }

            if (panelBackgroundImage != null)
            {
                panelBackgroundImage.color = isActive ? activeTurnColor : inactiveTurnColor;
            }
        }

        public void SetScore(int rackWins)
        {
            if (scoreText != null)
            {
                scoreText.text = rackWins.ToString();
            }
        }
    }
}
