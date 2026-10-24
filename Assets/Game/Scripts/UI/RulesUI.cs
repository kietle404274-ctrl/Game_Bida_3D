using System;
using UnityEngine;
using UnityEngine.UI;

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

            ShowTab(0);
        }

        public void Show()
        {
            if (!HudModalState.TryOpen("rules")) return;
            if (modalRoot != null) modalRoot.SetActive(true);
            ShowTab(0);
        }

        public void Hide()
        {
            HudModalState.Release("rules");
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private void OnDestroy()
        {
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

        public void ShowTab(int tabIndex)
        {
            switch (tabIndex)
            {
                case 0:
                    SetContent(
                        "1. QUY TẮC KHAI CUỘC (BREAK SHOT)",
                        "• Bi cái được đặt sau lằn ngang phía trên (Head String).\n" +
                        "• Cú đánh khai cuộc hợp lệ phải đưa ít nhất một bi mục tiêu vào lỗ HOẶC có tối thiểu 4 bi mục tiêu chạm vào băng.\n" +
                        "• Nếu không đạt một trong hai điều kiện trên: phạm lỗi Khai Cuộc Không Hợp Lệ (Illegal Break).\n" +
                        "• Rơi bi cái (Scratch) khi khai cuộc: đối thủ nhận quyền Ball-in-Hand phía sau lằn ngang (Behind the Head String).\n" +
                        "• Theo luật WPA: Dù có bi vào lỗ khi khai cuộc thì bàn vẫn luôn ở trạng thái MỞ (Open Table)."
                    );
                    break;

                case 1:
                    SetContent(
                        "2. BÀN MỞ & CHỌN NHÓM (OPEN TABLE & GROUPS)",
                        "• Khi bàn còn MỞ (Open Table), người chơi có quyền chạm bất kỳ bi nào (trơn 1-7 hoặc sọc 9-15, ngoại trừ bi 8 đen) đầu tiên.\n" +
                        "• Nhóm bi được xác định bởi quả bi trơn/sọc đầu tiên vào lỗ (bất kỳ lỗ nào) sau khai cuộc, không cần gọi bi hay gọi lỗ.\n" +
                        "• Cơ thủ đưa bi trơn hợp lệ vào lỗ sẽ được gán nhóm Bi Trơn (Solids 1-7); đối thủ sẽ nhận nhóm Bi Sọc (Stripes 9-15)."
                    );
                    break;

                case 2:
                    SetContent(
                        "3. THỨ TỰ TIẾP XÚC, CHẠM BĂNG & CÁC LỖI PHẠT (FOULS)",
                        "• <b>Thứ tự chạm bi:</b> Sau khi đã gán nhóm, bi cái phải chạm vào bi thuộc nhóm của mình trước tiên. Chạm bi đối thủ hoặc bi 8 trước sẽ bị phạt lỗi WrongBallFirst.\n" +
                        "• <b>Quy tắc chạm băng:</b> Sau khi bi cái tiếp xúc bi mục tiêu hợp lệ, phải có ít nhất một bi (bi cái hoặc bất kỳ bi mục tiêu nào) chạm băng hoặc đi vào lỗ. Nếu không sẽ bị phạt lỗi NoRailAfterContact.\n" +
                        "• <b>Hết giờ (Shot Clock Expired):</b> Không thực hiện cú đánh khi hết thời gian cho phép.\n" +
                        "• <b>Hình phạt:</b> Mọi lỗi phạm quy (Foul) đều trao cho đối thủ quyền Đặt Bi Tự Do Trên Toàn Bàn (Ball-in-Hand Anywhere on Table)."
                    );
                    break;

                case 3:
                    SetContent(
                        "4. QUY TẮC VỀ BI SỐ 8 (THE 8-BALL)",
                        "• Chỉ khi cơ thủ đã dọn sạch toàn bộ 7 bi thuộc nhóm của mình thì bi số 8 mới trở thành bi mục tiêu hợp lệ.\n" +
                        "• Không cần gọi lỗ: đưa bi số 8 vào bất kỳ lỗ nào (sau khi đã ăn hết nhóm, không phạm lỗi) là thắng ván.\n" +
                        "• <b>Xử THUA VÁN NGAY LẬP TỨC (Loss of Game) nếu:</b>\n" +
                        "  1. Đưa bi 8 vào lỗ khi chưa ăn hết bi nhóm mình.\n" +
                        "  2. Đưa bi 8 vào lỗ khi bàn còn mở (chưa chia nhóm).\n" +
                        "  3. Rơi bi cái (Scratch) trong cú đánh đưa bi số 8 vào lỗ.\n" +
                        "  4. Làm bi số 8 bay văng ra khỏi bàn."
                    );
                    break;

                case 4:
                    SetContent(
                        "5. ĐIỀU KIỆN THẮNG TRẬN (WIN CONDITIONS)",
                        "• Cơ thủ dọn sạch nhóm bi của mình và đưa bi số 8 vào lỗ một cách hợp lệ (không phạm lỗi) sẽ GIÀNH CHIẾN THẮNG VÁN ĐẤU (Rack Win).\n" +
                        "• Trận đấu diễn ra theo thể thức Race-to (chạm đích):\n" +
                        "  Cơ thủ đầu tiên đạt đủ số ván thắng theo quy định (Race to 3, 5, 7, hoặc 9) sẽ trở thành NGƯỜI CHIẾN THẮNG CHUNG CUỘC TRẬN ĐẤU."
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
