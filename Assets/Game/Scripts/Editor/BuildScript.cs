#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Game.Billiards.Editor
{
    [InitializeOnLoad]
    public static class BuildScript
    {
        private static readonly string[] Scenes = new string[]
        {
            "Assets/Game/Scenes/Boot.unity",
            "Assets/Game/Scenes/Lobby.unity",
            "Assets/Game/Scenes/Gameplay.unity"
        };

        private const string BuildOutputFolder = @"D:\TAI_LIEU_HOC_TAP\DoAnTotNghiep\Game_Build";
        private const string ExecutableName = "Game_Bida_3D.exe";

        static BuildScript()
        {
            EditorApplication.delayCall += CheckAndRunBuild;
            EditorApplication.update += CheckAndRunBuild;
        }

        private static void CheckAndRunBuild()
        {
            string refreshFlag = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_refresh.flag");
            if (File.Exists(refreshFlag))
            {
                try { File.Delete(refreshFlag); } catch { }
                Debug.Log("[BuildScript] Đang làm mới AssetDatabase và yêu cầu biên dịch lại script...");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            }

            string flagPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_build.flag");
            if (!File.Exists(flagPath)) return;

            string targetDir = @"D:\TAI_LIEU_HOC_TAP\DoAnTotNghiep\Game_Build";
            try
            {
                string custom = File.ReadAllText(flagPath).Trim();
                if (!string.IsNullOrEmpty(custom) && Directory.Exists(custom))
                {
                    targetDir = custom;
                }
            }
            catch { }

            try
            {
                File.Delete(flagPath);
            }
            catch { }

            // Làm mới tài sản trước khi đóng gói
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();

            // Xóa cờ và thực hiện build
            BuildToCustomPath(targetDir);
        }

        [MenuItem("Tools/Game/Build Windows Standalone Player (x64)")]
        public static void BuildWindowsStandalone()
        {
            BuildToCustomPath(@"D:\TAI_LIEU_HOC_TAP\DoAnTotNghiep\Game_Build");
        }

        public static void BuildToCustomPath(string outputDirectory)
        {
            Debug.Log($"<color=cyan>====== BẮT ĐẦU XÂY DỰNG BẢN WINDOWS STANDALONE 64-BIT VÀO: {outputDirectory} ======</color>");

            string statusFilePath = Path.Combine(outputDirectory, "build_status.txt");

            try
            {
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                    Debug.Log($"[BuildScript] Đã tạo thư mục xuất bản: {outputDirectory}");
                }

                string fullExecutablePath = Path.Combine(outputDirectory, ExecutableName);

                // Xóa file exe cũ nếu có để đảm bảo Unity tạo file mới với timestamp cập nhật chính xác
                if (File.Exists(fullExecutablePath))
                {
                    try { File.Delete(fullExecutablePath); } catch { }
                }

                // Kiểm tra danh sách scene
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                foreach (var scenePath in Scenes)
                {
                    if (!File.Exists(Path.Combine(projectRoot, scenePath)))
                    {
                        string err = $"Thiếu scene bắt buộc: {scenePath}";
                        Debug.LogError($"[BuildScript] {err}");
                        File.WriteAllText(statusFilePath, "FAILED: " + err);
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
                File.WriteAllText(statusFilePath, "BUILDING...");

                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    string info = $"SUCCESS: File={fullExecutablePath}, Size={summary.totalSize / (1024 * 1024):F2}MB, Duration={summary.totalTime.TotalSeconds:F1}s";
                    Debug.Log($"<color=green>✓ XÂY DỰNG THÀNH CÔNG! {info}</color>");
                    File.WriteAllText(statusFilePath, info);
                }
                else
                {
                    string err = $"FAILED: Errors={summary.totalErrors}, Warnings={summary.totalWarnings}";
                    Debug.LogError($"<color=red>✗ XÂY DỰNG THẤT BẠI! {err}</color>");
                    File.WriteAllText(statusFilePath, err);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                try
                {
                    File.WriteAllText(statusFilePath, "EXCEPTION: " + ex.Message);
                }
                catch { }
            }
        }
    }
}
#endif
