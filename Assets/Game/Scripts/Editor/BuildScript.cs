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
            EditorApplication.delayCall += RepairBallMaterialsInProjectAndActiveScene;
            EditorApplication.delayCall += CheckAndRunBuild;
            EditorApplication.update += CheckAndRunBuild;
        }

        private static void CheckAndRunBuild()
        {
            string reloadFlag = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "reload_clean_scene.flag");
            if (File.Exists(reloadFlag))
            {
                try { File.Delete(reloadFlag); } catch { }
                Debug.Log("[BuildScript] Đang nạp lại Gameplay.unity sạch từ đĩa...");
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", UnityEditor.SceneManagement.OpenSceneMode.Single);
                RepairBallMaterialsInProjectAndActiveScene();
            }

            string refreshFlag = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_refresh.flag");
            if (File.Exists(refreshFlag))
            {
                try { File.Delete(refreshFlag); } catch { }
                Debug.Log("[BuildScript] Đang làm mới AssetDatabase và yêu cầu biên dịch lại script...");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                RepairBallMaterialsInProjectAndActiveScene();
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
            RepairBallMaterialsInProjectAndActiveScene();

            // Xóa cờ và thực hiện build
            BuildToCustomPath(targetDir);
        }

        [MenuItem("Tools/Game/Repair Ball Materials (Fix Pink Balls)")]
        public static void RepairBallMaterialsInProjectAndActiveScene()
        {
            Material ballMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/AK Studio Art/The Billiards Room/Materials/Balls Billiard.mat");
            Texture2D ballTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/AK Studio Art/The Billiards Room/Models/Balls Billiard/Balls Billiard.png");
            if (ballMat == null)
            {
                Debug.LogWarning("[BuildScript] Không tìm thấy Balls Billiard.mat!");
                return;
            }

            if (ballTex != null)
            {
                if (ballMat.HasProperty("_BaseColorMap")) ballMat.SetTexture("_BaseColorMap", ballTex);
                if (ballMat.HasProperty("_MainTex")) ballMat.SetTexture("_MainTex", ballTex);
                EditorUtility.SetDirty(ballMat);
            }

            for (int i = 0; i <= 15; i++)
            {
                string prefabPath = $"Assets/Game/Prefabs/Balls/GameBall_{i:D2}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    var mr = prefab.GetComponentInChildren<MeshRenderer>(true);
                    if (mr != null && mr.sharedMaterial != ballMat)
                    {
                        mr.sharedMaterial = ballMat;
                        EditorUtility.SetDirty(prefab);
                        PrefabUtility.SavePrefabAsset(prefab);
                    }
                }
            }

            var sceneBalls = UnityEngine.Object.FindObjectsOfType<Game.Billiards.Physics.BallController>(true);
            foreach (var b in sceneBalls)
            {
                if (b == null) continue;
                var mr = b.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != ballMat)
                {
                    mr.sharedMaterial = ballMat;
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(mr);
                    }
                }
            }

            if (!Application.isPlaying)
            {
                SyncAudioLibraryAssets();
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
            Debug.Log("<color=green>[BuildScript] Đã gán lại vật liệu HDRP Balls Billiard.mat cho toàn bộ 16 bi!</color>");
        }

        private static void SyncAudioLibraryAssets()
        {
            string[] libPaths = new[]
            {
                "Assets/Resources/DefaultAudioLibrary.asset",
                "Assets/Game/Configs/DefaultAudioLibrary.asset"
            };

            AudioClip bgm1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen.mp3");
            AudioClip bgm2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen_2.mp3");
            AudioClip foul = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Loi_Bi.mp3");
            AudioClip rackWin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Ngon_Thi.mp3");
            AudioClip rackLose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Meo_Khoc.mp3");
            AudioClip timerTick = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Dong_Ho.mp3");
            AudioClip matchWin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Win.mp3");
            AudioClip matchLose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Thua.mp3");

            foreach (string path in libPaths)
            {
                var lib = AssetDatabase.LoadAssetAtPath<Game.Billiards.Audio.AudioLibrary>(path);
                if (lib == null) continue;

                SerializedObject so = new SerializedObject(lib);
                bool changed = false;
                changed |= AssignClipProp(so, "backgroundMusicClip", bgm1);
                changed |= AssignClipProp(so, "backgroundMusicClip2", bgm2);
                changed |= AssignClipProp(so, "foulClip", foul);
                changed |= AssignClipProp(so, "rackWinClip", rackWin);
                changed |= AssignClipProp(so, "rackLoseClip", rackLose);
                changed |= AssignClipProp(so, "timerTickClip", timerTick);
                changed |= AssignClipProp(so, "matchWinClip", matchWin);
                changed |= AssignClipProp(so, "matchLoseClip", matchLose);

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(lib);
                }
            }
        }

        private static bool AssignClipProp(SerializedObject so, string propName, AudioClip clip)
        {
            if (clip == null) return false;
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null && prop.objectReferenceValue != clip)
            {
                prop.objectReferenceValue = clip;
                return true;
            }
            return false;
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
            string localStatusPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "build_status.txt");
            Action<string> writeStatus = (msg) =>
            {
                try { File.WriteAllText(statusFilePath, msg); } catch { }
                try { File.WriteAllText(localStatusPath, msg); } catch { }
            };

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
                        writeStatus("FAILED: " + err);
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
                writeStatus("BUILDING...");

                BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
                BuildSummary summary = report.summary;

                if (summary.result == BuildResult.Succeeded)
                {
                    string info = $"SUCCESS: File={fullExecutablePath}, Size={summary.totalSize / (1024 * 1024):F2}MB, Duration={summary.totalTime.TotalSeconds:F1}s";
                    Debug.Log($"<color=green>✓ XÂY DỰNG THÀNH CÔNG! {info}</color>");
                    writeStatus(info);
                }
                else
                {
                    string err = $"FAILED: Errors={summary.totalErrors}, Warnings={summary.totalWarnings}";
                    Debug.LogError($"<color=red>✗ XÂY DỰNG THẤT BẠI! {err}</color>");
                    writeStatus(err);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                writeStatus("EXCEPTION: " + ex.Message);
            }
        }
    }
}
#endif
