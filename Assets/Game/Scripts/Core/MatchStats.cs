using System;

namespace Game.Billiards.Core
{
    /// <summary>Danh sách các bộ đếm thống kê của một người chơi.</summary>
    public enum StatId
    {
        Shots,
        Visits,
        BallsPocketed,
        PotAttempts,
        PotSuccess,
        CalledAttempts,
        CalledSuccess,
        LongestRun,
        RunOuts,
        BreakAndRuns,
        Breaks,
        BreakLegal,
        BreakPocketed,
        Fouls,
        FoulScratch,
        FoulWrongBall,
        FoulNoRail,
        FoulOffTable,
        FoulTimeout,
        FoulIllegalBreak,
        FoulOther,
        Safeties,
        SafetiesOk,
        BihChances,
        BihConverted,
        PowerSum,
        SpinShots,
        ThinkSum,
        RacksWon,
        Count
    }

    /// <summary>Thống kê của một người chơi (mảng số thực để dễ đóng gói/đồng bộ mạng).</summary>
    [Serializable]
    public class PlayerStats
    {
        public readonly float[] v = new float[(int)StatId.Count];

        public float Get(StatId id) => v[(int)id];
        public void Add(StatId id, float amount = 1f) { v[(int)id] += amount; }
        public void Max(StatId id, float value) { if (value > v[(int)id]) v[(int)id] = value; }
        public void Clear() { Array.Clear(v, 0, v.Length); }

        public static float Ratio(float num, float den) => den > 0.0001f ? num / den : 0f;
    }

    /// <summary>Thống kê trận đấu: hai người chơi × hai phạm vi (cả trận / ván hiện tại).</summary>
    public class MatchStats
    {
        public readonly PlayerStats[] match = { new PlayerStats(), new PlayerStats() };
        public readonly PlayerStats[] rack = { new PlayerStats(), new PlayerStats() };

        /// <summary>Thời gian (giây) đã trôi qua; mỗi máy tự đếm.</summary>
        public float matchTime;
        public float rackTime;

        public void ResetRack()
        {
            rack[0].Clear();
            rack[1].Clear();
            rackTime = 0f;
        }

        public void ResetAll()
        {
            match[0].Clear();
            match[1].Clear();
            matchTime = 0f;
            ResetRack();
        }

        /// <summary>Áp một thao tác lên cả thống kê cả trận và ván hiện tại của người chơi.</summary>
        public void Add(int player, StatId id, float amount = 1f)
        {
            if (player < 0 || player > 1) return;
            match[player].Add(id, amount);
            rack[player].Add(id, amount);
        }

        public void Max(int player, StatId id, float value)
        {
            if (player < 0 || player > 1) return;
            match[player].Max(id, value);
            rack[player].Max(id, value);
        }

        public float[] Pack()
        {
            int n = (int)StatId.Count;
            float[] data = new float[n * 4];
            Array.Copy(match[0].v, 0, data, 0, n);
            Array.Copy(match[1].v, 0, data, n, n);
            Array.Copy(rack[0].v, 0, data, n * 2, n);
            Array.Copy(rack[1].v, 0, data, n * 3, n);
            return data;
        }

        public bool Unpack(float[] data)
        {
            int n = (int)StatId.Count;
            if (data == null || data.Length != n * 4) return false;
            Array.Copy(data, 0, match[0].v, 0, n);
            Array.Copy(data, n, match[1].v, 0, n);
            Array.Copy(data, n * 2, rack[0].v, 0, n);
            Array.Copy(data, n * 3, rack[1].v, 0, n);
            return true;
        }
    }
}
