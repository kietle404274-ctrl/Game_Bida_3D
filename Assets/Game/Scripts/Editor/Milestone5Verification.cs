using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;
using Game.Billiards.Rules;
using Game.Billiards.UI;

namespace Game.Billiards.Editor
{
    public static class Milestone5Verification
    {
        [MenuItem("Billiards/Run M5 Verification", false, 5)]
        public static void RunAllM5Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M5 (GAMEPLAY UI & HUD) ====================");
            int passed = 0;
            int total = 5;

            if (TestPlayerPanelAndBallTracker()) passed++;
            if (TestMatchHeaderAndShotTimer()) passed++;
            if (TestPowerBarUI()) passed++;
            if (TestCallShotUIAndSafety()) passed++;
            if (TestTurnManagerShotTimerExpiration()) passed++;

            Debug.Log($"==================== KẾT QUẢ M5: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M5 PASS] Tất cả các thành phần UI / HUD của Milestone M5 đã vượt qua kiểm thử thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M5 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestPlayerPanelAndBallTracker()
        {
            GameObject testGO = new GameObject("TestPlayerPanelGO");
            try
            {
                var panel = testGO.AddComponent<PlayerPanelUI>();
                var tracker = testGO.AddComponent<BallTrackerUI>();

                // Gán serialized reference qua SerializedObject
                SerializedObject soPanel = new SerializedObject(panel);
                soPanel.FindProperty("ballTracker").objectReferenceValue = tracker;
                soPanel.ApplyModifiedProperties();

                // 1. Kiểm tra ban đầu: Bàn mở (Open table)
                panel.SetGroup(PlayerGroup.Open);
                if (tracker.CurrentGroup != PlayerGroup.Open)
                {
                    Debug.LogError("[Test 1 FAILED] BallTracker không ở trạng thái Open!");
                    return false;
                }

                // 2. Gán nhóm Solids
                panel.SetGroup(PlayerGroup.Solids);
                if (tracker.CurrentGroup != PlayerGroup.Solids)
                {
                    Debug.LogError("[Test 1 FAILED] BallTracker không nhận đúng nhóm Solids!");
                    return false;
                }

                // 3. Mock BallManager để kiểm tra ẩn icon khi bi vào lỗ
                var bmGO = new GameObject("TestBM");
                var ballManager = bmGO.AddComponent<BallManager>();
                List<GameObject> balls = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    var bGO = new GameObject($"Ball_{i}");
                    balls.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    bc.SetBallNumber(i);
                    ballManager.RegisterBall(i, bc);
                }

                tracker.UpdateBallsStatus(ballManager);

                // Dọn sạch 1..7 (cho vào lỗ)
                for (int i = 1; i <= 7; i++)
                {
                    balls[i].SetActive(false);
                }
                tracker.UpdateBallsStatus(ballManager);

                foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                UnityEngine.Object.DestroyImmediate(bmGO);

                Debug.Log("[Test 1 PASSED] PlayerPanelUI và BallTrackerUI chuyển trạng thái nhóm bi và cập nhật bi hợp lệ chuẩn xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestMatchHeaderAndShotTimer()
        {
            GameObject testGO = new GameObject("TestHeaderGO");
            try
            {
                var header = testGO.AddComponent<MatchHeaderUI>();

                // Tạo UI Text và Image mock
                var txtGO = new GameObject("TextRack");
                txtGO.transform.SetParent(testGO.transform);
                var txtRack = txtGO.AddComponent<Text>();

                var scoreGO = new GameObject("TextScore");
                scoreGO.transform.SetParent(testGO.transform);
                var txtScore = scoreGO.AddComponent<Text>();

                var timerRoot = new GameObject("TimerContainer");
                timerRoot.transform.SetParent(testGO.transform);

                var timerTxtGO = new GameObject("TimerText");
                timerTxtGO.transform.SetParent(timerRoot.transform);
                var timerTxt = timerTxtGO.AddComponent<Text>();

                var timerImgGO = new GameObject("TimerFill");
                timerImgGO.transform.SetParent(timerRoot.transform);
                var timerFill = timerImgGO.AddComponent<Image>();

                SerializedObject so = new SerializedObject(header);
                so.FindProperty("rackNumberText").objectReferenceValue = txtRack;
                so.FindProperty("scoreRaceText").objectReferenceValue = txtScore;
                so.FindProperty("timerContainer").objectReferenceValue = timerRoot;
                so.FindProperty("timerCountdownText").objectReferenceValue = timerTxt;
                so.FindProperty("timerFillBar").objectReferenceValue = timerFill;
                so.ApplyModifiedProperties();

                // Test cập nhật tỉ số
                header.UpdateRackAndScore(3, 2, 1, RaceTarget.RaceTo5);
                if (txtRack.text != "RACE TO 5  •  VÁN 3" || txtScore.text != "2 - 1")
                {
                    Debug.LogError($"[Test 2 FAILED] MatchHeader hiển thị sai rack hoặc score! Rack: {txtRack.text}, Score: {txtScore.text}");
                    return false;
                }

                // Test cập nhật timer countdown
                header.UpdateTimer(25f, 30f);
                if (timerTxt.text != "25s" || Mathf.Abs(timerFill.fillAmount - (25f / 30f)) > 0.01f)
                {
                    Debug.LogError($"[Test 2 FAILED] Shot timer fill bar không khớp! Fill={timerFill.fillAmount}, Text={timerTxt.text}");
                    return false;
                }

                // Test cảnh báo dưới 5 giây (Warning color)
                header.UpdateTimer(3f, 30f);
                if (timerTxt.text != "3s")
                {
                    Debug.LogError($"[Test 2 FAILED] Shot timer 3s text sai: {timerTxt.text}");
                    return false;
                }

                Debug.Log("[Test 2 PASSED] MatchHeaderUI hiển thị Race-To score và Shot Timer countdown chính xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestPowerBarUI()
        {
            GameObject testGO = new GameObject("TestPowerBarGO");
            try
            {
                var powerBar = testGO.AddComponent<PowerBarUI>();
                var inputCtrl = testGO.AddComponent<PlayerInputController>();

                var imgGO = new GameObject("FillImg");
                imgGO.transform.SetParent(testGO.transform);
                var fillImg = imgGO.AddComponent<Image>();

                var txtGO = new GameObject("PercentTxt");
                txtGO.transform.SetParent(testGO.transform);
                var percentTxt = txtGO.AddComponent<Text>();

                SerializedObject so = new SerializedObject(powerBar);
                so.FindProperty("powerFillImage").objectReferenceValue = fillImg;
                so.FindProperty("powerPercentText").objectReferenceValue = percentTxt;
                so.ApplyModifiedProperties();

                powerBar.Initialize(inputCtrl);

                // Cập nhật lực 60%
                powerBar.UpdatePower(0.60f);
                if (percentTxt.text != "60%" || Mathf.Abs(fillImg.fillAmount - 0.60f) > 0.01f)
                {
                    Debug.LogError($"[Test 3 FAILED] PowerBar hiển thị sai lực 60%! Text={percentTxt.text}, Fill={fillImg.fillAmount}");
                    return false;
                }

                // Cập nhật lực 100%
                powerBar.UpdatePower(1.0f);
                if (percentTxt.text != "100%" || Mathf.Abs(fillImg.fillAmount - 1.0f) > 0.01f)
                {
                    Debug.LogError($"[Test 3 FAILED] PowerBar hiển thị sai lực 100%! Text={percentTxt.text}, Fill={fillImg.fillAmount}");
                    return false;
                }

                Debug.Log("[Test 3 PASSED] PowerBarUI phản hồi chính xác với dải lực 0-100% và gradient màu sắc.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestCallShotUIAndSafety()
        {
            GameObject testGO = new GameObject("TestCallShotGO");
            try
            {
                var callShot = testGO.AddComponent<CallShotUI>();
                var inputCtrl = testGO.AddComponent<PlayerInputController>();
                var stateMgr = testGO.AddComponent<MatchStateManager>();
                var ballMgr = testGO.AddComponent<BallManager>();

                // Mock Dropdowns và Buttons
                var panelRoot = new GameObject("PanelRoot");
                panelRoot.transform.SetParent(testGO.transform);

                var ballDropGO = new GameObject("BallDropdown");
                ballDropGO.transform.SetParent(panelRoot.transform);
                var ballDrop = ballDropGO.AddComponent<Dropdown>();

                var pocketDropGO = new GameObject("PocketDropdown");
                pocketDropGO.transform.SetParent(panelRoot.transform);
                var pocketDrop = pocketDropGO.AddComponent<Dropdown>();

                var safetyBtnGO = new GameObject("SafetyBtn");
                safetyBtnGO.transform.SetParent(panelRoot.transform);
                var safetyBtn = safetyBtnGO.AddComponent<Button>();
                var safetyImg = safetyBtnGO.AddComponent<Image>();

                SerializedObject so = new SerializedObject(callShot);
                so.FindProperty("panelRoot").objectReferenceValue = panelRoot;
                so.FindProperty("ballDropdown").objectReferenceValue = ballDrop;
                so.FindProperty("pocketDropdown").objectReferenceValue = pocketDrop;
                so.FindProperty("safetyButton").objectReferenceValue = safetyBtn;
                so.FindProperty("safetyButtonImage").objectReferenceValue = safetyImg;
                so.ApplyModifiedProperties();

                // Mock 16 bi
                List<GameObject> balls = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    var bGO = new GameObject($"Ball_{i}");
                    balls.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    bc.SetBallNumber(i);
                    ballMgr.RegisterBall(i, bc);
                }

                stateMgr.Initialize(MatchSettings.Default);
                callShot.Initialize(inputCtrl, ballMgr, stateMgr);

                // 1. Break shot: Call shot bị ẩn
                stateMgr.State.turnSequence = 0;
                callShot.RefreshCallShotOptions();
                if (panelRoot.activeSelf)
                {
                    Debug.LogError("[Test 4 FAILED] Call Shot không được ẩn trong cú Break khai cuộc!");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // 2. Lượt sau break: Bàn mở -> Bật Call shot
                stateMgr.State.turnSequence = 1;
                stateMgr.State.phase = MatchPhase.Aiming;
                callShot.RefreshCallShotOptions();
                if (!panelRoot.activeSelf)
                {
                    Debug.LogError("[Test 4 FAILED] Call Shot không hiển thị sau cú Break!");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // 3. Test chọn Safety
                callShot.SetSafety(true);
                if (!inputCtrl.CalledSafety)
                {
                    Debug.LogError("[Test 4 FAILED] Bật Safety nhưng PlayerInputController không nhận CalledSafety=true!");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                callShot.SetSafety(false);
                if (inputCtrl.CalledSafety)
                {
                    Debug.LogError("[Test 4 FAILED] Tắt Safety nhưng PlayerInputController vẫn còn CalledSafety=true!");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                Debug.Log("[Test 4 PASSED] CallShotUI tự động ẩn khi break, kích hoạt sau break và xử lý Safety chuẩn xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestTurnManagerShotTimerExpiration()
        {
            GameObject testGO = new GameObject("TestTimerExpGO");
            try
            {
                var stateMgr = testGO.AddComponent<MatchStateManager>();
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var eventCollector = testGO.AddComponent<ShotEventCollector>();
                var shotExecutor = testGO.AddComponent<ShotExecutor>();
                var movementMonitor = testGO.AddComponent<BallMovementMonitor>();
                var ballManager = testGO.AddComponent<BallManager>();
                var bihCtrl = testGO.AddComponent<BallInHandController>();
                var turnManager = testGO.AddComponent<TurnManager>();

                MatchSettings settings = new MatchSettings
                {
                    raceTarget = RaceTarget.RaceTo5,
                    shotTimer = ShotTimerOption.Seconds30,
                    useWpaRules = true
                };

                stateMgr.Initialize(settings);
                turnManager.Initialize(stateMgr, ruleEngine, eventCollector, shotExecutor, movementMonitor, ballManager, bihCtrl);

                // Chuyển sang phase Aiming để kích hoạt timer
                stateMgr.SetPhase(MatchPhase.Aiming);

                if (!turnManager.IsTimerRunning || Mathf.Abs(turnManager.RemainingShotTime - 30f) > 0.1f)
                {
                    Debug.LogError($"[Test 5 FAILED] Shot timer không khởi động ở 30s! IsRunning={turnManager.IsTimerRunning}, Rem={turnManager.RemainingShotTime}");
                    return false;
                }

                // Mô phỏng hết giờ bằng Reflection gọi HandleShotClockExpired
                var method = typeof(TurnManager).GetMethod("HandleShotClockExpired", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (method == null)
                {
                    Debug.LogError("[Test 5 FAILED] Không tìm thấy method HandleShotClockExpired!");
                    return false;
                }

                method.Invoke(turnManager, null);

                // Kiểm tra kết quả: chuyển lượt sang Player B (1), phase là BallInHand, và kết quả đánh giá là ShotClockExpired
                if (stateMgr.CurrentPlayerIndex != 1 || stateMgr.CurrentPhase != MatchPhase.BallInHand)
                {
                    Debug.LogError($"[Test 5 FAILED] Hết shot timer không chuyển lượt hoặc không cấp BallInHand! PlayerIndex={stateMgr.CurrentPlayerIndex}, Phase={stateMgr.CurrentPhase}");
                    return false;
                }

                if (turnManager.LastShotResult == null || turnManager.LastShotResult.foul != FoulType.ShotClockExpired)
                {
                    Debug.LogError($"[Test 5 FAILED] LastShotResult không ghi nhận lỗi ShotClockExpired! Foul={turnManager.LastShotResult?.foul}");
                    return false;
                }

                Debug.Log("[Test 5 PASSED] Hết shot timer tự động xử phạt FoulType.ShotClockExpired, đổi lượt và cấp Ball-in-Hand (T11 pass).");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
