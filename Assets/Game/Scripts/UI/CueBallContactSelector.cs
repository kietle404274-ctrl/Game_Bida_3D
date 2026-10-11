using System;
using UnityEngine;

namespace Game.Billiards.UI
{
    public class CueBallContactSelector : MonoBehaviour
    {
        [Header("Contact Configuration")]
        [Tooltip("Điểm chạm hiện tại (x: -1..1 là side-spin, y: -1..1 là top/back-spin)")]
        [SerializeField] private Vector2 currentContact = Vector2.zero;

        [Tooltip("Tốc độ dịch chuyển điểm chạm bằng phím điều hướng")]
        [SerializeField] private float keyAdjustSpeed = 1.5f;

        [Tooltip("Trạng thái hiển thị bộ chọn điểm chạm")]
        [SerializeField] private bool isOpen = false;

        public Vector2 CurrentContact => currentContact;
        public bool IsOpen => isOpen;

        public event Action<Vector2> OnContactPointChanged;
        public event Action<bool> OnSelectorToggled;

        public void ToggleSelector()
        {
            SetOpen(!isOpen);
        }

        public void SetOpen(bool open)
        {
            isOpen = open;
            OnSelectorToggled?.Invoke(isOpen);
        }

        public void SetContact(Vector2 contact)
        {
            currentContact = Vector2.ClampMagnitude(contact, 1.0f);
            OnContactPointChanged?.Invoke(currentContact);
        }

        public void ResetToCenter()
        {
            SetContact(Vector2.zero);
        }

        private void Update()
        {
            // Phím tắt C để bật/tắt bộ chọn điểm chạm
            if (UnityEngine.Input.GetKeyDown(KeyCode.C))
            {
                ToggleSelector();
            }

            // Nếu đang mở hoặc người chơi bấm tổ hợp phím chỉnh spin (I, K, J, L hoặc Mũi tên)
            float h = 0f;
            float v = 0f;

            if (UnityEngine.Input.GetKey(KeyCode.I) || (isOpen && UnityEngine.Input.GetKey(KeyCode.UpArrow))) v += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.K) || (isOpen && UnityEngine.Input.GetKey(KeyCode.DownArrow))) v -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.J) || (isOpen && UnityEngine.Input.GetKey(KeyCode.LeftArrow))) h -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.L) || (isOpen && UnityEngine.Input.GetKey(KeyCode.RightArrow))) h += 1f;

            if (UnityEngine.Input.GetKeyDown(KeyCode.R) && isOpen)
            {
                ResetToCenter();
                return;
            }

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                Vector2 delta = new Vector2(h, v) * (keyAdjustSpeed * Time.deltaTime);
                SetContact(currentContact + delta);
            }
        }

        private void OnGUI()
        {
            // Hiển thị một widget trực quan ở góc dưới màn hình
            int size = 100;
            int margin = 20;
            Rect boxRect = new Rect(margin, Screen.height - size - margin - 40, size, size + 30);

            GUI.Box(boxRect, "Spin (C)");

            Rect ballRect = new Rect(boxRect.x + 10, boxRect.y + 25, size - 20, size - 20);
            GUI.Box(ballRect, "");

            // Vẽ tâm bi
            Vector2 center = ballRect.center;
            float radius = (ballRect.width * 0.5f);

            // Tọa độ chấm đỏ chỉ điểm chạm
            float dotX = center.x + (currentContact.x * radius * 0.8f);
            float dotY = center.y - (currentContact.y * radius * 0.8f); // y đảo ngược trên GUI màn hình
            Rect dotRect = new Rect(dotX - 4, dotY - 4, 8, 8);

            GUI.color = Color.red;
            GUI.Box(dotRect, "");
            GUI.color = Color.white;

            // Xử lý click chuột trong vòng tròn bi cái để chọn nhanh điểm chạm
            Event e = Event.current;
            if (e.isMouse && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
            {
                if (ballRect.Contains(e.mousePosition))
                {
                    Vector2 localPos = e.mousePosition - center;
                    float normX = localPos.x / radius;
                    float normY = -localPos.y / radius;
                    SetContact(new Vector2(normX, normY));
                    e.Use();
                }
            }
        }
    }
}
