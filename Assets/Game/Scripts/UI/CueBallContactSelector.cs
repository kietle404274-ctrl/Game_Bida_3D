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
            if (open)
            {
                // Chỉ mở được khi không có popup khác (Hướng dẫn / Luật / Menu) đang mở
                if (!HudModalState.TryOpen("spin")) return;
            }
            else
            {
                HudModalState.Release("spin");
            }
            isOpen = open;
            OnSelectorToggled?.Invoke(isOpen);
        }

        private void OnDestroy()
        {
            HudModalState.Release("spin");
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

        private Input.PlayerInputController inputController;

        private void Update()
        {
            if (!isOpen)
            {
                // Phím J mở popup chọn điểm chạm (chỉ khi đang được phép ngắm)
                if (UnityEngine.Input.GetKeyDown(KeyCode.J))
                {
                    if (inputController == null) inputController = FindObjectOfType<Input.PlayerInputController>();
                    if (inputController == null || inputController.IsAimingAllowed) SetOpen(true);
                }
                return;
            }

            // Enter: xác nhận và quay lại thao tác bình thường
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SetOpen(false);
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                ResetToCenter();
                return;
            }

            // Mũi tên tinh chỉnh điểm chạm khi popup đang mở
            float h = 0f;
            float v = 0f;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                Vector2 delta = new Vector2(h, v) * (keyAdjustSpeed * Time.deltaTime);
                SetContact(currentContact + delta);
            }
        }
    }
}
