namespace Game.Billiards.UI
{
    /// <summary>
    /// Chỉ cho phép một popup HUD (Hướng dẫn / Luật / Menu / Chọn điểm cơ) mở tại một thời điểm.
    /// Khi có popup mở, mọi thao tác ngắm / bắn / đặt bi bị khóa cho đến khi popup đó đóng.
    /// </summary>
    public static class HudModalState
    {
        private static string current;

        public static bool IsModalOpen => current != null;
        public static string Current => current;

        /// <summary>Xin quyền mở popup. Trả về false nếu đang có popup khác mở.</summary>
        public static bool TryOpen(string id)
        {
            if (current != null && current != id) return false;
            current = id;
            return true;
        }

        /// <summary>Ép mở (popup kết quả ván đấu có ưu tiên cao nhất, thay thế mọi popup khác).</summary>
        public static void ForceOpen(string id)
        {
            current = id;
        }

        public static void Release(string id)
        {
            if (current == id) current = null;
        }

        public static void Reset()
        {
            current = null;
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            current = null;
        }
    }
}
