using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;

namespace Game.Billiards.UI
{
    public class CallShotUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Dropdown ballDropdown;
        [SerializeField] private Dropdown pocketDropdown;
        [SerializeField] private Button safetyButton;
        [SerializeField] private Image safetyButtonImage;
        [SerializeField] private Text statusSummaryText;

        [Header("Styling")]
        [SerializeField] private Color safetyActiveColor = new Color(0.9f, 0.4f, 0.1f, 1f);
        [SerializeField] private Color safetyInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        private PlayerInputController inputController;
        private BallManager ballManager;
        private MatchStateManager matchStateManager;

        private List<int> availableBallsList = new List<int>();
        private bool isSafetyActive = false;

        private readonly string[] pocketNames = new string[]
        {
            "Lỗ Góc Trên Trái (Top-Left)",
            "Lỗ Góc Dưới Trái (Bottom-Left)",
            "Lỗ Góc Trên Phải (Top-Right)",
            "Lỗ Góc Dưới Phải (Bottom-Right)",
            "Lỗ Giữa Trái (Mid-Left)",
            "Lỗ Giữa Phải (Mid-Right)"
        };

        public bool IsSafetyActive => isSafetyActive;
        public int CurrentSelectedBall => (ballDropdown != null && availableBallsList.Count > ballDropdown.value) ? availableBallsList[ballDropdown.value] : -1;
        public int CurrentSelectedPocket => ballDropdown != null ? pocketDropdown.value : -1;

        public void Initialize(PlayerInputController input, BallManager bManager, MatchStateManager stateMgr)
        {
            inputController = input;
            ballManager = bManager;
            matchStateManager = stateMgr;

            SetupPocketDropdown();

            if (ballDropdown != null)
            {
                ballDropdown.onValueChanged.RemoveAllListeners();
                ballDropdown.onValueChanged.AddListener(OnBallSelectionChanged);
            }

            if (pocketDropdown != null)
            {
                pocketDropdown.onValueChanged.RemoveAllListeners();
                pocketDropdown.onValueChanged.AddListener(OnPocketSelectionChanged);
            }

            if (safetyButton != null)
            {
                safetyButton.onClick.RemoveAllListeners();
                safetyButton.onClick.AddListener(ToggleSafety);
            }
        }

        private void SetupPocketDropdown()
        {
            if (pocketDropdown == null) return;

            pocketDropdown.ClearOptions();
            List<string> options = new List<string>(pocketNames);
            pocketDropdown.AddOptions(options);
        }

        public void RefreshCallShotOptions()
        {
            if (matchStateManager == null || matchStateManager.State == null) return;

            MatchState state = matchStateManager.State;

            // 1. Break shot: bỏ qua call shot theo WPA Rule 9.3
            if (state.turnSequence == 0 || state.phase == MatchPhase.BreakSetup)
            {
                if (panelRoot != null) panelRoot.SetActive(false);
                if (inputController != null) inputController.ClearCallShot();
                return;
            }

            if (panelRoot != null) panelRoot.SetActive(true);

            // 2. Lấy danh sách bi mục tiêu hợp lệ
            availableBallsList.Clear();
            PlayerGroup shooterGroup = state.CurrentShooterGroup;

            if (ballManager != null)
            {
                if (state.tableOpen || shooterGroup == PlayerGroup.Open)
                {
                    // Bàn mở: được chọn bất kỳ bi mục tiêu nào từ 1 đến 15 (trừ bi 8)
                    for (int i = 1; i <= 15; i++)
                    {
                        if (i == 8) continue;
                        var b = ballManager.GetBall(i);
                        if (b != null && !b.IsPocketed && b.gameObject.activeInHierarchy)
                        {
                            availableBallsList.Add(i);
                        }
                    }
                }
                else
                {
                    int startNum = (shooterGroup == PlayerGroup.Solids) ? 1 : 9;
                    int endNum = startNum + 6;

                    for (int i = startNum; i <= endNum; i++)
                    {
                        var b = ballManager.GetBall(i);
                        if (b != null && !b.IsPocketed && b.gameObject.activeInHierarchy)
                        {
                            availableBallsList.Add(i);
                        }
                    }

                    // Nếu đã ăn hết nhóm -> Bắt buộc chọn bi 8
                    if (availableBallsList.Count == 0)
                    {
                        var b8 = ballManager.GetBall(8);
                        if (b8 != null && !b8.IsPocketed && b8.gameObject.activeInHierarchy)
                        {
                            availableBallsList.Add(8);
                        }
                    }
                }
            }

            // Cập nhật Options vào Ball Dropdown
            if (ballDropdown != null)
            {
                ballDropdown.ClearOptions();
                List<string> ballLabels = new List<string>();
                for (int i = 0; i < availableBallsList.Count; i++)
                {
                    int num = availableBallsList[i];
                    string typeStr = (num == 8) ? "Bi 8 ĐEN (Quyết Định)" : (num <= 7 ? $"Bi {num} (Trơn)" : $"Bi {num} (Sọc)");
                    ballLabels.Add(typeStr);
                }
                ballDropdown.AddOptions(ballLabels);
                if (availableBallsList.Count > 0)
                {
                    ballDropdown.value = 0;
                }
            }

            // Cập nhật trạng thái sang Input Controller
            ApplyCallShotToInput();
        }

        private void OnBallSelectionChanged(int index)
        {
            ApplyCallShotToInput();
        }

        private void OnPocketSelectionChanged(int index)
        {
            ApplyCallShotToInput();
        }

        public void ToggleSafety()
        {
            SetSafety(!isSafetyActive);
        }

        public void SetSafety(bool active)
        {
            isSafetyActive = active;

            if (safetyButtonImage != null)
            {
                safetyButtonImage.color = isSafetyActive ? safetyActiveColor : safetyInactiveColor;
            }

            if (ballDropdown != null) ballDropdown.interactable = !isSafetyActive;
            if (pocketDropdown != null) pocketDropdown.interactable = !isSafetyActive;

            ApplyCallShotToInput();
        }

        private void ApplyCallShotToInput()
        {
            if (inputController == null) return;

            if (isSafetyActive)
            {
                inputController.SetCallSafety(true);
                if (statusSummaryText != null)
                {
                    statusSummaryText.text = "CHIẾN THUẬT: ĐÁNH AN TOÀN (SAFETY)";
                    statusSummaryText.color = safetyActiveColor;
                }
            }
            else
            {
                int ballNum = CurrentSelectedBall;
                int pocketId = CurrentSelectedPocket;

                if (ballNum > 0 && pocketId >= 0)
                {
                    inputController.SetCallShot(ballNum, pocketId);
                    if (statusSummaryText != null)
                    {
                        statusSummaryText.text = $"GỌI: Bi {ballNum} vào {pocketNames[pocketId]}";
                        statusSummaryText.color = Color.white;
                    }
                }
                else
                {
                    inputController.ClearCallShot();
                    if (statusSummaryText != null)
                    {
                        statusSummaryText.text = "Chưa chọn cú đánh gọi!";
                        statusSummaryText.color = Color.yellow;
                    }
                }
            }
        }
    }
}
