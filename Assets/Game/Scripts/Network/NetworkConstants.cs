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
        public const string PROP_RACK_BALLS = "rb";

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
        public const byte EVENT_SURRENDER = 106;
        public const byte EVENT_STATS_SYNC = 107;
        public const byte EVENT_RACK_SYNC = 108;
        public const byte EVENT_AIM_SYNC = 109;
        public const byte EVENT_BALL_IN_HAND_CONFIRMED = 110;
        public const byte EVENT_NEXT_RACK = 111;
        public const byte EVENT_REMATCH = 112;
        public const byte EVENT_BALL_IN_HAND_PREVIEW = 113;
        public const byte EVENT_SHOT_REJECTED = 114;      // Master -> Client: lệnh đánh bị từ chối
        public const byte EVENT_SURRENDER_REQUEST = 115;  // Client -> Master: xin đầu hàng (Master quyết định)
        public const byte EVENT_CHAT_MESSAGE = 116;       // Gửi / nhận tin nhắn chat thời gian thực trong trận Online
        public const byte EVENT_VOICE_DATA = 117;         // Luồng gói âm thanh G.711 μ-law 8kHz thời gian thực (SendUnreliable)
        public const byte EVENT_VOICE_STATE = 118;        // Đồng bộ trạng thái bật/tắt Mic & Loa giữa 2 cơ thủ (SendReliable)

        // Helper sinh mã phòng ngẫu nhiên (VD: "POOL-8821")
        public static string GenerateRoomId()
        {
            var rnd = new System.Random();
            int num = rnd.Next(1000, 9999);
            return $"POOL-{num}";
        }
    }
}
