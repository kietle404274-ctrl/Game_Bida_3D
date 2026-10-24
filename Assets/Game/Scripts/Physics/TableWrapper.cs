using System;
using UnityEngine;

namespace Game.Billiards.Physics
{
    public class TableWrapper : MonoBehaviour
    {
        [Header("Cloth & Playing Surface")]
        [Tooltip("Độ cao Y của mặt nỉ bida")]
        [SerializeField] private float clothSurfaceY = 0.835f;

        [Tooltip("Nửa chiều rộng khu vực đánh bóng (trục Z)")]
        [SerializeField] private float playableHalfWidth = 0.5023f;

        [Tooltip("Nửa chiều dài khu vực đánh bóng (trục X)")]
        [SerializeField] private float playableHalfLength = 1.066f;

        [Header("Markers & Spots")]
        [Tooltip("Tọa độ mốc chân bàn (Foot Spot - nơi đặt đỉnh dàn bi)")]
        [SerializeField] private Vector3 footSpot = new Vector3(-0.502f, 0.8635f, 0f);

        [Tooltip("Tọa độ mốc đầu bàn (Head Spot - nơi đặt bi cái khi khai cuộc)")]
        [SerializeField] private Vector3 headSpot = new Vector3(0.502f, 0.8635f, 0f);

        [Tooltip("Tọa độ X của đường lằn đầu bàn (Head String)")]
        [SerializeField] private float headStringX = 0.502f;

        [Header("Pockets (6 lỗ)")]
        [Tooltip("Danh sách 6 PocketDetector (0=FootLeft, 1=FootRight, 2=SideLeft, 3=SideRight, 4=HeadLeft, 5=HeadRight)")]
        [SerializeField] private PocketDetector[] pockets = new PocketDetector[6];

        public const float FootCushionX = -1.0724f; // Mép mũi băng ngắn đuôi bàn (-X) thực tế của model 3D
        public const float HeadCushionX = 1.0603f;  // Mép mũi băng ngắn đầu bàn (+X) thực tế của model 3D
        public const float CushionHalfWidth = 0.5023f; // Mép mũi băng dài (±Z) thực tế của model 3D
        public const float ShortCushionHalfSpanZ = 0.4467f; // Chiều dài nửa băng ngắn (tới góc vát miệng lỗ)

        public float ClothSurfaceY => clothSurfaceY;
        public float PlayableHalfWidth => playableHalfWidth;
        public float PlayableHalfLength => playableHalfLength;
        public Vector3 FootSpot => footSpot;
        public Vector3 HeadSpot => headSpot;
        public float HeadStringX => headStringX;
        public float HeadStringZ => headStringX; // Giữ tương thích ngược
        public PocketDetector[] Pockets => pockets;

        private void Awake()
        {
            // Đảm bảo kích thước đồng bộ chính xác với mép mũi băng 3D thực tế
            if (playableHalfLength < 1.04f || playableHalfLength > 1.08f) playableHalfLength = 1.066f;
            if (playableHalfWidth < 0.49f || playableHalfWidth > 0.52f) playableHalfWidth = 0.5023f;
            if (Mathf.Abs(headStringX) > 0.52f) headStringX = 0.502f;
            if (Mathf.Abs(footSpot.x) > 0.52f) footSpot = new Vector3(-0.502f, clothSurfaceY + 0.02855f, 0f);
            if (Mathf.Abs(headSpot.x) > 0.52f) headSpot = new Vector3(0.502f, clothSurfaceY + 0.02855f, 0f);

            EnsureClothBedCollider();
        }

