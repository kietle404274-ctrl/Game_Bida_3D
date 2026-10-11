using System;
using UnityEngine;
using Game.Billiards.Camera;

namespace Game.Billiards.VFX
{
    public class VFXManager : MonoBehaviour
    {
        public static VFXManager Instance { get; private set; }

        [Header("Particle Systems")]
        [Tooltip("Hiệu ứng bụi lơ gậy khi đầu gậy tác dụng lực vào bi cái")]
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

            CreateDefaultChalkParticlesIfMissing();
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
                GameObject psObj = new GameObject("ChalkPuffVFX");
                psObj.transform.SetParent(transform);
                chalkPuffParticles = psObj.AddComponent<ParticleSystem>();

                var main = chalkPuffParticles.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = 0.35f;
                main.startSpeed = 0.8f;
                main.startSize = 0.04f;
                main.startColor = new Color(0.4f, 0.7f, 1.0f, 0.6f); // Xanh lơ phấn bida
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = chalkPuffParticles.emission;
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

                var shape = chalkPuffParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.02f;

                var colorOverLifetime = chalkPuffParticles.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.7f, 1.0f), 0f), new GradientColorKey(Color.white, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
                );
                colorOverLifetime.color = grad;
            }
        }

        public void PlayChalkPuff(Vector3 contactPosition, Vector3 shotDirection)
        {
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
