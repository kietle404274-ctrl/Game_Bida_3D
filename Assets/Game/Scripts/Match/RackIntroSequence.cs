using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Game.Billiards.Camera;
using Game.Billiards.Core;
using Game.Billiards.Physics;

namespace Game.Billiards.Match
{
    /// <summary>
    /// Cảnh mở đầu mỗi ván: khung tam giác 3D hạ xuống, bi lần lượt rơi vào khung (từng hàng),
    /// camera lia vòng quanh dàn bi, khung bay lên rồi biến mất, sau đó trả quyền chơi.
    /// Nhấn Space / chuột trái để bỏ qua.
    /// </summary>
    public class RackIntroSequence : MonoBehaviour
    {
        [Header("Timeline (giây)")]
        [SerializeField] private float rackDropStart = 0.35f;
        [SerializeField] private float rackDropDuration = 0.55f;
        [SerializeField] private float ballsStart = 1.0f;
        [SerializeField] private float ballInterval = 0.09f;
        [SerializeField] private float ballDropDuration = 0.26f;
        [SerializeField] private float holdAfterBalls = 0.5f;
        [SerializeField] private float rackLiftDuration = 0.9f;
        [SerializeField] private float endHold = 0.45f;

        [Header("Camera")]
        [SerializeField] private float startAngleDeg = -125f;
        [SerializeField] private float startRadius = 1.9f;
        [SerializeField] private float endRadius = 1.05f;
        [SerializeField] private float startHeight = 1.7f;
        [SerializeField] private float endHeight = 0.42f;

        private BallManager ballManager;
        private TableWrapper tableWrapper;
        private CameraDirector cameraDirector;
        private CueController cueController;

        private bool isPlaying;
        private bool skipRequested;
        private CanvasGroup hudGroup;

        public bool IsPlaying => isPlaying;

        /// <summary>Chạy cảnh mở đầu. onComplete luôn được gọi (kể cả khi bỏ qua / không đủ tham chiếu).</summary>
        public void Play(Action onComplete)
        {
            if (isPlaying) return;
            StartCoroutine(Run(onComplete));
        }

        private void ResolveRefs()
        {
            if (ballManager == null) ballManager = FindObjectOfType<BallManager>();
            if (tableWrapper == null) tableWrapper = FindObjectOfType<TableWrapper>();
            if (cameraDirector == null) cameraDirector = FindObjectOfType<CameraDirector>();
            if (cueController == null) cueController = FindObjectOfType<CueController>();
        }

        private static bool IsOnlineMatch()
        {
            var session = GameSession.Instance;
            return session != null && session.CurrentGameMode == GameMode.OnlinePvP;
        }

