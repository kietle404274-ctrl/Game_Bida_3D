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
            bool isBreakShot = (matchState.phase == MatchPhase.BreakSetup || matchState.turnSequence == 0);
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
            result.outcome = RackOutcome.Continue;
            return result;
        }

        private ShotResult EvaluateRegularShot(ShotCommand command, ShotResult result, MatchState matchState, BallManager ballManager)
        {
            PlayerGroup shooterGroup = matchState.CurrentShooterGroup;

            // --- BƯỚC 1: KIỂM TRA ĐIỂM CHẠM ĐẦU TIÊN (LEGAL FIRST CONTACT) ---
            if (result.firstContactBall <= 0)
            {
                // Không chạm bất kỳ quả bi mục tiêu nào
                result.foul = FoulType.WrongBallFirst;
            }
            else
            {
                if (matchState.tableOpen)
                {
                    // Khi bàn Open: không được chạm bi 8 trước
                    if (result.firstContactBall == 8)
                    {
                        result.foul = FoulType.WrongBallFirst;
                    }
                }
                else
                {
                    // Khi đã chọn nhóm
                    int remainingInGroup = CountRemainingBalls(shooterGroup, ballManager);
                    if (remainingInGroup > 0)
                    {
                        // Phải chạm bi thuộc nhóm của mình trước
                        PlayerGroup contactGroup = GetBallGroup(result.firstContactBall);
                        if (contactGroup != shooterGroup)
                        {
                            result.foul = FoulType.WrongBallFirst;
                        }
                    }
                    else
                    {
                        // Đã ăn hết nhóm: mục tiêu hợp lệ duy nhất là bi 8
                        if (result.firstContactBall != 8)
                        {
                            result.foul = FoulType.WrongBallFirst;
                        }
                    }
                }
            }

            // --- BƯỚC 2: KIỂM TRA CHẠM BĂNG (RAIL REQUIREMENT) ---
            if (result.foul == FoulType.None)
            {
                // Sau khi chạm bi hợp lệ, phải có ít nhất 1 bi vào lỗ HOẶC ít nhất 1 bi chạm băng
                if (result.pocketedBalls.Count == 0 && result.ballsThatHitRail.Count == 0)
                {
                    result.foul = FoulType.NoRailAfterContact;
                }
            }

            // --- BƯỚC 3: KIỂM TRA BI BAY KHỎI BÀN ---
            if (result.anyBallOffTable && result.foul == FoulType.None)
            {
                result.foul = FoulType.BallDrivenOffTable;
            }

            // --- BƯỚC 4: ĐÁNH GIÁ BI 8 VÀO LỖ (THẮNG / THUA RACK) ---
            if (result.pocketedBalls.Contains(8))
            {
                result.rackEnded = true;

                int remainingBeforeShot = CountRemainingBalls(shooterGroup, ballManager, excludeBallNumber: 8);
                bool calledPocketMatched = (result.pocketedBallPockets.ContainsKey(8) && result.pocketedBallPockets[8] == command.calledPocketId);
                bool calledBallIsEight = (command.calledBallNumber == 8);

                // Điều kiện THUA RACK ngay lập tức:
                // 1. Phạm bất kỳ lỗi nào (scratch, sai bi trước, không chạm băng...)
                // 2. Bàn vẫn đang Open
                // 3. Chưa ăn hết nhóm của mình
                // 4. Bi 8 vào lỗ khác với lỗ đã gọi
                // 5. Không gọi bi 8
                if (result.HasFoul || matchState.tableOpen || remainingBeforeShot > 0 || !calledPocketMatched || !calledBallIsEight)
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

            // --- BƯỚC 5: XỬ LÝ CALL SHOT, GÁN NHÓM VÀ DUY TRÌ LƯỢT ---
            if (!result.rackEnded && result.foul == FoulType.None)
            {
                if (command.calledSafety)
                {
                    // Gọi Safety: Kết thúc lượt dù có bi vào lỗ hợp lệ, đối thủ đánh tiếp từ vị trí hiện tại
                    result.calledShotMade = false;
                }
                else
                {
                    // So khớp called ball và called pocket
                    bool calledMade = (command.calledBallNumber > 0 &&
                                       result.pocketedBallPockets.ContainsKey(command.calledBallNumber) &&
                                       result.pocketedBallPockets[command.calledBallNumber] == command.calledPocketId);
                    result.calledShotMade = calledMade;

                    // Nếu bàn đang Open và người chơi đưa called ball vào đúng called pocket hợp lệ -> Gán nhóm!
                    if (matchState.tableOpen && calledMade)
                    {
                        PlayerGroup group = GetBallGroup(command.calledBallNumber);
                        if (group == PlayerGroup.Solids || group == PlayerGroup.Stripes)
                        {
                            matchState.AssignGroups(group);
                        }
                    }
                }
            }

            result.outcome = RackOutcome.Continue;
            return result;
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
            if (ballManager == null || group == PlayerGroup.Open) return 0;

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
