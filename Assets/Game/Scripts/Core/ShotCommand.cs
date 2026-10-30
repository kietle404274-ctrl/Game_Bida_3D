using System;
using UnityEngine;

namespace Game.Billiards.Core
{
    [Serializable]
    public struct ShotCommand
    {
        [Tooltip("ID của người chơi hoặc AI thực hiện cú đánh")]
        public int shooterActorId;

        [Tooltip("Hướng đánh trên mặt phẳng bàn XZ (chuẩn hóa)")]
        public Vector3 direction;

        [Tooltip("Lực bắn từ 0.0 đến 1.0")]
        public float power01;

        [Tooltip("Điểm chạm cơ trên bi cái: x (-1..1) là side spin, y (-1..1) là top/back spin")]
        public Vector2 cueContact01;

        [Tooltip("Số bi gọi (dành cho Call Shot), -1 nếu là khai cuộc/không gọi")]
        public int calledBallNumber;

        [Tooltip("Lỗ gọi (0..5), -1 nếu không gọi")]
        public int calledPocketId;

        [Tooltip("Có gọi Safety (đánh an toàn) hay không")]
        public bool calledSafety;

        [Tooltip("Số thứ tự lượt đánh (Turn sequence)")]
        public int turnSequence;

        [Tooltip("Góc nâng cơ so với mặt phẳng bàn (độ). 0 = đánh bình thường, lớn hơn ngưỡng = nhảy bi")]
        public float cueElevationDeg;

        public ShotCommand(int shooterActorId, Vector3 direction, float power01, Vector2 cueContact01, int calledBallNumber = -1, int calledPocketId = -1, bool calledSafety = false, int turnSequence = 0, float cueElevationDeg = 0f)
        {
            this.shooterActorId = shooterActorId;
            this.direction = new Vector3(direction.x, 0f, direction.z).normalized;
            this.power01 = Mathf.Clamp01(power01);
            this.cueContact01 = Vector2.ClampMagnitude(cueContact01, 1f);
            this.calledBallNumber = calledBallNumber;
            this.calledPocketId = calledPocketId;
            this.calledSafety = calledSafety;
            this.turnSequence = turnSequence;
            this.cueElevationDeg = Mathf.Clamp(cueElevationDeg, 0f, 90f);
        }
    }
}
