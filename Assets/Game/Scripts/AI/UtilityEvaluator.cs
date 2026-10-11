using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Billiards.AI
{
    public static class UtilityEvaluator
    {
        public static float EvaluateCandidate(ShotCandidate candidate, AIDifficultyProfile profile)
        {
            if (candidate == null) return -999f;
            if (profile == null) profile = AIDifficultyProfile.NormalProfile;

            float score = 
                (profile.wPot * candidate.potFeasibility)
              + (profile.wAngle * candidate.angleScore)
              + (profile.wDistance * candidate.distanceScore)
              + (profile.wClearance * candidate.clearanceScore)
              + (profile.wPosition * candidate.cueBallPositionScore)
              + (profile.wSafety * candidate.safetyScore)
              - (profile.wRisk * candidate.foulRisk);

            candidate.finalUtilityScore = score;
            return score;
        }

        public static ShotCandidate SelectBestCandidate(List<ShotCandidate> candidates, AIDifficultyProfile profile)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (profile == null) profile = AIDifficultyProfile.NormalProfile;

            // Đánh giá điểm từng candidate
            for (int i = 0; i < candidates.Count; i++)
            {
                EvaluateCandidate(candidates[i], profile);
            }

            // Sắp xếp theo thứ tự giảm dần của finalUtilityScore
            candidates.Sort((a, b) => b.finalUtilityScore.CompareTo(a.finalUtilityScore));

            // Chọn candidate theo profile cấp độ khó
            switch (profile.difficulty)
            {
                case AIDifficulty.Easy:
                    // Easy: Chọn ngẫu nhiên trong top 3 candidate (nếu có) để tạo cảm giác tự nhiên, đôi khi chọn cú đánh phụ
                    int poolSizeEasy = Mathf.Min(3, candidates.Count);
                    int pickedIndexEasy = UnityEngine.Random.Range(0, poolSizeEasy);
                    return candidates[pickedIndexEasy];

                case AIDifficulty.Normal:
                    // Normal: 80% chọn top 1, 20% chọn top 2
                    if (candidates.Count >= 2 && UnityEngine.Random.value < 0.2f)
                    {
                        return candidates[1];
                    }
                    return candidates[0];

                case AIDifficulty.Hard:
                default:
                    // Hard: Luôn chọn candidate có Utility Score cao nhất
                    return candidates[0];
            }
        }
    }
}
