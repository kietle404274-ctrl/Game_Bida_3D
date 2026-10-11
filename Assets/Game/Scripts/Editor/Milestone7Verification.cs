using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Game.Billiards.AI;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.UI;

namespace Game.Billiards.Editor
{
    public static class Milestone7Verification
    {
        [MenuItem("Billiards/Run M7 Verification", false, 7)]
        public static void RunAllM7Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M7 (LOBBY 3D & OFFLINE GAME FLOW) ====================");
            int passed = 0;
            int total = 4;

            if (TestGameSessionConfiguration()) passed++;
            if (TestLobbyUIDifficultySelection()) passed++;
            if (TestLobbyUIModalNavigation()) passed++;
            if (TestSceneTransitionWiring()) passed++;

            Debug.Log($"==================== KẾT QUẢ M7: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M7 PASS] Tất cả các thành phần Lobby 3D và luồng Game Flow của Milestone M7 đã hoàn thành thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M7 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestGameSessionConfiguration()
        {
            GameObject testGO = new GameObject("TestSessionGO");
            try
            {
                var session = testGO.AddComponent<GameSession>();

                // Cấu hình match Offline vs AI
                session.StartOfflineMatch(AIDifficulty.Hard, RaceTarget.RaceTo7, ShotTimerOption.Seconds45, "Thợ Săn Bi");

                if (session.CurrentGameMode != GameMode.OfflineVsAI)
                {
                    Debug.LogError($"[Test 1 FAILED] GameMode không phải OfflineVsAI: {session.CurrentGameMode}");
                    return false;
                }

                if (session.SelectedAIDifficulty != AIDifficulty.Hard)
                {
                    Debug.LogError($"[Test 1 FAILED] AI Difficulty không nhận đúng Hard: {session.SelectedAIDifficulty}");
                    return false;
                }

                if (session.CurrentSettings.raceTarget != RaceTarget.RaceTo7 || session.CurrentSettings.shotTimer != ShotTimerOption.Seconds45)
                {
                    Debug.LogError("[Test 1 FAILED] MatchSettings không lưu đúng RaceTarget hoặc ShotTimer!");
                    return false;
                }

                if (!session.IsPlayerBAi || session.PlayerBName != "AI (Khó)")
                {
                    Debug.LogError($"[Test 1 FAILED] Cấu hình đối thủ AI không đúng: IsAi={session.IsPlayerBAi}, Name={session.PlayerBName}");
                    return false;
                }

                Debug.Log("[Test 1 PASSED] GameSession lưu trữ và đồng bộ chính xác cấu hình trận đấu Offline vs AI.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestLobbyUIDifficultySelection()
        {
            GameObject testGO = new GameObject("TestLobbyUIGO");
            try
            {
                var lobbyUI = testGO.AddComponent<LobbyUIController>();

                // Mock visual highlights
                var imgEasy = new GameObject("HighlightEasy").AddComponent<Image>();
                imgEasy.transform.SetParent(testGO.transform);
                var imgNormal = new GameObject("HighlightNormal").AddComponent<Image>();
                imgNormal.transform.SetParent(testGO.transform);
                var imgHard = new GameObject("HighlightHard").AddComponent<Image>();
                imgHard.transform.SetParent(testGO.transform);

                SerializedObject so = new SerializedObject(lobbyUI);
                so.FindProperty("diffEasyHighlight").objectReferenceValue = imgEasy;
                so.FindProperty("diffNormalHighlight").objectReferenceValue = imgNormal;
                so.FindProperty("diffHardHighlight").objectReferenceValue = imgHard;
                so.ApplyModifiedProperties();

                // Chọn Easy
                lobbyUI.SelectDifficulty(AIDifficulty.Easy);
                if (lobbyUI.SelectedDifficulty != AIDifficulty.Easy)
                {
                    Debug.LogError("[Test 2 FAILED] Không chọn được Easy!");
                    return false;
                }

                // Chọn Hard
                lobbyUI.SelectDifficulty(AIDifficulty.Hard);
                if (lobbyUI.SelectedDifficulty != AIDifficulty.Hard)
                {
                    Debug.LogError("[Test 2 FAILED] Không chọn được Hard!");
                    return false;
                }

                // Chọn Normal
                lobbyUI.SelectDifficulty(AIDifficulty.Normal);
                if (lobbyUI.SelectedDifficulty != AIDifficulty.Normal)
                {
                    Debug.LogError("[Test 2 FAILED] Không chọn được Normal!");
                    return false;
                }

                Debug.Log("[Test 2 PASSED] LobbyUIController chuyển đổi mượt mà giữa các cấp độ khó Easy / Normal / Hard.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestLobbyUIModalNavigation()
        {
            GameObject testGO = new GameObject("TestModalNavGO");
            try
            {
                var lobbyUI = testGO.AddComponent<LobbyUIController>();

                var diffModal = new GameObject("DiffModal");
                diffModal.transform.SetParent(testGO.transform);
                var onlineModal = new GameObject("OnlineModal");
                onlineModal.transform.SetParent(testGO.transform);
                var settingsModal = new GameObject("SettingsModal");
                settingsModal.transform.SetParent(testGO.transform);
                var rulesModal = new GameObject("RulesModal");
                rulesModal.transform.SetParent(testGO.transform);
                var quitModal = new GameObject("QuitModal");
                quitModal.transform.SetParent(testGO.transform);

                SerializedObject so = new SerializedObject(lobbyUI);
                so.FindProperty("difficultyModalRoot").objectReferenceValue = diffModal;
                so.FindProperty("onlineModalRoot").objectReferenceValue = onlineModal;
                so.FindProperty("settingsModalRoot").objectReferenceValue = settingsModal;
                so.FindProperty("rulesModalRoot").objectReferenceValue = rulesModal;
                so.FindProperty("quitConfirmModalRoot").objectReferenceValue = quitModal;
                so.ApplyModifiedProperties();

                // 1. Mở modal chọn độ khó
                lobbyUI.ShowDifficultyModal();
                if (!diffModal.activeSelf || onlineModal.activeSelf || settingsModal.activeSelf)
                {
                    Debug.LogError("[Test 3 FAILED] Mở DifficultyModal nhưng trạng thái các modal khác bị sai!");
                    return false;
                }

                // 2. Mở modal cài đặt
                lobbyUI.ShowSettingsModal();
                if (!settingsModal.activeSelf || diffModal.activeSelf)
                {
                    Debug.LogError("[Test 3 FAILED] Mở SettingsModal không đóng modal cũ!");
                    return false;
                }

                // 3. Đóng tất cả modal
                lobbyUI.HideAllModals();
                if (diffModal.activeSelf || onlineModal.activeSelf || settingsModal.activeSelf || rulesModal.activeSelf || quitModal.activeSelf)
                {
                    Debug.LogError("[Test 3 FAILED] HideAllModals không ẩn hết các hộp thoại!");
                    return false;
                }

                Debug.Log("[Test 3 PASSED] Hệ thống modal của LobbyUIController điều hướng độc lập và không bị chồng chéo.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestSceneTransitionWiring()
        {
            // Kiểm tra tính toàn vẹn của scene GameplayInitializer khi nhận GameSession
            GameObject testGO = new GameObject("TestWiringGO");
            try
            {
                var session = testGO.AddComponent<GameSession>();
                session.StartOfflineMatch(AIDifficulty.Easy, RaceTarget.RaceTo3, ShotTimerOption.Unlimited, "TestPlayer");

                var stateMgr = testGO.AddComponent<MatchStateManager>();
                var aiCtrl = testGO.AddComponent<AIController>();

                // Giả lập logic wiring trong GameplayInitializer
                stateMgr.Initialize(session.CurrentSettings, session.PlayerAName, session.PlayerBName, session.IsPlayerBAi);
                aiCtrl.SetAIEnabled(session.IsPlayerBAi, 1);
                aiCtrl.SetDifficulty(session.SelectedAIDifficulty);

                if (stateMgr.State.settings.raceTarget != RaceTarget.RaceTo3)
                {
                    Debug.LogError("[Test 4 FAILED] MatchStateManager không nhận đúng RaceTarget từ GameSession!");
                    return false;
                }

                if (aiCtrl.Difficulty != AIDifficulty.Easy)
                {
                    Debug.LogError("[Test 4 FAILED] AIController không nhận đúng AIDifficulty từ GameSession!");
                    return false;
                }

                Debug.Log("[Test 4 PASSED] Luồng truyền dữ liệu từ GameSession sang MatchStateManager và AIController hoạt động hoàn hảo.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
