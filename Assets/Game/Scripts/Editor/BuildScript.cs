#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Billiards.Editor
{
    public static class BuildScript
    {
        private static readonly string[] Scenes = new string[]
        {
            "Assets/Game/Scenes/Boot.unity",
            "Assets/Game/Scenes/Lobby.unity",
            "Assets/Game/Scenes/Gameplay.unity"
        };

        private const string BuildOutputFolder = "Builds";
        private const string ExecutableName = "Game_Bida_3D.exe";

        [MenuItem("Tools/Game/Build Windows Standalone Player (x64)")]
        public static void BuildWindowsStandalone()
        {
            Debug.Log("<color=cyan>====== BẮT ĐẦU XÂY DỰNG BẢN WINDOWS STANDALONE 64-BIT (MILESTONE 11) ======</color>");

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string outputDirectory = Path.Combine(projectRoot, BuildOutputFolder);

            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                Debug.Log($"[BuildScript] Đã tạo thư mục xuất bản: {outputDirectory}");
            }

            string fullExecutablePath = Path.Combine(outputDirectory, ExecutableName);

            // Kiểm tra danh sách scene
            foreach (var scenePath in Scenes)
            {
                if (!File.Exists(Path.Combine(projectRoot, scenePath)))
                {
                    Debug.LogError($"[BuildScript] Thiếu scene bắt buộc: {scenePath}");
                    return;
                }
            }

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = fullExecutablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[BuildScript] Đang đóng gói {Scenes.Length} scene vào {fullExecutablePath}...");

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=green>✓ XÂY DỰNG THÀNH CÔNG!</color>");
                Debug.Log($"[BuildScript] Kích thước file: {summary.totalSize / (1024 * 1024):F2} MB");
                Debug.Log($"[BuildScript] Thời gian build: {summary.totalTime.TotalSeconds:F1}s");
                Debug.Log($"[BuildScript] Đường dẫn: {fullExecutablePath}");
            }
            else if (summary.result == BuildResult.Failed)
            {
                Debug.LogError($"<color=red>✗ XÂY DỰNG THẤT BẠI! Lỗi: {summary.totalErrors}, Cảnh báo: {summary.totalWarnings}</color>");
            }
        }
    }
}
#endif
