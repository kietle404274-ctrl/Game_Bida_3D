using System;
using UnityEngine;
using Game.Billiards.Camera;

namespace Game.Billiards.VFX
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Particle Systems")]
        [Tooltip("Hiệu ứng bụi lơ gậy khi đầu gậy tác dụng lực vào bi cái (Mặc định tắt để giữ phong cách bida 3D chân thực, không bị vụn hạt vướng tầm nhìn)")]
        [SerializeField] private bool enableChalkPuff = false;
        [SerializeField] private ParticleSystem chalkPuffParticles;

        [Header("Call Shot Pocket Marker")]
        [Tooltip("Prefab hoặc GameObject đánh dấu lỗ chỉ định")]
        [SerializeField] private GameObject pocketCallMarker;

        [Header("Camera Director Reference")]
        [SerializeField] private CameraDirector cameraDirector;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (enableChalkPuff)
            {
                CreateDefaultChalkParticlesIfMissing();
            }
        }

        private void Start()
        {
            if (cameraDirector == null)
            {
                cameraDirector = FindObjectOfType<CameraDirector>();
            }
        }

        private void CreateDefaultChalkParticlesIfMissing()
        {
            if (chalkPuffParticles == null)
            {
                var existingChild = transform.Find("ChalkPuffVFX");
                if (existingChild != null)
                {
                    chalkPuffParticles = existingChild.GetComponent<ParticleSystem>();
                }
            }

            if (chalkPuffParticles == null)
            {
                GameObject psObj = new GameObject("ChalkPuffVFX");
                psObj.transform.SetParent(transform);
                chalkPuffParticles = psObj.AddComponent<ParticleSystem>();
            }

            // Gán Material trong suốt chuẩn HDRP (High Definition Render Pipeline)
            var psRenderer = chalkPuffParticles.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && (psRenderer.sharedMaterial == null || psRenderer.sharedMaterial.shader == null || psRenderer.sharedMaterial.shader.name == "Hidden/InternalErrorShader"))
            {
                Shader s = Shader.Find("HDRP/Unlit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Unlit/Transparent");
                if (s == null) s = Shader.Find("Sprites/Default");
                if (s != null)
                {
                    Material mat = new Material(s);
                    mat.name = "ChalkDustMaterial";
                    mat.mainTexture = CreateSoftCircleTexture();
                    
                    // Thiết lập thuộc tính Transparent chuẩn HDRP
                    if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f); // 1 = Transparent trong HDRP
                    if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f);     // 0 = Alpha
                    if (mat.HasProperty("_AlphaCutoffEnable")) mat.SetFloat("_AlphaCutoffEnable", 0f);
                    if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
                    if (mat.HasProperty("_CullMode")) mat.SetFloat("_CullMode", 0f);
                    if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);

                    // Fallback cho URP / Standard shader
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);

                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    psRenderer.material = mat;
                }
            }

            var main = chalkPuffParticles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.15f;
            main.startSpeed = 0.15f;
            main.startSize = 0.004f; // 4mm - vi hạt cực nhỏ, mịn màng tự nhiên thay vì khối pixel to
            main.startColor = new Color(0.85f, 0.92f, 1.0f, 0.2f); // Bụi mờ nhạt, trong suốt
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = chalkPuffParticles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 5) });

            var shape = chalkPuffParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.005f;

            var colorOverLifetime = chalkPuffParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.85f, 0.92f, 1.0f), 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.25f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = chalkPuffParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(1f, 1.2f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        }

        private static Texture2D CreateSoftCircleTexture()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.name = "SoftChalkParticleTex";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color[] cols = new Color[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    cols[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(cols);
            tex.Apply();
            return tex;
        }

        public void PlayChalkPuff(Vector3 contactPosition, Vector3 shotDirection)
        {
            if (!enableChalkPuff) return;

            if (chalkPuffParticles == null)
            {
                CreateDefaultChalkParticlesIfMissing();
            }

            if (chalkPuffParticles != null)
            {
                chalkPuffParticles.transform.position = contactPosition;
                chalkPuffParticles.transform.rotation = Quaternion.LookRotation(-shotDirection);
                chalkPuffParticles.Play();
            }
        }

        public void SetPocketCallMarker(Vector3 pocketPosition, bool isVisible)
        {
            if (pocketCallMarker != null)
            {
                pocketCallMarker.transform.position = pocketPosition + Vector3.up * 0.05f;
                pocketCallMarker.SetActive(isVisible);
            }
        }

        public void TriggerBreakShotFeedback(float power01)
        {
            // Nếu lực đánh mạnh (> 60%), kích hoạt rung nhẹ camera tạo cảm giác uy lực
            if (power01 >= 0.6f)
            {
                if (cameraDirector == null) cameraDirector = FindObjectOfType<CameraDirector>();
                if (cameraDirector != null)
                {
                    float intensity = Mathf.Lerp(0.015f, 0.045f, (power01 - 0.6f) / 0.4f);
                    cameraDirector.TriggerCameraShake(0.25f, intensity);
                }
            }
        }
    }
}
