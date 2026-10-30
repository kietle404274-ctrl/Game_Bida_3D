using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public static class ShotCandidateGenerator
    {
        public const float BALL_RADIUS = 0.02855f;
        public const float BALL_DIAMETER = 2f * BALL_RADIUS; // 0.0571m

        public static List<ShotCandidate> GenerateAllPotCandidates(
            Vector3 cueBallPos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            List<ShotCandidate> candidates = new List<ShotCandidate>();
            if (state == null || ballManager == null) return candidates;

            List<int> targetBalls = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);

            float maxAngle = (profile != null) ? profile.maxCutAngleDegrees : 65f;

            for (int t = 0; t < targetBalls.Count; t++)
            {
                int ballNum = targetBalls[t];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null) continue;

                Vector3 targetPos = ball.transform.position;

                for (int p = 0; p < pockets.Length; p++)
                {
                    Vector3 pocketPos = pockets[p];
                    var candidate = EvaluatePotPair(cueBallPos, targetPos, pocketPos, ballNum, p, ballManager, tableWrapper, maxAngle, profile, state);
                    if (candidate != null)
                    {
                        candidates.Add(candidate);

                        // Hard AI: Sinh thêm các biến thể điều bi cái (Follow - ép tới / Draw - kéo về)
                        // Giúp AI chủ động chọn spin và lực để điều bi cái vào vị trí tối ưu cho bi tiếp theo
                        if (profile != null && profile.difficulty == AIDifficulty.Hard && profile.usePositionPlay && candidate.potFeasibility >= 0.20f)
                        {
                            // 1. Biến thể Follow (Cu-lê / Topspin): Bi cái cuộn tới trước sau va chạm
                            ShotCandidate followCandidate = candidate.Clone();
                            followCandidate.estimatedSpin = new Vector2(0f, 0.50f);
                            followCandidate.estimatedPower01 = Mathf.Clamp(candidate.estimatedPower01 * 1.15f, 0.15f, 0.75f);
                            followCandidate.cueBallPositionScore = PositionPlayPredictor.EvaluatePositionQuality(followCandidate, state, ballManager, tableWrapper);
                            candidates.Add(followCandidate);

                            // 2. Biến thể Draw (Trô / Backspin): Bi cái giật lùi về sau va chạm
                            ShotCandidate drawCandidate = candidate.Clone();
                            drawCandidate.estimatedSpin = new Vector2(0f, -0.65f);
                            drawCandidate.estimatedPower01 = Mathf.Clamp(candidate.estimatedPower01 * 1.25f, 0.18f, 0.85f);
                            drawCandidate.cueBallPositionScore = PositionPlayPredictor.EvaluatePositionQuality(drawCandidate, state, ballManager, tableWrapper);
                            candidates.Add(drawCandidate);
                        }
                    }
                }
            }

            return candidates;
        }

        public static ShotCandidate EvaluatePotPair(
            Vector3 cuePos, 
            Vector3 targetPos, 
            Vector3 pocketPos, 
            int targetBallNum, 
            int pocketId, 
            BallManager ballManager, 
            TableWrapper table, 
            float maxCutAngle,
            AIDifficultyProfile profile = null,
            MatchState state = null)
        {
            Vector3 targetToPocket = new Vector3(pocketPos.x - targetPos.x, 0f, pocketPos.z - targetPos.z);
            float distTargetToPocket = targetToPocket.magnitude;
            if (distTargetToPocket < 0.01f) return null;

            Vector3 targetDir = targetToPocket.normalized;

            // Kiểm tra góc vào miệng lỗ: nếu góc quá chéo so với miệng lỗ, bi sẽ bị má lỗ (facing jaws) chặn lại
            if (!TableStateAnalyzer.IsPocketAngleAcceptable(pocketId, targetDir))
            {
                return null;
            }

            // 1. Tính toán vị trí Ghost-ball: lùi lại đúng 1 đường kính bi theo hướng ngược với lỗ
            Vector3 ghostPos = targetPos - targetDir * BALL_DIAMETER;

            // Kiểm tra ghostPos có nằm trong mặt bàn không
            if (table != null && !table.IsInsidePlayArea(ghostPos, -0.02f))
            {
                return null;
            }

            // 2. Hướng ngắm từ Bi Cái tới Ghost Ball
            Vector3 cueToGhost = new Vector3(ghostPos.x - cuePos.x, 0f, ghostPos.z - cuePos.z);
            float distCueToGhost = cueToGhost.magnitude;
            if (distCueToGhost < 0.01f) return null;

            Vector3 aimDir = cueToGhost.normalized;

            // 3. Tính góc cắt (Cut Angle)
            float dot = Vector3.Dot(aimDir, targetDir);
            if (dot <= 0.05f) // Góc quá lớn (> 87 độ) hoặc đập ngược lại
            {
                return null;
            }

            float cutAngleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            if (cutAngleDeg > maxCutAngle)
            {
                return null;
            }

            // 4. Kiểm tra độ thông thoáng (Clearance) của 2 hành lang:
            // Hành lang 1: Cue -> Ghost
            int[] ignoredCuePath = new int[] { 0, targetBallNum };
            float cueClearance = PathClearanceChecker.CalculateClearanceScore(cuePos, ghostPos, ignoredCuePath, ballManager);
            if (cueClearance <= 0f)
            {
                return null; // Bị bi khác chặn đường ngắm
            }

            // Hành lang 2: Target -> Pocket
            int[] ignoredTargetPath = new int[] { targetBallNum };
            float targetClearance = PathClearanceChecker.CalculateClearanceScore(targetPos, pocketPos, ignoredTargetPath, ballManager);
            if (targetClearance <= 0f)
            {
                return null; // Bị bi khác chặn đường vào lỗ
            }

            // 5. Ước tính lực đánh (Power 0..1) thông minh, kiểm soát chống quá lực văng bi:
            // Kẹp hệ số bù góc tối đa 1.45 để tránh nhân lực lên quá đà khi góc cắt lớn
            float angleComp = Mathf.Clamp(1f / Mathf.Max(0.40f, dot), 1.0f, 1.45f);
            
            // Lực đánh: Bi mớm lỗ (< 0.25m) dùng lực êm dịu cực chuẩn (0.12 - 0.25) để bi rơi tọt vào lỗ
            float estimatedPower;
            if (distTargetToPocket < 0.25f)
            {
                estimatedPower = Mathf.Clamp(0.11f + distCueToGhost * 0.11f, 0.12f, 0.32f);
            }
            else if (distTargetToPocket < 0.45f)
            {
                estimatedPower = Mathf.Clamp(0.14f + (distCueToGhost * 0.12f + distTargetToPocket * 0.14f) * angleComp, 0.14f, 0.45f);
            }
            else
            {
                estimatedPower = Mathf.Clamp(0.16f + (distCueToGhost * 0.13f + distTargetToPocket * 0.18f) * angleComp, 0.16f, 0.65f);
            }

            // 6. Tính toán các thành phần điểm hình học - ĐẶC BIỆT ƯU TIÊN BI GẦN LỖ:
            // Điểm khoảng cách từ bi mục tiêu tới lỗ (Pocket Proximity Score):
            // Bi mớm lỗ (< 22cm) điểm tuyệt đối 1.0; bi gần lỗ (< 45cm) điểm 0.85 - 0.95
            float pocketDistScore = Mathf.Clamp01(1f - (distTargetToPocket / 1.35f));
            if (distTargetToPocket < 0.22f)
            {
                pocketDistScore = 1.0f; // Bi mớm lỗ (Hanger duck)
            }
            else if (distTargetToPocket < 0.45f)
            {
                pocketDistScore = Mathf.Lerp(0.85f, 1.0f, (0.45f - distTargetToPocket) / 0.23f);
            }

            // Điểm khoảng cách từ bi cái tới ghost ball
            float cueDistScore = Mathf.Clamp01(1f - (distCueToGhost / 2.0f));

            // Điểm khoảng cách tổng hợp: khoảng cách bi-tới-lỗ chiếm tới 75%
            float distanceScore = (pocketDistScore * 0.75f) + (cueDistScore * 0.25f);

            // Điểm góc cắt: Bi ở sát lỗ có dung sai góc tiếp nhận lớn hơn rất nhiều so với bi xa
            float cutAngleTolerance = (distTargetToPocket < 0.30f) ? 80f : 65f;
            float angleScore = Mathf.Clamp01(1f - (cutAngleDeg / cutAngleTolerance));

            float clearanceScore = (cueClearance * 0.5f) + (targetClearance * 0.5f);

            // Nguy cơ rơi bi cái (Scratch Risk): đường văng của bi cái sau va chạm theo tiếp tuyến 90 độ
            Vector3 cueTangentDir = Vector3.Cross(Vector3.up, targetDir);
            if (Vector3.Dot(cueTangentDir, aimDir) < 0f)
            {
                cueTangentDir = -cueTangentDir;
            }

            float foulRisk = EvaluateScratchRisk(ghostPos, cueTangentDir, table);

            // Tự động tính Ép-phê (Smart Spin) để hãm bi hoặc né lỗ
            Vector2 smartSpin = Vector2.zero;
            if (profile != null && profile.allowSmartSpin)
            {
                // Cú đánh gần như thẳng (< 18 độ): Bi cái dễ trôi theo bi mục tiêu vào cùng một lỗ
                if (cutAngleDeg < 18f && distCueToGhost < 1.0f)
                {
                    smartSpin = new Vector2(0f, -0.45f); // Trô nhẹ giữ bi cái lại
                }
                else if (foulRisk > 0.35f)
                {
                    smartSpin = new Vector2(0f, -0.55f); // Trô mạnh hãm đà văng vào lỗ
                }
            }

            // Khả thi ăn bi (Pot Feasibility):
            // Bi gần lỗ (pocketDistScore cao) + góc không quá gắt + thoáng -> Khả thi ăn cực cao
            float potFeasibility = Mathf.Clamp01(pocketDistScore * 0.45f + angleScore * 0.30f + clearanceScore * 0.25f);

            // Thưởng lớn cho bi mớm miệng lỗ (Hanger bonus): gần như 100% ăn bi!
            if (distTargetToPocket < 0.25f && targetClearance > 0.8f)
            {
                potFeasibility = Mathf.Max(potFeasibility, 0.96f);
            }

            ShotCandidate candidate = new ShotCandidate
            {
                shotType = AIShotType.DirectPot,
                targetBallNumber = targetBallNum,
                targetPocketId = pocketId,
                targetPocketPosition = pocketPos,
                ghostBallPosition = ghostPos,
                targetBallPosition = targetPos,
                cueBallPosition = cuePos,
                aimDirection = aimDir,
                cutAngleDegrees = cutAngleDeg,
                cueToGhostDistance = distCueToGhost,
                targetToPocketDistance = distTargetToPocket,
                estimatedPower01 = estimatedPower,
                estimatedSpin = smartSpin,
                isSafety = false,
                isLegalBreak = false,
                potFeasibility = potFeasibility,
                angleScore = angleScore,
                distanceScore = distanceScore,
                clearanceScore = clearanceScore,
                cueBallPositionScore = 0.5f,
                safetyScore = 0f,
                foulRisk = foulRisk
            };

            // Dự đoán thế bi tiếp theo (Position Play) cho cấp độ Khó
            if (profile != null && profile.usePositionPlay && state != null)
            {
                candidate.cueBallPositionScore = PositionPlayPredictor.EvaluatePositionQuality(candidate, state, ballManager, table);
            }

            return candidate;
        }

        public static List<ShotCandidate> GenerateComboCandidates(
            Vector3 cuePos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            List<ShotCandidate> combos = new List<ShotCandidate>();
            if (state == null || ballManager == null || profile == null || !profile.allowComboShots) 
                return combos;

            List<int> legalFirstBalls = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);

            // Danh sách bi thứ 2 (bi B) có thể rơi vào lỗ:
            List<int> eligibleSecondBalls = new List<int>();
            if (state.tableOpen || state.CurrentShooterGroup == PlayerGroup.Open)
            {
                for (int b = 1; b <= 15; b++) if (b != 8) eligibleSecondBalls.Add(b);
            }
            else
            {
                int start = (state.CurrentShooterGroup == PlayerGroup.Solids) ? 1 : 9;
                for (int b = start; b <= start + 6; b++) eligibleSecondBalls.Add(b);
                if (legalFirstBalls.Count == 1 && legalFirstBalls[0] == 8)
                {
                    eligibleSecondBalls.Add(8);
                }
            }

            for (int i = 0; i < legalFirstBalls.Count; i++)
            {
                int ballA = legalFirstBalls[i];
                var bA = ballManager.GetBall(ballA);
                if (bA == null || bA.IsPocketed || !bA.gameObject.activeInHierarchy) continue;
                Vector3 posA = bA.transform.position;

                for (int j = 0; j < eligibleSecondBalls.Count; j++)
                {
                    int ballB = eligibleSecondBalls[j];
                    if (ballB == ballA) continue;
                    var bB = ballManager.GetBall(ballB);
                    if (bB == null || bB.IsPocketed || !bB.gameObject.activeInHierarchy) continue;
                    Vector3 posB = bB.transform.position;

                    float distAB = Vector2.Distance(new Vector2(posA.x, posA.z), new Vector2(posB.x, posB.z));
                    if (distAB > 1.2f || distAB < BALL_DIAMETER * 0.95f) continue;

                    for (int p = 0; p < pockets.Length; p++)
                    {
                        Vector3 pocketPos = pockets[p];
                        Vector3 toPocket = (pocketPos - posB);
                        toPocket.y = 0f;
                        float distBToPocket = toPocket.magnitude;
                        if (distBToPocket < 0.05f) continue;
                        Vector3 dirBToPocket = toPocket.normalized;
                        if (!TableStateAnalyzer.IsPocketAngleAcceptable(p, dirBToPocket))
                            continue;

                        Vector3 ghostB = posB - dirBToPocket * BALL_DIAMETER;
                        
                        if (!PathClearanceChecker.IsSegmentClear(posB, pocketPos, PathClearanceChecker.MIN_CLEARANCE_RADIUS, new int[] { ballB }, ballManager, out _))
                            continue;

                        Vector3 toGhostB = (ghostB - posA);
                        toGhostB.y = 0f;
                        float distAToGhostB = toGhostB.magnitude;
                        if (distAToGhostB < 0.05f) continue;
                        Vector3 dirAToGhostB = toGhostB.normalized;

                        float dotAB = Vector3.Dot(dirAToGhostB, dirBToPocket);
                        if (dotAB < 0.70f) continue; // Góc cắt A->B không quá 45 độ

                        if (!PathClearanceChecker.IsSegmentClear(posA, ghostB, PathClearanceChecker.MIN_CLEARANCE_RADIUS, new int[] { ballA, ballB }, ballManager, out _))
                            continue;

                        Vector3 ghostA = posA - dirAToGhostB * BALL_DIAMETER;

                        Vector3 toGhostA = (ghostA - cuePos);
                        toGhostA.y = 0f;
                        float distCueToGhostA = toGhostA.magnitude;
                        if (distCueToGhostA < 0.05f) continue;
                        Vector3 aimDir = toGhostA.normalized;

                        float dotCueA = Vector3.Dot(aimDir, dirAToGhostB);
                        if (dotCueA < 0.70f) continue; // Góc cắt Cue->A không quá 45 độ

                        if (!PathClearanceChecker.IsSegmentClear(cuePos, ghostA, PathClearanceChecker.MIN_CLEARANCE_RADIUS, new int[] { 0, ballA }, ballManager, out _))
                            continue;

                        float totalDist = distCueToGhostA + distAToGhostB + distBToPocket;
                        float power = Mathf.Clamp(0.25f + totalDist * 0.15f, 0.28f, 0.65f);

                        float angleScore = (dotAB + dotCueA) * 0.5f;
                        float distScore = Mathf.Clamp01(1f - totalDist / 2.8f);
                        float comboFeasibility = angleScore * 0.6f + distScore * 0.4f;

                        var comboCandidate = new ShotCandidate
                        {
                            shotType = AIShotType.Combination,
                            targetBallNumber = ballB,
                            intermediateBallNumber = ballA,
                            targetPocketId = p,
                            targetPocketPosition = pocketPos,
                            targetBallPosition = posB,
                            ghostBallPosition = ghostA,
                            cueBallPosition = cuePos,
                            aimDirection = aimDir,
                            cutAngleDegrees = Mathf.Acos(Mathf.Clamp(dotCueA, -1f, 1f)) * Mathf.Rad2Deg,
                            cueToGhostDistance = distCueToGhostA,
                            targetToPocketDistance = distBToPocket,
                            estimatedPower01 = power,
                            estimatedSpin = Vector2.zero,
                            isSafety = false,
                            isLegalBreak = false,
                            potFeasibility = comboFeasibility * 0.85f,
                            angleScore = angleScore,
                            distanceScore = distScore,
                            clearanceScore = 0.8f,
                            cueBallPositionScore = 0.5f,
                            safetyScore = 0f,
                            foulRisk = 0.05f,
                            finalUtilityScore = comboFeasibility * 0.75f
                        };

                        if (profile.usePositionPlay && state != null)
                        {
                            comboCandidate.cueBallPositionScore = PositionPlayPredictor.EvaluatePositionQuality(comboCandidate, state, ballManager, tableWrapper);
                        }

                        combos.Add(comboCandidate);
                    }
                }
            }

            return combos;
        }

        private struct BankRailSegment
        {
            public bool isXAxis;
            public float railCoord;
            public float minOther;
            public float maxOther;
            public Vector3 normal;
        }

        public static List<ShotCandidate> GenerateBankCandidates(
            Vector3 cuePos, 
            MatchState state, 
            BallManager ballManager, 
            TableWrapper tableWrapper, 
            AIDifficultyProfile profile)
        {
            List<ShotCandidate> bankCandidates = new List<ShotCandidate>();
            if (state == null || ballManager == null || profile == null || !profile.allowBankShots) 
                return bankCandidates;

            List<int> legalTargets = TableStateAnalyzer.GetLegalTargetBalls(state, ballManager);
            if (legalTargets.Count == 0) return bankCandidates;

            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(tableWrapper);

            float footX = TableWrapper.FootCushionX;
            float headX = TableWrapper.HeadCushionX;
            float sideZ = TableWrapper.CushionHalfWidth;

            // 4 băng phẳng (tránh các miệng lỗ góc và lỗ giữa)
            BankRailSegment[] rails = new BankRailSegment[]
            {
                // Băng ngắn Foot (-X)
                new BankRailSegment { isXAxis = true, railCoord = footX, minOther = -0.42f, maxOther = 0.42f, normal = Vector3.right },
                // Băng ngắn Head (+X)
                new BankRailSegment { isXAxis = true, railCoord = headX, minOther = -0.42f, maxOther = 0.42f, normal = Vector3.left },
                // Băng dài Side (+Z), tránh lỗ giữa tại X in [-0.08, 0.08]
                new BankRailSegment { isXAxis = false, railCoord = sideZ, minOther = 0.08f, maxOther = 0.98f, normal = Vector3.back },
                new BankRailSegment { isXAxis = false, railCoord = sideZ, minOther = -0.98f, maxOther = -0.08f, normal = Vector3.back },
                // Băng dài Side (-Z)
                new BankRailSegment { isXAxis = false, railCoord = -sideZ, minOther = 0.08f, maxOther = 0.98f, normal = Vector3.forward },
                new BankRailSegment { isXAxis = false, railCoord = -sideZ, minOther = -0.98f, maxOther = -0.08f, normal = Vector3.forward },
            };

            for (int t = 0; t < legalTargets.Count; t++)
            {
                int ballNum = legalTargets[t];
                var ball = ballManager.GetBall(ballNum);
                if (ball == null || ball.IsPocketed || !ball.gameObject.activeInHierarchy) continue;

                Vector3 targetPos = ball.transform.position;

                for (int r = 0; r < rails.Length; r++)
                {
                    BankRailSegment rail = rails[r];

                    // Bi mục tiêu phải nằm trong sân và cách băng một khoảng hợp lý
                    float distToRail = rail.isXAxis ? 
                        (rail.railCoord - targetPos.x) * (rail.normal.x < 0 ? -1f : 1f) : 
                        (rail.railCoord - targetPos.z) * (rail.normal.z < 0 ? -1f : 1f);

                    if (distToRail < 0.02f || distToRail > 1.25f) continue;

                    for (int p = 0; p < pockets.Length; p++)
                    {
                        Vector3 pocketPos = pockets[p];

                        // Lỗ đích phải nằm ở phía trước mặt băng (trong lòng bàn đối diện băng)
                        if (Vector3.Dot(pocketPos - targetPos, rail.normal) < -0.1f)
                            continue;

                        // Phản xạ tọa độ lỗ qua thành băng (Mirror Pocket)
                        Vector3 mirrorPocket;
                        if (rail.isXAxis)
                        {
                            mirrorPocket = new Vector3(2f * rail.railCoord - pocketPos.x, pocketPos.y, pocketPos.z);
                        }
                        else
                        {
                            mirrorPocket = new Vector3(pocketPos.x, pocketPos.y, 2f * rail.railCoord - pocketPos.z);
                        }

                        // Tia từ targetPos tới mirrorPocket
                        Vector3 delta = mirrorPocket - targetPos;
                        delta.y = 0f;
                        if (delta.sqrMagnitude < 0.001f) continue;

                        float factor;
                        float hitOther;
                        Vector3 bouncePoint;

                        if (rail.isXAxis)
                        {
                            if (Mathf.Abs(delta.x) < 0.001f) continue;
                            factor = (rail.railCoord - targetPos.x) / delta.x;
                            if (factor <= 0.04f || factor >= 0.96f) continue;
                            hitOther = targetPos.z + factor * delta.z;
                            if (hitOther < rail.minOther || hitOther > rail.maxOther) continue;
                            bouncePoint = new Vector3(rail.railCoord, targetPos.y, hitOther);
                        }
                        else
                        {
                            if (Mathf.Abs(delta.z) < 0.001f) continue;
                            factor = (rail.railCoord - targetPos.z) / delta.z;
                            if (factor <= 0.04f || factor >= 0.96f) continue;
                            hitOther = targetPos.x + factor * delta.x;
                            if (hitOther < rail.minOther || hitOther > rail.maxOther) continue;
                            bouncePoint = new Vector3(hitOther, targetPos.y, rail.railCoord);
                        }

                        // 1. Kiểm tra góc tới băng của bi mục tiêu:
                        Vector3 dirTargetToBounce = (bouncePoint - targetPos).normalized;
                        float dotRailIncidence = Vector3.Dot(dirTargetToBounce, -rail.normal);
                        if (dotRailIncidence < 0.35f) continue; // Tránh góc đập quá nông vào băng

                        // 2. Kiểm tra góc dội từ điểm nảy trên băng vào miệng lỗ:
                        Vector3 dirBounceToPocket = (pocketPos - bouncePoint).normalized;
                        if (!TableStateAnalyzer.IsPocketAngleAcceptable(p, dirBounceToPocket))
                            continue;

                        // 3. Tính toán vị trí Ghost Ball của bi mục tiêu
                        Vector3 ghostPos = targetPos - dirTargetToBounce * BALL_DIAMETER;
                        if (tableWrapper != null && !tableWrapper.IsInsidePlayArea(ghostPos, -0.02f))
                            continue;

                        // 4. Hướng ngắm từ Bi Cái tới Ghost Ball
                        Vector3 cueToGhost = ghostPos - cuePos;
                        cueToGhost.y = 0f;
                        float distCueToGhost = cueToGhost.magnitude;
                        if (distCueToGhost < 0.05f) continue;
                        Vector3 aimDir = cueToGhost.normalized;

                        // 5. Kiểm tra góc cắt (Cut Angle): Cú đánh băng đòi hỏi góc cắt dày (dưới 55 độ)
                        float dotCut = Vector3.Dot(aimDir, dirTargetToBounce);
                        if (dotCut < 0.58f) continue; // dot >= 0.58 tương đương góc cắt <= 54.5 độ

                        float cutAngleDeg = Mathf.Acos(Mathf.Clamp(dotCut, -1f, 1f)) * Mathf.Rad2Deg;

                        // 6. Kiểm tra độ thông thoáng của cả 3 hành lang
                        // Hành lang 1: Cue -> Ghost
                        float c1 = PathClearanceChecker.CalculateClearanceScore(cuePos, ghostPos, new int[] { 0, ballNum }, ballManager);
                        if (c1 <= 0f) continue;

                        // Hành lang 2: Target -> Băng
                        float c2 = PathClearanceChecker.CalculateClearanceScore(targetPos, bouncePoint, new int[] { ballNum }, ballManager);
                        if (c2 <= 0f) continue;

                        // Hành lang 3: Băng -> Lỗ
                        float c3 = PathClearanceChecker.CalculateClearanceScore(bouncePoint, pocketPos, new int[] { ballNum }, ballManager);
                        if (c3 <= 0f) continue;

                        float distTargetToBounce = Vector3.Distance(targetPos, bouncePoint);
                        float distBounceToPocket = Vector3.Distance(bouncePoint, pocketPos);
                        float totalDist = distCueToGhost + distTargetToBounce + distBounceToPocket;
                        if (totalDist > 3.2f) continue;

                        // Ước tính lực đánh (bù tổn hao năng lượng khi va chạm băng)
                        float angleComp = Mathf.Clamp(1f / Mathf.Max(0.5f, dotCut), 1.0f, 1.45f);
                        float power = Mathf.Clamp(0.28f + (distCueToGhost * 0.12f + (distTargetToBounce + distBounceToPocket) * 0.18f) * angleComp, 0.28f, 0.68f);

                        float angleScore = Mathf.Clamp01((dotCut - 0.5f) / 0.5f);
                        float distScore = Mathf.Clamp01(1f - totalDist / 3.0f);
                        float clearanceScore = (c1 * 0.4f + c2 * 0.3f + c3 * 0.3f);
                        // Hệ số 0.68f để luôn ưu tiên Direct Pot nếu có cơ hội ăn thẳng
                        float bankFeasibility = (angleScore * 0.50f + clearanceScore * 0.30f + distScore * 0.20f) * 0.68f;

                        ShotCandidate candidate = new ShotCandidate
                        {
                            shotType = AIShotType.Bank,
                            targetBallNumber = ballNum,
                            targetPocketId = p,
                            targetPocketPosition = pocketPos,
                            ghostBallPosition = ghostPos,
                            targetBallPosition = targetPos,
                            cueBallPosition = cuePos,
                            aimDirection = aimDir,
                            cutAngleDegrees = cutAngleDeg,
                            cueToGhostDistance = distCueToGhost,
                            targetToPocketDistance = distTargetToBounce + distBounceToPocket,
                            estimatedPower01 = power,
                            estimatedSpin = Vector2.zero,
                            isSafety = false,
                            isLegalBreak = false,
                            potFeasibility = bankFeasibility,
                            angleScore = angleScore,
                            distanceScore = distScore,
                            clearanceScore = clearanceScore,
                            cueBallPositionScore = 0.5f,
                            safetyScore = 0f,
                            foulRisk = 0.05f,
                            finalUtilityScore = bankFeasibility * 0.70f
                        };

                        if (profile.usePositionPlay && state != null)
                        {
                            candidate.cueBallPositionScore = PositionPlayPredictor.EvaluatePositionQuality(candidate, state, ballManager, tableWrapper);
                        }

                        bankCandidates.Add(candidate);
                    }
                }
            }

            return bankCandidates;
        }

        private static float EvaluateScratchRisk(Vector3 fromPos, Vector3 tangentDir, TableWrapper table)
        {
            if (table == null) return 0f;
            Vector3[] pockets = TableStateAnalyzer.GetPocketPositions(table);

            float highestRisk = 0f;
            for (int i = 0; i < pockets.Length; i++)
            {
                Vector3 toPocket = pockets[i] - fromPos;
                toPocket.y = 0f;
                float dist = toPocket.magnitude;
                if (dist > 0.1f)
                {
                    float alignment = Vector3.Dot(tangentDir, toPocket.normalized);
                    if (alignment > 0.85f && dist < 0.6f) // Hướng thẳng vào lỗ gần
                    {
                        float risk = alignment * (1f - (dist / 0.6f));
                        if (risk > highestRisk) highestRisk = risk;
                    }
                }
            }

            return Mathf.Clamp01(highestRisk);
        }
    }
}
