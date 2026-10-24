using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.UI;
using Game.Billiards.Match;
using Game.Billiards.Input;
using Game.Billiards.Core;
using Game.Billiards.Physics;

namespace Game.Billiards.Editor
{
    public static class GameplayUIBuilder
    {
        [MenuItem("Tools/Game/Build Gameplay UI Canvas", false, 11)]
        public static void Build()
        {
            const string scenePath = "Assets/Game/Scenes/Gameplay.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Remove existing GameplayCanvas if any
            GameObject existingCanvas = GameObject.Find("GameplayCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            // 2. Find Systems GameObject
            GameObject systemsGo = GameObject.Find("Systems");
            if (systemsGo == null)
            {
                Debug.LogError("Systems GameObject not found in Gameplay scene!");
                return;
            }

            GameplayHUD hud = systemsGo.GetComponent<GameplayHUD>();
            if (hud == null) hud = systemsGo.AddComponent<GameplayHUD>();

            CueBallContactSelector contactSelector = systemsGo.GetComponent<CueBallContactSelector>();
            MatchStateManager stateMgr = systemsGo.GetComponent<MatchStateManager>();
            TurnManager turnMgr = systemsGo.GetComponent<TurnManager>();
            PlayerInputController inputCtrl = systemsGo.GetComponent<PlayerInputController>();
            BallManager ballMgr = systemsGo.GetComponent<BallManager>();
            BallMovementMonitor moveMonitor = systemsGo.GetComponent<BallMovementMonitor>();

            // Loại bỏ 2 gậy cơ thừa nằm trên bàn từ asset mẫu ban đầu nếu có
            GameObject oldCue1 = GameObject.Find("Cue Stick (1)");
            if (oldCue1 != null) Undo.DestroyObjectImmediate(oldCue1);
            GameObject oldCue2 = GameObject.Find("Cue Stick (2)");
            if (oldCue2 != null) Undo.DestroyObjectImmediate(oldCue2);

            // Tự động tìm kiếm và liên kết 16 quả bi vào BallManager
            if (ballMgr != null)
            {
                ballMgr.InitializeRegistry();
                EditorUtility.SetDirty(ballMgr);
            }

            // 2.1 Đảm bảo Prefab gậy cơ 3D (CueStickWrapper) tồn tại trong Scene và liên kết với các Controller
            GameObject cueGo = GameObject.Find("CueStickWrapper");
            if (cueGo == null)
            {
                GameObject cuePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Cue/CueStickWrapper.prefab");
                if (cuePrefab != null)
                {
                    cueGo = (GameObject)PrefabUtility.InstantiatePrefab(cuePrefab);
                    cueGo.name = "CueStickWrapper";
                    if (systemsGo.transform.parent != null)
                    {
                        cueGo.transform.SetParent(systemsGo.transform.parent, false);
                    }
                    cueGo.transform.position = new Vector3(0f, 0.8635f, -0.60f);
                }
            }

            CueController cueCtrl = cueGo != null ? cueGo.GetComponent<CueController>() : null;
            if (cueGo != null && cueCtrl == null)
            {
                cueCtrl = cueGo.AddComponent<CueController>();
                SerializedObject soCue = new SerializedObject(cueCtrl);
                soCue.FindProperty("cueStickRoot").objectReferenceValue = cueGo.transform;
                soCue.ApplyModifiedProperties();
            }

            if (cueCtrl != null)
            {
                // Liên kết với GameplayInitializer
                GameplayInitializer initializer = systemsGo.GetComponent<GameplayInitializer>();
                if (initializer != null)
                {
                    SerializedObject soInit = new SerializedObject(initializer);
                    soInit.FindProperty("cueController").objectReferenceValue = cueCtrl;
                    soInit.ApplyModifiedProperties();
                }

                // Liên kết với PlayerInputController
                if (inputCtrl != null)
                {
                    SerializedObject soInput = new SerializedObject(inputCtrl);
                    soInput.FindProperty("cueController").objectReferenceValue = cueCtrl;
                    soInput.ApplyModifiedProperties();
                }

                // Liên kết với AIController
                AI.AIController aiCtrl = systemsGo.GetComponent<AI.AIController>();
                if (aiCtrl != null)
                {
                    SerializedObject soAi = new SerializedObject(aiCtrl);
                    soAi.FindProperty("cueController").objectReferenceValue = cueCtrl;
                    soAi.ApplyModifiedProperties();
                }
            }

            // 2.2 Đảm bảo TableWrapper có BoxCollider mặt nỉ phẳng tuyệt đối
            TableWrapper tblWrapper = GameObject.FindObjectOfType<TableWrapper>();
            if (tblWrapper != null)
            {
                tblWrapper.EnsureClothBedCollider();
            }

            // 3. Create Main Canvas
            GameObject canvasGo = UIBuilderUtilities.CreateCanvas("GameplayCanvas");
            Transform canvasTrans = canvasGo.transform;

            // -------------------------------------------------------------------------
            // 4. TOP HUD BAR: [Cơ thủ 1 - hình bình hành] [Tỷ số - hình thang ngược] [Cơ thủ 2 - hình bình hành]
            // -------------------------------------------------------------------------
            GameObject topBarGo = new GameObject("TopHUDBar", typeof(RectTransform));
            topBarGo.transform.SetParent(canvasTrans, false);
            RectTransform tbRt = topBarGo.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0f, 1f);
            tbRt.anchorMax = new Vector2(1f, 1f);
            tbRt.pivot = new Vector2(0.5f, 1f);
            tbRt.anchoredPosition = new Vector2(0f, -12f);
            tbRt.sizeDelta = new Vector2(0f, 104f);

            const float panelW = 600f;
            const float panelH = 96f;
            const float skew = 38f;
            const float centerW = 400f;
            const float centerH = 84f;
            const float gap = 14f;
            // Cạnh trong của panel cơ thủ song song với cạnh xiên của khung tỷ số -> khe hở đều, nhìn như một khối liền mạch
            float panelOffset = centerW * 0.5f + gap - skew;

            Color panelColor = new Color(0.13f, 0.21f, 0.35f, 0.96f);
            Color borderColor = new Color(0.30f, 0.78f, 0.95f, 0.55f);

            // 4.1 Player 1 Panel (Left): hình bình hành, cạnh trong (phải) xiên song song với khung tỷ số
            GameObject p1Glow = CreateSkewedPanel(topBarGo.transform, "TurnGlow", UIBuilderUtilities.ColorAccentEmerald, 0f, skew, skew, 0f);
            RectTransform p1gRt = p1Glow.GetComponent<RectTransform>();
            p1gRt.anchorMin = p1gRt.anchorMax = new Vector2(0.5f, 0.5f);
            p1gRt.pivot = new Vector2(1f, 0.5f);
            p1gRt.anchoredPosition = new Vector2(-panelOffset + 8f, 0f);
            p1gRt.sizeDelta = new Vector2(panelW + 16f, panelH + 16f);
            p1Glow.SetActive(false);

            GameObject p1Border = CreateBorderProgress(topBarGo.transform, "Border", borderColor, 0f, skew, skew, 0f);
            RectTransform p1bRt = p1Border.GetComponent<RectTransform>();
            p1bRt.anchorMin = p1bRt.anchorMax = new Vector2(0.5f, 0.5f);
            p1bRt.pivot = new Vector2(1f, 0.5f);
            p1bRt.anchoredPosition = new Vector2(-panelOffset + 3f, 0f);
            p1bRt.sizeDelta = new Vector2(panelW + 6f, panelH + 6f);

            GameObject p1PanelGo = CreateSkewedPanel(topBarGo.transform, "PlayerLeftPanel", panelColor, 0f, skew, skew, 0f, 0.42f);
            RectTransform p1Rt = p1PanelGo.GetComponent<RectTransform>();
            p1Rt.anchorMin = p1Rt.anchorMax = new Vector2(0.5f, 0.5f);
            p1Rt.pivot = new Vector2(1f, 0.5f);
            p1Rt.anchoredPosition = new Vector2(-panelOffset, 0f);
            p1Rt.sizeDelta = new Vector2(panelW, panelH);
            PlayerPanelUI p1UI = p1PanelGo.AddComponent<PlayerPanelUI>();
            AddAccentStrip(p1PanelGo.transform, UIBuilderUtilities.ColorAccentEmerald, panelH, 5f, 0f, skew, skew, 0f, true);

            GameObject p1Avatar = UIBuilderUtilities.CreatePanel(p1PanelGo.transform, "Avatar", UIBuilderUtilities.CircleSprite, UIBuilderUtilities.ColorAccentEmerald);
            RectTransform p1aRt = p1Avatar.GetComponent<RectTransform>();
            p1aRt.anchoredPosition = new Vector2(-235f, 0f);
            p1aRt.sizeDelta = new Vector2(58f, 58f);
            Image p1AvatarImg = p1Avatar.GetComponent<Image>();
            p1AvatarImg.type = Image.Type.Simple;
            p1AvatarImg.raycastTarget = false;

            Text p1NameText = UIBuilderUtilities.CreateText(p1PanelGo.transform, "NameText", "CƠ THỦ 1 (BẠN)", 20, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleLeft, FontStyle.Bold);
            p1NameText.rectTransform.anchoredPosition = new Vector2(-48f, 28f);
            p1NameText.rectTransform.sizeDelta = new Vector2(290f, 26f);

            Text p1GroupText = UIBuilderUtilities.CreateText(p1PanelGo.transform, "GroupText", "BÀN MỞ (OPEN)", 12, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft, FontStyle.Normal);
            p1GroupText.rectTransform.anchoredPosition = new Vector2(-48f, 7f);
            p1GroupText.rectTransform.sizeDelta = new Vector2(290f, 18f);

            GameObject p1ScoreBadge = CreateSkewedPanel(p1PanelGo.transform, "ScoreBadge", new Color(0.04f, 0.07f, 0.12f, 0.95f), 0f, 0f, 0f, 0f);
            RectTransform p1sbRt = p1ScoreBadge.GetComponent<RectTransform>();
            p1sbRt.anchoredPosition = new Vector2(232f, 0f);
            p1sbRt.sizeDelta = new Vector2(52f, 52f);
            Text p1ScoreText = UIBuilderUtilities.CreateText(p1ScoreBadge.transform, "ScoreText", "0", 26, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            p1ScoreText.rectTransform.sizeDelta = new Vector2(52f, 52f);

            GameObject p1TrackerGo = new GameObject("BallTracker", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            p1TrackerGo.transform.SetParent(p1PanelGo.transform, false);
            RectTransform p1tRt = p1TrackerGo.GetComponent<RectTransform>();
            p1tRt.anchoredPosition = new Vector2(-48f, -24f);
            p1tRt.sizeDelta = new Vector2(290f, 30f);
            ConfigureTrackerLayout(p1TrackerGo.GetComponent<HorizontalLayoutGroup>(), TextAnchor.MiddleLeft);
            BallTrackerUI p1Tracker = p1TrackerGo.AddComponent<BallTrackerUI>();

            BallTrackerUI.BallSlot[] p1Slots = CreateBallSlots(p1TrackerGo.transform, 1, 7);
            BallTrackerUI.BallSlot p1EightSlot = CreateSingleBallSlot(p1TrackerGo.transform, 8);

            WireBallTracker(p1Tracker, p1Slots, p1EightSlot);
            WirePlayerPanel(p1UI, p1NameText, p1GroupText, p1ScoreText, p1AvatarImg, p1Glow, p1PanelGo.GetComponent<Graphic>(), p1Tracker, CreateTurnTimer(p1PanelGo.transform, 150f, p1Border.GetComponent<BorderProgressGraphic>()));

            // 4.2 Center Scoreboard: hình thang cân ngược (đáy lớn ở trên) - tỷ số ở trên, RACE TO ở dưới
            GameObject centerBorder = CreateSkewedPanel(topBarGo.transform, "Border", borderColor, 0f, 0f, skew + 1.5f, skew + 1.5f);
            RectTransform cbbRt = centerBorder.GetComponent<RectTransform>();
            cbbRt.anchorMin = cbbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbbRt.pivot = new Vector2(0.5f, 0.5f);
            cbbRt.anchoredPosition = Vector2.zero;
            cbbRt.sizeDelta = new Vector2(centerW + 6f, centerH + 5f);

            GameObject centerBoardGo = CreateSkewedPanel(topBarGo.transform, "MatchHeader", new Color(0.10f, 0.15f, 0.26f, 0.97f), 0f, 0f, skew, skew, 0.30f);
            AddAccentStrip(centerBoardGo.transform, UIBuilderUtilities.ColorAccentGold, centerH, 4f, 0f, 0f, skew, skew, false);

            RectTransform cbRt = centerBoardGo.GetComponent<RectTransform>();
            cbRt.anchorMin = cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.anchoredPosition = Vector2.zero;
            cbRt.sizeDelta = new Vector2(centerW, centerH);
            MatchHeaderUI matchHeader = centerBoardGo.AddComponent<MatchHeaderUI>();

            Text scoreRaceText = UIBuilderUtilities.CreateText(centerBoardGo.transform, "ScoreRaceText", "0 - 0", 44, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            scoreRaceText.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            scoreRaceText.rectTransform.sizeDelta = new Vector2(320f, 56f);

            Text rackNumText = UIBuilderUtilities.CreateText(centerBoardGo.transform, "RackNumText", "RACE TO 5  •  VÁN 1", 15, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            rackNumText.rectTransform.anchoredPosition = new Vector2(0f, -19f);
            rackNumText.rectTransform.sizeDelta = new Vector2(300f, 22f);

            // Đồng hồ lượt đánh nằm trong bảng tên từng cơ thủ (TurnTimerUI), không còn thanh ở giữa
            WireMatchHeader(matchHeader, rackNumText, scoreRaceText, null, null, null);

            // 4.3 Player 2 Panel (Right): đối xứng với panel trái qua tâm màn hình
            GameObject p2Glow = CreateSkewedPanel(topBarGo.transform, "TurnGlow", UIBuilderUtilities.ColorAccentBlue, skew, 0f, 0f, skew);
            RectTransform p2gRt = p2Glow.GetComponent<RectTransform>();
            p2gRt.anchorMin = p2gRt.anchorMax = new Vector2(0.5f, 0.5f);
            p2gRt.pivot = new Vector2(0f, 0.5f);
            p2gRt.anchoredPosition = new Vector2(panelOffset - 8f, 0f);
            p2gRt.sizeDelta = new Vector2(panelW + 16f, panelH + 16f);
            p2Glow.SetActive(false);

            GameObject p2Border = CreateBorderProgress(topBarGo.transform, "Border", borderColor, skew, 0f, 0f, skew);
            RectTransform p2bRt = p2Border.GetComponent<RectTransform>();
            p2bRt.anchorMin = p2bRt.anchorMax = new Vector2(0.5f, 0.5f);
            p2bRt.pivot = new Vector2(0f, 0.5f);
            p2bRt.anchoredPosition = new Vector2(panelOffset - 3f, 0f);
            p2bRt.sizeDelta = new Vector2(panelW + 6f, panelH + 6f);

            GameObject p2PanelGo = CreateSkewedPanel(topBarGo.transform, "PlayerRightPanel", panelColor, skew, 0f, 0f, skew, 0.42f);
            RectTransform p2Rt = p2PanelGo.GetComponent<RectTransform>();
            p2Rt.anchorMin = p2Rt.anchorMax = new Vector2(0.5f, 0.5f);
            p2Rt.pivot = new Vector2(0f, 0.5f);
            p2Rt.anchoredPosition = new Vector2(panelOffset, 0f);
            p2Rt.sizeDelta = new Vector2(panelW, panelH);
            PlayerPanelUI p2UI = p2PanelGo.AddComponent<PlayerPanelUI>();
            AddAccentStrip(p2PanelGo.transform, UIBuilderUtilities.ColorAccentBlue, panelH, 5f, skew, 0f, 0f, skew, true);

            GameObject p2Avatar = UIBuilderUtilities.CreatePanel(p2PanelGo.transform, "Avatar", UIBuilderUtilities.CircleSprite, UIBuilderUtilities.ColorAccentBlue);
            RectTransform p2aRt = p2Avatar.GetComponent<RectTransform>();
            p2aRt.anchoredPosition = new Vector2(235f, 0f);
            p2aRt.sizeDelta = new Vector2(58f, 58f);
            Image p2AvatarImg = p2Avatar.GetComponent<Image>();
            p2AvatarImg.type = Image.Type.Simple;
            p2AvatarImg.raycastTarget = false;

            Text p2NameText = UIBuilderUtilities.CreateText(p2PanelGo.transform, "NameText", "CƠ THỦ 2 (AI)", 20, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleRight, FontStyle.Bold);
            p2NameText.rectTransform.anchoredPosition = new Vector2(48f, 28f);
            p2NameText.rectTransform.sizeDelta = new Vector2(290f, 26f);

            Text p2GroupText = UIBuilderUtilities.CreateText(p2PanelGo.transform, "GroupText", "BÀN MỞ (OPEN)", 12, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleRight, FontStyle.Normal);
            p2GroupText.rectTransform.anchoredPosition = new Vector2(48f, 7f);
            p2GroupText.rectTransform.sizeDelta = new Vector2(290f, 18f);

            GameObject p2ScoreBadge = CreateSkewedPanel(p2PanelGo.transform, "ScoreBadge", new Color(0.04f, 0.07f, 0.12f, 0.95f), 0f, 0f, 0f, 0f);
            RectTransform p2sbRt = p2ScoreBadge.GetComponent<RectTransform>();
            p2sbRt.anchoredPosition = new Vector2(-232f, 0f);
            p2sbRt.sizeDelta = new Vector2(52f, 52f);
            Text p2ScoreText = UIBuilderUtilities.CreateText(p2ScoreBadge.transform, "ScoreText", "0", 26, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            p2ScoreText.rectTransform.sizeDelta = new Vector2(52f, 52f);

            GameObject p2TrackerGo = new GameObject("BallTracker", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            p2TrackerGo.transform.SetParent(p2PanelGo.transform, false);
            RectTransform p2tRt = p2TrackerGo.GetComponent<RectTransform>();
            p2tRt.anchoredPosition = new Vector2(48f, -24f);
            p2tRt.sizeDelta = new Vector2(290f, 30f);
            ConfigureTrackerLayout(p2TrackerGo.GetComponent<HorizontalLayoutGroup>(), TextAnchor.MiddleRight);
            BallTrackerUI p2Tracker = p2TrackerGo.AddComponent<BallTrackerUI>();

            BallTrackerUI.BallSlot[] p2Slots = CreateBallSlots(p2TrackerGo.transform, 9, 15);
            BallTrackerUI.BallSlot p2EightSlot = CreateSingleBallSlot(p2TrackerGo.transform, 8);

            WireBallTracker(p2Tracker, p2Slots, p2EightSlot);
            WirePlayerPanel(p2UI, p2NameText, p2GroupText, p2ScoreText, p2AvatarImg, p2Glow, p2PanelGo.GetComponent<Graphic>(), p2Tracker, CreateTurnTimer(p2PanelGo.transform, -150f, p2Border.GetComponent<BorderProgressGraphic>()));

            // -------------------------------------------------------------------------
            // 5. BOTTOM-LEFT POWER BAR (PowerBarUI)
            // -------------------------------------------------------------------------
            const float pwW = 120f;
            const float pwH = 350f;
            const float pwCut = 24f;
            GameObject powerMeterGo = CreateSkewedPanel(canvasTrans, "PowerMeterPanel", new Color(0.07f, 0.11f, 0.19f, 0.94f), 0f, pwCut, pwCut, 0f, 0.45f);
            RectTransform pmRt = powerMeterGo.GetComponent<RectTransform>();
            pmRt.anchorMin = new Vector2(0f, 0f);
            pmRt.anchorMax = new Vector2(0f, 0f);
            pmRt.pivot = new Vector2(0f, 0f);
            pmRt.anchoredPosition = new Vector2(30f, 30f);
            pmRt.sizeDelta = new Vector2(pwW, pwH);
            PowerBarUI powerBarUI = powerMeterGo.AddComponent<PowerBarUI>();

            // Viền sáng vát góc (nhấp nháy đỏ khi lực > 90%) + dải vàng trên đỉnh
            GameObject powerFrame = CreateBorderProgress(powerMeterGo.transform, "Frame", borderColor, 0f, pwCut, pwCut, 0f);
            RectTransform pfrRt = powerFrame.GetComponent<RectTransform>();
            pfrRt.anchorMin = Vector2.zero;
            pfrRt.anchorMax = Vector2.one;
            pfrRt.offsetMin = Vector2.zero;
            pfrRt.offsetMax = Vector2.zero;
            AddAccentStrip(powerMeterGo.transform, UIBuilderUtilities.ColorAccentGold, pwH, 4f, 0f, pwCut, pwCut, 0f, false);

            Text powerTitle = UIBuilderUtilities.CreateText(powerMeterGo.transform, "PowerTitle", "POWER", 13, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            powerTitle.rectTransform.anchoredPosition = new Vector2(-10f, 152f);
            powerTitle.rectTransform.sizeDelta = new Vector2(90f, 20f);

            Text powerPercentText = UIBuilderUtilities.CreateText(powerMeterGo.transform, "PercentText", "50%", 30, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            powerPercentText.rectTransform.anchoredPosition = new Vector2(-9f, 125f);
            powerPercentText.rectTransform.sizeDelta = new Vector2(96f, 38f);

            // Khung viền quanh rãnh lực (hình bình hành nghiêng cùng chiều với khung ngoài)
            const float trSlant = 17f;
            GameObject powerTrackFrame = CreateSkewedPanel(powerMeterGo.transform, "TrackFrame", new Color(0.30f, 0.78f, 0.95f, 0.55f), 0f, trSlant, trSlant, 0f);
            RectTransform ptfRt = powerTrackFrame.GetComponent<RectTransform>();
            ptfRt.anchoredPosition = new Vector2(-10f, -22f);
            ptfRt.sizeDelta = new Vector2(58f, 248f);

            GameObject powerTrackBg = CreateSkewedPanel(powerMeterGo.transform, "TrackBg", new Color(0.03f, 0.05f, 0.09f, 1f), 0f, trSlant, trSlant, 0f);
            RectTransform ptbRt = powerTrackBg.GetComponent<RectTransform>();
            ptbRt.anchoredPosition = new Vector2(-10f, -22f);
            ptbRt.sizeDelta = new Vector2(52f, 242f);

            // Thanh lực nghiêng chia ô LED xanh -> vàng -> đỏ
            GameObject powerBarGo = new GameObject("PowerBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(SlantedBarGraphic));
            powerBarGo.layer = LayerMask.NameToLayer("UI");
            powerBarGo.transform.SetParent(powerTrackBg.transform, false);
            RectTransform pbRt = powerBarGo.GetComponent<RectTransform>();
            pbRt.anchorMin = Vector2.zero;
            pbRt.anchorMax = Vector2.one;
            pbRt.offsetMin = new Vector2(3f, 3f);
            pbRt.offsetMax = new Vector2(-3f, -3f);
            SlantedBarGraphic slantedBar = powerBarGo.GetComponent<SlantedBarGraphic>();
            slantedBar.raycastTarget = false;
            SerializedObject soBar = new SerializedObject(slantedBar);
            soBar.FindProperty("slant").floatValue = trSlant - 0.5f;
            soBar.ApplyModifiedPropertiesWithoutUndo();


            // Vạch đỉnh (giữ mức lực cao nhất)
            GameObject peakGo = UIBuilderUtilities.CreatePanel(powerTrackBg.transform, "PeakMarker", null, Color.white);
            RectTransform peakRt = peakGo.GetComponent<RectTransform>();
            peakRt.anchorMin = new Vector2(0.5f, 0f);
            peakRt.anchorMax = new Vector2(0.5f, 0f);
            peakRt.pivot = new Vector2(0.5f, 0.5f);
            peakRt.anchoredPosition = Vector2.zero;
            peakRt.sizeDelta = new Vector2(44f, 3f);
            peakGo.GetComponent<Image>().raycastTarget = false;

            // Nhãn mốc bên phải rãnh (dịch theo độ nghiêng của khung)
            string[] pwLabels = { "MAX", "50", "MIN" };
            float[] pwLabelY = { 99f, -22f, -143f };
            for (int i = 0; i < 3; i++)
            {
                float lf = (pwLabelY[i] + pwH * 0.5f) / pwH;
                Text lbl = UIBuilderUtilities.CreateText(powerMeterGo.transform, "Label" + pwLabels[i], pwLabels[i], 10, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
                lbl.rectTransform.anchoredPosition = new Vector2(31f + pwCut * (0.437f - lf), pwLabelY[i]);
                lbl.rectTransform.sizeDelta = new Vector2(30f, 14f);
            }

            WirePowerBar(powerBarUI, null, powerPercentText, powerTrackBg.GetComponent<RectTransform>(), peakRt, powerFrame.GetComponent<BorderProgressGraphic>(), slantedBar);
            powerMeterGo.AddComponent<CanvasGroup>();
            powerMeterGo.AddComponent<AimOnlyHudVisibility>();

            // -------------------------------------------------------------------------
            // 5b. CUE BALL CONTACT: panel nhỏ bottom-right (chỉ hiển thị) + popup lớn giữa màn hình (phím J)
            // -------------------------------------------------------------------------
            const float spW = 180f;
            const float spH = 220f;
            const float spCut = 20f;
            // Khung vát góc đối xứng với khung lực, cùng viền sáng + dải vàng
            GameObject spinPanelGo = CreateSkewedPanel(canvasTrans, "SpinSelectorPanel", new Color(0.07f, 0.11f, 0.19f, 0.94f), spCut, 0f, 0f, spCut, 0.45f);
            RectTransform spRt = spinPanelGo.GetComponent<RectTransform>();
            spRt.anchorMin = new Vector2(1f, 0f);
            spRt.anchorMax = new Vector2(1f, 0f);
            spRt.pivot = new Vector2(1f, 0f);
            spRt.anchoredPosition = new Vector2(-30f, 30f);
            spRt.sizeDelta = new Vector2(spW, spH);
            spinPanelGo.AddComponent<CanvasGroup>();
            spinPanelGo.AddComponent<AimOnlyHudVisibility>();

            GameObject spinFrame = CreateBorderProgress(spinPanelGo.transform, "Frame", borderColor, spCut, 0f, 0f, spCut);
            RectTransform spfRt = spinFrame.GetComponent<RectTransform>();
            spfRt.anchorMin = Vector2.zero;
            spfRt.anchorMax = Vector2.one;
            spfRt.offsetMin = Vector2.zero;
            spfRt.offsetMax = Vector2.zero;
            AddAccentStrip(spinPanelGo.transform, UIBuilderUtilities.ColorAccentGold, spH, 4f, spCut, 0f, 0f, spCut, false);

            Text spinTitle = UIBuilderUtilities.CreateText(spinPanelGo.transform, "SpinTitle", "ĐIỂM CƠ [J]", 14, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            spinTitle.rectTransform.anchoredPosition = new Vector2(4f, 92f);
            spinTitle.rectTransform.sizeDelta = new Vector2(150f, 24f);

            // Vòng viền sáng quanh bi cái
            GameObject spinRing = UIBuilderUtilities.CreatePanel(spinPanelGo.transform, "BallRing", UIBuilderUtilities.CircleSprite, new Color(0.30f, 0.78f, 0.95f, 0.65f));
            RectTransform spinRingRt = spinRing.GetComponent<RectTransform>();
            spinRingRt.anchoredPosition = new Vector2(0f, 5f);
            spinRingRt.sizeDelta = new Vector2(150f, 150f);
            spinRing.GetComponent<Image>().type = Image.Type.Simple;
            spinRing.GetComponent<Image>().raycastTarget = false;

            BuildCueBallWidget(spinPanelGo.transform, contactSelector, 140f, 18f, 15, new Vector2(0f, 5f), new Vector2(0f, -92f), false);

            // Popup lớn giữa màn hình
            GameObject popupGo = UIBuilderUtilities.CreatePanel(canvasTrans, "SpinPopup", null, new Color(0f, 0f, 0f, 0.65f));
            RectTransform popRt = popupGo.GetComponent<RectTransform>();
            popRt.anchorMin = Vector2.zero;
            popRt.anchorMax = Vector2.one;
            popRt.offsetMin = Vector2.zero;
            popRt.offsetMax = Vector2.zero;
            popupGo.GetComponent<Image>().type = Image.Type.Simple;
            popupGo.AddComponent<CanvasGroup>();
            SpinPopupVisibility popVis = popupGo.AddComponent<SpinPopupVisibility>();
            SerializedObject soPop = new SerializedObject(popVis);
            soPop.FindProperty("selector").objectReferenceValue = contactSelector;
            soPop.ApplyModifiedPropertiesWithoutUndo();

            GameObject popCard = UIBuilderUtilities.CreatePanel(popupGo.transform, "PopupCard", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            popCard.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 700f);

            Text popTitle = UIBuilderUtilities.CreateText(popCard.transform, "PopupTitle", "CHỌN ĐIỂM ĐÁNH TRÊN BI CÁI", 26, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            popTitle.rectTransform.anchoredPosition = new Vector2(0f, 310f);
            popTitle.rectTransform.sizeDelta = new Vector2(520f, 40f);

            BuildCueBallWidget(popCard.transform, contactSelector, 420f, 36f, 28, new Vector2(0f, 20f), new Vector2(0f, -220f), true);

            Text popHint = UIBuilderUtilities.CreateText(popCard.transform, "PopupHint", "Click / kéo để chọn  •  Mũi tên: tinh chỉnh  •  R: về giữa  •  ENTER: xác nhận", 16, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Normal);
            popHint.rectTransform.anchoredPosition = new Vector2(0f, -290f);
            popHint.rectTransform.sizeDelta = new Vector2(540f, 30f);
            popupGo.transform.SetAsLastSibling();

            // -------------------------------------------------------------------------
            // 6. TOP-RIGHT ACTION BAR (Hướng dẫn, Luật, Menu) - nằm dưới thanh tỷ số ở góc phải
            // -------------------------------------------------------------------------
            GameObject actionBarGo = new GameObject("ActionBar", typeof(RectTransform), typeof(VerticalLayoutGroup));
            actionBarGo.transform.SetParent(canvasTrans, false);
            RectTransform abRt = actionBarGo.GetComponent<RectTransform>();
            abRt.anchorMin = new Vector2(1f, 1f);
            abRt.anchorMax = new Vector2(1f, 1f);
            abRt.pivot = new Vector2(1f, 1f);
            abRt.anchoredPosition = new Vector2(-14f, -14f);
            abRt.sizeDelta = new Vector2(160f, 140f);

            VerticalLayoutGroup abHz = actionBarGo.GetComponent<VerticalLayoutGroup>();
            abHz.spacing = 8f;
            abHz.childAlignment = TextAnchor.UpperRight;
            abHz.childControlWidth = true;
            abHz.childControlHeight = true;
            abHz.childForceExpandWidth = false;
            abHz.childForceExpandHeight = false;

            Button btnHelp = CreateHudButton(actionBarGo.transform, "BtnHelp", "HƯỚNG DẪN", "H", 160f, borderColor, "help", 40f);
            Button btnRules = CreateHudButton(actionBarGo.transform, "BtnRules", "LUẬT", "R", 160f, borderColor, "rules", 40f);
            Button btnSettings = CreateHudButton(actionBarGo.transform, "BtnSettings", "MENU", "ESC", 160f, borderColor, "menu", 40f);

            // -------------------------------------------------------------------------
            // 7. CALL SHOT PANEL (CallShotUI)
            // -------------------------------------------------------------------------
            GameObject callShotGo = UIBuilderUtilities.CreatePanel(canvasTrans, "CallShotPanel", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform csRt = callShotGo.GetComponent<RectTransform>();
            csRt.anchorMin = new Vector2(0.5f, 0f);
            csRt.anchorMax = new Vector2(0.5f, 0f);
            csRt.pivot = new Vector2(0.5f, 0f);
            csRt.anchoredPosition = new Vector2(0f, 25f);
            csRt.sizeDelta = new Vector2(620f, 130f);
            CallShotUI callShotUI = callShotGo.AddComponent<CallShotUI>();

            Text csTitle = UIBuilderUtilities.CreateText(callShotGo.transform, "Title", "🎱 CÚ ĐÁNH GỌI (CALL SHOT / SAFETY)", 16, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            csTitle.rectTransform.anchoredPosition = new Vector2(0f, 44f);
            csTitle.rectTransform.sizeDelta = new Vector2(580f, 26f);

            Dropdown ballDrop = UIBuilderUtilities.CreateDropdown(callShotGo.transform, "BallDropdown", new List<string> { "Bi số 1" }, 14);
            ballDrop.GetComponent<RectTransform>().anchoredPosition = new Vector2(-170f, 5f);
            ballDrop.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 36f);

            Dropdown pocketDrop = UIBuilderUtilities.CreateDropdown(callShotGo.transform, "PocketDropdown", new List<string> { "Lỗ Góc Trên Trái", "Lỗ Góc Dưới Trái", "Lỗ Góc Trên Phải", "Lỗ Góc Dưới Phải", "Lỗ Giữa Trái", "Lỗ Giữa Phải" }, 13);
            pocketDrop.GetComponent<RectTransform>().anchoredPosition = new Vector2(25f, 5f);
            pocketDrop.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 36f);

            Button btnSafety = UIBuilderUtilities.CreateButton(callShotGo.transform, "BtnSafety", "🛡️ SAFETY", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorCard, UIBuilderUtilities.ColorTextWhite, 14);
            btnSafety.GetComponent<RectTransform>().anchoredPosition = new Vector2(210f, 5f);
            btnSafety.GetComponent<RectTransform>().sizeDelta = new Vector2(130f, 36f);
            Image btnSafetyImg = btnSafety.GetComponent<Image>();

            Text csSummary = UIBuilderUtilities.CreateText(callShotGo.transform, "SummaryText", "Gọi: Bi # -- Lỗ: --", 13, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            csSummary.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            csSummary.rectTransform.sizeDelta = new Vector2(580f, 22f);

            WireCallShotUI(callShotUI, callShotGo, ballDrop, pocketDrop, btnSafety, btnSafetyImg, csSummary);
            callShotGo.SetActive(false);

            // -------------------------------------------------------------------------
            // 8. EVENT TOAST NOTIFICATIONS (EventToastUI)
            // -------------------------------------------------------------------------
            GameObject toastGo = UIBuilderUtilities.CreatePanel(canvasTrans, "EventToast", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorAccentCyan);
            RectTransform trt = toastGo.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -125f);
            trt.sizeDelta = new Vector2(580f, 85f);
            EventToastUI eventToastUI = toastGo.AddComponent<EventToastUI>();
            CanvasGroup toastCg = toastGo.AddComponent<CanvasGroup>();

            Text toastTitle = UIBuilderUtilities.CreateText(toastGo.transform, "Title", "THÔNG BÁO", 18, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            toastTitle.rectTransform.anchoredPosition = new Vector2(0f, 18f);
            toastTitle.rectTransform.sizeDelta = new Vector2(540f, 28f);

            Text toastMsg = UIBuilderUtilities.CreateText(toastGo.transform, "Message", "Chi tiết sự kiện...", 14, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter);
            toastMsg.rectTransform.anchoredPosition = new Vector2(0f, -12f);
            toastMsg.rectTransform.sizeDelta = new Vector2(540f, 24f);

            WireEventToast(eventToastUI, toastGo, toastTitle, toastMsg, toastGo.GetComponent<Image>(), toastCg);
            toastGo.SetActive(false);

            // -------------------------------------------------------------------------
            // 9. MODALS (ResultUI, SettingsUI, RulesUI, HelpUI)
            // -------------------------------------------------------------------------
            GameObject modalsContainer = new GameObject("ModalsContainer", typeof(RectTransform));
            modalsContainer.transform.SetParent(canvasTrans, false);
            RectTransform mcRt = modalsContainer.GetComponent<RectTransform>();
            mcRt.anchorMin = Vector2.zero;
            mcRt.anchorMax = Vector2.one;
            mcRt.offsetMin = Vector2.zero;
            mcRt.offsetMax = Vector2.zero;

            // 9.1 Result Modal (ResultUI)
            GameObject resultModalGo = UIBuilderUtilities.CreatePanel(modalsContainer.transform, "ResultModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform rmRt = resultModalGo.GetComponent<RectTransform>();
            rmRt.anchorMin = new Vector2(0.5f, 0.5f);
            rmRt.anchorMax = new Vector2(0.5f, 0.5f);
            rmRt.sizeDelta = new Vector2(650f, 480f);
            ResultUI resultUI = resultModalGo.AddComponent<ResultUI>();

            Text resTitle = UIBuilderUtilities.CreateText(resultModalGo.transform, "Title", "🏆 KẾT THÚC VÁN ĐẤU 🏆", 28, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            resTitle.rectTransform.anchoredPosition = new Vector2(0f, 170f);
            resTitle.rectTransform.sizeDelta = new Vector2(600f, 45f);

            Text resWinner = UIBuilderUtilities.CreateText(resultModalGo.transform, "WinnerName", "CƠ THỦ 1 CHIẾN THẮNG!", 22, UIBuilderUtilities.ColorAccentEmerald, TextAnchor.MiddleCenter, FontStyle.Bold);
            resWinner.rectTransform.anchoredPosition = new Vector2(0f, 110f);
            resWinner.rectTransform.sizeDelta = new Vector2(600f, 35f);

            Text resScore = UIBuilderUtilities.CreateText(resultModalGo.transform, "ScoreSummary", "TỈ SỐ HIỆN TẠI: 1 - 0 (Race to 5)", 18, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            resScore.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            resScore.rectTransform.sizeDelta = new Vector2(600f, 30f);

            Text resDetails = UIBuilderUtilities.CreateText(resultModalGo.transform, "Details", "Ăn bi số 8 hợp lệ vào lỗ gọi!", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            resDetails.rectTransform.anchoredPosition = new Vector2(0f, 10f);
            resDetails.rectTransform.sizeDelta = new Vector2(600f, 25f);

            Button btnNextRack = UIBuilderUtilities.CreateButton(resultModalGo.transform, "BtnNextRack", "⚡ VÁN TIẾP THEO ⚡", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentEmerald, UIBuilderUtilities.ColorTextWhite, 18);
            btnNextRack.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -65f);
            btnNextRack.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 50f);

            Button btnRematch = UIBuilderUtilities.CreateButton(resultModalGo.transform, "BtnRematch", "🔄 ĐẤU LẠI TỪ ĐẦU", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentCyan, UIBuilderUtilities.ColorTextWhite, 18);
            btnRematch.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -65f);
            btnRematch.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 50f);
            btnRematch.gameObject.SetActive(false);

            Button btnReturnLobby = UIBuilderUtilities.CreateButton(resultModalGo.transform, "BtnReturnLobby", "🚪 VỀ SẢNH CHỜ (LOBBY)", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorCard, UIBuilderUtilities.ColorTextMuted, 16);
            btnReturnLobby.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -135f);
            btnReturnLobby.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 45f);

            WireResultUI(resultUI, resultModalGo, resTitle, resWinner, resScore, resDetails, btnNextRack, btnRematch, btnReturnLobby);
            resultModalGo.SetActive(false);

            // 9.2 In-game Settings / Pause Modal (SettingsUI)
            GameObject settingsModalGo = UIBuilderUtilities.CreatePanel(modalsContainer.transform, "SettingsModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform smRt = settingsModalGo.GetComponent<RectTransform>();
            smRt.anchorMin = new Vector2(0.5f, 0.5f);
            smRt.anchorMax = new Vector2(0.5f, 0.5f);
            smRt.sizeDelta = new Vector2(580f, 620f);
            SettingsUI settingsUI = settingsModalGo.AddComponent<SettingsUI>();

            Text setmTitle = UIBuilderUtilities.CreateText(settingsModalGo.transform, "Title", "TẠM DỪNG & CÀI ĐẶT", 24, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            setmTitle.rectTransform.anchoredPosition = new Vector2(0f, 250f);
            setmTitle.rectTransform.sizeDelta = new Vector2(500f, 40f);

            Slider sldMaster = UIBuilderUtilities.CreateSlider(settingsModalGo.transform, "SldMaster", 0f, 1f, 0.8f);
            sldMaster.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 175f);
            sldMaster.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 25f);
            Text lblMaster = UIBuilderUtilities.CreateText(settingsModalGo.transform, "LblMaster", "Âm lượng tổng (Master):", 14, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblMaster.rectTransform.anchoredPosition = new Vector2(0f, 205f);
            lblMaster.rectTransform.sizeDelta = new Vector2(460f, 22f);

            Slider sldMusic = UIBuilderUtilities.CreateSlider(settingsModalGo.transform, "SldMusic", 0f, 1f, 0.6f);
            sldMusic.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 105f);
            sldMusic.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 25f);
            Text lblMusic = UIBuilderUtilities.CreateText(settingsModalGo.transform, "LblMusic", "Nhạc nền (BGM):", 14, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblMusic.rectTransform.anchoredPosition = new Vector2(0f, 135f);
            lblMusic.rectTransform.sizeDelta = new Vector2(460f, 22f);

            Slider sldSFX = UIBuilderUtilities.CreateSlider(settingsModalGo.transform, "SldSFX", 0f, 1f, 0.9f);
            sldSFX.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 35f);
            sldSFX.GetComponent<RectTransform>().sizeDelta = new Vector2(460f, 25f);
            Text lblSFX = UIBuilderUtilities.CreateText(settingsModalGo.transform, "LblSFX", "Hiệu ứng va chạm (SFX):", 14, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblSFX.rectTransform.anchoredPosition = new Vector2(0f, 65f);
            lblSFX.rectTransform.sizeDelta = new Vector2(460f, 22f);

            Button btnResume = CreateHudButton(settingsModalGo.transform, "BtnResume", "TIẾP TỤC TRẬN ĐẤU", "ESC", 440f, borderColor, "play", 46f);
            btnResume.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -40f);

            Button btnSurrender = CreateHudButton(settingsModalGo.transform, "BtnSurrender", "XIN CHỊU THUA", "", 440f, new Color(0.95f, 0.30f, 0.30f, 0.75f), "flag", 44f);
            btnSurrender.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -100f);

            Button btnQuitMatch = CreateHudButton(settingsModalGo.transform, "BtnQuitMatch", "THOÁT VỀ SẢNH CHỜ", "", 440f, borderColor, "exit", 44f);
            btnQuitMatch.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -155f);

            Button btnCloseSettings = CreateHudButton(settingsModalGo.transform, "BtnClose", "", "", 44f, borderColor, "close", 40f);
            btnCloseSettings.GetComponent<RectTransform>().anchorMin = new Vector2(1f, 1f);
            btnCloseSettings.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
            btnCloseSettings.GetComponent<RectTransform>().anchoredPosition = new Vector2(-70f, -34f);

            // Confirm Dialog Inside Settings
            GameObject confirmDialogGo = UIBuilderUtilities.CreatePanel(settingsModalGo.transform, "ConfirmDialog", UIBuilderUtilities.PanelSprite, new Color(0.05f, 0.08f, 0.14f, 0.98f));
            RectTransform cdRt = confirmDialogGo.GetComponent<RectTransform>();
            cdRt.anchorMin = Vector2.zero;
            cdRt.anchorMax = Vector2.one;
            cdRt.offsetMin = Vector2.zero;
            cdRt.offsetMax = Vector2.zero;

            Text confirmText = UIBuilderUtilities.CreateText(confirmDialogGo.transform, "ConfirmText", "Xác nhận hành động?", 18, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            confirmText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            confirmText.rectTransform.sizeDelta = new Vector2(450f, 60f);

            Button btnConfirmYes = UIBuilderUtilities.CreateButton(confirmDialogGo.transform, "BtnYes", "XÁC NHẬN", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentCrimson, UIBuilderUtilities.ColorTextWhite, 16);
            btnConfirmYes.GetComponent<RectTransform>().anchoredPosition = new Vector2(-100f, -40f);
            btnConfirmYes.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 45f);

            Button btnConfirmNo = UIBuilderUtilities.CreateButton(confirmDialogGo.transform, "BtnNo", "HỦY BỎ", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorCard, UIBuilderUtilities.ColorTextWhite, 16);
            btnConfirmNo.GetComponent<RectTransform>().anchoredPosition = new Vector2(100f, -40f);
            btnConfirmNo.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 45f);

            confirmDialogGo.SetActive(false);
            DecorateModal(settingsModalGo, 580f, 620f, borderColor);
            WireSettingsUI(settingsUI, settingsModalGo, btnCloseSettings, btnResume, sldMaster, sldMusic, sldSFX, btnSurrender, btnQuitMatch, confirmDialogGo, confirmText, btnConfirmYes, btnConfirmNo);
            settingsModalGo.SetActive(false);

            // 9.3 Rules Modal (RulesUI)
            GameObject rulesModalGo = UIBuilderUtilities.CreatePanel(modalsContainer.transform, "RulesModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform rsmRt = rulesModalGo.GetComponent<RectTransform>();
            rsmRt.anchorMin = new Vector2(0.5f, 0.5f);
            rsmRt.anchorMax = new Vector2(0.5f, 0.5f);
            rsmRt.sizeDelta = new Vector2(820f, 600f);
            RulesUI rulesUI = rulesModalGo.AddComponent<RulesUI>();

            Text rulesTitle = UIBuilderUtilities.CreateText(rulesModalGo.transform, "Title", "LUẬT THI ĐẤU WPA 8-BALL", 24, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            rulesTitle.rectTransform.anchoredPosition = new Vector2(0f, 250f);
            rulesTitle.rectTransform.sizeDelta = new Vector2(760f, 40f);

            GameObject tabsBar = new GameObject("TabsBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            tabsBar.transform.SetParent(rulesModalGo.transform, false);
            RectTransform tabRt = tabsBar.GetComponent<RectTransform>();
            tabRt.anchoredPosition = new Vector2(0f, 195f);
            tabRt.sizeDelta = new Vector2(760f, 40f);
            HorizontalLayoutGroup tabHz = tabsBar.GetComponent<HorizontalLayoutGroup>();
            tabHz.spacing = 8f;
            tabHz.childForceExpandWidth = true;
            tabHz.childForceExpandHeight = true;

            Button tabBreak = CreateHudButton(tabsBar.transform, "TabBreak", "Khai Cuộc", "", 140f, borderColor, "none", 38f);
            Button tabOpen = CreateHudButton(tabsBar.transform, "TabOpen", "Bàn Mở", "", 140f, borderColor, "none", 38f);
            Button tabFoul = CreateHudButton(tabsBar.transform, "TabFoul", "Phạm Quy", "", 140f, borderColor, "none", 38f);
            Button tabEight = CreateHudButton(tabsBar.transform, "TabEight", "Bi Số 8", "", 140f, borderColor, "none", 38f);
            Button tabWinLose = CreateHudButton(tabsBar.transform, "TabWinLose", "Thắng/Thua", "", 140f, borderColor, "none", 38f);

            Text ruleHeadText = UIBuilderUtilities.CreateText(rulesModalGo.transform, "RuleHeadText", "Chi tiết điều luật:", 18, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            ruleHeadText.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            ruleHeadText.rectTransform.sizeDelta = new Vector2(760f, 30f);

            Text ruleBodyText = UIBuilderUtilities.CreateText(rulesModalGo.transform, "RuleBodyText", "Nội dung luật...", 15, UIBuilderUtilities.ColorTextWhite, TextAnchor.UpperLeft);
            ruleBodyText.rectTransform.anchoredPosition = new Vector2(-4f, -26f);
            ruleBodyText.rectTransform.sizeDelta = new Vector2(690f, 270f);
            ruleBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            ruleBodyText.lineSpacing = 1.35f;
            ruleBodyText.fontSize = 15;

            Button btnCloseRules = CreateHudButton(rulesModalGo.transform, "BtnClose", "ĐÃ HIỂU & ĐÓNG", "ESC", 380f, borderColor, "close", 44f);
            btnCloseRules.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -235f);

            DecorateModal(rulesModalGo, 820f, 600f, borderColor);
            WireRulesUI(rulesUI, rulesModalGo, btnCloseRules, ruleHeadText, ruleBodyText, tabBreak, tabOpen, tabFoul, tabEight, tabWinLose);
            rulesModalGo.SetActive(false);

            // 9.4 Help Modal (HelpUI)
            GameObject helpModalGo = UIBuilderUtilities.CreatePanel(modalsContainer.transform, "HelpModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform hmRt = helpModalGo.GetComponent<RectTransform>();
            hmRt.anchorMin = new Vector2(0.5f, 0.5f);
            hmRt.anchorMax = new Vector2(0.5f, 0.5f);
            hmRt.sizeDelta = new Vector2(780f, 540f);
            HelpUI helpUI = helpModalGo.AddComponent<HelpUI>();

            Text helpTitle = UIBuilderUtilities.CreateText(helpModalGo.transform, "Title", "HƯỚNG DẪN THAO TÁC GAME", 24, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            helpTitle.rectTransform.anchoredPosition = new Vector2(0f, 220f);
            helpTitle.rectTransform.sizeDelta = new Vector2(720f, 40f);

            Text helpContent = UIBuilderUtilities.CreateText(helpModalGo.transform, "HelpContent", "", 15, UIBuilderUtilities.ColorTextWhite, TextAnchor.UpperLeft);
            helpContent.rectTransform.anchoredPosition = new Vector2(-4f, 6f);
            helpContent.rectTransform.sizeDelta = new Vector2(660f, 330f);
            helpContent.horizontalOverflow = HorizontalWrapMode.Wrap;
            helpContent.lineSpacing = 1.5f;
            helpContent.fontSize = 16;

            Button btnCloseHelp = CreateHudButton(helpModalGo.transform, "BtnClose", "ĐÓNG HƯỚNG DẪN", "ESC", 340f, borderColor, "close", 44f);
            btnCloseHelp.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -210f);

            DecorateModal(helpModalGo, 780f, 540f, borderColor);
            WireHelpUI(helpUI, helpModalGo, btnCloseHelp, helpContent);
            helpModalGo.SetActive(false);

            // 9.5 Match Start Intro Modal (Prominent Match Start Screen)
            GameObject startIntroModalGo = UIBuilderUtilities.CreatePanel(modalsContainer.transform, "MatchStartIntroModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform simRt = startIntroModalGo.GetComponent<RectTransform>();
            simRt.anchorMin = new Vector2(0.5f, 0.5f);
            simRt.anchorMax = new Vector2(0.5f, 0.5f);
            simRt.sizeDelta = new Vector2(720f, 480f);

            Text simTitle = UIBuilderUtilities.CreateText(startIntroModalGo.transform, "Title", "🏆 TRẬN ĐẤU BIDA 3D - BẮT ĐẦU! 🏆", 28, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            simTitle.rectTransform.anchoredPosition = new Vector2(0f, 185f);
            simTitle.rectTransform.sizeDelta = new Vector2(680f, 42f);

            Text simSub = UIBuilderUtilities.CreateText(startIntroModalGo.transform, "SubTitle", "CHAMPIONSHIP TOURNAMENT EDITION • WPA OFFICIAL RULES", 14, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleCenter);
            simSub.rectTransform.anchoredPosition = new Vector2(0f, 145f);
            simSub.rectTransform.sizeDelta = new Vector2(680f, 25f);

            // Versus Box
            GameObject vsBox = UIBuilderUtilities.CreatePanel(startIntroModalGo.transform, "VersusBox", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorCard);
            RectTransform vbRt = vsBox.GetComponent<RectTransform>();
            vbRt.anchoredPosition = new Vector2(0f, 65f);
            vbRt.sizeDelta = new Vector2(640f, 100f);

            Text vsText = UIBuilderUtilities.CreateText(vsBox.transform, "VersusText", "CƠ THỦ 1 (BẠN)  ⚔️  CƠ THỦ 2 (AI)", 22, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            vsText.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            vsText.rectTransform.sizeDelta = new Vector2(600f, 35f);

            Text vsStatus = UIBuilderUtilities.CreateText(vsBox.transform, "VersusStatus", "CẢ HAI CƠ THỦ ĐÃ SẴN SÀNG TRANH TÀI", 14, UIBuilderUtilities.ColorAccentEmerald, TextAnchor.MiddleCenter, FontStyle.Bold);
            vsStatus.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            vsStatus.rectTransform.sizeDelta = new Vector2(600f, 25f);

            // Details Text
            Text simDetails = UIBuilderUtilities.CreateText(startIntroModalGo.transform, "DetailsText", "THỂ THỨC: RACE TO 5 (CHẠM 5) • LUẬT THI ĐẤU: WPA 8-BALL QUỐC TẾ\nQUYỀN KHAI CUỘC (BREAK SHOT): <b>BẠN</b>", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            simDetails.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            simDetails.rectTransform.sizeDelta = new Vector2(640f, 60f);

            // Ready Action Button
            Button btnStartMatchReady = UIBuilderUtilities.CreateButton(startIntroModalGo.transform, "BtnStartMatchReady", "⚡ BẮT ĐẦU TRẬN ĐẤU (VÀO BÀN) ⚡", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentEmerald, UIBuilderUtilities.ColorTextWhite, 20);
            btnStartMatchReady.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -150f);
            btnStartMatchReady.GetComponent<RectTransform>().sizeDelta = new Vector2(520f, 55f);

            startIntroModalGo.SetActive(false);

            // -------------------------------------------------------------------------
            // 10. Wire GameplayHUD serialized fields
            // -------------------------------------------------------------------------
            SerializedObject soHud = new SerializedObject(hud);
            soHud.FindProperty("playerLeftPanel").objectReferenceValue = p1UI;
            soHud.FindProperty("playerRightPanel").objectReferenceValue = p2UI;
            soHud.FindProperty("matchHeader").objectReferenceValue = matchHeader;
            soHud.FindProperty("powerBar").objectReferenceValue = powerBarUI;
            soHud.FindProperty("callShotUI").objectReferenceValue = callShotUI;
            soHud.FindProperty("eventToast").objectReferenceValue = eventToastUI;
            soHud.FindProperty("helpUI").objectReferenceValue = helpUI;
            soHud.FindProperty("rulesUI").objectReferenceValue = rulesUI;
            soHud.FindProperty("settingsUI").objectReferenceValue = settingsUI;
            soHud.FindProperty("resultUI").objectReferenceValue = resultUI;
            soHud.FindProperty("contactSelector").objectReferenceValue = contactSelector;

            soHud.FindProperty("matchStartIntroRoot").objectReferenceValue = startIntroModalGo;
            soHud.FindProperty("matchStartTitleText").objectReferenceValue = simTitle;
            soHud.FindProperty("matchStartVersusText").objectReferenceValue = vsText;
            soHud.FindProperty("matchStartDetailsText").objectReferenceValue = simDetails;
            soHud.FindProperty("matchStartReadyButton").objectReferenceValue = btnStartMatchReady;

            soHud.FindProperty("matchStateManager").objectReferenceValue = stateMgr;
            soHud.FindProperty("turnManager").objectReferenceValue = turnMgr;
            soHud.FindProperty("inputController").objectReferenceValue = inputCtrl;
            soHud.FindProperty("ballManager").objectReferenceValue = ballMgr;
            soHud.FindProperty("movementMonitor").objectReferenceValue = moveMonitor;

            soHud.FindProperty("helpButton").objectReferenceValue = btnHelp;
            soHud.FindProperty("rulesButton").objectReferenceValue = btnRules;
            soHud.FindProperty("settingsButton").objectReferenceValue = btnSettings;
            soHud.FindProperty("cueContactButton").objectReferenceValue = null; // đã thay bằng widget bi cái

            soHud.ApplyModifiedProperties();

            UIBuilderUtilities.SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("★★★ THÀNH CÔNG: Xây dựng Canvas UI trận đấu (Gameplay HUD) cực ngầu, chuyên nghiệp và kết nối hoàn chỉnh! ★★★");
        }

        private static BallTrackerUI.BallSlot[] CreateBallSlots(Transform parent, int startBall, int endBall)
        {
            int count = endBall - startBall + 1;
            BallTrackerUI.BallSlot[] slots = new BallTrackerUI.BallSlot[count];

            for (int i = 0; i < count; i++)
            {
                int ballNum = startBall + i;
                slots[i] = CreateSingleBallSlot(parent, ballNum);
            }
            return slots;
        }

        private const float BallSlotSize = 28f;

        private static BallTrackerUI.BallSlot CreateSingleBallSlot(Transform parent, int ballNum)
        {
            // Thân bi tròn (Mask để dải sọc bị cắt theo hình tròn)
            GameObject slotGo = UIBuilderUtilities.CreatePanel(parent, $"Slot_{ballNum}", UIBuilderUtilities.CircleSprite, BallTrackerUI.GetBallColor(ballNum));
            RectTransform rt = slotGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(BallSlotSize, BallSlotSize);
            Image bodyImg = slotGo.GetComponent<Image>();
            bodyImg.type = Image.Type.Simple;
            bodyImg.raycastTarget = false;
            bodyImg.color = ballNum >= 9 ? Color.white : BallTrackerUI.GetBallColor(ballNum);
            Mask mask = slotGo.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            LayoutElement le = slotGo.AddComponent<LayoutElement>();
            le.preferredWidth = BallSlotSize;
            le.preferredHeight = BallSlotSize;

            // Dải màu (bi sọc)
            GameObject bandGo = UIBuilderUtilities.CreatePanel(slotGo.transform, "Band", null, BallTrackerUI.GetBallColor(ballNum));
            Image bandImg = bandGo.GetComponent<Image>();
            bandImg.sprite = null;
            bandImg.type = Image.Type.Simple;
            bandImg.raycastTarget = false;
            RectTransform bandRt = bandGo.GetComponent<RectTransform>();
            bandRt.anchorMin = new Vector2(0f, 0.22f);
            bandRt.anchorMax = new Vector2(1f, 0.78f);
            bandRt.offsetMin = Vector2.zero;
            bandRt.offsetMax = Vector2.zero;
            bandGo.SetActive(ballNum >= 9);

            // Đĩa trắng chứa số
            GameObject discGo = UIBuilderUtilities.CreatePanel(slotGo.transform, "Disc", UIBuilderUtilities.CircleSprite, Color.white);
            Image discImg = discGo.GetComponent<Image>();
            discImg.type = Image.Type.Simple;
            discImg.raycastTarget = false;
            discGo.GetComponent<RectTransform>().sizeDelta = new Vector2(BallSlotSize * 0.56f, BallSlotSize * 0.56f);

            Text numText = UIBuilderUtilities.CreateText(discGo.transform, "Num", ballNum.ToString(), 10, Color.black, TextAnchor.MiddleCenter, FontStyle.Bold);
            numText.rectTransform.sizeDelta = new Vector2(BallSlotSize * 0.56f, BallSlotSize * 0.56f);

            BallTrackerUI.BallSlot slot = new BallTrackerUI.BallSlot
            {
                rootObject = slotGo,
                ballImage = bodyImg,
                ballNumberText = numText,
                ballNumber = ballNum,
                isPocketed = false,
                bandImage = bandImg,
                discImage = discImg
            };
            return slot;
        }

        private static void ConfigureTrackerLayout(HorizontalLayoutGroup hz, TextAnchor alignment)
        {
            hz.spacing = 4f;
            hz.childAlignment = alignment;
            hz.childControlWidth = true;
            hz.childControlHeight = true;
            hz.childForceExpandWidth = false;
            hz.childForceExpandHeight = false;
        }

        private static Image HudBar(Transform parent, string name, Vector2 size, Vector2 pos, Color color, float rotZ = 0f)
        {
            GameObject go = UIBuilderUtilities.CreatePanel(parent, name, null, color);
            Image img = go.GetComponent<Image>();
            img.sprite = null;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return img;
        }

        /// <summary>Vẽ icon đơn giản bằng các khối hình (không phụ thuộc font emoji).</summary>
        private static void AddHudIcon(Transform parent, string kind, Vector2 pos)
        {
            if (kind == null || kind == "none") return;
            GameObject root = new GameObject("Icon", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            root.transform.SetParent(parent, false);
            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(24f, 24f);
            Transform t = root.transform;
            Color gold = UIBuilderUtilities.ColorAccentGold;
            Color white = new Color(0.92f, 0.96f, 1f, 1f);
            Color dark = new Color(0.06f, 0.10f, 0.18f, 1f);

            switch (kind)
            {
                case "help":
                {
                    GameObject c = UIBuilderUtilities.CreatePanel(t, "Circle", UIBuilderUtilities.CircleSprite, gold);
                    c.GetComponent<Image>().type = Image.Type.Simple;
                    c.GetComponent<Image>().raycastTarget = false;
                    c.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 22f);
                    Text q = UIBuilderUtilities.CreateText(c.transform, "Q", "?", 16, dark, TextAnchor.MiddleCenter, FontStyle.Bold);
                    q.rectTransform.sizeDelta = new Vector2(22f, 22f);
                    break;
                }
                case "rules":
                    HudBar(t, "Page", new Vector2(16f, 21f), Vector2.zero, white);
                    HudBar(t, "L1", new Vector2(10f, 2f), new Vector2(0f, 5f), dark);
                    HudBar(t, "L2", new Vector2(10f, 2f), new Vector2(0f, 0f), dark);
                    HudBar(t, "L3", new Vector2(10f, 2f), new Vector2(0f, -5f), dark);
                    break;
                case "menu":
                    HudBar(t, "B1", new Vector2(20f, 3f), new Vector2(0f, 7f), white);
                    HudBar(t, "B2", new Vector2(20f, 3f), Vector2.zero, white);
                    HudBar(t, "B3", new Vector2(20f, 3f), new Vector2(0f, -7f), white);
                    break;
                case "close":
                    HudBar(t, "X1", new Vector2(22f, 3f), Vector2.zero, white, 45f);
                    HudBar(t, "X2", new Vector2(22f, 3f), Vector2.zero, white, -45f);
                    break;
                case "play":
                {
                    // Tam giác: hình thang thu về đỉnh rồi xoay để hướng sang phải
                    GameObject tri = CreateSkewedPanel(t, "Triangle", gold, 11f, 11f, 0f, 0f);
                    RectTransform tr = tri.GetComponent<RectTransform>();
                    tr.sizeDelta = new Vector2(22f, 20f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, -90f);
                    break;
                }
                case "flag":
                    HudBar(t, "Pole", new Vector2(3f, 22f), new Vector2(-6f, 0f), white);
                    HudBar(t, "Cloth", new Vector2(14f, 10f), new Vector2(2f, 6f), new Color(1f, 0.45f, 0.40f, 1f));
                    break;
                case "exit":
                    HudBar(t, "Door", new Vector2(13f, 21f), new Vector2(-4f, 0f), white);
                    HudBar(t, "Gap", new Vector2(7f, 15f), new Vector2(-4f, -1f), dark);
                    HudBar(t, "Arrow", new Vector2(11f, 3f), new Vector2(6f, 0f), gold);
                    HudBar(t, "Head", new Vector2(5f, 5f), new Vector2(10f, 0f), gold, 45f);
                    break;
            }
        }

        /// <summary>Biến popup thành khung vát góc cùng phong cách HUD: nền tối, viền sáng, dải vàng, lớp mờ che toàn màn hình.</summary>
        internal static void DecorateModal(GameObject modal, float w, float h, Color frameColor)
        {
            Image rootImg = modal.GetComponent<Image>();
            if (rootImg != null)
            {
                rootImg.sprite = null;
                rootImg.type = Image.Type.Simple;
                rootImg.color = new Color(0f, 0f, 0f, 0f);
                rootImg.raycastTarget = true;
            }

            float cut = Mathf.Round(h * 0.07f);

            // Lớp mờ che toàn màn hình: chặn click vào các nút HUD phía sau
            GameObject dim = UIBuilderUtilities.CreatePanel(modal.transform, "Dim", null, new Color(0.02f, 0.04f, 0.08f, 0.72f));
            Image dimImg = dim.GetComponent<Image>();
            dimImg.sprite = null;
            dimImg.type = Image.Type.Simple;
            dim.GetComponent<RectTransform>().sizeDelta = new Vector2(6000f, 4000f);

            GameObject card = CreateSkewedPanel(modal.transform, "Card", new Color(0.06f, 0.10f, 0.18f, 0.98f), 0f, cut, cut, 0f, 0.5f);
            RectTransform cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = Vector2.zero;
            cardRt.anchorMax = Vector2.one;
            cardRt.offsetMin = Vector2.zero;
            cardRt.offsetMax = Vector2.zero;

            GameObject frame = CreateBorderProgress(modal.transform, "Frame", frameColor, 0f, cut, cut, 0f);
            RectTransform frRt = frame.GetComponent<RectTransform>();
            frRt.anchorMin = Vector2.zero;
            frRt.anchorMax = Vector2.one;
            frRt.offsetMin = Vector2.zero;
            frRt.offsetMax = Vector2.zero;

            AddAccentStrip(modal.transform, UIBuilderUtilities.ColorAccentGold, h, 4f, 0f, cut, cut, 0f, false);
            Transform strip = modal.transform.GetChild(modal.transform.childCount - 1);

            dim.transform.SetSiblingIndex(0);
            card.transform.SetSiblingIndex(1);
            frame.transform.SetSiblingIndex(2);
            strip.SetSiblingIndex(3);
        }

        /// <summary>Nút HUD hình bình hành: nền tối, viền sáng, dải vàng dưới đáy, icon + nhãn + phím tắt.</summary>
        internal static Button CreateHudButton(Transform parent, string name, string label, string keyHint, float width, Color frameColor, string icon = "none", float h = 46f)
        {
            keyHint = keyHint ?? "";
            const float s = 12f;
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(Button), typeof(LayoutElement));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(width, h);
            LayoutElement le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = h;

            SkewedPanelGraphic g = go.GetComponent<SkewedPanelGraphic>();
            g.color = new Color(0.16f, 0.27f, 0.44f, 0.96f);
            g.raycastTarget = true;
            g.SetInsets(0f, s, s, 0f);
            SerializedObject so = new SerializedObject(g);
            so.FindProperty("topLeftInset").floatValue = 0f;
            so.FindProperty("topRightInset").floatValue = s;
            so.FindProperty("bottomLeftInset").floatValue = s;
            so.FindProperty("bottomRightInset").floatValue = 0f;
            so.FindProperty("bottomDarken").floatValue = 0.5f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = g;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(0.82f, 0.86f, 0.92f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.60f, 0.66f, 0.76f, 1f);
            cb.selectedColor = cb.normalColor;
            cb.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            GameObject frame = CreateBorderProgress(go.transform, "Frame", frameColor, 0f, s, s, 0f);
            RectTransform fr = frame.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            AddAccentStrip(go.transform, UIBuilderUtilities.ColorAccentGold, h, 3f, 0f, s, s, 0f, true);

            AddHudIcon(go.transform, icon, new Vector2(label.Length == 0 ? 0f : -width * 0.5f + 28f, 0f));

            if (label.Length > 0)
            {
                string text = label + (keyHint.Length > 0 ? "  <size=11><color=#FFC933>[" + keyHint + "]</color></size>" : "");
                Text t = UIBuilderUtilities.CreateText(go.transform, "Label", text, 13, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = new Vector2(icon == "none" ? 0f : 44f, 0f);
                t.rectTransform.offsetMax = Vector2.zero;
            }
            return btn;
        }

        /// <summary>Viền chạy theo chu vi khung xiên, dùng làm đồng hồ đếm ngược.</summary>
        private static GameObject CreateBorderProgress(Transform parent, string name, Color baseColor, float topLeft, float topRight, float bottomLeft, float bottomRight)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            BorderProgressGraphic g = go.GetComponent<BorderProgressGraphic>();
            g.raycastTarget = false;
            g.SetInsets(topLeft, topRight, bottomLeft, bottomRight);
            g.SetBaseColor(baseColor);
            SerializedObject so = new SerializedObject(g);
            so.FindProperty("topLeftInset").floatValue = topLeft;
            so.FindProperty("topRightInset").floatValue = topRight;
            so.FindProperty("bottomLeftInset").floatValue = bottomLeft;
            so.FindProperty("bottomRightInset").floatValue = bottomRight;
            so.FindProperty("baseColor").colorValue = baseColor;
            so.FindProperty("thickness").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Tạo khung tứ giác (hình bình hành / hình thang) bằng SkewedPanelGraphic, không nhận raycast.</summary>
        private static GameObject CreateSkewedPanel(Transform parent, string name, Color color, float topLeft, float topRight, float bottomLeft, float bottomRight, float bottomDarken = 1f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(parent, false);
            SkewedPanelGraphic g = go.GetComponent<SkewedPanelGraphic>();
            g.color = color;
            g.raycastTarget = false;
            g.SetInsets(topLeft, topRight, bottomLeft, bottomRight);
            SerializedObject so = new SerializedObject(g);
            so.FindProperty("topLeftInset").floatValue = topLeft;
            so.FindProperty("topRightInset").floatValue = topRight;
            so.FindProperty("bottomLeftInset").floatValue = bottomLeft;
            so.FindProperty("bottomRightInset").floatValue = bottomRight;
            so.FindProperty("bottomDarken").floatValue = bottomDarken;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        /// <summary>Dải màu mỏng dọc theo cạnh dưới/trên của khung xiên, góc xiên khớp với khung cha.</summary>
        private static void AddAccentStrip(Transform panel, Color color, float panelH, float stripH, float topL, float topR, float botL, float botR, bool atBottom)
        {
            float t = stripH / panelH;
            // Inset của khung cha tại độ cao tương đối f (0 = đáy, 1 = đỉnh)
            System.Func<float, float> insetL = f => Mathf.Lerp(botL, topL, f);
            System.Func<float, float> insetR = f => Mathf.Lerp(botR, topR, f);

            float f0 = atBottom ? 0f : 1f - t;
            float f1 = atBottom ? t : 1f;

            GameObject strip = CreateSkewedPanel(panel, atBottom ? "AccentBottom" : "AccentTop", color, insetL(f1), insetR(f1), insetL(f0), insetR(f0));
            RectTransform rt = strip.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, atBottom ? 0f : 1f);
            rt.anchorMax = new Vector2(1f, atBottom ? 0f : 1f);
            rt.pivot = new Vector2(0.5f, atBottom ? 0f : 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, stripH);
        }

        private static void WireBallTracker(BallTrackerUI tracker, BallTrackerUI.BallSlot[] groupSlots, BallTrackerUI.BallSlot eightSlot)
        {
            SerializedObject so = new SerializedObject(tracker);
            SerializedProperty groupProp = so.FindProperty("groupBallSlots");
            groupProp.arraySize = groupSlots.Length;
            for (int i = 0; i < groupSlots.Length; i++)
            {
                SerializedProperty elem = groupProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("rootObject").objectReferenceValue = groupSlots[i].rootObject;
                elem.FindPropertyRelative("ballImage").objectReferenceValue = groupSlots[i].ballImage;
                elem.FindPropertyRelative("ballNumberText").objectReferenceValue = groupSlots[i].ballNumberText;
                elem.FindPropertyRelative("ballNumber").intValue = groupSlots[i].ballNumber;
                elem.FindPropertyRelative("isPocketed").boolValue = groupSlots[i].isPocketed;
                elem.FindPropertyRelative("bandImage").objectReferenceValue = groupSlots[i].bandImage;
                elem.FindPropertyRelative("discImage").objectReferenceValue = groupSlots[i].discImage;
            }

            SerializedProperty eightProp = so.FindProperty("eightBallSlot");
            eightProp.FindPropertyRelative("rootObject").objectReferenceValue = eightSlot.rootObject;
            eightProp.FindPropertyRelative("ballImage").objectReferenceValue = eightSlot.ballImage;
            eightProp.FindPropertyRelative("ballNumberText").objectReferenceValue = eightSlot.ballNumberText;
            eightProp.FindPropertyRelative("ballNumber").intValue = eightSlot.ballNumber;
            eightProp.FindPropertyRelative("isPocketed").boolValue = eightSlot.isPocketed;
            eightProp.FindPropertyRelative("bandImage").objectReferenceValue = eightSlot.bandImage;
            eightProp.FindPropertyRelative("discImage").objectReferenceValue = eightSlot.discImage;

            so.ApplyModifiedProperties();
        }

        private static void WirePlayerPanel(PlayerPanelUI panel, Text nameText, Text groupText, Text scoreText, Image avatar, GameObject glow, Graphic bg, BallTrackerUI tracker, TurnTimerUI timer = null)
        {
            SerializedObject so = new SerializedObject(panel);
            so.FindProperty("playerNameText").objectReferenceValue = nameText;
            so.FindProperty("groupText").objectReferenceValue = groupText;
            so.FindProperty("scoreText").objectReferenceValue = scoreText;
            so.FindProperty("avatarImage").objectReferenceValue = avatar;
            so.FindProperty("turnHighlightObject").objectReferenceValue = glow;
            so.FindProperty("panelBackgroundImage").objectReferenceValue = bg;
            so.FindProperty("ballTracker").objectReferenceValue = tracker;
            so.FindProperty("turnTimer").objectReferenceValue = timer;
            so.ApplyModifiedProperties();
        }

        private static TurnTimerUI CreateTurnTimer(Transform panel, float x, BorderProgressGraphic border)
        {
            GameObject go = new GameObject("TurnTimer", typeof(RectTransform), typeof(TurnTimerUI));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(panel, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(100f, 60f);
            Text num = UIBuilderUtilities.CreateText(go.transform, "Number", "30", 40, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            num.rectTransform.sizeDelta = new Vector2(100f, 60f);
            TurnTimerUI ui = go.GetComponent<TurnTimerUI>();
            SerializedObject so = new SerializedObject(ui);
            so.FindProperty("numberText").objectReferenceValue = num;
            so.FindProperty("border").objectReferenceValue = border;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ui;
        }

        private static void WireMatchHeader(MatchHeaderUI header, Text rackNum, Text scoreRace, GameObject timerContainer, Text countdownText, Image timerFill)
        {
            SerializedObject so = new SerializedObject(header);
            so.FindProperty("rackNumberText").objectReferenceValue = rackNum;
            so.FindProperty("scoreRaceText").objectReferenceValue = scoreRace;
            so.FindProperty("timerContainer").objectReferenceValue = timerContainer;
            so.FindProperty("timerCountdownText").objectReferenceValue = countdownText;
            so.FindProperty("timerFillBar").objectReferenceValue = timerFill;
            so.ApplyModifiedProperties();
        }

        private static void BuildCueBallWidget(Transform parent, CueBallContactSelector selector, float ballSize, float dotSize, int fontSize, Vector2 ballPos, Vector2 textPos, bool interactive)
        {
            GameObject ball = UIBuilderUtilities.CreatePanel(parent, "CueBallImage", UIBuilderUtilities.CircleSprite, new Color(0.97f, 0.97f, 0.94f, 1f));
            ball.GetComponent<RectTransform>().anchoredPosition = ballPos;
            ball.GetComponent<RectTransform>().sizeDelta = new Vector2(ballSize, ballSize);
            Image ballImg = ball.GetComponent<Image>();
            ballImg.raycastTarget = interactive;
            ballImg.type = Image.Type.Simple;
            ballImg.preserveAspect = true;

            GameObject crossH = UIBuilderUtilities.CreatePanel(ball.transform, "CrossH", null, new Color(0f, 0f, 0f, 0.25f));
            crossH.GetComponent<Image>().raycastTarget = false;
            crossH.GetComponent<Image>().type = Image.Type.Simple;
            crossH.GetComponent<RectTransform>().sizeDelta = new Vector2(ballSize * 0.93f, 2f);
            GameObject crossV = UIBuilderUtilities.CreatePanel(ball.transform, "CrossV", null, new Color(0f, 0f, 0f, 0.25f));
            crossV.GetComponent<Image>().raycastTarget = false;
            crossV.GetComponent<Image>().type = Image.Type.Simple;
            crossV.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, ballSize * 0.93f);

            GameObject dot = UIBuilderUtilities.CreatePanel(ball.transform, "ContactDot", UIBuilderUtilities.CircleSprite, new Color(0.9f, 0.1f, 0.1f, 1f));
            dot.GetComponent<Image>().raycastTarget = false;
            dot.GetComponent<Image>().type = Image.Type.Simple;
            dot.GetComponent<RectTransform>().sizeDelta = new Vector2(dotSize, dotSize);

            Text value = UIBuilderUtilities.CreateText(parent, "SpinValue", "Giữa", fontSize, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            value.rectTransform.anchoredPosition = textPos;
            value.rectTransform.sizeDelta = new Vector2(ballSize + 30f, 30f);

            CueBallSpinWidgetUI widget = ball.AddComponent<CueBallSpinWidgetUI>();
            SerializedObject so = new SerializedObject(widget);
            so.FindProperty("selector").objectReferenceValue = selector;
            so.FindProperty("dot").objectReferenceValue = dot.GetComponent<RectTransform>();
            so.FindProperty("valueText").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WirePowerBar(PowerBarUI pb, Image fill, Text percentText, RectTransform track = null, RectTransform peak = null, BorderProgressGraphic frame = null, SlantedBarGraphic bar = null)
        {
            SerializedObject so = new SerializedObject(pb);
            so.FindProperty("powerFillImage").objectReferenceValue = fill;
            so.FindProperty("powerPercentText").objectReferenceValue = percentText;
            so.FindProperty("trackRect").objectReferenceValue = track;
            so.FindProperty("slantedBar").objectReferenceValue = bar;
            so.FindProperty("peakMarker").objectReferenceValue = peak;
            so.FindProperty("frameBorder").objectReferenceValue = frame;
            so.ApplyModifiedProperties();
        }

        private static void WireCallShotUI(CallShotUI cs, GameObject root, Dropdown ballDrop, Dropdown pocketDrop, Button safetyBtn, Image safetyImg, Text summary)
        {
            SerializedObject so = new SerializedObject(cs);
            so.FindProperty("panelRoot").objectReferenceValue = root;
            so.FindProperty("ballDropdown").objectReferenceValue = ballDrop;
            so.FindProperty("pocketDropdown").objectReferenceValue = pocketDrop;
            so.FindProperty("safetyButton").objectReferenceValue = safetyBtn;
            so.FindProperty("safetyButtonImage").objectReferenceValue = safetyImg;
            so.FindProperty("statusSummaryText").objectReferenceValue = summary;
            so.ApplyModifiedProperties();
        }

        private static void WireEventToast(EventToastUI toast, GameObject root, Text title, Text msg, Image bg, CanvasGroup cg)
        {
            SerializedObject so = new SerializedObject(toast);
            so.FindProperty("toastRoot").objectReferenceValue = root;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("messageText").objectReferenceValue = msg;
            so.FindProperty("backgroundImage").objectReferenceValue = bg;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.ApplyModifiedProperties();
        }

        private static void WireResultUI(ResultUI res, GameObject root, Text title, Text winner, Text score, Text details, Button nextBtn, Button rematchBtn, Button lobbyBtn)
        {
            SerializedObject so = new SerializedObject(res);
            so.FindProperty("resultModalRoot").objectReferenceValue = root;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("winnerNameText").objectReferenceValue = winner;
            so.FindProperty("scoreSummaryText").objectReferenceValue = score;
            so.FindProperty("detailsText").objectReferenceValue = details;
            so.FindProperty("nextRackButton").objectReferenceValue = nextBtn;
            so.FindProperty("rematchButton").objectReferenceValue = rematchBtn;
            so.FindProperty("returnToLobbyButton").objectReferenceValue = lobbyBtn;
            so.ApplyModifiedProperties();
        }

        private static void WireSettingsUI(SettingsUI set, GameObject root, Button close, Button resume, Slider master, Slider music, Slider sfx, Button surrender, Button quit, GameObject confirmRoot, Text confirmMsg, Button cYes, Button cNo)
        {
            SerializedObject so = new SerializedObject(set);
            so.FindProperty("modalRoot").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("resumeButton").objectReferenceValue = resume;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = master;
            so.FindProperty("musicVolumeSlider").objectReferenceValue = music;
            so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfx;
            so.FindProperty("surrenderButton").objectReferenceValue = surrender;
            so.FindProperty("quitMatchButton").objectReferenceValue = quit;
            so.FindProperty("confirmDialogRoot").objectReferenceValue = confirmRoot;
            so.FindProperty("confirmMessageText").objectReferenceValue = confirmMsg;
            so.FindProperty("confirmYesButton").objectReferenceValue = cYes;
            so.FindProperty("confirmNoButton").objectReferenceValue = cNo;
            so.ApplyModifiedProperties();
        }

        private static void WireRulesUI(RulesUI r, GameObject root, Button close, Text head, Text body, Button bBreak, Button bOpen, Button bFoul, Button bEight, Button bWinLose)
        {
            SerializedObject so = new SerializedObject(r);
            so.FindProperty("modalRoot").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("ruleTitleText").objectReferenceValue = head;
            so.FindProperty("ruleContentText").objectReferenceValue = body;
            so.FindProperty("breakTabButton").objectReferenceValue = bBreak;
            so.FindProperty("openTableTabButton").objectReferenceValue = bOpen;
            so.FindProperty("foulTabButton").objectReferenceValue = bFoul;
            so.FindProperty("eightBallTabButton").objectReferenceValue = bEight;
            so.FindProperty("winLoseTabButton").objectReferenceValue = bWinLose;
            so.ApplyModifiedProperties();
        }

        private static void WireHelpUI(HelpUI h, GameObject root, Button close, Text content)
        {
            SerializedObject so = new SerializedObject(h);
            so.FindProperty("modalRoot").objectReferenceValue = root;
            so.FindProperty("closeButton").objectReferenceValue = close;
            so.FindProperty("helpContentText").objectReferenceValue = content;
            so.ApplyModifiedProperties();
        }
    }
}
