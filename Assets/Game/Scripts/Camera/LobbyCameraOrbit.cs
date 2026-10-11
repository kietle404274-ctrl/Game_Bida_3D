using System;
using UnityEngine;

namespace Game.Billiards.Camera
{
    public class LobbyCameraOrbit : MonoBehaviour
    {
        [Header("Orbit Target")]
        [SerializeField] private Vector3 lookTarget = new Vector3(0f, 0.85f, 0f);
        [SerializeField] private float orbitDistance = 2.4f;
        [SerializeField] private float baseHeight = 1.6f;

        [Header("Cinematic Motion")]
        [SerializeField] private float orbitSpeedDegPerSec = 8.0f;
        [SerializeField] private float bobbingAmplitude = 0.08f;
        [SerializeField] private float bobbingFrequency = 0.4f;

        private float currentAngleDeg = 45.0f;

        private void Start()
        {
            UpdateCameraPosition();
        }

        private void LateUpdate()
        {
            currentAngleDeg += orbitSpeedDegPerSec * Time.deltaTime;
            if (currentAngleDeg >= 360f) currentAngleDeg -= 360f;

            UpdateCameraPosition();
        }

        private void UpdateCameraPosition()
        {
            float rad = currentAngleDeg * Mathf.Deg2Rad;
            float x = lookTarget.x + Mathf.Cos(rad) * orbitDistance;
            float z = lookTarget.z + Mathf.Sin(rad) * orbitDistance;
            float y = baseHeight + Mathf.Sin(Time.time * bobbingFrequency) * bobbingAmplitude;

            transform.position = new Vector3(x, y, z);
            transform.LookAt(lookTarget);
        }
    }
}
