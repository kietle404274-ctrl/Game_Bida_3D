using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Match;
using Game.Billiards.Physics;

namespace Game.Billiards.AI
{
    public class AIController : MonoBehaviour
    {
        [Header("AI Configuration")]
        [SerializeField] private bool isAIEnabled = true;
        [SerializeField] private int aiPlayerIndex = 1; // Mặc định Player B (1) là AI
        [SerializeField] private AIDifficulty difficulty = AIDifficulty.Normal;

        [Header("System References")]
        [SerializeField] private MatchStateManager matchStateManager;
        [SerializeField] private TurnManager turnManager;
        [SerializeField] private BallManager ballManager;
        [SerializeField] private TableWrapper tableWrapper;
        [SerializeField] private ShotExecutor shotExecutor;
        [SerializeField] private CueController cueController;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private BallInHandController ballInHandController;

        [Header("Pacing")]
        [SerializeField] private float thinkDurationSeconds = 1.0f;
        [Tooltip("Thời gian AI loay hoay ngắm (ngẫu nhiên trong khoảng này) trước khi đánh")]
        [SerializeField] private float aimWanderMinSeconds = 2.0f;
        [SerializeField] private float aimWanderMaxSeconds = 3.0f;

        [Header("Debug")]
        [Tooltip("Bật để AI cố tình đánh bi cái xuống lỗ (scratch) mỗi lượt — dùng test đặt bi. Trong lúc chơi bấm F9 để bật/tắt.")]
        [SerializeField] private bool debugForceFoul = false;

        private AIDifficultyProfile currentProfile;
        private Coroutine activeAIRoutine;

        public bool IsAIEnabled => isAIEnabled;
        public AIDifficulty Difficulty => difficulty;
        public AIDifficultyProfile CurrentProfile => currentProfile;

        public event Action<ShotCandidate> OnAIDecisionMade;

        private void Awake()
        {
            ApplyDifficultyProfile();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                debugForceFoul = !debugForceFoul;
                Debug.Log($"[AIController] DEBUG ep AI danh loi (scratch): {(debugForceFoul ? "BAT" : "TAT")}");
            }
        }

        public void Initialize(
            MatchStateManager stateMgr, 
            TurnManager tManager, 
            BallManager bManager, 
            TableWrapper tWrapper, 
            ShotExecutor executor, 
            CueController cueCtrl, 
            CameraDirector camDir, 
            BallInHandController bihCtrl)
        {
            matchStateManager = stateMgr;
            turnManager = tManager;
            ballManager = bManager;
            tableWrapper = tWrapper;
            shotExecutor = executor;
            cueController = cueCtrl;
            cameraDirector = camDir;
            ballInHandController = bihCtrl;

            ApplyDifficultyProfile();
            SubscribeEvents();
        }

        public void SetDifficulty(AIDifficulty newDifficulty)
        {
            difficulty = newDifficulty;
            ApplyDifficultyProfile();
        }

        public void SetAIEnabled(bool enabled, int playerIndex = 1)
        {
            isAIEnabled = enabled;
            aiPlayerIndex = playerIndex;
        }

        private void ApplyDifficultyProfile()
        {
            currentProfile = AIDifficultyProfile.GetProfile(difficulty);
        }

