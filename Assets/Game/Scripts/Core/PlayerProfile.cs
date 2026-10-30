using System;
using UnityEngine;

namespace Game.Billiards.Core
{
    /// <summary>
    /// Hồ sơ người chơi dùng chung cho toàn game (tên + avatar mặc định), lưu bằng PlayerPrefs.
    /// Chỉ chỉnh sửa trong màn hình Cài đặt ở sảnh; các nơi khác chỉ hiển thị.
    /// </summary>
    public static class PlayerProfile
    {
        public const string NameKey = "PlayerName";
        public const string AvatarKey = "PlayerAvatar";
        public const int MaxNameLength = 16;

        /// <summary>Phát ra khi tên hoặc avatar thay đổi.</summary>
        public static event Action OnChanged;

        public static string Name
        {
            get
            {
                string n = PlayerPrefs.GetString(NameKey, "");
                return string.IsNullOrWhiteSpace(n) ? LocalizationManager.Get("default_player_name") : n;
            }
        }

        /// <summary>Chỉ số avatar đã lưu (chưa chuẩn hóa; dùng AvatarLibrary.Wrap khi cần).</summary>
        public static int AvatarIndex => Mathf.Max(0, PlayerPrefs.GetInt(AvatarKey, 0));

        public static void SetName(string value)
        {
            string n = (value ?? "").Trim();
            if (n.Length > MaxNameLength) n = n.Substring(0, MaxNameLength);
            PlayerPrefs.SetString(NameKey, n);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void SetAvatar(int index)
        {
            PlayerPrefs.SetInt(AvatarKey, Mathf.Max(0, index));
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }
}
