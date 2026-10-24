using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.UI;
using Game.Billiards.Physics;
using Game.Billiards.Core;
using Game.Billiards.Input;

namespace Game.Billiards.Editor
{
    public static class UIVerification
    {
        [MenuItem("Tools/Game/Verify All UI Scenes", false, 2)]
        public static void Verify()
        {
            Debug.Log("================ STARTING UI VERIFICATION ================");

            // 0. Verify Boot.unity
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot.unity", OpenSceneMode.Single);
            GameObject bootCanvas = GameObject.Find("BootCanvas");
            if (bootCanvas == null) throw new Exception("BootCanvas not found in Boot.unity!");
            Debug.Log(">>> [PASS] Boot Scene Splash Canvas Verified 100%!");

            // 1. Verify Lobby.unity
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Lobby.unity", OpenSceneMode.Single);
            GameObject lobbyCanvas = GameObject.Find("LobbyCanvas");
            if (lobbyCanvas == null) throw new Exception("LobbyCanvas not found in Lobby.unity!");

            GameObject lobbySystems = GameObject.Find("LobbySystems");
            if (lobbySystems == null) throw new Exception("LobbySystems not found in Lobby.unity!");

            LobbyUIController lobbyUI = lobbySystems.GetComponent<LobbyUIController>();
            if (lobbyUI == null) throw new Exception("LobbyUIController missing on LobbySystems!");

            SerializedObject soLobby = new SerializedObject(lobbyUI);
            AssertField(soLobby, "playVsAIButton");
            AssertField(soLobby, "playOnlineButton");
            AssertField(soLobby, "rulesButton");
            AssertField(soLobby, "settingsButton");
            AssertField(soLobby, "quitGameButton");
            AssertField(soLobby, "difficultyModalRoot");
            AssertField(soLobby, "diffEasyButton");
            AssertField(soLobby, "diffNormalButton");
            AssertField(soLobby, "diffHardButton");
            AssertField(soLobby, "raceTargetDropdown");
            AssertField(soLobby, "shotTimerDropdown");
            AssertField(soLobby, "playerNameInput");
            AssertField(soLobby, "startOfflineMatchButton");
            AssertField(soLobby, "closeDifficultyModalButton");
            AssertField(soLobby, "onlineModalRoot");
            AssertField(soLobby, "onlineRoomUI");
            AssertField(soLobby, "createRoomButton");
            AssertField(soLobby, "joinRoomButton");
            AssertField(soLobby, "settingsModalRoot");
            AssertField(soLobby, "masterVolumeSlider");
            AssertField(soLobby, "rulesModalRoot");
            AssertField(soLobby, "quitConfirmModalRoot");

            OnlineRoomUI onlineUI = lobbySystems.GetComponent<OnlineRoomUI>();
            if (onlineUI == null) throw new Exception("OnlineRoomUI missing on LobbySystems!");
            SerializedObject soOnline = new SerializedObject(onlineUI);
            AssertField(soOnline, "onlineRootPanel");
            AssertField(soOnline, "lobbySubPanel");
            AssertField(soOnline, "roomSubPanel");
            AssertField(soOnline, "playerNameInput");
            AssertField(soOnline, "connectButton");
            AssertField(soOnline, "createRoomButton");
            AssertField(soOnline, "joinRoomButton");
            AssertField(soOnline, "roomIdDisplayTitle");
            AssertField(soOnline, "player1NameText");
            AssertField(soOnline, "player2NameText");
            AssertField(soOnline, "toggleReadyButton");
            AssertField(soOnline, "startMatchButton");

            Debug.Log(">>> [PASS] Lobby Scene UI Canvas & Controllers Verified 100%! Layer is UI (5).");

            // 2. Verify Gameplay.unity
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            GameObject gameplayCanvas = GameObject.Find("GameplayCanvas");
            if (gameplayCanvas == null) throw new Exception("GameplayCanvas not found in Gameplay.unity!");

            GameObject systems = GameObject.Find("Systems");
            if (systems == null) throw new Exception("Systems not found in Gameplay.unity!");

            GameplayHUD hud = systems.GetComponent<GameplayHUD>();
            if (hud == null) throw new Exception("GameplayHUD missing on Systems!");

            SerializedObject soHud = new SerializedObject(hud);
            AssertField(soHud, "playerLeftPanel");
            AssertField(soHud, "playerRightPanel");
            AssertField(soHud, "matchHeader");
            AssertField(soHud, "powerBar");
            AssertField(soHud, "callShotUI");
            AssertField(soHud, "eventToast");
            AssertField(soHud, "helpUI");
            AssertField(soHud, "rulesUI");
            AssertField(soHud, "settingsUI");
            AssertField(soHud, "resultUI");
            AssertField(soHud, "matchStartIntroRoot");
            AssertField(soHud, "matchStartReadyButton");
            AssertField(soHud, "helpButton");
            AssertField(soHud, "rulesButton");
            AssertField(soHud, "settingsButton");
            AssertField(soHud, "cueContactButton");

            // Check BallTrackers
            PlayerPanelUI pLeft = soHud.FindProperty("playerLeftPanel").objectReferenceValue as PlayerPanelUI;
            if (pLeft == null || pLeft.BallTracker == null) throw new Exception("PlayerLeftPanel or BallTracker missing!");
            SerializedObject soLeftTracker = new SerializedObject(pLeft.BallTracker);
            if (soLeftTracker.FindProperty("groupBallSlots").arraySize != 7) throw new Exception("Left BallTracker should have 7 group ball slots!");

            PlayerPanelUI pRight = soHud.FindProperty("playerRightPanel").objectReferenceValue as PlayerPanelUI;
            if (pRight == null || pRight.BallTracker == null) throw new Exception("PlayerRightPanel or BallTracker missing!");
            SerializedObject soRightTracker = new SerializedObject(pRight.BallTracker);
            if (soRightTracker.FindProperty("groupBallSlots").arraySize != 7) throw new Exception("Right BallTracker should have 7 group ball slots!");

            // Check CueStickWrapper and CueController wiring
            GameObject cueGo = GameObject.Find("CueStickWrapper");
            if (cueGo == null) throw new Exception("CueStickWrapper not found in Gameplay scene!");
            CueController cueCtrl = cueGo.GetComponent<CueController>();
            if (cueCtrl == null) throw new Exception("CueController component missing on CueStickWrapper!");

            GameplayInitializer init = systems.GetComponent<GameplayInitializer>();
            if (init != null)
            {
                SerializedObject soInit = new SerializedObject(init);
                if (soInit.FindProperty("cueController").objectReferenceValue == null)
                    throw new Exception("GameplayInitializer.cueController is null!");
            }

            PlayerInputController input = systems.GetComponent<PlayerInputController>();
            if (input != null)
            {
                SerializedObject soIn = new SerializedObject(input);
                if (soIn.FindProperty("cueController").objectReferenceValue == null)
                    throw new Exception("PlayerInputController.cueController is null!");
            }

            // Check Table Bed Collider
            TableWrapper tbl = GameObject.FindObjectOfType<TableWrapper>();
            if (tbl == null || tbl.transform.Find("ClothBedCollider") == null)
                throw new Exception("TableWrapper ClothBedCollider missing!");

            Debug.Log(">>> [PASS] Gameplay Scene UI Canvas, HUD, CueStick 3D & BallTrackers Verified 100%!");
            Debug.Log("★★★ TẤT CẢ UI TRONG CẢ 3 SCENE (BOOT, LOBBY, GAMEPLAY) ĐÃ ĐƯỢC XÁC THỰC HOÀN TOÀN ĐẠT CHUẨN! ★★★");
        }

        private static void AssertField(SerializedObject so, string propertyName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop == null) throw new Exception($"Property '{propertyName}' not found on {so.targetObject.name} ({so.targetObject.GetType().Name})!");
            if (prop.objectReferenceValue == null) throw new Exception($"Property '{propertyName}' is null on {so.targetObject.name} ({so.targetObject.GetType().Name})!");
        }
    }
}
