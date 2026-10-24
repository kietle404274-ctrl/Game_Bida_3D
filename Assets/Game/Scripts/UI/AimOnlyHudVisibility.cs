using UnityEngine;

namespace Game.Billiards.UI
{
    /// <summary>Chỉ hiện panel khi người chơi đang được phép ngắm bắn.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class AimOnlyHudVisibility : MonoBehaviour
    {
        private CanvasGroup group;
        private Input.PlayerInputController input;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (input == null) input = FindObjectOfType<Input.PlayerInputController>();
            bool show = input == null || input.IsAimingAllowed;
            group.alpha = show ? 1f : 0f;
            group.interactable = show;
            group.blocksRaycasts = show;
        }
    }
}
