using System;

namespace Game.Billiards.Network
{
    public static class NetworkConstants
    {
        public const string GAME_VERSION = "1.0.0";
        public const byte MAX_PLAYERS = 2;

        // Custom Room Properties Keys
        public const string PROP_GAME_VERSION = "gv";
        public const string PROP_ROOM_STATE = "rs";
        public const string PROP_RACE_TARGET = "rt";
        public const string PROP_SHOT_TIMER = "st";
        public const string PROP_P1_READY = "p1r";
        public const string PROP_P2_READY = "p2r";
        public const string PROP_P1_NAME = "p1n";
        public const string PROP_P2_NAME = "p2n";

        // Room States
        public const string STATE_WAITING = "Waiting";
        public const string STATE_READY = "Ready";
        public const string STATE_IN_MATCH = "InMatch";

        // Photon RaiseEvent Codes (Dành cho M8/M9)
        public const byte EVENT_SHOT_COMMAND = 101;
        public const byte EVENT_BALLS_SYNC = 102;
        public const byte EVENT_TURN_RESULT = 103;
        public const byte EVENT_MATCH_ENDED = 104;
        public const byte EVENT_CALL_SHOT = 105;

        // Helper sinh mã phòng ngẫu nhiên (VD: "POOL-8821")
        public static string GenerateRoomId()
        {
            var rnd = new System.Random();
            int num = rnd.Next(1000, 9999);
            return $"POOL-{num}";
        }
    }
}
