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
            [Tooltip("Dải màu của bi sọc (ẩn với bi trơn)")]
            public Image bandImage;
            [Tooltip("Đĩa trắng chứa số bi")]
            public Image discImage;
        }

        /// <summary>Màu thân bi theo bộ bi tiêu chuẩn (9-15 dùng lại màu của 1-7).</summary>
        public static Color GetBallColor(int number)
        {
            int n = number > 8 ? number - 8 : number;
            switch (n)
            {
                case 1: return new Color(0.98f, 0.82f, 0.10f);
                case 2: return new Color(0.10f, 0.25f, 0.80f);
                case 3: return new Color(0.90f, 0.15f, 0.12f);
                case 4: return new Color(0.42f, 0.15f, 0.60f);
                case 5: return new Color(0.98f, 0.45f, 0.08f);
                case 6: return new Color(0.08f, 0.55f, 0.25f);
                case 7: return new Color(0.55f, 0.10f, 0.12f);
                case 8: return new Color(0.05f, 0.05f, 0.05f);
                default: return Color.white;
            }
        }

        private void ApplyBallVisual(BallSlot slot, int number)
        {
            bool stripe = number >= 9;
            Color body = GetBallColor(number);
            if (slot.ballImage != null) slot.ballImage.color = stripe ? Color.white : body;
            if (slot.bandImage != null)
            {
                slot.bandImage.gameObject.SetActive(stripe);
                slot.bandImage.color = body;
            }
            if (slot.discImage != null)
            {
                slot.discImage.gameObject.SetActive(true);
                slot.discImage.color = Color.white;
            }
            if (slot.ballNumberText != null)
            {
                slot.ballNumberText.text = number.ToString();
                slot.ballNumberText.color = Color.black;
            }
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
            if (groupBallSlots == null || groupBallSlots.Length == 0) return;

            if (currentGroup == PlayerGroup.Open)
            {
                // Trạng thái trung tính trước khi chọn nhóm
                for (int i = 0; i < groupBallSlots.Length; i++)
                {
                    if (groupBallSlots[i] == null) continue;
                    if (groupBallSlots[i].rootObject != null)
                    {
                        groupBallSlots[i].rootObject.SetActive(true);
                    }
                    if (groupBallSlots[i].ballNumberText != null)
                    {
                        groupBallSlots[i].ballNumberText.text = "?";
                        groupBallSlots[i].ballNumberText.color = Color.white;
                    }
                    if (groupBallSlots[i].ballImage != null)
                    {
                        groupBallSlots[i].ballImage.color = neutralColor;
                    }
                    if (groupBallSlots[i].bandImage != null) groupBallSlots[i].bandImage.gameObject.SetActive(false);
                    if (groupBallSlots[i].discImage != null) groupBallSlots[i].discImage.gameObject.SetActive(false);
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

                for (int i = 0; i < Mathf.Min(7, groupBallSlots.Length); i++)
                {
                    if (groupBallSlots[i] == null) continue;
                    int bNum = startNum + i;
                    groupBallSlots[i].ballNumber = bNum;

                    if (groupBallSlots[i].rootObject != null)
                    {
                        groupBallSlots[i].rootObject.SetActive(!groupBallSlots[i].isPocketed);
                    }
                    ApplyBallVisual(groupBallSlots[i], bNum);
                }

                if (eightBallSlot != null)
                {
                    eightBallSlot.ballNumber = 8;
                    ApplyBallVisual(eightBallSlot, 8);
                }
            }
        }

        public void UpdateBallsStatus(BallManager ballManager)
        {
            if (ballManager == null || groupBallSlots == null || groupBallSlots.Length == 0) return;

            if (currentGroup == PlayerGroup.Open)
            {
                // Chưa chọn nhóm, giữ trạng thái trung tính
                return;
            }

            int activeCount = 0;
            for (int i = 0; i < groupBallSlots.Length; i++)
            {
                if (groupBallSlots[i] == null) continue;
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
