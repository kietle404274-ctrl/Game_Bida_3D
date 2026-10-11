using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Billiards.Core
{
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float splashDelaySeconds = 0.5f;
        [SerializeField] private string targetInitialScene = "Lobby";

        private void Start()
        {
            // Đảm bảo GameSession đã tồn tại
            GameSession.GetOrCreateInstance();

            // Khởi động luồng scene vào Lobby
            StartCoroutine(BootRoutine());
        }

        private IEnumerator BootRoutine()
        {
            if (splashDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(splashDelaySeconds);
            }

            SceneManager.LoadScene(targetInitialScene);
        }
    }
}
