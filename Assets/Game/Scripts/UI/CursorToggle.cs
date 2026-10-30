using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Quản lý con trỏ chuột toàn game:
    /// - Ở Sảnh (Lobby): Mặc định và luôn luôn HIỆN con trỏ (CursorLockMode.None), phím Alt bị vô hiệu hóa hoàn toàn.
    /// - Vào Trận (Gameplay): Mặc định ẨN và KHÓA con trỏ (CursorLockMode.Locked) để xoay camera ngắm bi mượt mà.
    ///   + Chỉ khi ở trong trận mới cho phép nhấn phím Alt (LeftAlt / RightAlt) để bật/tắt con trỏ thao tác UI.
    ///   + Khi có popup modal mở (Pause, Luật, Hướng dẫn, Chọn điểm cơ, Kết quả...), con trỏ tự động hiện.
    /// </summary>
    public class CursorToggle : MonoBehaviour
    {
        private static CursorToggle instance;

        private bool userShowCursor = false;
        private bool inMatch = false;

        public static bool IsCursorToggledByPlayer => instance != null && instance.inMatch && instance.userShowCursor;

        public static void SetCursorToggled(bool value)
        {
            if (instance != null)
            {
                instance.userShowCursor = value;
                instance.Apply();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("CursorToggle");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CursorToggle>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        }

        private void Start()
        {
            RefreshMatchStatus();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshMatchStatus();
        }

        private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            RefreshMatchStatus();
        }

        public void RefreshMatchStatus()
        {
            bool nowInMatch = DetermineIfInMatch();
            if (nowInMatch != inMatch)
            {
                inMatch = nowInMatch;
                userShowCursor = false; // Khi đổi cảnh (đặc biệt khi vào trận), luôn mặc định là ẩn con trỏ
            }
            Apply();
        }

        private static bool DetermineIfInMatch()
        {
            var activeScene = SceneManager.GetActiveScene();
            string sceneName = activeScene.name;

            // Nếu scene là Lobby hoặc Boot hoặc có LobbyUIController trong scene -> Chắc chắn là ở Sảnh
            if (sceneName == "Lobby" || sceneName == "Boot" || Object.FindObjectOfType<LobbyUIController>(true) != null)
            {
                return false;
            }

            // Nếu scene là Gameplay -> Chắc chắn là trong trận đấu
            if (sceneName == "Gameplay")
            {
                return true;
            }

            // Phòng hờ các scene test khác: nếu có GameplayInitializer và không có LobbyUIController
            return Object.FindObjectOfType<Core.GameplayInitializer>(true) != null && Object.FindObjectOfType<LobbyUIController>(true) == null;
        }

        private void Update()
        {
            // Kiểm tra trạng thái scene liên tục phòng khi load scene async
            bool currentMatchState = DetermineIfInMatch();
            if (currentMatchState != inMatch)
            {
                inMatch = currentMatchState;
                userShowCursor = false; // Mặc định vào trận là ẩn con trỏ
                Apply();
            }

            // Ở Sảnh: Luôn giữ con trỏ hiện, phím Alt hoàn toàn không có tác dụng
            if (!inMatch)
            {
                if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
                {
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                return;
            }

            // CHỈ TRONG TRẬN ĐẤU: Cho phép nhấn Alt (LeftAlt hoặc RightAlt) để bật/tắt con trỏ
            if (Application.isFocused &&
                (UnityEngine.Input.GetKeyDown(KeyCode.LeftAlt) || UnityEngine.Input.GetKeyDown(KeyCode.RightAlt)))
            {
                userShowCursor = !userShowCursor;
            }

            Apply();
        }

        private void Apply()
        {
            if (!inMatch)
            {
                // Ngoài trận (Sảnh): con trỏ luôn luôn hiện, tự do click
                if (!Cursor.visible) Cursor.visible = true;
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                return;
            }

            // Trong trận:
            // show = true nếu người chơi bấm Alt để hiện HOẶC có modal popup mở
            bool show = userShowCursor || HudModalState.IsModalOpen;

            if (Cursor.visible != show) Cursor.visible = show;

            // Hiện: thả tự do để click nút/thanh HUD; Ẩn: khóa tâm để xoay camera mượt mà
            CursorLockMode want = show ? CursorLockMode.None : CursorLockMode.Locked;
            if (Cursor.lockState != want) Cursor.lockState = want;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus)
            {
                Apply();
            }
            else
            {
                // Mất focus ứng dụng (Alt-Tab ra ngoài): nhả chuột tự do
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }
}
