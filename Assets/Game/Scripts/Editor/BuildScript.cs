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
        private const string ExecutableName = "Co_Thu_Dai_Chien.exe";

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
                return;
            }

            string flagPath = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "trigger_build.flag");
            if (!File.Exists(flagPath)) return;

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            // Làm mới tài sản trước khi đóng gói; nếu phát sinh biên dịch mới thì giữ lại flag để đợi Domain Reload xong
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

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

            RepairBallMaterialsInProjectAndActiveScene();

            // Thực hiện build
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
                SyncBrandingAndAppIcon();
                SyncAudioLibraryAssets();
                CleanBurstDebugDirs(BuildOutputFolder);
                SyncDocumentationToTaiLieu();
                AssetDatabase.SaveAssets();
            }
            SceneView.RepaintAll();
            Debug.Log("<color=green>[BuildScript] Đã gán lại vật liệu HDRP Balls Billiard.mat cho toàn bộ 16 bi!</color>");
        }

        private static void SyncDocumentationToTaiLieu()
        {
            try
            {
                string taiLieuDir = @"D:\TAI_LIEU_HOC_TAP\DoAnTotNghiep\TaiLieu";
                if (!Directory.Exists(taiLieuDir)) Directory.CreateDirectory(taiLieuDir);

                string tempDir = Path.Combine(Directory.GetCurrentDirectory(), "Temp");
                string srcMd = Path.Combine(tempDir, "Tai_Lieu_Toan_Tap_Co_Thu_Dai_Chien_3D.md");
                if (File.Exists(srcMd))
                {
                    File.Copy(srcMd, Path.Combine(taiLieuDir, "Huong_Dan_Toan_Tap_Co_Thu_Dai_Chien_3D.md"), true);
                }

                string srcDocx = Path.Combine(tempDir, "Huong_Dan_Toan_Tap_Co_Thu_Dai_Chien_3D.docx");
                if (File.Exists(srcDocx))
                {
                    File.Copy(srcDocx, Path.Combine(taiLieuDir, "Huong_Dan_Toan_Tap_Co_Thu_Dai_Chien_3D.docx"), true);
                }
            }
            catch { }
        }

        private static void CleanBurstDebugDirs(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            try
            {
                foreach (string subDir in Directory.GetDirectories(dir, "*_BurstDebugInformation_DoNotShip"))
                {
                    try { Directory.Delete(subDir, true); } catch { }
                }
            }
            catch { }
        }

        private static void SyncBrandingAndAppIcon()
        {
            PlayerSettings.productName = "Cơ Thủ Đại Chiến";
            PlayerSettings.companyName = "CueClashStudio";

            const string iconPath = "Assets/Game/Textures/Icon/AppIcon.png";
            string fullIconPath = Path.Combine(Directory.GetCurrentDirectory(), iconPath);
            if (!File.Exists(fullIconPath)) return;

            AssetDatabase.ImportAsset(iconPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter ti = AssetImporter.GetAtPath(iconPath) as TextureImporter;
            if (ti != null)
            {
                bool dirty = false;
                if (ti.textureType != TextureImporterType.Default) { ti.textureType = TextureImporterType.Default; dirty = true; }
                if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; dirty = true; }
                if (!ti.isReadable) { ti.isReadable = true; dirty = true; }
                if (!ti.mipmapEnabled) { ti.mipmapEnabled = true; dirty = true; }
                if (ti.npotScale != TextureImporterNPOTScale.None) { ti.npotScale = TextureImporterNPOTScale.None; dirty = true; }
                if (ti.maxTextureSize < 1024) { ti.maxTextureSize = 1024; dirty = true; }
                if (dirty) ti.SaveAndReimport();
            }

            Texture2D iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (iconTex != null)
            {
                PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new Texture2D[] { iconTex });
                int[] sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Standalone);
                if (sizes != null && sizes.Length > 0)
                {
                    Texture2D[] standaloneIcons = new Texture2D[sizes.Length];
                    for (int i = 0; i < sizes.Length; i++) standaloneIcons[i] = iconTex;
                    PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, standaloneIcons);
                }
            }
        }

        private static void SyncAudioLibraryAssets()
        {
            string[] libPaths = new[]
            {
                "Assets/Resources/DefaultAudioLibrary.asset",
                "Assets/Game/Configs/DefaultAudioLibrary.asset"
            };

            AssetDatabase.ImportAsset("Assets/Game/Audio/Tieng_Vo_Tay_1.mp3", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/Game/Audio/Tieng_Thua_Game.mp3", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/Resources/Audio/Tieng_Vo_Tay_1.mp3", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset("Assets/Resources/Audio/Tieng_Thua_Game.mp3", ImportAssetOptions.ForceSynchronousImport);

            AudioClip bgm1 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen.mp3");
            AudioClip bgm2 = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Nhac_nen_2.mp3");
            AudioClip foul = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_That_Vong.mp3")
                          ?? AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Loi_Bi.mp3");
            AudioClip rackWin = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Vo_Tay_1.mp3");
            AudioClip rackLose = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game/Audio/Tieng_Thua_Game.mp3");
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

                // Đóng các tiến trình game đang chạy (nếu có) để tránh khóa file khi build
                try
                {
                    string[] procNames = { "Game_Bida_3D", "Co_Thu_Dai_Chien" };
                    foreach (string pName in procNames)
                    {
                        var runningProcs = System.Diagnostics.Process.GetProcessesByName(pName);
                        if (runningProcs != null && runningProcs.Length > 0)
                        {
                            foreach (var proc in runningProcs)
                            {
                                try
                                {
                                    proc.Kill();
                                    proc.WaitForExit(3000);
                                }
                                catch { }
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                    }
                }
                catch { }

                // Dọn file exe và thư mục _Data của tên cũ Game_Bida_3D nếu còn tồn tại
                try
                {
                    string oldExe = Path.Combine(outputDirectory, "Game_Bida_3D.exe");
                    if (File.Exists(oldExe)) File.Delete(oldExe);
                    string oldData = Path.Combine(outputDirectory, "Game_Bida_3D_Data");
                    if (Directory.Exists(oldData)) Directory.Delete(oldData, true);
                }
                catch { }

                // Xóa file exe cũ nếu có để đảm bảo Unity tạo file mới với timestamp cập nhật chính xác
                if (File.Exists(fullExecutablePath))
                {
                    try
                    {
                        File.Delete(fullExecutablePath);
                    }
                    catch (Exception delEx)
                    {
                        writeStatus("FAILED: Cannot delete locked exe: " + delEx.Message);
                        return;
                    }
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
                    // Dọn thư mục BurstDebugInformation_DoNotShip và build_status.txt trong thư mục phát hành để sẵn sàng nén gửi người chơi
                    try
                    {
                        CleanBurstDebugDirs(outputDirectory);
                        if (File.Exists(statusFilePath)) File.Delete(statusFilePath);
                    }
                    catch { }

                    string info = $"SUCCESS: File={fullExecutablePath}, Size={summary.totalSize / (1024 * 1024):F2}MB, Duration={summary.totalTime.TotalSeconds:F1}s";
                    Debug.Log($"<color=green>✓ XÂY DỰNG THÀNH CÔNG! {info}</color>");
                    try { File.WriteAllText(localStatusPath, info); } catch { }
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append($"FAILED ({summary.result}): Errors={summary.totalErrors}, Warnings={summary.totalWarnings}");
                    if (report.steps != null)
                    {
                        foreach (var step in report.steps)
                        {
                            if (step.messages != null)
                            {
                                foreach (var msg in step.messages)
                                {
                                    if (msg.type == LogType.Error || msg.type == LogType.Exception || msg.type == LogType.Warning)
                                    {
                                        sb.Append(" | ").Append(msg.content);
                                    }
                                }
                            }
                        }
                    }
                    string err = sb.ToString();
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
