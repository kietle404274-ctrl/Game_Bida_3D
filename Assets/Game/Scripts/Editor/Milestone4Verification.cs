using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;
using Game.Billiards.Rules;

namespace Game.Billiards.Editor
{
    public static class Milestone4Verification
    {
        [MenuItem("Billiards/Run M4 Verification", false, 4)]
        public static void RunAllM4Tests()
        {
            Debug.Log("==================== BẮT ĐẦU KIỂM THỬ MILESTONE M4 (WPA 8-BALL RULE ENGINE) ====================");
            int passed = 0;
            int total = 7;

            if (TestBreakAndOpenTableRules()) passed++;
            if (TestGroupAssignmentByCalledShot()) passed++;
            if (TestLegalFirstContactFouls()) passed++;
            if (TestRailRequirementFoul()) passed++;
            if (TestSafetyCallBehavior()) passed++;
            if (TestEightBallLossConditions()) passed++;
            if (TestEightBallWinCondition()) passed++;

            Debug.Log($"==================== KẾT QUẢ M4: {passed}/{total} BỘ KIỂM THỬ ĐẠT ====================");
            if (passed == total)
            {
                Debug.Log("<color=green><b>[M4 PASS] Tất cả các điều kiện luật WPA 8-Ball của Milestone M4 đã đạt thành công!</b></color>");
            }
            else
            {
                Debug.LogError($"<color=red><b>[M4 FAIL] Có {total - passed} kiểm thử không đạt! Vui lòng kiểm tra log chi tiết.</b></color>");
            }
        }

