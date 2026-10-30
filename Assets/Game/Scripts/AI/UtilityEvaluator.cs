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

            // Full công: tập trung 100% vào ăn bi, khoảng cách gần lỗ, góc cắt, độ thoáng và điều bi
            float score = 
                (profile.wPot * candidate.potFeasibility)
              + (profile.wAngle * candidate.angleScore)
              + (profile.wDistance * candidate.distanceScore)
              + (profile.wClearance * candidate.clearanceScore)
              + (profile.wPosition * candidate.cueBallPositionScore)
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

            // Chọn candidate tốt nhất theo cấp độ
            switch (profile.difficulty)
            {
                case AIDifficulty.Easy:
                    // Easy: Chọn trong top 2 candidate
                    int poolSizeEasy = Mathf.Min(2, candidates.Count);
                    int pickedIndexEasy = UnityEngine.Random.Range(0, poolSizeEasy);
                    return candidates[pickedIndexEasy];

                case AIDifficulty.Normal:
                case AIDifficulty.Hard:
                default:
                    // Normal & Hard: Luôn chọn phương án ăn bi ngon nhất, tối ưu nhất
                    return candidates[0];
            }
        }
    }
}
