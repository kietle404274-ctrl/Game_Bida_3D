#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace AKStudioArt.BilliardsRoom
{
    public class ShadowFollower : MonoBehaviour
    {
        public GameObject shadowPrefab; // Reference to the shadow plane Prefab
        public float yOffset = 0.028f; // Y-axis offset so the shadow plane stays below the ball

        private GameObject shadowInstance; // Instance of the shadow plane
        private Vector3 lastBallPosition = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        void Start()
        {
            EnsureShadowInstance();
            SyncShadow(true);
        }

        void OnEnable()
        {
            EnsureShadowInstance();
            SyncShadow(true);
        }

        private void EnsureShadowInstance()
        {
            if (shadowInstance == null && shadowPrefab != null)
            {
                // Instantiate the shadow plane Prefab
                shadowInstance = Instantiate(shadowPrefab);
            }
        }

        void OnDisable()
        {
            if (shadowInstance != null)
            {
                shadowInstance.SetActive(false);
            }
        }

        void OnDestroy()
        {
            if (shadowInstance != null)
            {
                Destroy(shadowInstance);
                shadowInstance = null;
            }
        }

        void Update()
        {
            SyncShadow(false);
        }

        private void SyncShadow(bool forcePositionUpdate)
        {
            if (shadowInstance == null) return;

            Vector3 currentPos = transform.position;
            bool shouldBeVisible = gameObject.activeInHierarchy
                                   && transform.localScale.sqrMagnitude > 1e-6f
                                   && currentPos.y > 0f;

            if (shadowInstance.activeSelf != shouldBeVisible)
            {
                shadowInstance.SetActive(shouldBeVisible);
                if (shouldBeVisible)
                {
                    forcePositionUpdate = true;
                }
            }

            if (!shouldBeVisible) return;

            if (forcePositionUpdate || (currentPos - lastBallPosition).sqrMagnitude > 1e-8f)
            {
                lastBallPosition = currentPos;
                // Update the position of the shadow plane to follow the ball on the X and Z axes, but with an offset on the Y axis
                shadowInstance.transform.position = new Vector3(currentPos.x, currentPos.y - yOffset, currentPos.z);
            }
        }
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ShadowFollower))]
    public class ShadowFollowerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ShadowFollower controller = (ShadowFollower)target;
            // Show the custom image in the inspector if the path is defined
            string fullPath = "Assets/AK Studio Art/The Billiards Room/Editor/Shadow Follower.png";
            Texture2D customInspectorImage = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
            if (customInspectorImage != null)
            {
                Rect rect = EditorGUILayout.GetControlRect(false, 100);
                EditorGUI.DrawTextureTransparent(rect, customInspectorImage, ScaleMode.ScaleToFit);
            }

            // Show the other fields in the inspector
            DrawDefaultInspector();
        }
    }
#endif
}
