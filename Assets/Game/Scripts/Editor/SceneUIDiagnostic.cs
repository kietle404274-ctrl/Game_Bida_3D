using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Billiards.Editor
{
    public static class SceneUIDiagnostic
    {
        [MenuItem("Tools/Game/Diagnose and Fix UI Scenes", false, 3)]
        public static void DiagnoseAndFix()
        {
            Debug.Log("================ DIAGNOSING LOBBY SCENE ================");
            var lobbyScene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Lobby.unity", OpenSceneMode.Single);
            
            // Check LobbyCanvas
            GameObject lobbyCanvas = GameObject.Find("LobbyCanvas");
            if (lobbyCanvas != null)
            {
                Debug.Log($"LobbyCanvas found: activeSelf={lobbyCanvas.activeSelf}, layer={LayerMask.LayerToName(lobbyCanvas.layer)}");
                Canvas c = lobbyCanvas.GetComponent<Canvas>();
                Debug.Log($"Canvas renderMode={c.renderMode}, enabled={c.enabled}");

                // Fix layer to UI (5) recursively
                int uiLayer = LayerMask.NameToLayer("UI");
                SetLayerRecursively(lobbyCanvas, uiLayer);

                // Ensure MainMenuCard is active and centered/visible
                Transform mmc = lobbyCanvas.transform.Find("MainMenuCard");
                if (mmc != null)
                {
                    mmc.gameObject.SetActive(true);
                    Debug.Log($"MainMenuCard found: activeSelf={mmc.gameObject.activeSelf}");
                }
                else
                {
                    Debug.LogWarning("MainMenuCard not found under LobbyCanvas!");
                }

                EditorSceneManager.MarkSceneDirty(lobbyScene);
                EditorSceneManager.SaveScene(lobbyScene);
            }
            else
            {
                Debug.LogError("LobbyCanvas not found in Lobby.unity!");
            }

            Debug.Log("================ DIAGNOSING GAMEPLAY SCENE ================");
            var gameplayScene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            GameObject gameplayCanvas = GameObject.Find("GameplayCanvas");
            if (gameplayCanvas != null)
            {
                int uiLayer = LayerMask.NameToLayer("UI");
                SetLayerRecursively(gameplayCanvas, uiLayer);

                EditorSceneManager.MarkSceneDirty(gameplayScene);
                EditorSceneManager.SaveScene(gameplayScene);
            }
            else
            {
                Debug.LogError("GameplayCanvas not found in Gameplay.unity!");
            }

            Debug.Log(">>> DIAGNOSTIC COMPLETE!");
        }

        private static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }
    }
}
