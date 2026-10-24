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
        private CueController cueController;

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
        public bool IsHeadStringOnly => behindHeadStringOnly;
        public bool IsPlacementValid => isPlacementValid;
        public Vector3 CurrentPosition => currentPosition;

        /// <summary>
        /// Do PlayerInputController gán: trả về true khi đang là lượt đối thủ (khóa phím/chuột đặt bi của người chơi).
        /// AI vẫn đặt bi qua TryPlaceBallAt/ConfirmPlacement nên không bị ảnh hưởng.
        /// </summary>
        public Func<bool> InputBlockedPredicate;

        // Chỉ báo 4 mũi tên xanh quanh bi cái khi đang đặt bi
        private GameObject arrowRoot;
        private Transform[] arrowTransforms;
        private Material arrowMaterial;
        private Mesh arrowMesh;
        private MaterialPropertyBlock tintBlock;
        private bool tintApplied = false;

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
                // Vị trí mặc định: Head Spot nếu chỉ trong vùng đầu bàn, hoặc giữa bàn
                currentPosition = headStringOnly ? tableWrapper.HeadSpot : new Vector3(0f, tableWrapper.ClothSurfaceY + 0.02855f, 0f);

                // Nếu vị trí ban đầu bị đè bi, tìm vị trí trống lân cận
                currentPosition = FindSafePlacementNear(currentPosition);

                // Hủy hoạt ảnh rơi lỗ còn dang dở (bi cái vừa bị ăn có thể đang rơi xuống sàn) và khôi phục bi về mặt bàn
                cueBall.ResetTo(currentPosition, Quaternion.identity);

                // Tắt va chạm của bi cái trong lúc đặt để nó không thể đẩy các bi khác (ResetTo sẽ bật lại khi chốt)
                if (cueBall.SphereCollider != null) cueBall.SphereCollider.enabled = false;
                if (cueBall.Rigidbody != null)
                {
                    cueBall.Rigidbody.velocity = Vector3.zero;
                    cueBall.Rigidbody.angularVelocity = Vector3.zero;
                    cueBall.Rigidbody.isKinematic = true;
                    // Tắt nội suy: nếu không, hình ảnh bi trễ so với vị trí đặt và mũi tên trông như bị rơi ra ngoài
                    cueBall.Rigidbody.interpolation = RigidbodyInterpolation.None;
                    cueBall.Rigidbody.position = currentPosition;
                }
                cueBall.transform.position = currentPosition;
            }

            if (cameraDirector != null)
            {
                if (InputBlockedPredicate != null && InputBlockedPredicate())
                {
                    // Đối thủ đặt bi: camera cuối bàn, thấy trọn mặt bàn
                    cameraDirector.SwitchToOpponentCamera();
                }
                else
                {
                    // Camera nhìn thẳng từ trên xuống, bám theo bi cái để đặt bi chính xác
                    if (cueBall != null) cameraDirector.SetCueBallTarget(cueBall.transform);
                    cameraDirector.SwitchToTopDownCamera();
                }
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

        public bool TryPlaceBallAt(Vector3 targetPosition)
        {
            if (!isActive) return false;
            currentPosition = targetPosition;

            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null)
            {
                MoveCueBallTo(cueBall, currentPosition);
            }

            return ValidatePlacement();
        }

        /// <summary>
        /// Hủy chế độ đặt bi mà không chốt (dùng khi ván/trận kết thúc giữa chừng, ví dụ đầu hàng lúc đang ball-in-hand).
        /// Nếu không hủy, Update() sẽ tiếp tục ẩn cây cơ ở ván sau.
        /// </summary>
        public void CancelBallInHand()
        {
            if (!isActive) return;

            isActive = false;
            SetArrowsVisible(false);
            ApplyCueBallTint(false);

            // Khôi phục vật lý bi cái (va chạm + không kinematic) giống như khi chốt vị trí
            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null)
            {
                cueBall.ResetTo(cueBall.transform.position, Quaternion.identity);
                RestoreCueBallInterpolation(cueBall);
            }
        }

        private void Update()
        {
            // Ẩn cây cơ trong suốt quá trình đặt bi (chỉ hiện lại khi đặt xong)
            if (isActive) HideCueStick();

            if (!isActive) { arrowsWanted = false; SetArrowsVisible(false); ApplyCueBallTint(false); return; }
            if ((InputBlockedPredicate != null && InputBlockedPredicate()) || Game.Billiards.UI.HudModalState.IsModalOpen) { arrowsWanted = false; SetArrowsVisible(false); ApplyCueBallTint(false); return; }

            // Lưu ý: không xoay góc nhìn bằng chuột khi đặt bi — camera top-down giữ nguyên hướng

            HandleKeyboardMovement();
            HandleConfirmationInput();
            arrowsWanted = isActive;
            ApplyCueBallTint(!isPlacementValid);
        }

        // Mũi tên cập nhật sau cùng trong frame, bám theo vị trí hiển thị thật của bi cái
        private void LateUpdate()
        {
            if (isActive && arrowsWanted) UpdateArrows();
        }

        private bool arrowsWanted = false;

        /// <summary>Di chuyển bi cái (kinematic) tới vị trí mới, đồng bộ cả Rigidbody lẫn Transform.</summary>
        private void MoveCueBallTo(BallController cueBall, Vector3 pos)
        {
            if (cueBall.Rigidbody != null) cueBall.Rigidbody.position = pos;
            cueBall.transform.position = pos;
        }

        private void RestoreCueBallInterpolation(BallController cueBall)
        {
            if (cueBall != null && cueBall.Rigidbody != null)
            {
                cueBall.Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        private void HideCueStick()
        {
            if (cueController == null) cueController = FindObjectOfType<CueController>();
            if (cueController != null) cueController.SetAimingState(false);
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
                GetCameraPlanarAxes(out Vector3 camRight, out Vector3 camForward);
                Vector3 moveDelta = (camRight * h + camForward * v).normalized * (moveSpeed * Time.deltaTime);
                currentPosition += moveDelta;

                // Bi cái không được xuyên qua các bi khác: đẩy ngược ra và trượt dọc theo bề mặt bi
                currentPosition = ResolveBallPenetration(currentPosition);

                // Giới hạn trong mặt bàn
                if (tableWrapper != null)
                {
                    currentPosition = tableWrapper.ClampToPlayArea(currentPosition, 0.035f);
                }
                currentPosition = ResolveBallPenetration(currentPosition);

                var cueBall = ballManager != null ? ballManager.CueBall : null;
                if (cueBall != null)
                {
                    MoveCueBallTo(cueBall, currentPosition);
                }

                ValidatePlacement();
            }
        }

        private void HandleConfirmationInput()
        {
            // Chỉ dùng Enter để chốt vị trí bi cái, sau đó chuyển sang ngắm bắn
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
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
            SetArrowsVisible(false);
            ApplyCueBallTint(false);

            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall != null)
            {
                cueBall.ResetTo(currentPosition, Quaternion.identity);
                RestoreCueBallInterpolation(cueBall);
            }

            if (cameraDirector != null)
            {
                cameraDirector.SetCueBallTarget(cueBall != null ? cueBall.transform : null);
                if (InputBlockedPredicate != null && InputBlockedPredicate())
                {
                    // Lượt đối thủ vẫn giữ camera cuối bàn
                    cameraDirector.SwitchToOpponentCamera();
                }
                else
                {
                    cameraDirector.SwitchToAimCamera();
                }
            }

            OnBallInHandConfirmed?.Invoke(currentPosition);
        }

        private float GetBallRadius()
        {
            return (ballManager != null && ballManager.PhysicsConfig != null) ? ballManager.PhysicsConfig.BallRadius : 0.02855f;
        }

        /// <summary>
        /// Đẩy bi cái ra khỏi mọi bi khác (khoảng cách tâm tối thiểu = 2R) để không thể xuyên qua bi khi đặt.
        /// </summary>
        private Vector3 ResolveBallPenetration(Vector3 pos)
        {
            if (ballManager == null) return pos;

            float minDist = GetBallRadius() * 2f;
            var balls = ballManager.GetActiveObjectBalls();

            for (int iter = 0; iter < 3; iter++)
            {
                bool moved = false;
                for (int i = 0; i < balls.Count; i++)
                {
                    var ball = balls[i];
                    if (ball == null || ball.IsPocketed || !ball.gameObject.activeInHierarchy) continue;

                    Vector3 diff = new Vector3(pos.x - ball.transform.position.x, 0f, pos.z - ball.transform.position.z);
                    float dist = diff.magnitude;
                    if (dist < minDist)
                    {
                        Vector3 dir = dist > 0.0001f ? diff / dist : Vector3.right;
                        pos.x = ball.transform.position.x + dir.x * minDist;
                        pos.z = ball.transform.position.z + dir.z * minDist;
                        moved = true;
                    }
                }
                if (!moved) break;
            }

            return pos;
        }

        /// <summary>
        /// Bi cái chuyển đỏ nhạt khi vị trí đặt không hợp lệ (chạm bi khác, gần lỗ, ngoài vùng cho phép).
        /// </summary>
        private void ApplyCueBallTint(bool invalid)
        {
            if (tintApplied == invalid) return;

            var cueBall = ballManager != null ? ballManager.CueBall : null;
            if (cueBall == null) return;

            var renderers = cueBall.GetComponentsInChildren<Renderer>(true);
            if (invalid)
            {
                if (tintBlock == null) tintBlock = new MaterialPropertyBlock();
                Color lightRed = new Color(1f, 0.5f, 0.5f, 1f);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].GetPropertyBlock(tintBlock);
                    tintBlock.SetColor("_BaseColor", lightRed);
                    tintBlock.SetColor("_Color", lightRed);
                    renderers[i].SetPropertyBlock(tintBlock);
                }
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(null);
            }

            tintApplied = invalid;
        }

        /// <summary>
        /// Trục phẳng (XZ) theo hướng nhìn thực tế của camera: W = hướng lên màn hình, D = sang phải màn hình.
        /// </summary>
        private void GetCameraPlanarAxes(out Vector3 right, out Vector3 forward)
        {
            forward = Vector3.forward;
            Transform camT = (cameraDirector != null && cameraDirector.MainCamera != null) ? cameraDirector.MainCamera.transform : null;
            if (camT != null)
            {
                Vector3 f = camT.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.01f)
                {
                    f = camT.up;
                    f.y = 0f;
                }
                if (f.sqrMagnitude > 0.0001f) forward = f.normalized;
            }
            right = Vector3.Cross(Vector3.up, forward).normalized;
        }

        private void EnsureArrows()
        {
            if (arrowRoot != null) return;

            arrowRoot = new GameObject("BallInHandArrows");
            arrowRoot.hideFlags = HideFlags.DontSave;

            // Mũi tên phẳng chỉ về +Z: đầu tam giác + thân chữ nhật
            arrowMesh = new Mesh { name = "BallInHandArrowMesh" };
            arrowMesh.vertices = new[]
            {
                new Vector3(-0.020f, 0f, 0f), new Vector3(0f, 0f, 0.036f), new Vector3(0.020f, 0f, 0f),
                new Vector3(-0.007f, 0f, -0.026f), new Vector3(-0.007f, 0f, 0f), new Vector3(0.007f, 0f, 0f), new Vector3(0.007f, 0f, -0.026f)
            };
            arrowMesh.triangles = new[] { 0, 1, 2, 3, 4, 5, 3, 5, 6 };
            arrowMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            arrowMesh.RecalculateBounds();

            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            arrowMaterial = new Material(shader);
            Color blue = new Color(0.12f, 0.62f, 1f, 1f);
            if (arrowMaterial.HasProperty("_UnlitColor")) arrowMaterial.SetColor("_UnlitColor", blue);
            if (arrowMaterial.HasProperty("_BaseColor")) arrowMaterial.SetColor("_BaseColor", blue);
            if (arrowMaterial.HasProperty("_Color")) arrowMaterial.SetColor("_Color", blue);
            if (arrowMaterial.HasProperty("_CullMode")) arrowMaterial.SetFloat("_CullMode", 0f);
            if (arrowMaterial.HasProperty("_Cull")) arrowMaterial.SetFloat("_Cull", 0f);
            if (arrowMaterial.HasProperty("_EmissiveColor")) arrowMaterial.SetColor("_EmissiveColor", blue * 2f);

            arrowTransforms = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("Arrow_" + i);
                go.hideFlags = HideFlags.DontSave;
                go.transform.SetParent(arrowRoot.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = arrowMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = arrowMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                arrowTransforms[i] = go.transform;
            }
        }

        private void SetArrowsVisible(bool visible)
        {
            if (arrowRoot != null && arrowRoot.activeSelf != visible) arrowRoot.SetActive(visible);
        }

        /// <summary>
        /// 4 mũi tên xanh dưới chân bi cái, chuyển động ra/vào liên tục để báo hiệu đang được đặt bi.
        /// Hướng mũi tên khớp với hướng W/A/S/D theo camera.
        /// </summary>
        private void UpdateArrows()
        {
            EnsureArrows();
            SetArrowsVisible(true);

            var arrowCueBall = ballManager != null ? ballManager.CueBall : null;
            Vector3 center = arrowCueBall != null ? arrowCueBall.transform.position : currentPosition;
            float y = (tableWrapper != null ? tableWrapper.ClothSurfaceY : center.y) + 0.004f;
            arrowRoot.transform.position = new Vector3(center.x, y, center.z);

            GetCameraPlanarAxes(out Vector3 camRight, out Vector3 camForward);
            Vector3[] dirs = { camForward, camRight, -camForward, -camRight };

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float radius = 0.065f + 0.03f * pulse;
            float scale = 1f + 0.2f * pulse;

            for (int i = 0; i < 4; i++)
            {
                arrowTransforms[i].localPosition = dirs[i] * radius;
                arrowTransforms[i].rotation = Quaternion.LookRotation(dirs[i], Vector3.up);
                arrowTransforms[i].localScale = Vector3.one * scale;
            }
        }

        private void OnDestroy()
        {
            if (arrowRoot != null) Destroy(arrowRoot);
            if (arrowMaterial != null) Destroy(arrowMaterial);
            if (arrowMesh != null) Destroy(arrowMesh);
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