        private void SubscribeEvents()
        {
            if (matchStateManager != null)
            {
                matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                matchStateManager.OnPhaseChanged += HandlePhaseChanged;

                matchStateManager.OnRackEnded -= HandleRackEnded;
                matchStateManager.OnRackEnded += HandleRackEnded;
                matchStateManager.OnMatchWon -= HandleMatchWon;
                matchStateManager.OnMatchWon += HandleMatchWon;
            }

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
                turnManager.OnTurnChanged += HandleTurnChanged;
            }
        }

        private void OnDestroy()
        {
            if (matchStateManager != null)
            {
                matchStateManager.OnPhaseChanged -= HandlePhaseChanged;
                matchStateManager.OnRackEnded -= HandleRackEnded;
                matchStateManager.OnMatchWon -= HandleMatchWon;
            }

            if (turnManager != null)
            {
                turnManager.OnTurnChanged -= HandleTurnChanged;
            }

            if (activeAIRoutine != null)
            {
                StopCoroutine(activeAIRoutine);
                activeAIRoutine = null;
            }
        }

        private void HandleRackEnded(Core.RackOutcome outcome)
        {
            CancelActiveRoutine();
        }

        private void HandleMatchWon(int winnerIndex)
        {
            CancelActiveRoutine();
        }

        /// <summary>Dừng mọi hành động đang dở của AI (suy nghĩ, ngắm, đặt bi) khi ván/trận kết thúc.</summary>
        private void CancelActiveRoutine()
        {
            if (activeAIRoutine != null)
            {
                StopCoroutine(activeAIRoutine);
                activeAIRoutine = null;
            }
        }

        private bool IsShotAllowedPhase()
        {
            if (matchStateManager == null) return false;
            var p = matchStateManager.CurrentPhase;
            return p == MatchPhase.Aiming || p == MatchPhase.BreakSetup;
        }

        private bool IsMyTurn()
        {
            if (!isAIEnabled || matchStateManager == null || matchStateManager.State == null)
                return false;

            return matchStateManager.CurrentPlayerIndex == aiPlayerIndex;
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
            Debug.Log($"[AIController] TurnChanged -> {newPlayerIndex} (aiIndex={aiPlayerIndex}, enabled={isAIEnabled})");
            if (newPlayerIndex == aiPlayerIndex && isAIEnabled)
            {
                EvaluateCurrentStateAndAct();
            }
        }

        private void HandlePhaseChanged(MatchPhase newPhase)
        {
            if (IsMyTurn())
            {
                EvaluateCurrentStateAndAct();
            }
        }

        private void EvaluateCurrentStateAndAct()
        {
            if (!IsMyTurn()) return;

            if (activeAIRoutine != null)
            {
                StopCoroutine(activeAIRoutine);
            }

            MatchPhase phase = matchStateManager.CurrentPhase;
            Debug.Log($"[AIController] Evaluate: phase={phase}");

            if (phase == MatchPhase.BallInHand)
            {
                activeAIRoutine = StartCoroutine(AIBallInHandRoutine());
            }
            else if (phase == MatchPhase.Aiming || phase == MatchPhase.BreakSetup)
            {
                activeAIRoutine = StartCoroutine(AIAimingAndShotRoutine());
            }
        }

        private IEnumerator AIBallInHandRoutine()
        {
            if (cameraDirector != null) cameraDirector.SwitchToOpponentCamera();

            // Khoảng nghỉ tạo nhịp điệu tự nhiên như cơ thủ đang suy nghĩ
            yield return new WaitForSeconds(thinkDurationSeconds * 0.7f);

            if (!IsMyTurn() || matchStateManager.CurrentPhase != MatchPhase.BallInHand)
            {
                activeAIRoutine = null;
                yield break;
            }

            bool headStringOnly = (ballInHandController != null) && ballInHandController.IsHeadStringOnly;
            Vector3 bestPos = BallInHandPlanner.FindBestPlacement(
                headStringOnly, 
                matchStateManager.State, 
                ballManager, 
                tableWrapper, 
                currentProfile
            );

            if (ballInHandController != null)
            {
                ballInHandController.TryPlaceBallAt(bestPos);
                yield return new WaitForSeconds(0.4f);
                ballInHandController.ConfirmPlacement();
            }

            activeAIRoutine = null;
        }

        private IEnumerator AIAimingAndShotRoutine()
        {
            bool isBreakNow = matchStateManager.State.turnSequence == 0 || matchStateManager.CurrentPhase == MatchPhase.BreakSetup;
            if (cameraDirector != null)
            {
                // Cú phá bi: giữ camera đầu bàn nhìn vào dàn bi; các lượt thường: camera cuối bàn thấy toàn bàn
                if (isBreakNow) cameraDirector.SwitchToAimCamera();
                else cameraDirector.SwitchToOpponentCamera();
            }

            // Suy nghĩ trước khi ra quyết định
            yield return new WaitForSeconds(thinkDurationSeconds);

            if (!IsMyTurn() || (matchStateManager.CurrentPhase != MatchPhase.Aiming && matchStateManager.CurrentPhase != MatchPhase.BreakSetup))
            {
                activeAIRoutine = null;
                yield break;
            }

            var cueBall = (ballManager != null) ? ballManager.CueBall : null;
            if (cueBall == null)
            {
                activeAIRoutine = null;
                yield break;
            }

            Vector3 cuePos = cueBall.transform.position;
            ShotCandidate chosenCandidate = null;

            // 1. Trường hợp Khai cuộc (Break Shot)
            if (matchStateManager.State.turnSequence == 0 || matchStateManager.CurrentPhase == MatchPhase.BreakSetup)
            {
                chosenCandidate = GenerateBreakCandidate(cuePos);
            }
            else
            {
                // 2. Lượt đánh thường: Lập danh sách ứng viên (Pot candidates)
                var potCandidates = ShotCandidateGenerator.GenerateAllPotCandidates(
                    cuePos, 
                    matchStateManager.State, 
                    ballManager, 
                    tableWrapper, 
                    currentProfile
                );

                // Tạo phương án An Toàn (Safety)
                var safetyCandidate = SafetyShotGenerator.GenerateBestSafety(
                    cuePos, 
                    matchStateManager.State, 
                    ballManager, 
                    tableWrapper, 
                    currentProfile
                );

                List<ShotCandidate> allCandidates = new List<ShotCandidate>(potCandidates);
                if (safetyCandidate != null)
                {
                    allCandidates.Add(safetyCandidate);
                }

                chosenCandidate = UtilityEvaluator.SelectBestCandidate(allCandidates, currentProfile);

                // Fallback nếu không tìm được ứng viên nào hợp lệ
                if (chosenCandidate == null)
                {
                    chosenCandidate = safetyCandidate ?? GenerateFallbackShot(cuePos);
                }
            }

            OnAIDecisionMade?.Invoke(chosenCandidate);

            // 3. Chuyển candidate thành ShotCommand có thêm sai số theo cấp độ khó
            ShotCommand finalCommand = chosenCandidate.ToShotCommand(aiPlayerIndex, currentProfile);

            // DEBUG: ép AI đánh bi cái thẳng xuống lỗ gần nhất để test nhanh đặt bi
            if (debugForceFoul && tableWrapper != null && tableWrapper.Pockets != null)
            {
                float bestDist = float.MaxValue;
                Vector3 bestDir = finalCommand.direction;
                foreach (var pocket in tableWrapper.Pockets)
                {
                    if (pocket == null) continue;
                    Vector3 to = pocket.PocketPosition - cuePos;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f && to.sqrMagnitude < bestDist)
                    {
                        bestDist = to.sqrMagnitude;
                        bestDir = to.normalized;
                    }
                }
                finalCommand.direction = bestDir;
                finalCommand.power01 = 0.6f;
            }

            // 4. Đồng bộ hướng ngắm hiển thị gậy cơ và camera
            if (cameraDirector != null)
            {
                cameraDirector.SetCueBallTarget(cueBall.transform);
            }

            if (cueController != null)
            {
                cueController.SetAimingState(true);
                cueController.UpdateAim(finalCommand.direction, finalCommand.power01);
            }

            // Loay hoay ngắm 2-3 giây cho giống người thật: cơ lắc qua lại quanh hướng đã chọn rồi thu hẹp dần về đúng hướng
            float wanderDuration = UnityEngine.Random.Range(aimWanderMinSeconds, aimWanderMaxSeconds);
            float wanderTimer = 0f;
            float phaseA = UnityEngine.Random.Range(0f, 6.28f);
            float phaseB = UnityEngine.Random.Range(0f, 6.28f);
            float startOffset = UnityEngine.Random.Range(8f, 16f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

            while (wanderTimer < wanderDuration)
            {
                if (!IsMyTurn() || !IsShotAllowedPhase()) { activeAIRoutine = null; yield break; }

                float t = wanderTimer / wanderDuration;
                float damping = 1f - Mathf.SmoothStep(0f, 1f, t); // 1 -> 0
                float wobbleDeg = (startOffset * Mathf.Cos(t * 3.2f) + 3.5f * Mathf.Sin(wanderTimer * 5.1f + phaseA)) * damping;
                float powerWobble = 0.07f * Mathf.Sin(wanderTimer * 3.7f + phaseB) * damping;

                Vector3 wobbleDir = Quaternion.Euler(0f, wobbleDeg, 0f) * finalCommand.direction;
                if (cueController != null)
                {
                    cueController.UpdateAim(wobbleDir, Mathf.Clamp01(finalCommand.power01 + powerWobble));
                }

                wanderTimer += Time.deltaTime;
                yield return null;
            }

            if (!IsMyTurn() || !IsShotAllowedPhase()) { activeAIRoutine = null; yield break; }

            if (cueController != null)
            {
                cueController.UpdateAim(finalCommand.direction, finalCommand.power01);
            }

            // Dừng ngắn trước khi hạ cơ đánh
            yield return new WaitForSeconds(0.25f);

            // 5. Thực thi cú đánh qua ShotExecutor (dùng chung pipeline với người chơi theo Rule A09)
            if (cueController != null)
            {
                cueController.TriggerStrikeAnimation(() =>
                {
                    if (shotExecutor != null && IsShotAllowedPhase())
                    {
                        shotExecutor.ExecuteShot(finalCommand);
                    }
                    if (cameraDirector != null)
                    {
                        cameraDirector.SwitchToOpponentCamera();
                    }
                });
            }
            else if (shotExecutor != null)
            {
                shotExecutor.ExecuteShot(finalCommand);
                if (cameraDirector != null)
                {
                    cameraDirector.SwitchToOpponentCamera();
                }
            }

            activeAIRoutine = null;
        }

        private ShotCandidate GenerateBreakCandidate(Vector3 cuePos)
        {
            Vector3 targetApex = (tableWrapper != null) ? tableWrapper.FootSpot : new Vector3(-0.56f, 0.8635f, 0f);
            Vector3 breakDir = (targetApex - cuePos);
            breakDir.y = 0f;
            breakDir = breakDir.normalized;

            // Random nhẹ góc break theo profile
            float breakPower = UnityEngine.Random.Range(0.85f, 0.98f);

            return new ShotCandidate
            {
                targetBallNumber = 1,
                targetPocketId = -1,
                aimDirection = breakDir,
                estimatedPower01 = breakPower,
                estimatedSpin = new Vector2(0f, 0.15f), // Topspin nhẹ
                isSafety = false,
                isLegalBreak = true,
                finalUtilityScore = 1.0f
            };
        }

        private ShotCandidate GenerateFallbackShot(Vector3 cuePos)
        {
            Vector3 forwardDir = Vector3.forward;
            return new ShotCandidate
            {
                targetBallNumber = -1,
                targetPocketId = -1,
                aimDirection = forwardDir,
                estimatedPower01 = 0.35f,
                estimatedSpin = Vector2.zero,
                isSafety = false,
                finalUtilityScore = 0.1f
            };
        }
    }
}
