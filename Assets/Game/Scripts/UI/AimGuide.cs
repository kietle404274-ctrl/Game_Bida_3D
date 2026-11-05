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
        [SerializeField] private float lineWidth = 0.0068f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float targetLineLength = 0.35f;
        [SerializeField] private float deflectLineLength = 0.28f;

        private LineRenderer pathLine;
        private LineRenderer ringLine;
        private LineRenderer targetLine;
        private LineRenderer deflectLine;

        // Lớp viền tối chống chìm (Dual-Layer Contrast Underlay) nằm ngay dưới 4 tia chính
        private LineRenderer pathOutline;
        private LineRenderer ringOutline;
        private LineRenderer targetOutline;
        private LineRenderer deflectOutline;

        private Material unifiedAimMat;
        private Material outlineMat;
        private TableTheme appliedTheme = (TableTheme)(-1);

        private const int RingSegments = 40;
        private const float OutlineWidthMultiplier = 1.95f;
        private const float OutlineYOffset = -0.0005f;
        private readonly RaycastHit[] sphereCastHits = new RaycastHit[24];

        public void Initialize(BallManager bMgr, CameraDirector camDir, PlayerInputController inputCtrl)
        {
            ballManager = bMgr;
            cameraDirector = camDir;
            inputController = inputCtrl;
        }

        private void Awake()
        {
            TableTheme initialTheme = TableThemeApplier.ResolveActiveMatchTableTheme();
            Color aimColor = TableThemeApplier.GetAimGuideColorForTheme(initialTheme);
            Color shadowColor = new Color(0.03f, 0.04f, 0.07f, 0.78f);

            unifiedAimMat = CreateMaterial(aimColor);
            outlineMat = CreateMaterial(shadowColor);

            float outlineWidth = lineWidth * OutlineWidthMultiplier;
            pathOutline = CreateLine("AimGuide_Path_Outline", outlineMat, shadowColor, outlineWidth, 2, false, -1);
            ringOutline = CreateLine("AimGuide_Ring_Outline", outlineMat, shadowColor, outlineWidth, RingSegments, true, -1);
            targetOutline = CreateLine("AimGuide_TargetDir_Outline", outlineMat, shadowColor, outlineWidth, 2, false, -1);
            deflectOutline = CreateLine("AimGuide_CueDeflect_Outline", outlineMat, shadowColor, outlineWidth, 2, false, -1);

            pathLine = CreateLine("AimGuide_Path", unifiedAimMat, aimColor, lineWidth, 2, false, 1);
            ringLine = CreateLine("AimGuide_Ring", unifiedAimMat, aimColor, lineWidth, RingSegments, true, 1);
            targetLine = CreateLine("AimGuide_TargetDir", unifiedAimMat, aimColor, lineWidth, 2, false, 1);
            deflectLine = CreateLine("AimGuide_CueDeflect", unifiedAimMat, aimColor, lineWidth, 2, false, 1);

            appliedTheme = initialTheme;
            SetAllVisible(false);
        }

        private void OnDestroy()
        {
            if (unifiedAimMat != null) Destroy(unifiedAimMat);
            if (outlineMat != null) Destroy(outlineMat);
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
            ApplyColorToMaterial(mat, color);
            if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            return mat;
        }

        private static void ApplyColorToMaterial(Material mat, Color color)
        {
            if (mat == null) return;
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissiveColor"))
            {
                // Chỉ phát sáng nhẹ cho tia chính (alpha == 1), tắt phát sáng cho lớp viền tối
                float emissiveMul = color.a >= 0.95f ? 1.6f : 0f;
                mat.SetColor("_EmissiveColor", color * emissiveMul);
            }
        }

        private void SyncThemePaletteIfNeeded()
        {
            TableTheme activeTheme = TableThemeApplier.ResolveActiveMatchTableTheme();
            if (activeTheme == appliedTheme) return;

            appliedTheme = activeTheme;
            Color aimColor = TableThemeApplier.GetAimGuideColorForTheme(activeTheme);
            ApplyColorToMaterial(unifiedAimMat, aimColor);
            SetLineVertexColor(pathLine, aimColor);
            SetLineVertexColor(ringLine, aimColor);
            SetLineVertexColor(targetLine, aimColor);
            SetLineVertexColor(deflectLine, aimColor);
        }

        private static void SetLineVertexColor(LineRenderer lr, Color color)
        {
            if (lr == null) return;
            lr.startColor = color;
            lr.endColor = color;
        }

        private LineRenderer CreateLine(string lineName, Material mat, Color vertexColor, float width, int positions, bool loop, int sortingOrder)
        {
            var go = new GameObject(lineName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = positions;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = vertexColor;
            lr.endColor = vertexColor;
            lr.sortingOrder = sortingOrder;
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

            if (pathOutline != null) pathOutline.enabled = visible;
            if (ringOutline != null) ringOutline.enabled = visible;
            if (targetOutline != null) targetOutline.enabled = visible;
            if (deflectOutline != null) deflectOutline.enabled = visible;
        }

        private static void SetSegmentWithOutline(LineRenderer mainLine, LineRenderer outlineLine, Vector3 p0, Vector3 p1, float drawY)
        {
            mainLine.positionCount = 2;
            mainLine.SetPosition(0, Flat(p0, drawY));
            mainLine.SetPosition(1, Flat(p1, drawY));
            mainLine.enabled = true;

            if (outlineLine != null)
            {
                float outY = drawY + OutlineYOffset;
                outlineLine.positionCount = 2;
                outlineLine.SetPosition(0, Flat(p0, outY));
                outlineLine.SetPosition(1, Flat(p1, outY));
                outlineLine.enabled = true;
            }
        }

        private static void DisableLineWithOutline(LineRenderer mainLine, LineRenderer outlineLine)
        {
            if (mainLine != null) mainLine.enabled = false;
            if (outlineLine != null) outlineLine.enabled = false;
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

            SyncThemePaletteIfNeeded();

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
                SetSegmentWithOutline(pathLine, pathOutline, origin, landing, drawY);
                DrawRing(landing, radius, drawY);
                DisableLineWithOutline(targetLine, targetOutline);
                DisableLineWithOutline(deflectLine, deflectOutline);
                return;
            }

            float hitDistance = maxDistance;
            BallController targetBall = null;
            Vector3 hitNormal = Vector3.zero;
            bool isPocketFacing = false;
            bool hitSomething = FindFirstHit(origin, dir, radius, cueBall, out hitDistance, out targetBall, out hitNormal, out isPocketFacing);
            if (!hitSomething) hitDistance = maxDistance;

            Vector3 ghost = origin + dir * hitDistance;

            // 1. Đường đi của bi cái
            SetSegmentWithOutline(pathLine, pathOutline, origin, ghost, drawY);

            if (!hitSomething)
            {
                DisableLineWithOutline(ringLine, ringOutline);
                DisableLineWithOutline(targetLine, targetOutline);
                DisableLineWithOutline(deflectLine, deflectOutline);
                return;
            }

            // 2. Vòng tròn bi cái tại điểm va chạm
            DrawRing(ghost, radius, drawY);

            if (targetBall != null)
            {
                // 3. Hướng bi mục tiêu bị đánh đi: từ tâm bi cái (tại điểm chạm) qua tâm bi mục tiêu
                Vector3 targetCenter = targetBall.transform.position;
                Vector3 targetDir = new Vector3(targetCenter.x - ghost.x, 0f, targetCenter.z - ghost.z);
                if (targetDir.sqrMagnitude > 0.000001f)
                {
                    targetDir.Normalize();
                    SetSegmentWithOutline(targetLine, targetOutline, targetCenter, targetCenter + targetDir * targetLineLength, drawY);

                    // 4. Hướng bi cái lệch sau va chạm (tiếp tuyến, vuông góc hướng bi mục tiêu)
                    Vector3 tangent = dir - targetDir * Vector3.Dot(dir, targetDir);
                    float mag = tangent.magnitude;
                    if (mag > 0.05f)
                    {
                        tangent /= mag;
                        float len = deflectLineLength * Mathf.Clamp01(mag);
                        SetSegmentWithOutline(deflectLine, deflectOutline, ghost, ghost + tangent * len, drawY);
                    }
                    else
                    {
                        DisableLineWithOutline(deflectLine, deflectOutline);
                    }
                }
                else
                {
                    DisableLineWithOutline(targetLine, targetOutline);
                    DisableLineWithOutline(deflectLine, deflectOutline);
                }
            }
            else
            {
                // Chạm băng (A-băng / Kick shot): giải chính xác hướng bật băng theo mô hình xung lực vật lý thực tế
                // (có xét tốc độ chạm, độ cao mũi băng CushionNoseHeight, ma sát Coulomb tại băng và xoáy ép-phê / cu-lê / trô)
                DisableLineWithOutline(targetLine, targetOutline);
                Vector3 n = new Vector3(hitNormal.x, 0f, hitNormal.z);
                if (n.sqrMagnitude > 0.0001f)
                {
                    n.Normalize();
                    Vector3 bounceDir = BallController.PredictCushionBounceDirection(
                        ballManager.PhysicsConfig,
                        dir,
                        hitDistance,
                        inputController.CurrentPower01,
                        inputController.CueContact01,
                        n,
                        isPocketFacing);
                    float cushionRayLen = Mathf.Max(deflectLineLength + 0.25f, 0.55f);
                    SetSegmentWithOutline(deflectLine, deflectOutline, ghost, ghost + bounceDir * cushionRayLen, drawY);
                }
                else
                {
                    DisableLineWithOutline(deflectLine, deflectOutline);
                }
            }
        }

        /// <summary>
        /// Tính giao điểm đầu tiên dọc hướng ngắm:
        /// - Với các bi mục tiêu trên bàn: giải giao điểm hình học phẳng XZ chính xác với khoảng cách tâm đúng bằng 2R
        ///   (không dùng bán kính thu nhỏ 0.98R để tránh lệch góc cắt và tránh bỏ sót các góc cắt mỏng 82°-90°).
        /// - Với thành băng bàn: quét SphereCastNonAlloc với bán kính thực 1.0R và chuẩn hóa pháp tuyến phẳng (TryResolveCushionPlanarNormal).
        /// Trả về vật cản nào có khoảng cách gần hơn.
        /// </summary>
        private bool FindFirstHit(Vector3 origin, Vector3 dir, float radius, BallController cueBall,
            out float distance, out BallController targetBall, out Vector3 normal, out bool isPocketFacing)
        {
            distance = 0f;
            targetBall = null;
            normal = Vector3.zero;
            isPocketFacing = false;

            // 1. Tìm giao điểm chính xác với các quả bi mục tiêu (khoảng cách tâm đúng bằng 2R trên mặt phẳng XZ)
            float bestBallDist = float.MaxValue;
            BallController bestBall = null;
            float twoR = radius * 2f;
            float twoRSq = twoR * twoR;

            var allBalls = ballManager != null ? ballManager.AllBalls : null;
            if (allBalls != null)
            {
                for (int i = 0; i < allBalls.Length; i++)
                {
                    var b = allBalls[i];
                    if (b == null || b == cueBall || b.IsPocketed || !b.gameObject.activeInHierarchy)
                        continue;

                    Vector3 bPos = b.transform.position;
                    Vector3 toBall = new Vector3(bPos.x - origin.x, 0f, bPos.z - origin.z);
                    float proj = Vector3.Dot(toBall, dir);
                    if (proj <= 0f)
                        continue;

                    float perpSq = toBall.sqrMagnitude - proj * proj;
                    if (perpSq >= twoRSq)
                        continue;

                    float hitDist = proj - Mathf.Sqrt(Mathf.Max(0f, twoRSq - perpSq));
                    if (hitDist >= 0f && hitDist < bestBallDist && hitDist <= maxDistance)
                    {
                        bestBallDist = hitDist;
                        bestBall = b;
                    }
                }
            }

            // 2. Tìm giao điểm đầu tiên với thành băng bàn (bỏ qua các bi, trigger lỗ và mặt nỉ) — Zero-GC với SphereCastNonAlloc
            // Dùng đúng bán kính thực radius (1.0R) và lọc pháp tuyến phẳng chuẩn giống hệt HandleCushionCollisionResponse
            float bestCushionDist = float.MaxValue;
            Vector3 bestCushionNormal = Vector3.zero;
            bool bestIsPocketFacing = false;

            int hitCount = UnityEngine.Physics.SphereCastNonAlloc(origin, radius, dir, sphereCastHits, maxDistance,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hitCount; i++)
            {
                var h = sphereCastHits[i];
                if (h.collider == null) continue;
                if (h.distance <= 0.0001f || h.distance >= bestCushionDist) continue;

                // Đã giải giao điểm chính xác 2R cho bi ở bước 1 nên bỏ qua collider của bi tại đây
                if (h.collider.GetComponentInParent<BallController>() != null) continue;

                // Chuẩn hóa pháp tuyến mặt phẳng của băng thẳng / má lỗ và loại bỏ mặt cắt đầu hồi (end-cap)
                if (!BallController.TryResolveCushionPlanarNormal(h.collider, h.normal, out Vector3 planarNormal, out bool pocketFacing))
                    continue;

                // Chỉ nhận va chạm khi tia ngắm thực sự lao vào bề mặt băng (hướng ngược pháp tuyến: dot < -0.02f)
                if (Vector3.Dot(dir, planarNormal) >= -0.02f)
                    continue;

                bestCushionDist = h.distance;
                bestCushionNormal = planarNormal;
                bestIsPocketFacing = pocketFacing;
            }

            // 3. Chọn vật cản gần hơn giữa Bi mục tiêu và Băng bàn
            if (bestBall != null && bestBallDist <= bestCushionDist)
            {
                distance = bestBallDist;
                targetBall = bestBall;
                Vector3 ghost = origin + dir * bestBallDist;
                Vector3 targetCenter = bestBall.transform.position;
                Vector3 n = new Vector3(ghost.x - targetCenter.x, 0f, ghost.z - targetCenter.z);
                normal = n.sqrMagnitude > 1e-10f ? n.normalized : -dir;
                isPocketFacing = false;
                return true;
            }

            if (bestCushionDist <= maxDistance)
            {
                distance = bestCushionDist;
                targetBall = null;
                normal = bestCushionNormal;
                isPocketFacing = bestIsPocketFacing;
                return true;
            }

            return false;
        }

        private void DrawRing(Vector3 center, float radius, float y)
        {
            ringLine.positionCount = RingSegments;
            float outY = y + OutlineYOffset;
            if (ringOutline != null) ringOutline.positionCount = RingSegments;

            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                float px = center.x + Mathf.Cos(a) * radius;
                float pz = center.z + Mathf.Sin(a) * radius;
                ringLine.SetPosition(i, new Vector3(px, y, pz));
                if (ringOutline != null)
                {
                    ringOutline.SetPosition(i, new Vector3(px, outY, pz));
                }
            }
            ringLine.enabled = true;
            if (ringOutline != null) ringOutline.enabled = true;
        }

        private static Vector3 Flat(Vector3 p, float y)
        {
            return new Vector3(p.x, y, p.z);
        }
    }
}