        public void EnsureClothBedCollider()
        {
            // 1. Tạo vật liệu nỉ mặt bàn TableClothPhysicMaterial (Chuẩn WPA cloth: friction 0.20, bounciness 0)
            PhysicMaterial clothMat = new PhysicMaterial("TableClothPhysicMaterial")
            {
                dynamicFriction = 0.20f,
                staticFriction = 0.20f,
                bounciness = 0.0f,
                frictionCombine = PhysicMaterialCombine.Multiply,
                bounceCombine = PhysicMaterialCombine.Minimum
            };

            // Gán clothMat cho mesh collider Play Field (nếu có trên TableVisual)
            var playField = transform.Find("TableVisual/Play Field");
            if (playField == null) playField = transform.Find("Play Field");
            if (playField != null)
            {
                var pfCol = playField.GetComponent<Collider>();
                if (pfCol != null)
                {
                    pfCol.sharedMaterial = clothMat;
                }
            }

            var existingBed = transform.Find("ClothBedCollider");
            if (existingBed == null)
            {
                GameObject bedGo = new GameObject("ClothBedCollider");
                bedGo.transform.SetParent(transform, false);
                bedGo.transform.localPosition = new Vector3(0f, clothSurfaceY - 0.01f, 0f);
                var box = bedGo.AddComponent<BoxCollider>();
                box.size = new Vector3(playableHalfLength * 2f + 0.15f, 0.02f, playableHalfWidth * 2f + 0.15f);
                box.sharedMaterial = clothMat;
            }
            else
            {
                var box = existingBed.GetComponent<BoxCollider>();
                if (box != null) box.sharedMaterial = clothMat;
            }

            // 2. Tạo hệ thống BoxCollider thành băng vuông góc phẳng (chuẩn cao su băng giải đấu: bounciness 0.78, friction 0.12)
            PhysicMaterial cushionMat = new PhysicMaterial("TableCushionPhysicMaterial")
            {
                dynamicFriction = 0.12f,
                staticFriction = 0.12f,
                bounciness = 0.78f,
                frictionCombine = PhysicMaterialCombine.Minimum,
                bounceCombine = PhysicMaterialCombine.Minimum
            };

            SetupCushionBoxColliders(cushionMat);
        }

