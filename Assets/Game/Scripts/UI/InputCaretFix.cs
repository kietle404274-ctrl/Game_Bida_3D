using UnityEngine;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Sửa lỗi con trỏ nhập liệu không hiện: InputField tự tạo con trỏ làm phần tử con ĐẦU TIÊN nên bị
    /// khung nền hình bình hành che mất. Component này luôn đưa con trỏ lên trên cùng khi ô đang được chọn.
    /// </summary>
    [RequireComponent(typeof(InputField))]
    public class InputCaretFix : MonoBehaviour
    {
        private InputField field;
        private Transform caret;

        private void Awake()
        {
            field = GetComponent<InputField>();
            field.caretWidth = 2;
            field.customCaretColor = true;
            field.caretColor = Color.white;
            field.caretBlinkRate = 0.85f;
        }

        private void LateUpdate()
        {
            if (field == null || !field.isFocused) return;
            if (caret == null) caret = transform.Find(gameObject.name + " Input Caret");
            if (caret != null && caret.GetSiblingIndex() != transform.childCount - 1) caret.SetAsLastSibling();
        }
    }
}