        private IEnumerator Run(Action onComplete)
        {
            ResolveRefs();

            UnityEngine.Camera cam = cameraDirector != null ? cameraDirector.MainCamera : null;
            if (IsOnlineMatch() || ballManager == null || tableWrapper == null || cam == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            isPlaying = true;
            skipRequested = false;

            float radius = ballManager.PhysicsConfig != null ? ballManager.PhysicsConfig.BallRadius : 0.0286f;
            float spacing = radius * 2f * 1.002f;
            float rowSpacing = spacing * 0.8660254f;
            Vector3 foot = tableWrapper.FootSpot;

            // --- Ghi nhớ vị trí cuối cùng của từng bi rồi ẩn đi ---
            var balls = new List<BallController>();
            for (int i = 1; i <= 15; i++)
            {
                var b = ballManager.GetBall(i);
                if (b != null) balls.Add(b);
            }
            balls.Sort((a, b) =>
            {
                int c = b.transform.position.x.CompareTo(a.transform.position.x); // đỉnh dàn trước
                return c != 0 ? c : a.transform.position.z.CompareTo(b.transform.position.z);
            });

            var cueBall = ballManager.CueBall;
            var all = new List<BallController>(balls);
            if (cueBall != null) all.Add(cueBall);

            int n = all.Count;
            var finalPos = new Vector3[n];
            var finalRot = new Quaternion[n];
            var finalScale = new Vector3[n];
            var interp = new RigidbodyInterpolation[n];
            var done = new bool[n];
            for (int i = 0; i < n; i++)
            {
                var t = all[i].transform;
                finalPos[i] = t.position;
                finalRot[i] = t.rotation;
                finalScale[i] = t.localScale;
                var rb = all[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    interp[i] = rb.interpolation;
                    rb.interpolation = RigidbodyInterpolation.None;
                }
                t.localScale = Vector3.zero;
            }

            // --- Camera & HUD & cơ ---
            cameraDirector.SwitchToCinematicCamera();
            if (cueController != null) cueController.SetAimingState(false);
            SetupHudGroup();
            if (hudGroup != null) { hudGroup.alpha = 0f; hudGroup.blocksRaycasts = false; hudGroup.interactable = false; }

            Vector3 pivot = new Vector3(foot.x - 2f * rowSpacing, foot.y, foot.z);
            Vector3 focus = pivot;

            // --- Khung tam giác ---
            GameObject rack = BuildRack(radius, spacing, rowSpacing, pivot);
            Vector3 rackRest = rack.transform.position;
            Vector3 rackBaseScale = Vector3.one;
            rack.SetActive(false);

            // --- Letterbox + gợi ý bỏ qua ---
            GameObject overlay = BuildLetterbox(out RectTransform topBar, out RectTransform botBar, out Text hint);

            float ballsEnd = ballsStart + (n - 1) * ballInterval + ballDropDuration;
            float liftStart = ballsEnd + holdAfterBalls;
            float liftEnd = liftStart + rackLiftDuration;
            float total = liftEnd + endHold;

            float time = 0f;
            bool rackLanded = false;

            while (time < total && !skipRequested)
            {
                time += Time.deltaTime;
                if (UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetMouseButtonDown(0))
                {
                    skipRequested = true;
                    break;
                }

                // Camera lia vòng
                float u = SmoothStep(Mathf.Clamp01(time / total));
                float ang = Mathf.Deg2Rad * Mathf.Lerp(startAngleDeg, 0f, u);
                float r = Mathf.Lerp(startRadius, endRadius, u);
                float h = Mathf.Lerp(startHeight, endHeight, SmoothStep(Mathf.Clamp01(time / (total * 0.85f))));
                Vector3 camPos = focus + new Vector3(Mathf.Cos(ang) * r, h, Mathf.Sin(ang) * r);
                Vector3 look = focus + new Vector3(0.18f * u, 0.02f, 0f);
                cam.transform.position = camPos;
                cam.transform.rotation = Quaternion.LookRotation(look - camPos, Vector3.up);

                // Letterbox
                float bar = Mathf.Min(SmoothStep(Mathf.Clamp01(time / 0.5f)), SmoothStep(Mathf.Clamp01((total - time) / 0.5f)));
                SetBars(topBar, botBar, bar);
                if (hint != null) hint.color = new Color(1f, 1f, 1f, 0.75f * bar);

                // Khung hạ xuống
                if (time >= rackDropStart && time < liftStart)
                {
                    rack.SetActive(true);
                    float p = Mathf.Clamp01((time - rackDropStart) / rackDropDuration);
                    float fall = p * p;
                    rack.transform.position = rackRest + Vector3.up * (0.45f * (1f - fall));
                    rack.transform.localScale = rackBaseScale;
                    if (p >= 1f && !rackLanded)
                    {
                        rackLanded = true;
                        PlayLand();
                    }
                }
                else if (time >= liftStart)
                {
                    float p = Mathf.Clamp01((time - liftStart) / rackLiftDuration);
                    float e = p * p * (3f - 2f * p);
                    rack.transform.position = rackRest + Vector3.up * (0.7f * e);
                    float s = 1f - Mathf.Clamp01((p - 0.55f) / 0.45f);
                    rack.transform.localScale = rackBaseScale * Mathf.Max(0.0001f, s);
                    if (p >= 1f) rack.SetActive(false);
                }

                // Bi rơi vào khung
                for (int i = 0; i < n; i++)
                {
                    if (done[i]) continue;
                    float st = ballsStart + i * ballInterval;
                    if (time < st) continue;
                    float p = Mathf.Clamp01((time - st) / ballDropDuration);
                    float y = 0.22f * (1f - p * p);
                    var tr = all[i].transform;
                    tr.position = finalPos[i] + Vector3.up * y;
                    tr.rotation = finalRot[i];
                    tr.localScale = finalScale[i] * Mathf.Clamp01(p * 4f);
                    if (p >= 1f)
                    {
                        done[i] = true;
                        tr.position = finalPos[i];
                        PlayClick();
                    }
                }

                yield return null;
            }

            // --- Kết thúc / bỏ qua: khôi phục trạng thái chuẩn ---
            for (int i = 0; i < n; i++)
            {
                var b = all[i];
                b.transform.localScale = finalScale[i];
                b.ResetTo(finalPos[i], finalRot[i]);
                b.SetKinematic(true);
                var rb = b.GetComponent<Rigidbody>();
                if (rb != null) rb.interpolation = interp[i];
            }
            if (rack != null) Destroy(rack);
            if (overlay != null) Destroy(overlay);

            if (cueBall != null)
            {
                cameraDirector.SetCueBallTarget(cueBall.transform);
                if (cueController != null) cueController.SetCueBallTarget(cueBall.transform);
            }
            cameraDirector.SwitchToAimCamera();
            if (cueController != null) cueController.SetAimingState(true);

            isPlaying = false;
            onComplete?.Invoke();

            if (hudGroup != null)
            {
                hudGroup.blocksRaycasts = true;
                hudGroup.interactable = true;
                float f = 0f;
                while (f < 0.6f)
                {
                    f += Time.deltaTime;
                    hudGroup.alpha = Mathf.Clamp01(f / 0.6f);
                    yield return null;
                }
                hudGroup.alpha = 1f;
            }
        }

        private static float SmoothStep(float x)
        {
            return x * x * (3f - 2f * x);
        }

        private void PlayClick()
        {
            var am = Audio.AudioManager.Instance;
            if (am != null) am.PlayBallHit(0.25f);
        }

        private void PlayLand()
        {
            var am = Audio.AudioManager.Instance;
            if (am != null) am.PlayCushionHit(0.6f);
        }

        // ---------------------------------------------------------------- HUD

        private void SetupHudGroup()
        {
            if (hudGroup != null) return;
            foreach (var c in FindObjectsOfType<Canvas>())
            {
                if (c != null && c.gameObject.name == "GameplayCanvas")
                {
                    hudGroup = c.GetComponent<CanvasGroup>();
                    if (hudGroup == null) hudGroup = c.gameObject.AddComponent<CanvasGroup>();
                    break;
                }
            }
        }

        private GameObject BuildLetterbox(out RectTransform top, out RectTransform bottom, out Text hint)
        {
            var go = new GameObject("RackIntroOverlay");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            go.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;

            top = MakeBar(go.transform, "Top", true);
            bottom = MakeBar(go.transform, "Bottom", false);

            var hintGo = new GameObject("SkipHint");
            hintGo.transform.SetParent(go.transform, false);
            hint = hintGo.AddComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 22;
            hint.alignment = TextAnchor.MiddleRight;
            hint.color = new Color(1f, 1f, 1f, 0f);
            hint.raycastTarget = false;
            hint.text = LocalizationManager.Get("rack_intro_skip");
            var rt = hint.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-36f, 22f);
            rt.sizeDelta = new Vector2(520f, 40f);
            return go;
        }

