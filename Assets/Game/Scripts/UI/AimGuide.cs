using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Billiards.Core;
using Game.Billiards.Input;
using Game.Billiards.Match;
using Game.Billiards.Physics;
using CameraDirector = Game.Billiards.Camera.CameraDirector;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Thước ngắm: khi người chơi đang ngắm, vẽ đường đi của bi cái tới điểm va chạm đầu tiên,
    /// vòng tròn bi cái tại điểm chạm, hướng bi mục tiêu bị đánh đi và hướng bi cái lệch sau va chạm.
    /// Bật/tắt qua GameSession.AimGuideEnabled (cài đặt trước trận ở Lobby).
    /// </summary>
    public class AimGuide : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BallManager ballManager;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private PlayerInputController inputController;

        [Header("Style")]
        [SerializeField] private float lineWidth = 0.006f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float targetLineLength = 0.35f;
        [SerializeField] private float deflectLineLength = 0.28f;

        private LineRenderer pathLine;
        private LineRenderer ringLine;
        private LineRenderer targetLine;
        private LineRenderer deflectLine;
        private Material whiteMat;
        private Material amberMat;
        private Material iceMat;

        private const int RingSegments = 40;
        private readonly List<RaycastHit> hitBuffer = new List<RaycastHit>();

        public void Initialize(BallManager bMgr, CameraDirector camDir, PlayerInputController inputCtrl)
        {
            ballManager = bMgr;
            cameraDirector = camDir;
            inputController = inputCtrl;
        }

        private void Awake()
        {
            whiteMat = CreateMaterial(new Color(1f, 1f, 1f, 1f));
            amberMat = CreateMaterial(new Color(1f, 0.72f, 0.12f, 1f));
            iceMat = CreateMaterial(new Color(0.35f, 0.85f, 1f, 1f));

            pathLine = CreateLine("AimGuide_Path", whiteMat, 2, false);
            ringLine = CreateLine("AimGuide_Ring", whiteMat, RingSegments, true);
            targetLine = CreateLine("AimGuide_TargetDir", amberMat, 2, false);
            deflectLine = CreateLine("AimGuide_CueDeflect", iceMat, 2, false);

            SetAllVisible(false);
        }

        private void OnDestroy()
        {
            if (whiteMat != null) Destroy(whiteMat);
            if (amberMat != null) Destroy(amberMat);
            if (iceMat != null) Destroy(iceMat);
        }

        private void OnDisable()
        {
            SetAllVisible(false);
        }

        private Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var mat = new Material(shader);
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color * 2f);
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            return mat;
        }

        private LineRenderer CreateLine(string lineName, Material mat, int positions, bool loop)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = positions;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;
            return lr;
        }

        private void SetAllVisible(bool visible)
        {
            if (pathLine != null) pathLine.enabled = visible;
            if (ringLine != null) ringLine.enabled = visible;
            if (targetLine != null) targetLine.enabled = visible;
            if (deflectLine != null) deflectLine.enabled = visible;
        }

        private bool ShouldShow()
        {
            if (!GameSession.AimGuideEnabled) return false;
            if (ballManager == null || cameraDirector == null || inputController == null) return false;
            if (!inputController.IsAimingAllowed) return false;
            if (HudModalState.IsModalOpen) return false;

            var cueBall = ballManager.CueBall;
            return cueBall != null && !cueBall.IsPocketed && cueBall.gameObject.activeInHierarchy;
        }

        // Cập nhật sau cùng trong frame để bám đúng hướng ngắm và vị trí bi hiện tại
        private void LateUpdate()
        {
            if (!ShouldShow())
            {
                SetAllVisible(false);
                return;
            }

            var cueBall = ballManager.CueBall;
            float radius = (ballManager.PhysicsConfig != null) ? ballManager.PhysicsConfig.BallRadius : 0.02855f;

            Vector3 origin = cueBall.transform.position;
            Vector3 dir = cameraDirector.AimDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                SetAllVisible(false);
                return;
            }
            dir.Normalize();

            // Đường vẽ nằm sát mặt nỉ (thấp hơn tâm bi) để trông như vạch kẻ trên bàn
            float drawY = origin.y - radius + 0.003f;

            // Cú nhảy bi: bi bay qua mọi vật cản, nên chỉ vẽ đường bay và vòng tròn tại điểm chạm nỉ dự kiến
            var elevation = CueElevationSelector.Instance;
            if (elevation != null && ballManager.PhysicsConfig != null &&
                CueElevationSelector.PredictJump(ballManager.PhysicsConfig, elevation.CurrentAngleDeg,
                    inputController.CurrentPower01, inputController.CueContact01, out float jumpHeight, out float jumpRange))
            {
                Vector3 landing = origin + dir * jumpRange;
                pathLine.positionCount = 2;
                pathLine.SetPosition(0, Flat(origin, drawY));
                pathLine.SetPosition(1, Flat(landing, drawY));
                pathLine.enabled = true;
                DrawRing(landing, radius, drawY);
                ringLine.enabled = true;
                targetLine.enabled = false;
                deflectLine.enabled = false;
                return;
            }

            float hitDistance = maxDistance;
            BallController targetBall = null;
            Vector3 hitNormal = Vector3.zero;
            bool hitSomething = FindFirstHit(origin, dir, radius, cueBall, out hitDistance, out targetBall, out hitNormal);
            if (!hitSomething) hitDistance = maxDistance;

            Vector3 ghost = origin + dir * hitDistance;

            // 1. Đường đi của bi cái
            pathLine.positionCount = 2;
            pathLine.SetPosition(0, Flat(origin, drawY));
            pathLine.SetPosition(1, Flat(ghost, drawY));
            pathLine.enabled = true;

            if (!hitSomething)
            {
                ringLine.enabled = false;
                targetLine.enabled = false;
                deflectLine.enabled = false;
                return;
            }

            // 2. Vòng tròn bi cái tại điểm va chạm
            DrawRing(ghost, radius, drawY);
            ringLine.enabled = true;

            if (targetBall != null)
            {
                // 3. Hướng bi mục tiêu bị đánh đi: từ tâm bi cái (tại điểm chạm) qua tâm bi mục tiêu
                Vector3 targetCenter = targetBall.transform.position;
                Vector3 targetDir = new Vector3(targetCenter.x - ghost.x, 0f, targetCenter.z - ghost.z);
                if (targetDir.sqrMagnitude > 0.000001f)
                {
                    targetDir.Normalize();
                    targetLine.SetPosition(0, Flat(targetCenter, drawY));
                    targetLine.SetPosition(1, Flat(targetCenter + targetDir * targetLineLength, drawY));
                    targetLine.enabled = true;

                    // 4. Hướng bi cái lệch sau va chạm (tiếp tuyến, vuông góc hướng bi mục tiêu)
                    Vector3 tangent = dir - targetDir * Vector3.Dot(dir, targetDir);
                    float mag = tangent.magnitude;
                    if (mag > 0.05f)
                    {
                        tangent /= mag;
                        float len = deflectLineLength * Mathf.Clamp01(mag);
                        deflectLine.SetPosition(0, Flat(ghost, drawY));
                        deflectLine.SetPosition(1, Flat(ghost + tangent * len, drawY));
                        deflectLine.enabled = true;
                    }
                    else
                    {
                        deflectLine.enabled = false;
                    }
                }
                else
                {
                    targetLine.enabled = false;
                    deflectLine.enabled = false;
                }
            }
            else
            {
                // Chạm băng: không có bi mục tiêu, vẽ hướng nảy ra khỏi băng
                targetLine.enabled = false;
                Vector3 n = new Vector3(hitNormal.x, 0f, hitNormal.z);
                if (n.sqrMagnitude > 0.0001f)
                {
                    n.Normalize();
                    Vector3 reflect = dir - 2f * Vector3.Dot(dir, n) * n;
                    deflectLine.SetPosition(0, Flat(ghost, drawY));
                    deflectLine.SetPosition(1, Flat(ghost + reflect.normalized * (deflectLineLength + 0.1f), drawY));
                    deflectLine.enabled = true;
                }
                else
                {
                    deflectLine.enabled = false;
                }
            }
        }

        /// <summary>
        /// Quét một hình cầu bán kính bằng bi cái dọc hướng ngắm, trả về vật cản đầu tiên là bi khác hoặc băng bàn.
        /// Bỏ qua trigger (lỗ), bi cái và các bề mặt gần như nằm ngang (mặt nỉ).
        /// </summary>
        private bool FindFirstHit(Vector3 origin, Vector3 dir, float radius, BallController cueBall,
            out float distance, out BallController targetBall, out Vector3 normal)
        {
            distance = 0f;
            targetBall = null;
            normal = Vector3.zero;

            RaycastHit[] hits = UnityEngine.Physics.SphereCastAll(origin, radius * 0.98f, dir, maxDistance,
                ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return false;

            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;

                var ball = h.collider.GetComponentInParent<BallController>();
                if (ball != null)
                {
                    if (ball == cueBall || ball.IsPocketed) continue;
                    distance = h.distance;
                    targetBall = ball;
                    normal = h.normal;
                    return true;
                }

                // Bỏ qua mặt nỉ / sàn (pháp tuyến hướng lên)
                if (Mathf.Abs(h.normal.y) > 0.6f) continue;
                if (h.distance <= 0.0001f) continue;

                // Chỉ nhận va chạm khi tia ngắm thực sự lao vào bề mặt (hướng ngược pháp tuyến: dot < 0).
                // Bỏ qua các bề mặt mở rộng xuôi theo hướng tia ngắm (như má vát mở rộng của lỗ góc).
                if (Vector3.Dot(dir, h.normal) >= -0.05f) continue;

                distance = h.distance;
                normal = h.normal;
                return true;
            }

            return false;
        }

        private void DrawRing(Vector3 center, float radius, float y)
        {
            ringLine.positionCount = RingSegments;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                ringLine.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * radius, y, center.z + Mathf.Sin(a) * radius));
            }
        }

        private static Vector3 Flat(Vector3 p, float y)
        {
            return new Vector3(p.x, y, p.z);
        }
    }
}
