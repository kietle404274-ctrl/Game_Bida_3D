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

        private bool IsMyTurn()
        {
            if (!isAIEnabled || matchStateManager == null || matchStateManager.State == null)
                return false;

            return matchStateManager.CurrentPlayerIndex == aiPlayerIndex;
        }

        private void HandleTurnChanged(int newPlayerIndex)
        {
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

            // Chờ ngắm bắn trong 0.5s để người chơi kịp quan sát hướng cơ của AI
            yield return new WaitForSeconds(0.6f);

            // 5. Thực thi cú đánh qua ShotExecutor (dùng chung pipeline với người chơi theo Rule A09)
            if (cueController != null)
            {
                cueController.TriggerStrikeAnimation(() =>
                {
                    if (shotExecutor != null)
                    {
                        shotExecutor.ExecuteShot(finalCommand);
                    }
                    if (cameraDirector != null)
                    {
                        cameraDirector.SwitchToShotCamera();
                    }
                });
            }
            else if (shotExecutor != null)
            {
                shotExecutor.ExecuteShot(finalCommand);
                if (cameraDirector != null)
                {
                    cameraDirector.SwitchToShotCamera();
                }
            }

            activeAIRoutine = null;
        }

        private ShotCandidate GenerateBreakCandidate(Vector3 cuePos)
        {
            Vector3 targetApex = (tableWrapper != null) ? tableWrapper.FootSpot : new Vector3(0f, 0.8635f, 0.56f);
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