        private void SetupCushionBoxColliders(PhysicMaterial cushionMat)
        {
            // Vô hiệu hóa toàn bộ MeshCollider phức tạp/góc nghiêng của model 3D
            // Giữ nguyên 100% MeshRenderer để hình ảnh bàn bida giữ nguyên vẹn như ban đầu
            DisableVisualMeshColliders();

            // Tìm hoặc tạo root CushionsColliders
            var existingRoot = transform.Find("CushionsColliders");
            GameObject rootGo;
            if (existingRoot == null)
            {
                rootGo = new GameObject("CushionsColliders");
                rootGo.transform.SetParent(transform, false);
            }
            else
            {
                rootGo = existingRoot.gameObject;
            }

            // Xóa các box cũ nếu có để rebuild sạch sẽ
            while (rootGo.transform.childCount > 0)
            {
                Transform child = rootGo.transform.GetChild(0);
                child.SetParent(null);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // Tọa độ mép mũi băng cao su 3D thực tế của model bàn (Visual Cushion Nose)
            // Đo đạc thực tế từ mesh 3D: Foot X = -1.0724m, Head X = +1.0603m, Side Z = ±0.5023m
            float yCenter = clothSurfaceY + 0.02855f;
            float boxH = 0.12f;
            float thick = 0.06f; // Độ dày 6cm đảm bảo mũi băng nối liền chính xác với má vát lỗ góc
            float shortSpanZ = 0.8934f; // Chiều dài tiếp xúc băng ngắn (nối liền 2 mũi vát lỗ góc tại Z = ±0.4467m)

            // Vật liệu tiếp xúc lòng lỗ và thành giữ bi (giảm nảy để bi rơi êm ái vào lỗ)
            PhysicMaterial pocketLinerMat = new PhysicMaterial("PocketLinerPhysicMaterial")
            {
                dynamicFriction = 0.35f,
                staticFriction = 0.35f,
                bounciness = 0.10f,
                frictionCombine = PhysicMaterialCombine.Average,
                bounceCombine = PhysicMaterialCombine.Minimum
            };

            // 1. Foot Cushion (Băng ngắn đuôi bàn tại -X): mặt va chạm trong tại X = -1.0724m (sát rạt mũi băng 3D)
            CreateCushionBox(rootGo, "Cushion_Foot",
                new Vector3(FootCushionX - thick * 0.5f, yCenter, 0f),
                new Vector3(thick, boxH, shortSpanZ), cushionMat);

            // 2. Head Cushion (Băng ngắn đầu bàn tại +X): mặt va chạm trong tại X = +1.0603m (sát rạt mũi băng 3D)
            CreateCushionBox(rootGo, "Cushion_Head",
                new Vector3(HeadCushionX + thick * 0.5f, yCenter, 0f),
                new Vector3(thick, boxH, shortSpanZ), cushionMat);

            // 3. Side Cushions (+Z): 2 đoạn băng dài chia bởi lỗ giữa tại X=0. Mặt va chạm trong tại Z = +0.5023m
            // Đoạn Foot: X in [-1.014m, -0.076m], length = 0.938m, center X = -0.545m
            CreateCushionBox(rootGo, "Cushion_SidePos_Foot",
                new Vector3(-0.545f, yCenter, CushionHalfWidth + thick * 0.5f),
                new Vector3(0.938f, boxH, thick), cushionMat);

            // Đoạn Head: X in [0.064m, 1.002m], length = 0.938m, center X = 0.533m
            CreateCushionBox(rootGo, "Cushion_SidePos_Head",
                new Vector3(0.533f, yCenter, CushionHalfWidth + thick * 0.5f),
                new Vector3(0.938f, boxH, thick), cushionMat);

            // 4. Side Cushions (-Z): 2 đoạn băng dài chia bởi lỗ giữa tại X=0. Mặt va chạm trong tại Z = -0.5023m
            CreateCushionBox(rootGo, "Cushion_SideNeg_Foot",
                new Vector3(-0.545f, yCenter, -CushionHalfWidth - thick * 0.5f),
                new Vector3(0.938f, boxH, thick), cushionMat);

            CreateCushionBox(rootGo, "Cushion_SideNeg_Head",
                new Vector3(0.533f, yCenter, -CushionHalfWidth - thick * 0.5f),
                new Vector3(0.938f, boxH, thick), cushionMat);

            // 5. Má băng vát tại 4 lỗ góc (Corner Pocket Facings / Jaws)
            // Khớp chính xác 100% với 2 mép vát mũi băng 3D tại mỗi lỗ góc
            // Ngăn chặn hoàn toàn hiện tượng bi xuyên qua mép băng khi bắn sượt mép lỗ
            float facingThickness = 0.030f;

            // Lỗ góc 0: FootLeft (-X, +Z)
            CreateFacingBox(rootGo, "Facing_Pocket0_Foot", new Vector2(-1.0724f, 0.4467f), new Vector2(-1.1294f, 0.4944f), new Vector2(-0.642f, -0.767f), yCenter, boxH, facingThickness, cushionMat);
            CreateFacingBox(rootGo, "Facing_Pocket0_Side", new Vector2(-1.0140f, 0.5023f), new Vector2(-1.0635f, 0.5596f), new Vector2(0.757f, 0.654f), yCenter, boxH, facingThickness, cushionMat);

            // Lỗ góc 1: FootRight (-X, -Z)
            CreateFacingBox(rootGo, "Facing_Pocket1_Foot", new Vector2(-1.0724f, -0.4467f), new Vector2(-1.1294f, -0.4944f), new Vector2(-0.642f, 0.767f), yCenter, boxH, facingThickness, cushionMat);
            CreateFacingBox(rootGo, "Facing_Pocket1_Side", new Vector2(-1.0140f, -0.5023f), new Vector2(-1.0635f, -0.5596f), new Vector2(0.757f, -0.654f), yCenter, boxH, facingThickness, cushionMat);

            // Lỗ góc 4: HeadLeft (+X, +Z)
            CreateFacingBox(rootGo, "Facing_Pocket4_Head", new Vector2(1.0603f, 0.4467f), new Vector2(1.1173f, 0.4944f), new Vector2(0.642f, -0.767f), yCenter, boxH, facingThickness, cushionMat);
            CreateFacingBox(rootGo, "Facing_Pocket4_Side", new Vector2(1.0020f, 0.5023f), new Vector2(1.0515f, 0.5596f), new Vector2(-0.757f, 0.654f), yCenter, boxH, facingThickness, cushionMat);

            // Lỗ góc 5: HeadRight (+X, -Z)
            CreateFacingBox(rootGo, "Facing_Pocket5_Head", new Vector2(1.0603f, -0.4467f), new Vector2(1.1173f, -0.4944f), new Vector2(0.642f, 0.767f), yCenter, boxH, facingThickness, cushionMat);
            CreateFacingBox(rootGo, "Facing_Pocket5_Side", new Vector2(1.0020f, -0.5023f), new Vector2(1.0515f, -0.5596f), new Vector2(-0.757f, -0.654f), yCenter, boxH, facingThickness, cushionMat);

            // Má lỗ giữa (Side Pocket Jaws)
            CreateRotatedBox(rootGo, "Jaw_SideLeft_Foot", new Vector3(-0.067f, yCenter, 0.531f), new Vector3(0f, 18f, 0f), new Vector3(0.04f, boxH, 0.07f), cushionMat);
            CreateRotatedBox(rootGo, "Jaw_SideLeft_Head", new Vector3(0.055f, yCenter, 0.531f), new Vector3(0f, -18f, 0f), new Vector3(0.04f, boxH, 0.07f), cushionMat);

            CreateRotatedBox(rootGo, "Jaw_SideRight_Foot", new Vector3(-0.067f, yCenter, -0.531f), new Vector3(0f, -18f, 0f), new Vector3(0.04f, boxH, 0.07f), cushionMat);
            CreateRotatedBox(rootGo, "Jaw_SideRight_Head", new Vector3(0.055f, yCenter, -0.531f), new Vector3(0f, 18f, 0f), new Vector3(0.04f, boxH, 0.07f), cushionMat);

            // 6. Thành giữ bi & lòng lỗ (Pocket Liners / Backstops)
            // Đảm bảo mọi quả bi lọt vào miệng lỗ đều bị giữ lại trong lòng lỗ, không bao giờ văng xuyên thành bàn
            CreateCushionBox(rootGo, "Backstop_Pocket0_X", new Vector3(-1.18f, yCenter, 0.555f), new Vector3(0.12f, boxH, 0.20f), pocketLinerMat);
            CreateCushionBox(rootGo, "Backstop_Pocket0_Z", new Vector3(-1.06f, yCenter, 0.625f), new Vector3(0.20f, boxH, 0.12f), pocketLinerMat);

            CreateCushionBox(rootGo, "Backstop_Pocket1_X", new Vector3(-1.18f, yCenter, -0.555f), new Vector3(0.12f, boxH, 0.20f), pocketLinerMat);
            CreateCushionBox(rootGo, "Backstop_Pocket1_Z", new Vector3(-1.06f, yCenter, -0.625f), new Vector3(0.20f, boxH, 0.12f), pocketLinerMat);

            CreateCushionBox(rootGo, "Backstop_Pocket4_X", new Vector3(1.18f, yCenter, 0.555f), new Vector3(0.12f, boxH, 0.20f), pocketLinerMat);
            CreateCushionBox(rootGo, "Backstop_Pocket4_Z", new Vector3(1.06f, yCenter, 0.625f), new Vector3(0.20f, boxH, 0.12f), pocketLinerMat);

            CreateCushionBox(rootGo, "Backstop_Pocket5_X", new Vector3(1.18f, yCenter, -0.555f), new Vector3(0.12f, boxH, 0.20f), pocketLinerMat);
            CreateCushionBox(rootGo, "Backstop_Pocket5_Z", new Vector3(1.06f, yCenter, -0.625f), new Vector3(0.20f, boxH, 0.12f), pocketLinerMat);

            CreateCushionBox(rootGo, "Backstop_Pocket2_Z", new Vector3(0f, yCenter, 0.65f), new Vector3(0.22f, boxH, 0.12f), pocketLinerMat);
            CreateCushionBox(rootGo, "Backstop_Pocket3_Z", new Vector3(0f, yCenter, -0.65f), new Vector3(0.22f, boxH, 0.12f), pocketLinerMat);

            // 7. Vành đai an toàn ngoại vi (Outer Safety Perimeter Fence)
            // Lớp bảo vệ vòng ngoài cùng bao quanh toàn bộ bàn bida để ngăn ngừa tuyệt đối mọi rủi ro văng bóng ra phòng
            CreateCushionBox(rootGo, "Table_OuterBoundary_North", new Vector3(1.35f, yCenter, 0f), new Vector3(0.16f, 0.30f, 2.00f), cushionMat);
            CreateCushionBox(rootGo, "Table_OuterBoundary_South", new Vector3(-1.35f, yCenter, 0f), new Vector3(0.16f, 0.30f, 2.00f), cushionMat);
            CreateCushionBox(rootGo, "Table_OuterBoundary_East", new Vector3(0f, yCenter, 0.85f), new Vector3(3.00f, 0.30f, 0.16f), cushionMat);
            CreateCushionBox(rootGo, "Table_OuterBoundary_West", new Vector3(0f, yCenter, -0.85f), new Vector3(3.00f, 0.30f, 0.16f), cushionMat);
        }

        private void DisableVisualMeshColliders()
        {
            var visual = transform.Find("TableVisual");
            if (visual == null) visual = transform;

            // Vô hiệu hóa toàn bộ Collider phức tạp của 3D visual model (bao gồm Play Field, Cushions, Upper part, Corner Pockets, Holes, Cabinet...)
            // Giữ nguyên 100% MeshRenderer để hình ảnh bàn bida và mặt nỉ xanh không bị thay đổi
            var cols = visual.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col.GetComponent<PocketDetector>() == null)
                {
                    col.enabled = false;
                }
            }
        }

