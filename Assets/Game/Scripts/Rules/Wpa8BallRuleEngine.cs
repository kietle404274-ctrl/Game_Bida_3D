using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Match;

namespace Game.Billiards.Rules
{
    public class Wpa8BallRuleEngine : MonoBehaviour, IRuleEngine
    {
        public ShotResult EvaluateShot(ShotCommand command, ShotResult rawResult, MatchState matchState, BallManager ballManager)
        {
            if (rawResult == null) rawResult = new ShotResult();

            // 1. Kiểm tra bi cái rơi vào lỗ (Scratch)
            if (rawResult.cueBallScratch)
            {
                rawResult.foul = FoulType.CueBallScratch;
            }

            // 2. Đánh giá cú khai cuộc (Break Shot)
            bool isBreakShot = (matchState.turnSequence == 0);
            if (isBreakShot)
            {
                return EvaluateBreakShot(command, rawResult, matchState, ballManager);
            }

            // 3. Đánh giá cú đánh thông thường sau khai cuộc
            return EvaluateRegularShot(command, rawResult, matchState, ballManager);
        }

        private ShotResult EvaluateBreakShot(ShotCommand command, ShotResult result, MatchState matchState, BallManager ballManager)
        {
            // Bàn luôn ở trạng thái Open sau khi khai cuộc theo WPA
            matchState.tableOpen = true;

            // Kiểm tra Legal Break: có ít nhất 1 bi mục tiêu vào lỗ HOẶC ít nhất 4 bi mục tiêu chạm băng
            bool hasPocketedObjectBall = false;
            for (int i = 0; i < result.pocketedBalls.Count; i++)
            {
                if (result.pocketedBalls[i] >= 1 && result.pocketedBalls[i] <= 15)
                {
                    hasPocketedObjectBall = true;
                    break;
                }
            }

            int objectRailsCount = 0;
            for (int i = 0; i < result.ballsThatHitRail.Count; i++)
            {
                if (result.ballsThatHitRail[i] >= 1 && result.ballsThatHitRail[i] <= 15)
                {
                    objectRailsCount++;
                }
            }

            bool isLegalBreak = hasPocketedObjectBall || (objectRailsCount >= 4);

            if (!isLegalBreak && result.foul == FoulType.None)
            {
                result.foul = FoulType.IllegalBreak;
            }

            // Xử lý bi 8 vào lỗ khi khai cuộc (8-ball on break)
            if (result.pocketedBalls.Contains(8))
            {
                // Theo luật WPA: 8-ball vào lỗ khi khai cuộc không xử thua ngay; đặt lại bi 8 vào Foot Spot
                if (ballManager != null && ballManager.EightBall != null && ballManager.TableWrapper != null)
                {
                    ballManager.EightBall.ResetTo(ballManager.TableWrapper.FootSpot, Quaternion.identity);
                }
                result.pocketedBalls.Remove(8);
                result.pocketedBallPockets.Remove(8);
                result.rackEnded = false;
            }

            // Kết quả lượt sau cú break:
            // Nếu khai cuộc hợp lệ, không scratch và có bi vào lỗ -> người khai cuộc tiếp tục
            // Ngược lại -> đổi lượt, đối thủ nhận ball-in-hand (sau Head String nếu scratch)
            if (isLegalBreak && result.foul == FoulType.None && hasPocketedObjectBall)
            {
                result.calledShotMade = true;
            }
            else
            {
                result.calledShotMade = false;
            }

            result.outcome = RackOutcome.Continue;
            return result;
        }