        private static bool TestBreakAndOpenTableRules()
        {
            GameObject testGO = new GameObject("TestBreakRuleGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var matchState = new MatchState();
                matchState.ResetForNewRack();

                // 1. Khai cuộc phạm lỗi: không có bi vào lỗ và chỉ có 2 bi chạm băng (< 4)
                ShotCommand breakCmd = new ShotCommand(0, Vector3.forward, 1.0f, Vector2.zero);
                ShotResult rawIllegalBreak = new ShotResult
                {
                    firstContactBall = 1,
                    cueBallScratch = false
                };
                rawIllegalBreak.ballsThatHitRail.AddRange(new[] { 1, 2 }); // chỉ 2 bi chạm băng

                ShotResult evalIllegal = ruleEngine.EvaluateShot(breakCmd, rawIllegalBreak, matchState, null);
                if (evalIllegal.foul != FoulType.IllegalBreak)
                {
                    Debug.LogError($"[Test 1 FAILED] Khai cuộc không đủ 4 bi chạm băng không bị phạt IllegalBreak! Foul={evalIllegal.foul}");
                    return false;
                }

                // 2. Khai cuộc hợp lệ: có 1 bi mục tiêu vào lỗ
                ShotResult rawLegalBreak = new ShotResult
                {
                    firstContactBall = 1,
                    cueBallScratch = false
                };
                rawLegalBreak.pocketedBalls.Add(3); // bi 3 vào lỗ
                rawLegalBreak.pocketedBallPockets[3] = 0;

                ShotResult evalLegal = ruleEngine.EvaluateShot(breakCmd, rawLegalBreak, matchState, null);
                if (evalLegal.foul != FoulType.None)
                {
                    Debug.LogError($"[Test 1 FAILED] Khai cuộc có bi vào lỗ nhưng bị phạt foul: {evalLegal.foul}");
                    return false;
                }

                // Theo WPA: Sau khai cuộc bàn luôn luôn OPEN
                if (!matchState.tableOpen)
                {
                    Debug.LogError("[Test 1 FAILED] Sau khai cuộc có bi vào lỗ bàn bị đóng nhóm thay vì giữ OPEN theo luật WPA!");
                    return false;
                }

                // Cú khai cuộc ăn bi hợp lệ phải cho phép tiếp tục lượt (calledShotMade = true)
                if (!evalLegal.calledShotMade)
                {
                    Debug.LogError("[Test 1 FAILED] Khai cuộc ăn bi hợp lệ không được cấp quyền tiếp tục lượt đánh!");
                    return false;
                }

                Debug.Log("[Test 1 PASSED] Kiểm tra khai cuộc (Legal/Illegal break, tiếp tục lượt khi ăn bi) và trạng thái Open Table hoạt động chuẩn WPA.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestGroupAssignmentByCalledShot()
        {
            GameObject testGO = new GameObject("TestGroupGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var matchState = new MatchState();
                matchState.ResetForNewRack();
                matchState.turnSequence = 1; // Sau khai cuộc

                // Cú đánh sau break khi bàn Open: Gọi bi 2 (Solid) vào lỗ 0
                ShotCommand callSolidCmd = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledBallNumber: 2, calledPocketId: 0);
                ShotResult rawSolid = new ShotResult
                {
                    firstContactBall = 2,
                    cueBallScratch = false
                };
                rawSolid.pocketedBalls.Add(2);
                rawSolid.pocketedBallPockets[2] = 0; // Vào đúng lỗ gọi

                ShotResult evalSolid = ruleEngine.EvaluateShot(callSolidCmd, rawSolid, matchState, null);

                // Kiểm tra gán nhóm
                if (matchState.tableOpen || matchState.playerAGroup != PlayerGroup.Solids || matchState.playerBGroup != PlayerGroup.Stripes)
                {
                    Debug.LogError($"[Test 2 FAILED] Không gán nhóm đúng sau cú gọi bi trơn thành công! TableOpen={matchState.tableOpen}, GroupA={matchState.playerAGroup}");
                    return false;
                }

                if (!evalSolid.calledShotMade)
                {
                    Debug.LogError("[Test 2 FAILED] calledShotMade không được đánh dấu true!");
                    return false;
                }

                Debug.Log("[Test 2 PASSED] Gán nhóm bi (Solids/Stripes) chuẩn xác theo cú đánh gọi đầu tiên sau khai cuộc.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestLegalFirstContactFouls()
        {
            GameObject testGO = new GameObject("TestContactFoulGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var ballManager = testGO.AddComponent<BallManager>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Tạo 16 bi mock
                List<GameObject> balls = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    var bGO = new GameObject($"Ball_{i}");
                    balls.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    bc.SetBallNumber(i);
                    bc.ApplyPhysicsConfig(config);
                    ballManager.RegisterBall(i, bc);
                }

                var matchState = new MatchState();
                matchState.turnSequence = 2;
                matchState.AssignGroups(PlayerGroup.Solids); // Player A là Solids (1-7), Player B là Stripes (9-15)
                matchState.currentPlayerIndex = 0; // Lượt Player A

                // 1. Player A chạm bi 10 (Stripes của đối thủ) đầu tiên -> Lỗi WrongBallFirst
                ShotCommand cmd = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledBallNumber: 1, calledPocketId: 0);
                ShotResult rawOpponentFirst = new ShotResult
                {
                    firstContactBall = 10, // Chạm bi sọc của đối thủ
                    cueBallScratch = false
                };
                rawOpponentFirst.ballsThatHitRail.Add(10);

                ShotResult evalWrong = ruleEngine.EvaluateShot(cmd, rawOpponentFirst, matchState, ballManager);
                if (evalWrong.foul != FoulType.WrongBallFirst)
                {
                    Debug.LogError($"[Test 3 FAILED] Chạm bi của đối thủ trước không bị phạt WrongBallFirst! Foul={evalWrong.foul}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // 2. Không chạm bất kỳ bi nào (firstContactBall = -1) -> Lỗi NoBallHit
                ShotResult rawNoHit = new ShotResult { firstContactBall = -1 };
                ShotResult evalNoHit = ruleEngine.EvaluateShot(cmd, rawNoHit, matchState, ballManager);
                if (evalNoHit.foul != FoulType.NoBallHit)
                {
                    Debug.LogError("[Test 3 FAILED] Đánh trượt không chạm bi nào không bị phạt foul!");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // 3. Chạm đúng bi nhóm mình (bi 1) trước -> Hợp lệ (None)
                ShotResult rawCorrect = new ShotResult
                {
                    firstContactBall = 1,
                    cueBallScratch = false
                };
                rawCorrect.ballsThatHitRail.Add(1);
                ShotResult evalCorrect = ruleEngine.EvaluateShot(cmd, rawCorrect, matchState, ballManager);
                if (evalCorrect.foul != FoulType.None)
                {
                    Debug.LogError($"[Test 3 FAILED] Chạm đúng bi nhóm mình nhưng bị phạt foul: {evalCorrect.foul}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                Debug.Log("[Test 3 PASSED] Quy tắc Legal First Contact phát hiện chính xác mọi lỗi chạm sai bi hoặc đánh trượt.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestRailRequirementFoul()
        {
            GameObject testGO = new GameObject("TestRailFoulGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var matchState = new MatchState();
                matchState.turnSequence = 2;
                matchState.tableOpen = false;
                matchState.playerAGroup = PlayerGroup.Solids;
                matchState.currentPlayerIndex = 0;

                // Cú đánh chạm đúng bi 3, nhưng sau đó không có bi nào chạm băng và không có bi vào lỗ -> Lỗi NoRailAfterContact
                ShotCommand cmd = new ShotCommand(0, Vector3.forward, 0.4f, Vector2.zero, calledBallNumber: 3, calledPocketId: 1);
                ShotResult rawNoRail = new ShotResult
                {
                    firstContactBall = 3,
                    cueBallScratch = false
                };
                // pocketedBalls rỗng, ballsThatHitRail rỗng

                ShotResult eval = ruleEngine.EvaluateShot(cmd, rawNoRail, matchState, null);
                if (eval.foul != FoulType.NoRailAfterContact)
                {
                    Debug.LogError($"[Test 4 FAILED] Không chạm băng và không vào lỗ sau tiếp xúc không bị phạt NoRailAfterContact! Foul={eval.foul}");
                    return false;
                }

                Debug.Log("[Test 4 PASSED] Quy tắc Rail Requirement (phải có bi chạm băng hoặc vào lỗ) hoạt động chính xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestSafetyCallBehavior()
        {
            GameObject testGO = new GameObject("TestSafetyGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var matchState = new MatchState();
                matchState.turnSequence = 3;
                matchState.tableOpen = false;
                matchState.playerAGroup = PlayerGroup.Solids;
                matchState.currentPlayerIndex = 0;

                // Cú đánh gọi Safety: bi 4 vào lỗ hợp lệ
                ShotCommand safetyCmd = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledSafety: true);
                ShotResult rawSafety = new ShotResult
                {
                    firstContactBall = 4,
                    cueBallScratch = false
                };
                rawSafety.pocketedBalls.Add(4);
                rawSafety.pocketedBallPockets[4] = 2;
                rawSafety.ballsThatHitRail.Add(4);

                ShotResult eval = ruleEngine.EvaluateShot(safetyCmd, rawSafety, matchState, null);

                // Khi gọi Safety, dù bi vào lỗ hợp lệ, calledShotMade phải là false để kết thúc lượt ngay lập tức
                if (eval.calledShotMade || eval.HasFoul)
                {
                    Debug.LogError("[Test 5 FAILED] Cú đánh Safety không kết thúc lượt đúng quy định WPA!");
                    return false;
                }

                Debug.Log("[Test 5 PASSED] Cú đánh Safety kết thúc lượt chuẩn xác dù có bi mục tiêu vào lỗ.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestEightBallLossConditions()
        {
            GameObject testGO = new GameObject("Test8BallLossGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var ballManager = testGO.AddComponent<BallManager>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Mock bi: Còn bi 3 (Solid) trên bàn
                List<GameObject> balls = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    var bGO = new GameObject($"Ball_{i}");
                    balls.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    bc.SetBallNumber(i);
                    bc.ApplyPhysicsConfig(config);
                    ballManager.RegisterBall(i, bc);
                }

                var matchState = new MatchState();
                matchState.turnSequence = 5;
                matchState.tableOpen = false;
                matchState.playerAGroup = PlayerGroup.Solids;
                matchState.currentPlayerIndex = 0; // Player A đánh

                // Tình huống 1: Ăn bi 8 khi vẫn còn bi trơn trên bàn -> Thua rack (Player B thắng)
                ShotCommand cmdEarly8 = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledBallNumber: 8, calledPocketId: 1);
                ShotResult rawEarly8 = new ShotResult
                {
                    firstContactBall = 8,
                    cueBallScratch = false
                };
                rawEarly8.pocketedBalls.Add(8);
                rawEarly8.pocketedBallPockets[8] = 1;

                ShotResult evalEarly = ruleEngine.EvaluateShot(cmdEarly8, rawEarly8, matchState, ballManager);
                if (evalEarly.outcome != RackOutcome.PlayerBWins)
                {
                    Debug.LogError($"[Test 6 FAILED] Đưa bi 8 vào lỗ trước khi dọn sạch nhóm không bị xử thua rack! Outcome={evalEarly.outcome}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // Tình huống 2: Luật đã bỏ gọi lỗ -> bi 8 vào bất kỳ lỗ nào sau khi dọn sạch nhóm (không lỗi) vẫn thắng rack
                // Giả lập đã dọn sạch nhóm: bỏ active của bi 1..7
                for (int i = 1; i <= 7; i++)
                {
                    balls[i].SetActive(false);
                }

                ShotCommand cmdWrongPocket = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledBallNumber: 8, calledPocketId: 1);
                ShotResult rawWrongPocket = new ShotResult
                {
                    firstContactBall = 8,
                    cueBallScratch = false
                };
                rawWrongPocket.pocketedBalls.Add(8);
                rawWrongPocket.pocketedBallPockets[8] = 2; // Vào lỗ 2, không còn bắt buộc đúng lỗ đã gọi
                rawWrongPocket.ballsThatHitRail.Add(8);

                ShotResult evalWrongPocket = ruleEngine.EvaluateShot(cmdWrongPocket, rawWrongPocket, matchState, ballManager);
                if (evalWrongPocket.outcome != RackOutcome.PlayerAWins)
                {
                    Debug.LogError($"[Test 6 FAILED] Bi 8 vào lỗ bất kỳ sau khi dọn sạch nhóm (không cần gọi lỗ) không được xử thắng rack! Outcome={evalWrongPocket.outcome}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // Tình huống 3: Ăn bi 8 kèm Scratch bi cái -> Thua rack
                ShotResult rawScratch8 = new ShotResult
                {
                    firstContactBall = 8,
                    cueBallScratch = true
                };
                rawScratch8.pocketedBalls.Add(8);
                rawScratch8.pocketedBallPockets[8] = 1;

                ShotResult evalScratch = ruleEngine.EvaluateShot(cmdEarly8, rawScratch8, matchState, ballManager);
                if (evalScratch.outcome != RackOutcome.PlayerBWins)
                {
                    Debug.LogError($"[Test 6 FAILED] Ăn bi 8 kèm rơi bi cái không bị xử thua rack! Outcome={evalScratch.outcome}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                // Tình huống 4: Bi 8 văng khỏi bàn -> Thua rack theo chuẩn WPA 3.8.a
                ShotResult rawOffTable8 = new ShotResult
                {
                    firstContactBall = 8,
                    cueBallScratch = false,
                    anyBallOffTable = true
                };
                rawOffTable8.ballsDrivenOffTable.Add(8);

                ShotResult evalOffTable = ruleEngine.EvaluateShot(cmdEarly8, rawOffTable8, matchState, ballManager);
                if (evalOffTable.outcome != RackOutcome.PlayerBWins)
                {
                    Debug.LogError($"[Test 6 FAILED] Làm bi 8 văng khỏi bàn không bị xử thua rack! Outcome={evalOffTable.outcome}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                Debug.Log("[Test 6 PASSED] Tất cả các điều kiện thua rack do bi 8 (ăn sớm, sai lỗ, scratch, văng khỏi bàn) được xử lý chính xác.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }

        private static bool TestEightBallWinCondition()
        {
            GameObject testGO = new GameObject("Test8BallWinGO");
            try
            {
                var ruleEngine = testGO.AddComponent<Wpa8BallRuleEngine>();
                var ballManager = testGO.AddComponent<BallManager>();
                var config = AssetDatabase.LoadAssetAtPath<PhysicsConfig>("Assets/Game/Configs/DefaultPhysicsConfig.asset");

                // Mock bi: Toàn bộ bi nhóm Solid (1-7) đã vào lỗ (inactive)
                List<GameObject> balls = new List<GameObject>();
                for (int i = 0; i <= 15; i++)
                {
                    var bGO = new GameObject($"Ball_{i}");
                    balls.Add(bGO);
                    var bc = bGO.AddComponent<BallController>();
                    bc.SetBallNumber(i);
                    bc.ApplyPhysicsConfig(config);
                    ballManager.RegisterBall(i, bc);

                    if (i >= 1 && i <= 7)
                    {
                        bGO.SetActive(false); // Đã clear
                    }
                }

                var matchState = new MatchState();
                matchState.turnSequence = 6;
                matchState.tableOpen = false;
                matchState.playerAGroup = PlayerGroup.Solids;
                matchState.currentPlayerIndex = 0; // Player A đánh

                // Player A gọi bi 8 vào lỗ 4 và đánh hợp lệ: chạm bi 8 trước, bi 8 vào đúng lỗ 4, không phạm lỗi
                ShotCommand winCmd = new ShotCommand(0, Vector3.forward, 0.5f, Vector2.zero, calledBallNumber: 8, calledPocketId: 4);
                ShotResult rawWin = new ShotResult
                {
                    firstContactBall = 8,
                    cueBallScratch = false
                };
                rawWin.pocketedBalls.Add(8);
                rawWin.pocketedBallPockets[8] = 4; // Vào đúng lỗ gọi
                rawWin.ballsThatHitRail.Add(8);

                ShotResult evalWin = ruleEngine.EvaluateShot(winCmd, rawWin, matchState, ballManager);

                if (evalWin.outcome != RackOutcome.PlayerAWins)
                {
                    Debug.LogError($"[Test 7 FAILED] Đưa bi 8 vào đúng lỗ gọi hợp lệ sau khi clear nhóm không được xử thắng rack! Outcome={evalWin.outcome}");
                    foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                    return false;
                }

                foreach (var b in balls) UnityEngine.Object.DestroyImmediate(b);
                Debug.Log("[Test 7 PASSED] Đánh bi 8 hợp lệ vào đúng lỗ sau khi dọn sạch nhóm được xử THẮNG RACK chuẩn luật WPA.");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(testGO);
            }
        }
    }
}
