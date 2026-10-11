using UnityEditor;
using UnityEngine;

namespace AKStudioArt.BilliardsRoom
{
    public class ShadowFollower : MonoBehaviour
    {
        public GameObject shadowPrefab; // Reference to the shadow plane Prefab
        public float yOffset = 0.028f; // Y-axis offset so the shadow plane stays below the ball

        private GameObject shadowInstance; // Instance of the shadow plane

        void Start()
        {
            if (shadowPrefab != null)
            {
                // Instantiate the shadow plane Prefab
                shadowInstance = Instantiate(shadowPrefab);
            }
        }

        void Update()
        {
            if (shadowInstance != null)
            {
                // Update the position of the shadow plane to follow the ball on the X and Z axes, but with an offset on the Y axis
                Vector3 newShadowPosition = new Vector3(transform.position.x, transform.position.y - yOffset, transform.position.z);
                shadowInstance.transform.position = newShadowPosition;
            }
        }
    }

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
}

