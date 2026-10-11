using System;
using UnityEngine;

namespace Game.Billiards.Physics
{
    public class TableWrapper : MonoBehaviour
    {
        [Header("Cloth & Playing Surface")]
        [Tooltip("Độ cao Y của mặt nỉ bida")]
        [SerializeField] private float clothSurfaceY = 0.835f;

        [Tooltip("Nửa chiều rộng khu vực đánh bóng (trục X)")]
        [SerializeField] private float playableHalfWidth = 0.56f;

        [Tooltip("Nửa chiều dài khu vực đánh bóng (trục Z)")]
        [SerializeField] private float playableHalfLength = 1.12f;

        [Header("Markers & Spots")]
        [Tooltip("Tọa độ mốc chân bàn (Foot Spot - nơi đặt đỉnh dàn bi)")]
        [SerializeField] private Vector3 footSpot = new Vector3(0f, 0.8635f, 0.56f);

        [Tooltip("Tọa độ mốc đầu bàn (Head Spot - nơi đặt bi cái khi khai cuộc)")]
        [SerializeField] private Vector3 headSpot = new Vector3(0f, 0.8635f, -0.56f);

        [Tooltip("Tọa độ Z của đường lằn đầu bàn (Head String)")]
        [SerializeField] private float headStringZ = -0.56f;

        [Header("Pockets (6 lỗ)")]
        [Tooltip("Danh sách 6 PocketDetector (0=FootLeft, 1=FootRight, 2=SideLeft, 3=SideRight, 4=HeadLeft, 5=HeadRight)")]
        [SerializeField] private PocketDetector[] pockets = new PocketDetector[6];

        public float ClothSurfaceY => clothSurfaceY;
        public float PlayableHalfWidth => playableHalfWidth;
        public float PlayableHalfLength => playableHalfLength;
        public Vector3 FootSpot => footSpot;
        public Vector3 HeadSpot => headSpot;
        public float HeadStringZ => headStringZ;
        public PocketDetector[] Pockets => pockets;

        public PocketDetector GetPocket(int id)
        {
            if (id >= 0 && id < pockets.Length)
            {
                return pockets[id];
            }
            return null;
        }

        public bool IsInsidePlayArea(Vector3 position, float margin = 0.03f)
        {
            float minX = -playableHalfWidth + margin;
            float maxX = playableHalfWidth - margin;
            float minZ = -playableHalfLength + margin;
            float maxZ = playableHalfLength - margin;

            return position.x >= minX && position.x <= maxX &&
                   position.z >= minZ && position.z <= maxZ;
        }

        public bool IsBehindHeadString(Vector3 position)
        {
            // Trong hệ tọa độ Z: Đầu bàn là Z âm (Z <= headStringZ)
            return position.z <= headStringZ;
        }

        public Vector3 ClampToPlayArea(Vector3 position, float margin = 0.035f)
        {
            float clampedX = Mathf.Clamp(position.x, -playableHalfWidth + margin, playableHalfWidth - margin);
            float clampedZ = Mathf.Clamp(position.z, -playableHalfLength + margin, playableHalfLength - margin);
            return new Vector3(clampedX, clothSurfaceY + 0.02855f, clampedZ);
        }

        private void OnDrawGizmosSelected()
        {
            // Vẽ ranh giới khu vực thi đấu
            Gizmos.color = Color.green;
            Vector3 center = new Vector3(0f, clothSurfaceY, 0f);
            Vector3 size = new Vector3(playableHalfWidth * 2f, 0.01f, playableHalfLength * 2f);
            Gizmos.DrawWireCube(center, size);

            // Vẽ Head String
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(-playableHalfWidth, clothSurfaceY + 0.005f, headStringZ),
                new Vector3(playableHalfWidth, clothSurfaceY + 0.005f, headStringZ)
            );

            // Vẽ Foot Spot & Head Spot
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(footSpot, 0.015f);
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(headSpot, 0.015f);
        }
    }
}