        private static void CreateCushionBox(GameObject parent, string name, Vector3 pos, Vector3 size, PhysicMaterial mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = mat;
        }

        private static void CreateRotatedBox(GameObject parent, string name, Vector3 pos, Vector3 eulerAngles, Vector3 size, PhysicMaterial mat)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = pos;
            go.transform.localEulerAngles = eulerAngles;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = mat;
        }

        private static void CreateFacingBox(GameObject parent, string name, Vector2 p1, Vector2 p2, Vector2 normalIntoRail, float yCenter, float boxH, float thickness, PhysicMaterial mat)
        {
            Vector2 tangent = p2 - p1;
            float length = tangent.magnitude;
            if (length < 0.001f) return;

            Vector2 mid = (p1 + p2) * 0.5f;
            Vector2 normal = normalIntoRail.normalized;
            Vector3 center = new Vector3(mid.x + normal.x * (thickness * 0.5f), yCenter, mid.y + normal.y * (thickness * 0.5f));
            float rotY = Mathf.Atan2(tangent.x, tangent.y) * Mathf.Rad2Deg;

            CreateRotatedBox(parent, name, center, new Vector3(0f, rotY, 0f), new Vector3(thickness, boxH, length), mat);
        }

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
            float minX = FootCushionX + margin;
            float maxX = HeadCushionX - margin;
            float minZ = -playableHalfWidth + margin;
            float maxZ = playableHalfWidth - margin;

            return position.x >= minX && position.x <= maxX &&
                   position.z >= minZ && position.z <= maxZ;
        }

        public bool IsBehindHeadString(Vector3 position)
        {
            // Trong hệ tọa độ trục X: Đầu bàn về phía X dương (X >= headStringX)
            return position.x >= headStringX;
        }

        public Vector3 ClampToPlayArea(Vector3 position, float margin = 0.035f)
        {
            float clampedX = Mathf.Clamp(position.x, FootCushionX + margin, HeadCushionX - margin);
            float clampedZ = Mathf.Clamp(position.z, -playableHalfWidth + margin, playableHalfWidth - margin);
            return new Vector3(clampedX, clothSurfaceY + 0.02855f, clampedZ);
        }

        private void OnDrawGizmos()
        {
            DrawTableGizmos();
        }

        private void OnDrawGizmosSelected()
        {
            DrawTableGizmos();
        }

        private void DrawTableGizmos()
        {
            float yCloth = clothSurfaceY + 0.005f; // Ngay trên mặt vải nỉ
            float yNose = clothSurfaceY + 0.02855f; // Mép cao su tiếp xúc tâm bi (0.8635m)

            // Đường ranh giới màu xanh lá (Green) ôm sát 100% toàn bộ chu vi thành băng và miệng 6 lỗ
            Gizmos.color = Color.green;

            // 1. Vẽ chu vi khép kín ôm khít từng đỉnh mesh 3D ở tầng mũi băng (Nose) và tầng mặt nỉ (Cloth)
            DrawCushionLoop(yNose);
            DrawCushionLoop(yCloth);

            // 2. Vẽ các thanh đứng kết nối giữa mép chân băng và mép mũi băng tại tất cả các điểm góc
            DrawVerticalStruts(yCloth, yNose);

            // 3. Vẽ Head String (dọc trục Z tại vị trí headStringX)
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(headStringX, yCloth, -CushionHalfWidth),
                new Vector3(headStringX, yCloth, CushionHalfWidth)
            );

            // 4. Vẽ Foot Spot & Head Spot
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(footSpot, 0.015f);
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(headSpot, 0.015f);
        }

        private void DrawCushionLoop(float y)
        {
            // 1. Băng ngắn Foot (-X) và 2 má vát vào 2 lỗ góc
            Gizmos.DrawLine(new Vector3(-1.1294f, y, 0.4944f), new Vector3(-1.0746f, y, 0.4506f));
            Gizmos.DrawLine(new Vector3(-1.0746f, y, 0.4506f), new Vector3(FootCushionX, y, ShortCushionHalfSpanZ));
            Gizmos.DrawLine(new Vector3(FootCushionX, y, ShortCushionHalfSpanZ), new Vector3(FootCushionX, y, -ShortCushionHalfSpanZ));
            Gizmos.DrawLine(new Vector3(FootCushionX, y, -ShortCushionHalfSpanZ), new Vector3(-1.0746f, y, -0.4506f));
            Gizmos.DrawLine(new Vector3(-1.0746f, y, -0.4506f), new Vector3(-1.1294f, y, -0.4944f));

            // Vòm miệng túi góc FootRight (Pocket 1)
            Gizmos.DrawLine(new Vector3(-1.1294f, y, -0.4944f), new Vector3(-1.0635f, y, -0.5596f));

            // 2. Băng dài SideNeg (-Z) - Đoạn Foot
            Gizmos.DrawLine(new Vector3(-1.0635f, y, -0.5596f), new Vector3(-1.0194f, y, -0.5044f));
            Gizmos.DrawLine(new Vector3(-1.0194f, y, -0.5044f), new Vector3(-1.0140f, y, -CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(-1.0140f, y, -CushionHalfWidth), new Vector3(-0.0759f, y, -CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(-0.0759f, y, -CushionHalfWidth), new Vector3(-0.0716f, y, -0.5046f));
            Gizmos.DrawLine(new Vector3(-0.0716f, y, -0.5046f), new Vector3(-0.0575f, y, -0.5589f));

            // Vòm miệng túi giữa SideRight (Pocket 3)
            Gizmos.DrawLine(new Vector3(-0.0575f, y, -0.5589f), new Vector3(0.0454f, y, -0.5589f));

            // 3. Băng dài SideNeg (-Z) - Đoạn Head
            Gizmos.DrawLine(new Vector3(0.0454f, y, -0.5589f), new Vector3(0.0595f, y, -0.5046f));
            Gizmos.DrawLine(new Vector3(0.0595f, y, -0.5046f), new Vector3(0.0638f, y, -CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(0.0638f, y, -CushionHalfWidth), new Vector3(1.0020f, y, -CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(1.0020f, y, -CushionHalfWidth), new Vector3(1.0074f, y, -0.5044f));
            Gizmos.DrawLine(new Vector3(1.0074f, y, -0.5044f), new Vector3(1.0515f, y, -0.5596f));

            // Vòm miệng túi góc HeadRight (Pocket 5)
            Gizmos.DrawLine(new Vector3(1.0515f, y, -0.5596f), new Vector3(1.1173f, y, -0.4944f));

            // 4. Băng ngắn Head (+X) và 2 má vát vào 2 lỗ góc
            Gizmos.DrawLine(new Vector3(1.1173f, y, -0.4944f), new Vector3(1.0626f, y, -0.4506f));
            Gizmos.DrawLine(new Vector3(1.0626f, y, -0.4506f), new Vector3(HeadCushionX, y, -ShortCushionHalfSpanZ));
            Gizmos.DrawLine(new Vector3(HeadCushionX, y, -ShortCushionHalfSpanZ), new Vector3(HeadCushionX, y, ShortCushionHalfSpanZ));
            Gizmos.DrawLine(new Vector3(HeadCushionX, y, ShortCushionHalfSpanZ), new Vector3(1.0626f, y, 0.4506f));
            Gizmos.DrawLine(new Vector3(1.0626f, y, 0.4506f), new Vector3(1.1173f, y, 0.4944f));

            // Vòm miệng túi góc HeadLeft (Pocket 4)
            Gizmos.DrawLine(new Vector3(1.1173f, y, 0.4944f), new Vector3(1.0515f, y, 0.5596f));

            // 5. Băng dài SidePos (+Z) - Đoạn Head
            Gizmos.DrawLine(new Vector3(1.0515f, y, 0.5596f), new Vector3(1.0074f, y, 0.5044f));
            Gizmos.DrawLine(new Vector3(1.0074f, y, 0.5044f), new Vector3(1.0020f, y, CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(1.0020f, y, CushionHalfWidth), new Vector3(0.0638f, y, CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(0.0638f, y, CushionHalfWidth), new Vector3(0.0595f, y, 0.5046f));
            Gizmos.DrawLine(new Vector3(0.0595f, y, 0.5046f), new Vector3(0.0454f, y, 0.5589f));

            // Vòm miệng túi giữa SideLeft (Pocket 2)
            Gizmos.DrawLine(new Vector3(0.0454f, y, 0.5589f), new Vector3(-0.0575f, y, 0.5589f));

            // 6. Băng dài SidePos (+Z) - Đoạn Foot
            Gizmos.DrawLine(new Vector3(-0.0575f, y, 0.5589f), new Vector3(-0.0716f, y, 0.5046f));
            Gizmos.DrawLine(new Vector3(-0.0716f, y, 0.5046f), new Vector3(-0.0759f, y, CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(-0.0759f, y, CushionHalfWidth), new Vector3(-1.0140f, y, CushionHalfWidth));
            Gizmos.DrawLine(new Vector3(-1.0140f, y, CushionHalfWidth), new Vector3(-1.0194f, y, 0.5044f));
            Gizmos.DrawLine(new Vector3(-1.0194f, y, 0.5044f), new Vector3(-1.0635f, y, 0.5596f));

            // Vòm miệng túi góc FootLeft (Pocket 0) - khép kín vòng chu vi
            Gizmos.DrawLine(new Vector3(-1.0635f, y, 0.5596f), new Vector3(-1.1294f, y, 0.4944f));
        }

        private void DrawVerticalStruts(float y1, float y2)
        {
            // Các đường gióng đứng tại các điểm góc của mũi băng
            DrawStrut(-1.0724f, ShortCushionHalfSpanZ, y1, y2);
            DrawStrut(-1.0724f, -ShortCushionHalfSpanZ, y1, y2);
            DrawStrut(1.0603f, ShortCushionHalfSpanZ, y1, y2);
            DrawStrut(1.0603f, -ShortCushionHalfSpanZ, y1, y2);
            DrawStrut(-1.0140f, CushionHalfWidth, y1, y2);
            DrawStrut(-0.0759f, CushionHalfWidth, y1, y2);
            DrawStrut(0.0638f, CushionHalfWidth, y1, y2);
            DrawStrut(1.0020f, CushionHalfWidth, y1, y2);
            DrawStrut(-1.0140f, -CushionHalfWidth, y1, y2);
            DrawStrut(-0.0759f, -CushionHalfWidth, y1, y2);
            DrawStrut(0.0638f, -CushionHalfWidth, y1, y2);
            DrawStrut(1.0020f, -CushionHalfWidth, y1, y2);
        }

        private static void DrawStrut(float x, float z, float y1, float y2)
        {
            Gizmos.DrawLine(new Vector3(x, y1, z), new Vector3(x, y2, z));
        }
    }
}
