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
        [SerializeField] private Image panelBackgroundImage;
        [SerializeField] private BallTrackerUI ballTracker;

        [Header("Colors")]
        [SerializeField] private Color activeTurnColor = new Color(0.2f, 0.8f, 0.4f, 1f);
        [SerializeField] private Color inactiveTurnColor = new Color(0.25f, 0.25f, 0.25f, 0.8f);

        public BallTrackerUI BallTracker => ballTracker;

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