        private static RectTransform MakeBar(Transform parent, string name, bool isTop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
            rt.sizeDelta = new Vector2(0f, 0f);
            return rt;
        }

        private static void SetBars(RectTransform top, RectTransform bottom, float t)
        {
            float hgt = 110f * t;
            if (top != null) top.sizeDelta = new Vector2(0f, hgt);
            if (bottom != null) bottom.sizeDelta = new Vector2(0f, hgt);
        }

        // ---------------------------------------------------------------- Khung tam giác

        private GameObject BuildRack(float radius, float spacing, float rowSpacing, Vector3 pivot)
        {
            // Tam giác tâm (đỉnh hướng +X), tính quanh trọng tâm G
            Vector2 apex = new Vector2(2f * rowSpacing, 0f);
            Vector2 b1 = new Vector2(-2f * rowSpacing, -2f * spacing);
            Vector2 b2 = new Vector2(-2f * rowSpacing, 2f * spacing);
            Vector2 g = (apex + b1 + b2) / 3f;

            float inradius = 4f * spacing / (2f * Mathf.Sqrt(3f));
            float dInner = radius + 0.0015f;
            float dOuter = dInner + 0.013f;
            float kIn = (inradius + dInner) / inradius;
            float kOut = (inradius + dOuter) / inradius;

            Vector2[] centre = { apex - g, b1 - g, b2 - g };
            var inner = new Vector2[3];
            var outer = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                inner[i] = centre[i] * kIn;
                outer[i] = centre[i] * kOut;
            }

            float y0 = -radius + 0.002f;
            float y1 = radius * 0.7f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                // Mặt trên
                AddQuad(verts, tris, V(inner[i], y1), V(outer[i], y1), V(outer[j], y1), V(inner[j], y1));
                // Mặt ngoài
                AddQuad(verts, tris, V(outer[i], y0), V(outer[j], y0), V(outer[j], y1), V(outer[i], y1));
                // Mặt trong
                AddQuad(verts, tris, V(inner[j], y0), V(inner[i], y0), V(inner[i], y1), V(inner[j], y1));
            }

            var mesh = new Mesh { name = "RackTriangle" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("RackTriangle");
            go.transform.position = new Vector3(pivot.x + g.x, pivot.y, pivot.z + g.y);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BuildRackMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static Vector3 V(Vector2 p, float y)
        {
            return new Vector3(p.x, y, p.y);
        }

        /// <summary>Thêm 1 quad hai mặt (hai cặp đỉnh riêng) để luôn nhìn thấy dù hướng cull thế nào.</summary>
        private static void AddQuad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int s = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(s); t.Add(s + 1); t.Add(s + 2);
            t.Add(s); t.Add(s + 2); t.Add(s + 3);

            int s2 = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(s2); t.Add(s2 + 2); t.Add(s2 + 1);
            t.Add(s2); t.Add(s2 + 3); t.Add(s2 + 2);
        }

        private static Material BuildRackMaterial()
        {
            Shader sh = Shader.Find("HDRP/Lit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("HDRP/Unlit");
            var mat = new Material(sh);
            Color wood = new Color(0.30f, 0.16f, 0.07f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", wood);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", wood);
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", wood);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.1f);
            return mat;
        }
    }
}