        private ShotResult EvaluateRegularShot(ShotCommand command, ShotResult result, MatchState matchState, BallManager ballManager)
        {
            PlayerGroup shooterGroup = matchState.CurrentShooterGroup;

            // --- BƯỚC 1: KIỂM TRA ĐIỂM CHẠM ĐẦU TIÊN (LEGAL FIRST CONTACT) ---
            if (result.firstContactBall <= 0)
            {
                // Không chạm bất kỳ quả bi mục tiêu nào: phạt NoBallHit nếu chưa bị lỗi khác (như CueBallScratch)
                if (result.foul == FoulType.None)
                {
                    result.foul = FoulType.NoBallHit;
                }
            }
            else
            {
                // WPA Rule 6.13: Xử lý va chạm đồng thời (Split Hit)
                // Nếu bi ghi nhận đầu tiên phạm lỗi, nhưng trong cùng cửa sổ va chạm có bi hợp lệ, ưu tiên cho cơ thủ
                if (!IsLegalFirstContactBall(result.firstContactBall, shooterGroup, matchState.tableOpen, ballManager, result.pocketedBalls))
                {
                    if (result.initialContactCandidates != null && result.initialContactCandidates.Count > 0)
                    {
                        for (int i = 0; i < result.initialContactCandidates.Count; i++)
                        {
                            int candidate = result.initialContactCandidates[i];
                            if (IsLegalFirstContactBall(candidate, shooterGroup, matchState.tableOpen, ballManager, result.pocketedBalls))
                            {
                                Debug.Log($"[Wpa8BallRuleEngine] WPA 6.13 Split Hit: Cơ thủ đánh trúng đồng thời! Công nhận bi hợp lệ #{candidate} thay vì #{result.firstContactBall}");
                                result.firstContactBall = candidate;
                                break;
                            }
                        }
                    }
                }

                // Đánh giá tính hợp lệ của cú chạm đầu tiên sau khi đã áp dụng WPA 6.13
                if (!IsLegalFirstContactBall(result.firstContactBall, shooterGroup, matchState.tableOpen, ballManager, result.pocketedBalls))
                {
                    if (result.foul == FoulType.None)
                    {
                        result.foul = FoulType.WrongBallFirst;
                    }
                }
            }

            // --- BƯỚC 2: KIỂM TRA CHẠM BĂNG (RAIL REQUIREMENT - WPA Rule 6.3) ---
            if (result.foul == FoulType.None)
            {
                // Sau khi chạm bi hợp lệ, phải có ít nhất 1 bi vào lỗ HOẶC ít nhất 1 bi chạm băng SAU TIẾP XÚC
                // Kiểm tra: có bi chạm băng sau tiếp xúc (ballsThatHitRailAfterContact),
                // hoặc có bi mục tiêu (1-15) chạm băng trong ballsThatHitRail (vì bi mục tiêu chỉ có thể di chuyển sau va chạm)
                bool hasLegalRailHit = result.ballsThatHitRailAfterContact.Count > 0;
                if (!hasLegalRailHit)
                {
                    for (int i = 0; i < result.ballsThatHitRail.Count; i++)
                    {
                        if (result.ballsThatHitRail[i] >= 1 && result.ballsThatHitRail[i] <= 15)
                        {
                            hasLegalRailHit = true;
                            break;
                        }
                    }
                }

                if (result.pocketedBalls.Count == 0 && !hasLegalRailHit)
                {
                    result.foul = FoulType.NoRailAfterContact;
                }
            }

            // --- BƯỚC 3: KIỂM TRA BI BAY KHỎI BÀN ---
            if (result.anyBallOffTable)
            {
                if (result.ballsDrivenOffTable.Contains(8))
                {
                    // Theo luật WPA: Làm bi 8 bay khỏi bàn ngoài cú break -> Xử THUA RACK ngay lập tức
                    result.rackEnded = true;
                    result.foul = FoulType.BallDrivenOffTable;
                    result.outcome = (matchState.currentPlayerIndex == 0) ? RackOutcome.PlayerBWins : RackOutcome.PlayerAWins;
                    return result;
                }

                if (result.foul == FoulType.None)
                {
                    result.foul = FoulType.BallDrivenOffTable;
                }
            }

            // --- BƯỚC 4: ĐÁNH GIÁ BI 8 VÀO LỖ (THẮNG / THUA RACK) ---
            if (result.pocketedBalls.Contains(8))
            {
                result.rackEnded = true;

                int pocketedOfMyGroupInThisShot = 0;
                for (int i = 0; i < result.pocketedBalls.Count; i++)
                {
                    if (result.pocketedBalls[i] != 8 && GetBallGroup(result.pocketedBalls[i]) == shooterGroup)
                        pocketedOfMyGroupInThisShot++;
                }
                int remainingBeforeShot = CountRemainingBalls(shooterGroup, ballManager, excludeBallNumber: 8) + pocketedOfMyGroupInThisShot;

                // Điều kiện THUA RACK ngay lập tức (không còn yêu cầu gọi bi/gọi lỗ):
                // 1. Phạm bất kỳ lỗi nào (scratch, sai bi trước, không chạm băng...)
                // 2. Bàn vẫn đang Open
                // 3. Chưa ăn hết nhóm của mình trước khi đánh bi 8
                if (result.HasFoul || matchState.tableOpen || remainingBeforeShot > 0)
                {
                    result.outcome = (matchState.currentPlayerIndex == 0) ? RackOutcome.PlayerBWins : RackOutcome.PlayerAWins;
                }
                else
                {
                    // THẮNG RACK hợp lệ
                    result.outcome = (matchState.currentPlayerIndex == 0) ? RackOutcome.PlayerAWins : RackOutcome.PlayerBWins;
                }

                return result;
            }

            // --- BƯỚC 5: GÁN NHÓM VÀ DUY TRÌ LƯỢT (không cần gọi bi/gọi lỗ) ---
            if (!result.rackEnded && result.foul == FoulType.None)
            {
                if (command.calledSafety)
                {
                    // Gọi Safety: Kết thúc lượt dù có bi vào lỗ hợp lệ, đối thủ đánh tiếp từ vị trí hiện tại
                    result.calledShotMade = false;
                }
                else
                {
                    // Bàn Open: bi trơn/sọc đầu tiên vào lỗ (bất kỳ lỗ nào) quyết định nhóm của người đánh
                    if (matchState.tableOpen)
                    {
                        for (int i = 0; i < result.pocketedBalls.Count; i++)
                        {
                            PlayerGroup group = GetBallGroup(result.pocketedBalls[i]);
                            if (group == PlayerGroup.Solids || group == PlayerGroup.Stripes)
                            {
                                matchState.AssignGroups(group);
                                break;
                            }
                        }
                    }

                    // Giữ lượt khi ăn được ít nhất 1 bi hợp lệ: bàn Open -> bất kỳ bi trơn/sọc; đã chia nhóm -> bi thuộc nhóm của mình
                    PlayerGroup activeGroup = matchState.CurrentShooterGroup;
                    bool legalPotted = false;
                    for (int i = 0; i < result.pocketedBalls.Count; i++)
                    {
                        PlayerGroup g = GetBallGroup(result.pocketedBalls[i]);
                        if (g == PlayerGroup.Solids || g == PlayerGroup.Stripes)
                        {
                            if (matchState.tableOpen || g == activeGroup)
                            {
                                legalPotted = true;
                                break;
                            }
                        }
                    }
                    result.calledShotMade = legalPotted;
                }
            }

            result.outcome = RackOutcome.Continue;
            return result;
        }

