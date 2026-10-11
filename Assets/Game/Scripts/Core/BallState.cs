using System;
using UnityEngine;

namespace Game.Billiards.Core
{
    [Serializable]
    public struct BallState
    {
        public int ballNumber;
        public bool isPocketed;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public BallState(int ballNumber, bool isPocketed, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            this.ballNumber = ballNumber;
            this.isPocketed = isPocketed;
            this.position = position;
            this.rotation = rotation;
            this.velocity = velocity;
            this.angularVelocity = angularVelocity;
        }

        public bool IsCueBall => ballNumber == 0;
        public bool IsEightBall => ballNumber == 8;
        public bool IsSolid => ballNumber >= 1 && ballNumber <= 7;
        public bool IsStripe => ballNumber >= 9 && ballNumber <= 15;
    }
}
