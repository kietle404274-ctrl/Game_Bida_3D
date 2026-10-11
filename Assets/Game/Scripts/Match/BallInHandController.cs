using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Camera;
using Game.Billiards.Physics;

namespace Game.Billiards.Match
{
    public class BallInHandController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallManager ballManager;
        [SerializeField] private TableWrapper tableWrapper;
        [SerializeField] private CameraDirector cameraDirector;

        [Header("Movement & Tuning")]
        [Tooltip("Tốc độ di chuyển bi cái khi dùng phím W/A/S/D (m/s)")]
        [SerializeField] private float moveSpeed = 1.0f;

        [Tooltip("Khoảng đệm an toàn giữa 2 quả bóng để không va chạm (m)")]
        [SerializeField] private float overlapBuffer = 0.005f;

        [Tooltip("Khoảng cách tối thiểu từ tâm bi tới tâm lỗ bida")]
        [SerializeField] private float minDistanceToPocket = 0.09f;

        private bool isActive = false;
        private bool behindHeadStringOnly = false;
        private bool isPlacementValid = true;
        private Vector3 currentPosition;

        public bool IsActive => isActive;
        public bool BehindHeadStringOnly => behindHeadStringOnly;
        public bool IsPlacementValid => isPlacementValid;
        public Vector3 CurrentPosition => currentPosition;

        public event Action<Vector3> OnBallInHandConfirmed;
        public event Action<bool> OnValidityChanged;

        public void Initialize(BallManager bManager, TableWrapper tWrapper, CameraDirector camDirector)
        {
            ballManager = bManager;
            tableWrapper = tWrapper;
            cameraDirector = camDirector;
        }

        public void EnterBallInHand(bool headStringOnly = false)
        {
            if (ballManager == null || tableWrapper == null) return;

            behindHeadStringOnly = headStringOnly;
            isActive = true;

            var cueBall = ballManager.CueBall;
            if (cueBall != null)
            {
                cueBall.gameObject.SetActive(true);
                if (cueBall.Rigidbody != null)
                {
                    cueBall.Rigidbody.isKinematic = true;
                    cueBall.Rigidbody.velocity = Vector3.zero;
                    cueBall.Rigidbody.angularVelocity = Vector3.zero;
                }

                // Vị trí mặc định: Head Spot nếu chỉ trong vùng đầu bàn, hoặc giữa bàn
                currentPosition = headStringOnly ? tableWrapper.HeadSpot : new Vector3(0f, tableWrapper.ClothSurfaceY + 0.02855f, 0f);

                // Nếu vị trí ban đầu bị đè bi, tìm vị trí trống lân cận
                currentPosition = FindSafePlacementNear(currentPosition);
                cueBall.transform.position = currentPosition;
            }

            if (cameraDirector != null)
            {
                // Chuyển sang góc nhìn rộng bao quát để đặt bi thuận tiện
                cameraDirector.SwitchToShotCamera();
            }

            ValidatePlacement();
        }

