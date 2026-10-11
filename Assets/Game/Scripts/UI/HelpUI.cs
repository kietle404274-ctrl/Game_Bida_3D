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
                    "• <b>Chỉnh Áp-phê (Spin):</b> Nhấn phím [C] hoặc bấm vào biểu tượng bi cái góc màn hình để mở bảng tiếp xúc (Cu-lê / Trô / Ép-phê trái-phải).\n" +
                    "• <b>Đặt bi tự do (Ball-in-Hand):</b> Sử dụng các phím [W, A, S, D] để di chuyển bi cái trên mặt bàn, nhấn [Enter] hoặc [Click Trái] để xác nhận vị trí hợp lệ.\n" +
                    "• <b>Cú đánh gọi (Call Shot):</b> Chọn số bi và lỗ mục tiêu tại bảng Call Shot trước khi bắn (hoặc chọn Đánh An Toàn - Safety).\n" +
                    "• <b>Phím tắt:</b> [Esc] Cài đặt, [R] Luật thi đấu WPA, [H] Hướng dẫn điều khiển.";
            }
        }

        public void Show()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(true);
            }
        }

        public void Hide()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(false);
            }
        }

        public void Toggle()
        {
            if (modalRoot != null)
            {
                modalRoot.SetActive(!modalRoot.activeSelf);
            }
        }
    }
}