        public bool IsLegalFirstContactBall(int ballNum, PlayerGroup shooterGroup, bool tableOpen, BallManager ballManager, List<int> pocketedBalls)
        {
            if (ballNum <= 0) return false;

            if (tableOpen || shooterGroup == PlayerGroup.Open)
            {
                // Khi bàn Open (hoặc nhóm chưa xác định): được chạm bất kỳ bi trơn hoặc sọc, không được chạm bi 8 trước
                return ballNum != 8;
            }

            PlayerGroup contactGroup = GetBallGroup(ballNum);
            if (contactGroup == shooterGroup)
            {
                // Chạm bi nhóm mình đầu tiên -> LUÔN HỢP LỆ!
                return true;
            }

            if (ballNum == 8)
            {
                // Chạm bi 8 đầu tiên: chỉ hợp lệ khi trước cú đánh cơ thủ ĐÃ dọn sạch toàn bộ bi nhóm mình.
                int pocketedOfMyGroupInThisShot = 0;
                if (pocketedBalls != null)
                {
                    for (int i = 0; i < pocketedBalls.Count; i++)
                    {
                        if (GetBallGroup(pocketedBalls[i]) == shooterGroup)
                            pocketedOfMyGroupInThisShot++;
                    }
                }
                int remainingBeforeShot = CountRemainingBalls(shooterGroup, ballManager) + pocketedOfMyGroupInThisShot;
                return remainingBeforeShot == 0;
            }

            // Chạm bi của đối thủ trước (hoặc bi không hợp lệ)
            return false;
        }

        public PlayerGroup GetBallGroup(int ballNumber)
        {
            if (ballNumber >= 1 && ballNumber <= 7) return PlayerGroup.Solids;
            if (ballNumber >= 9 && ballNumber <= 15) return PlayerGroup.Stripes;
            if (ballNumber == 8) return PlayerGroup.EightBall;
            return PlayerGroup.Open;
        }

        public int CountRemainingBalls(PlayerGroup group, BallManager ballManager, int excludeBallNumber = -1)
        {
            if (group == PlayerGroup.Open || group == PlayerGroup.EightBall) return 0;
            if (ballManager == null) return 7; // Fallback khi không có BallManager (giả định còn bi trên bàn)

            int count = 0;
            int start = (group == PlayerGroup.Solids) ? 1 : 9;
            int end = (group == PlayerGroup.Solids) ? 7 : 15;

            for (int i = start; i <= end; i++)
            {
                if (i == excludeBallNumber) continue;

                var ball = ballManager.GetBall(i);
                if (ball != null && !ball.IsPocketed && ball.gameObject.activeInHierarchy)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
