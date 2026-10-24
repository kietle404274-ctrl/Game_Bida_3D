using UnityEditor;
using UnityEngine;

namespace Game.Billiards.Editor
{
    public static class BuildAllGameUI
    {
        [MenuItem("Tools/Game/Build ALL Game UI (Boot + Lobby + Gameplay)", false, 1)]
        public static void BuildAll()
        {
            Debug.Log("=== BẮT ĐẦU XÂY DỰNG CANVAS UI CHO BOOT SPLASH ===");
            BootUIBuilder.Build();

            Debug.Log("=== BẮT ĐẦU XÂY DỰNG CANVAS UI CHO LOBBY ===");
            LobbyUIBuilder.Build();

            Debug.Log("=== BẮT ĐẦU XÂY DỰNG CANVAS UI CHO GAMEPLAY ===");
            GameplayUIBuilder.Build();

            Debug.Log("★★★ TẤT CẢ CANVAS UI (BOOT, LOBBY & GAMEPLAY) ĐÃ ĐƯỢC XÂY DỰNG VÀ HOÀN TẤT KẾT NỐI! ★★★");
        }
    }
}
