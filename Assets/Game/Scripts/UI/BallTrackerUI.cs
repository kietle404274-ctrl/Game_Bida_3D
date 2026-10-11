using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    public class BallTrackerUI : MonoBehaviour
    {
        [System.Serializable]
        public class BallSlot
        {
            public GameObject rootObject;
            public Image ballImage;
            public Text ballNumberText;
            public int ballNumber;
            public bool isPocketed;
        }

        [Header("Slots (7 Group Balls + 8-Ball)")]
        [SerializeField] private BallSlot[] groupBallSlots = new BallSlot[7];
        [SerializeField] private BallSlot eightBallSlot;

        [Header("Visual Colors")]
        [SerializeField] private Color neutralColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color solidColor = new Color(0.9f, 0.7f, 0.1f, 1f);
        [SerializeField] private Color stripeColor = new Color(0.2f, 0.6f, 0.9f, 1f);
        [SerializeField] private Color eightBallColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        private PlayerGroup currentGroup = PlayerGroup.Open;

        public PlayerGroup CurrentGroup => currentGroup;

        public void SetGroup(PlayerGroup group)
        {
            currentGroup = group;
            RefreshSlotsConfiguration();
        }

        public void RefreshSlotsConfiguration()
        {
            if (currentGroup == PlayerGroup.Open)
            {
                // Trạng thái trung tính trước khi chọn nhóm
                for (int i = 0; i < groupBallSlots.Length; i++)
                {
                    if (groupBallSlots[i].rootObject != null)
                    {
                        groupBallSlots[i].rootObject.SetActive(true);
                    }
                    if (groupBallSlots[i].ballNumberText != null)
                    {
                        groupBallSlots[i].ballNumberText.text = "?";
                    }
                    if (groupBallSlots[i].ballImage != null)
                    {
                        groupBallSlots[i].ballImage.color = neutralColor;
                    }
                    groupBallSlots[i].ballNumber = -1;
                    groupBallSlots[i].isPocketed = false;
                }

                if (eightBallSlot != null && eightBallSlot.rootObject != null)
                {
                    eightBallSlot.rootObject.SetActive(false);
                }
            }
            else
            {
                int startNum = (currentGroup == PlayerGroup.Solids) ? 1 : 9;
                Color groupColor = (currentGroup == PlayerGroup.Solids) ? solidColor : stripeColor;

                for (int i = 0; i < 7; i++)
                {
                    int bNum = startNum + i;
                    groupBallSlots[i].ballNumber = bNum;

                    if (groupBallSlots[i].rootObject != null)
                    {
                        groupBallSlots[i].rootObject.SetActive(!groupBallSlots[i].isPocketed);
                    }
                    if (groupBallSlots[i].ballNumberText != null)
                    {
                        groupBallSlots[i].ballNumberText.text = bNum.ToString();
                    }
                    if (groupBallSlots[i].ballImage != null)
                    {
                        groupBallSlots[i].ballImage.color = groupColor;
                    }
                }

                if (eightBallSlot != null)
                {
                    eightBallSlot.ballNumber = 8;
                    if (eightBallSlot.ballNumberText != null)
                    {
                        eightBallSlot.ballNumberText.text = "8";
                    }
                    if (eightBallSlot.ballImage != null)
                    {
                        eightBallSlot.ballImage.color = eightBallColor;
                    }
                }
            }
        }

        public void UpdateBallsStatus(BallManager ballManager)
        {
            if (ballManager == null) return;

            if (currentGroup == PlayerGroup.Open)
            {
                // Chưa chọn nhóm, giữ trạng thái trung tính
                return;
            }

            int activeCount = 0;
            for (int i = 0; i < groupBallSlots.Length; i++)
            {
                int bNum = groupBallSlots[i].ballNumber;
                if (bNum <= 0) continue;

                var ball = ballManager.GetBall(bNum);
                bool pocketed = (ball == null || ball.IsPocketed || !ball.gameObject.activeInHierarchy);
                groupBallSlots[i].isPocketed = pocketed;

                if (groupBallSlots[i].rootObject != null)
                {
                    // Theo luật Table 9: bi nào đã pocket hợp lệ thì icon biến mất
                    groupBallSlots[i].rootObject.SetActive(!pocketed);
                }

                if (!pocketed)
                {
                    activeCount++;
                }
            }

            // Khi đã dọn sạch cả 7 bi trong nhóm -> Kích hoạt icon bi 8
            if (eightBallSlot != null && eightBallSlot.rootObject != null)
            {
                bool show8Ball = (activeCount == 0);
                var ball8 = ballManager.GetBall(8);
                bool ball8Pocketed = (ball8 == null || ball8.IsPocketed || !ball8.gameObject.activeInHierarchy);
                eightBallSlot.rootObject.SetActive(show8Ball && !ball8Pocketed);
            }
        }
    }
}
