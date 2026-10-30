using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Billiards.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float splashDelaySeconds = 1.0f;
        [SerializeField] private string targetInitialScene = "Lobby";

        private void Start()
        {
            // Đảm bảo GameSession đã tồn tại
            GameSession.GetOrCreateInstance();

            // Khởi động luồng scene vào Lobby
            StartCoroutine(BootRoutine());
        }

        [Tooltip("Thời gian tối thiểu hiển thị màn hình khởi động (giây) để thanh tải chạy mượt")]
        [SerializeField] private float minLoadSeconds = 3.5f;

        private IEnumerator BootRoutine()
        {
            // Màn hình khởi động mới: ảnh nghệ thuật + thanh tải động theo tiến độ tải scene thật
            Game.Billiards.UI.BootSplashUI splash = Game.Billiards.UI.BootSplashUI.Create();
            splash.Begin(targetInitialScene, Mathf.Max(minLoadSeconds, splashDelaySeconds));
            yield break;
        }
    }
}
