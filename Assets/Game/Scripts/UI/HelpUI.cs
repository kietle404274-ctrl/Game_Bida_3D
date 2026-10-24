using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    public class HelpUI : MonoBehaviour
    {
        [Header("UI Containers")]
        [SerializeField] private GameObject modalRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text helpContentText;

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            SetupHelpText();
        }

        private void SetupHelpText()
        {
            if (helpContentText != null && string.IsNullOrEmpty(helpContentText.text))
            {
                helpContentText.text = 
                    "<b>HƯỚNG DẪN ĐIỀU KHIỂN & THAO TÁC GAME BIDA 3D:</b>\n\n" +
                    "• <b>Xoay hướng ngắm:</b> Di chuyển chuột để xoay camera 360 độ quanh bi cái.\n" +
                    "• <b>Chỉnh lực đánh:</b> Lăn con lăn chuột (Mouse ScrollWheel) từ 5% đến 100%.\n" +
                    "• <b>Bắn bi:</b> Nhấp chuột trái (Left Mouse Button).\n" +
                    "• <b>Chọn điểm đánh trên bi cái (Spin):</b> Nhấn phím [J] để mở bảng chọn, click/kéo hoặc dùng mũi tên để chọn điểm, [R] về giữa, [Enter] để xác nhận.\n" +
                    "• <b>Đặt bi tự do (Ball-in-Hand):</b> Di chuột để xoay góc nhìn, dùng [W, A, S, D] để di chuyển bi cái, nhấn [Enter] để xác nhận vị trí hợp lệ.\n" +
                    "• <b>Ăn bi:</b> Không cần gọi bi hay gọi lỗ. Đưa bi hợp lệ vào bất kỳ lỗ nào để được đánh tiếp.\n" +
                    "• <b>Phím tắt:</b> [Esc] Cài đặt, [R] Luật thi đấu WPA, [H] Hướng dẫn điều khiển.";
            }
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("help")) return;
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            HudModalState.Release("help");
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            HudModalState.Release("help");
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                if (modalRoot.activeSelf) Hide();
                else Show();
            }
        }
    }
}
