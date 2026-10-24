using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.Physics;
using Game.Billiards.Match;
using Game.Billiards.Rules;
using Game.Billiards.UI;
using Game.Billiards.Core;
using Game.Billiards.AI;
using Game.Billiards.Camera;

namespace Game.Billiards.Editor
{
    public static class GameplaySceneCheck
    {
        [MenuItem("Tools/Game/Check Gameplay Scene", false, 1)]
        public static void CheckScene()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("=== KIỂM TRA TÌNH TRẠNG SCENE GAMEPLAY.UNITY ===");
            sb.AppendLine("==================================================");

            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Gameplay.unity", OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("KHÔNG THỂ MỞ SCENE Gameplay.unity!");
                return;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            sb.AppendLine($"Số lượng Root GameObjects trong scene: {rootObjects.Length}");
            foreach (var root in rootObjects)
            {
                sb.AppendLine($" - Root: '{root.name}' (active={root.activeSelf}, children={root.transform.childCount})");
            }

            // 1. Kiểm tra TableWrapper
            var tableWrapper = UnityEngine.Object.FindObjectOfType<TableWrapper>();
            if (tableWrapper != null)
            {
                sb.AppendLine($"[TABLE] Đã tìm thấy TableWrapper: {tableWrapper.gameObject.name}, position={tableWrapper.transform.position}");
                sb.AppendLine($"        Playable Half Bounds: Length={tableWrapper.PlayableHalfLength}m, Width={tableWrapper.PlayableHalfWidth}m");
                sb.AppendLine($"        FootSpot: {tableWrapper.FootSpot}, HeadSpot: {tableWrapper.HeadSpot}");
            }
            else
            {
                sb.AppendLine("[TABLE ERROR] Không tìm thấy TableWrapper trong scene!");
            }

            // 2. Kiểm tra BallsRoot và 16 bi
            var balls = UnityEngine.Object.FindObjectsOfType<BallController>();
            sb.AppendLine($"[BALLS] Tìm thấy {balls.Length}/16 BallController trong scene.");
            Array.Sort(balls, (a, b) => a.BallNumber.CompareTo(b.BallNumber));
            foreach (var b in balls)
            {
                sb.AppendLine($"  - Bi #{b.BallNumber:D2} ({b.name}): Pos={b.transform.position}, Active={b.gameObject.activeSelf}");
            }

            // 3. Kiểm tra Cue Stick
            var cueController = UnityEngine.Object.FindObjectOfType<CueController>();
            if (cueController != null)
            {
                sb.AppendLine($"[CUE] Đã tìm thấy CueController: {cueController.gameObject.name}, pos={cueController.transform.position}, active={cueController.gameObject.activeSelf}");
            }
            else
            {
                sb.AppendLine("[CUE WARNING] Không tìm thấy CueController trong scene!");
            }

            // 4. Kiểm tra GameplayCanvas & UI
            var hud = UnityEngine.Object.FindObjectOfType<GameplayHUD>();
            var powerBar = UnityEngine.Object.FindObjectOfType<PowerBarUI>();
            var contactSelector = UnityEngine.Object.FindObjectOfType<CueBallContactSelector>();
            var ballTracker = UnityEngine.Object.FindObjectOfType<BallTrackerUI>();
            var settingsUI = UnityEngine.Object.FindObjectOfType<SettingsUI>(true);

            sb.AppendLine("[UI] Tình trạng UI Canvas:");
            sb.AppendLine($"  - GameplayHUD: {(hud != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - PowerBarUI: {(powerBar != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - CueBallContactSelector: {(contactSelector != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - BallTrackerUI: {(ballTracker != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - SettingsUI: {(settingsUI != null ? "OK" : "THIẾU")}");

            // 5. Kiểm tra Systems
            var turnManager = UnityEngine.Object.FindObjectOfType<TurnManager>();
            var ballManager = UnityEngine.Object.FindObjectOfType<BallManager>();
            var ruleEngine = UnityEngine.Object.FindObjectOfType<Wpa8BallRuleEngine>();
            var shotExecutor = UnityEngine.Object.FindObjectOfType<ShotExecutor>();
            var aiController = UnityEngine.Object.FindObjectOfType<AIController>();
            var cameraDirector = UnityEngine.Object.FindObjectOfType<CameraDirector>();

            sb.AppendLine("[SYSTEMS] Tình trạng các Manager:");
            sb.AppendLine($"  - TurnManager: {(turnManager != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - BallManager: {(ballManager != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - Wpa8BallRuleEngine: {(ruleEngine != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - ShotExecutor: {(shotExecutor != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - AIController: {(aiController != null ? "OK" : "THIẾU")}");
            sb.AppendLine($"  - CameraDirector: {(cameraDirector != null ? "OK" : "THIẾU")}");

            sb.AppendLine("==================================================");
            Debug.Log(sb.ToString());
        }
    }
}