        public bool ValidatePlacement()
        {
            if (tableWrapper == null || ballManager == null) return false;

            float radius = 0.02855f;
            if (ballManager.PhysicsConfig != null)
            {
                radius = ballManager.PhysicsConfig.BallRadius;
            }

            bool valid = true;

            // 1. Kiểm tra trong phạm vi mặt bàn
            if (!tableWrapper.IsInsidePlayArea(currentPosition, radius + 0.01f))
            {
                valid = false;
            }

            // 2. Kiểm tra quy tắc Head String (nếu foul on break)
            if (valid && behindHeadStringOnly)
            {
                if (!tableWrapper.IsBehindHeadString(currentPosition))
                {
                    valid = false;
                }
            }

            // 3. Kiểm tra không đè lên bất kỳ quả bi nào khác trên bàn
            if (valid)
            {
                float minBallDist = (radius * 2f) + overlapBuffer;
                List<BallController> activeBalls = ballManager.GetActiveObjectBalls();
                for (int i = 0; i < activeBalls.Count; i++)
                {
                    var ball = activeBalls[i];
                    if (ball != null && !ball.IsPocketed && ball.gameObject.activeInHierarchy)
                    {
                        float dist = Vector3.Distance(new Vector3(currentPosition.x, 0f, currentPosition.z),
                                                      new Vector3(ball.transform.position.x, 0f, ball.transform.position.z));
                        if (dist < minBallDist)
                        {
                            valid = false;
                            break;
                        }
                    }
                }
            }

            // 4. Kiểm tra không nằm trong miệng lỗ
            if (valid)
            {
                var pockets = tableWrapper.Pockets;
                for (int i = 0; i < pockets.Length; i++)
                {
                    if (pockets[i] != null)
                    {
                        float dist = Vector3.Distance(new Vector3(currentPosition.x, 0f, currentPosition.z),
                                                      new Vector3(pockets[i].PocketPosition.x, 0f, pockets[i].PocketPosition.z));
                        if (dist < minDistanceToPocket)
                        {
                            valid = false;
                            break;
                        }
                    }
                }
            }

            if (valid != isPlacementValid)
            {
                isPlacementValid = valid;
                OnValidityChanged?.Invoke(isPlacementValid);
            }

            return isPlacementValid;
        }

        private Vector3 FindSafePlacementNear(Vector3 center)
        {
            Vector3 testPos = center;
            if (IsPositionValid(testPos)) return testPos;

            // Thử các độ dời nhỏ theo hình xoắn ốc
            for (float r = 0.05f; r < 0.5f; r += 0.04f)
            {
                for (int angle = 0; angle < 360; angle += 45)
                {
                    float rad = angle * Mathf.Deg2Rad;
                    Vector3 candidate = center + new Vector3(Mathf.Cos(rad) * r, 0f, Mathf.Sin(rad) * r);
                    if (IsPositionValid(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return center;
        }

        public bool IsPositionValid(Vector3 pos)
        {
            currentPosition = pos;
            return ValidatePlacement();
        }

        private void Update()
        {
            if (!isActive) return;

            HandleKeyboardMovement();
            HandleConfirmationInput();
        }

        private void HandleKeyboardMovement()
        {
            float h = 0f;
            float v = 0f;

            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) v += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) v -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) h += 1f;

            if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            {
                Vector3 moveDelta = new Vector3(h, 0f, v).normalized * (moveSpeed * Time.deltaTime);
                currentPosition += moveDelta;

                // Giới hạn trong mặt bàn
                if (tableWrapper != null)
                {
                    currentPosition = tableWrapper.ClampToPlayArea(currentPosition, 0.035f);
                }

                var cueBall = ballManager != null ? ballManager.CueBall : null;
                if (cueBall != null)
                {
                    cueBall.transform.position = currentPosition;
                }

                ValidatePlacement();
            }
        }

        private void HandleConfirmationInput()
        {
            // Enter hoặc Chuột trái để chốt vị trí bi cái
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter) || UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (isPlacementValid)
                {
                    ConfirmPlacement();
                }
                else
                {
                    Debug.LogWarning("[BallInHand] Vị trí đặt bi không hợp lệ! Vui lòng di chuyển đến vùng trống hợp lệ.");
                }
            }
        }

        public void ConfirmPlacement()
        {
            if (!isPlacementValid) return;

            isActive = false;

            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null)
            {
                cueBall.ResetTo(currentPosition, Quaternion.identity);
            }

            if (cameraDirector != null)
            {
                cameraDirector.SetCueBallTarget(cueBall != null ? cueBall.transform : null);
                cameraDirector.SwitchToAimCamera();
            }

            OnBallInHandConfirmed?.Invoke(currentPosition);
        }

        private void OnDrawGizmos()
        {
            if (!isActive) return;

            // Hiển thị vòng tròn trực quan cho trạng thái hợp lệ/không hợp lệ
            Gizmos.color = isPlacementValid ? Color.green : Color.red;
            Gizmos.DrawWireSphere(currentPosition, 0.04f);
        }
    }
}
