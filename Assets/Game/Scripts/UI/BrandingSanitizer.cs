using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Xóa các dòng chữ có thể vướng bản quyền ("CHAMPIONSHIP TOURNAMENT EDITION ...") đã được lưu sẵn trong các scene
    /// (Lobby, Gameplay, Boot). Chạy mỗi khi một scene được nạp nên không cần sửa trực tiếp file scene.
    /// </summary>
    public static class BrandingSanitizer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Sanitize();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Sanitize();
        }

        private static void Sanitize()
        {
            foreach (Text t in Object.FindObjectsOfType<Text>(true))
            {
                if (t == null || string.IsNullOrEmpty(t.text)) continue;
                if (t.text.IndexOf("CHAMPIONSHIP TOURNAMENT", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    t.text = string.Empty;
                    t.gameObject.SetActive(false);
                }
            }
        }
    }
}
