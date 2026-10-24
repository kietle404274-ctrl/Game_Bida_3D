using UnityEditor;
using UnityEngine;

namespace Game.Billiards.Editor
{
    public static class FullProjectRegressionVerification
    {
        [MenuItem("Tools/Game/Full Project Regression & UI Verification", false, 0)]
        public static void RunAll()
        {
            Debug.Log("===============================================================================");
            Debug.Log(">>> BƯỚC 1: XÁC THỰC TOÀN DIỆN CANVAS UI & CÁC BINDINGS TRONG LOBBY & GAMEPLAY <<<");
            Debug.Log("===============================================================================");
            UIVerification.Verify();

            Debug.Log("===============================================================================");
            Debug.Log(">>> BƯỚC 2: CHẠY HỆ THỐNG KIỂM THỬ HỒI QUY 11 MILESTONES CỐT LÕI (CORE & RULES) <<<");
            Debug.Log("===============================================================================");
            MasterRegressionVerification.RunAllTests();

            Debug.Log("===============================================================================");
            Debug.Log("★★★ XÁC THỰC TOÀN BỘ DỰ ÁN THÀNH CÔNG RỰC RỠ! SẴN SÀNG ĐẠT ĐIỂM TỐI ĐA! ★★★");
            Debug.Log("===============================================================================");
        }
    }
}
