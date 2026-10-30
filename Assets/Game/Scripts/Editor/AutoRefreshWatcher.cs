using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Billiards.Editor
{
    /// <summary>
    /// Tự làm mới (Refresh) AssetDatabase khi file .cs / tài nguyên trong Assets bị sửa từ bên ngoài Unity
    /// (ví dụ trình soạn thảo hoặc công cụ AI), để Unity tự biên dịch lại mà không cần bấm Ctrl+R hay khởi động lại.
    /// Không làm gì khi đang Play hoặc đang biên dịch.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoRefreshWatcher
    {
        private static FileSystemWatcher watcher;
        private static volatile bool dirty;
        private static long lastChangeTicks;
        private const double DebounceSeconds = 1.0;

        static AutoRefreshWatcher()
        {
            try
            {
                string assets = Path.Combine(Directory.GetCurrentDirectory(), "Assets");
                watcher = new FileSystemWatcher(assets)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Renamed += (s, e) => OnChanged(s, e);
                watcher.Deleted += OnChanged;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[AutoRefreshWatcher] Không thể theo dõi thư mục Assets: " + ex.Message);
                return;
            }

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        private static void OnChanged(object sender, FileSystemEventArgs e)
        {
            string p = e.FullPath;
            if (p.EndsWith(".meta") || p.EndsWith("~") || p.EndsWith(".tmp")) return;
            System.Threading.Interlocked.Exchange(ref lastChangeTicks, System.DateTime.UtcNow.Ticks); // chỉ ghi nhận; xử lý ở luồng chính
            dirty = true;
        }

        private static void Tick()
        {
            if (!dirty) return;
            double since = (System.DateTime.UtcNow.Ticks - System.Threading.Interlocked.Read(ref lastChangeTicks)) / (double)System.TimeSpan.TicksPerSecond;
            if (since < DebounceSeconds) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            dirty = false;
            AssetDatabase.Refresh();
        }

        private static void Dispose()
        {
            EditorApplication.update -= Tick;
            if (watcher != null)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
                watcher = null;
            }
        }
    }
}
