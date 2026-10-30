using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.Billiards.UI;
using Game.Billiards.Network;

namespace Game.Billiards.Editor
{
    public static class LobbyUIBuilder
    {
        // Bảng màu tiết chế cho sảnh: một tông xanh thép duy nhất thay vì mỗi nút một màu
        private static readonly Color MutedFrame = new Color(0.42f, 0.56f, 0.74f, 1f);

        private static Button HudBtn(Transform p, string name, string label, string icon, Color frame, float w, float h, int fontSize = 16)
        {
            Button b = GameplayUIBuilder.CreateHudButton(p, name, label, "", w, MutedFrame, icon, h);
            foreach (Text t in b.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == "Label") t.fontSize = fontSize;
            }
            return b;
        }

        /// <summary>Khoác phong cách HUD gameplay (khung vát góc, viền sáng, dải vàng) lên một panel.</summary>
        private static void StyleCard(GameObject card, float w, float h, Color frame, bool withDim)
        {
            GameplayUIBuilder.DecorateModal(card, w, h, MutedFrame);
            if (!withDim)
            {
                Transform dim = card.transform.Find("Dim");
                if (dim != null) UnityEngine.Object.DestroyImmediate(dim.gameObject);
            }
        }

        /// <summary>Làm dịu màu: dải nhấn mờ, icon sáng trung tính, tiêu đề popup màu trắng.</summary>
        private static void ToneDownColors(GameObject canvasGo, GameObject modalsRoot)
        {
            foreach (Graphic g in canvasGo.GetComponentsInChildren<Graphic>(true))
            {
                if (g.gameObject.name.StartsWith("Accent"))
                {
                    g.color = new Color(MutedFrame.r, MutedFrame.g, MutedFrame.b, 0.55f);
                }
                else if (g.transform.parent != null && g.transform.parent.name == "Icon" || (g.transform.parent != null && g.transform.parent.parent != null && g.transform.parent.parent.name == "Icon"))
                {
                    Color c = g.color;
                    float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                    if (max > 0.5f && min < 0.85f)
                    {
                        g.color = new Color(0.85f, 0.90f, 0.97f, 1f);
                    }
                }
            }

            foreach (Text t in modalsRoot.GetComponentsInChildren<Text>(true))
            {
                if (t.gameObject.name == "Title") t.color = UIBuilderUtilities.ColorTextWhite;
            }
        }

        [MenuItem("Tools/Game/Build Lobby UI Canvas", false, 10)]
        public static void Build()
        {
            const string scenePath = "Assets/Game/Scenes/Lobby.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Remove existing LobbyCanvas if any
            GameObject existingCanvas = GameObject.Find("LobbyCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            // 2. Find LobbySystems GameObject
            GameObject lobbySystemsGo = GameObject.Find("LobbySystems");
            if (lobbySystemsGo == null)
            {
                Debug.LogError("LobbySystems GameObject not found in Lobby scene!");
                return;
            }

            LobbyUIController lobbyUI = lobbySystemsGo.GetComponent<LobbyUIController>();
            OnlineRoomUI onlineRoomUI = lobbySystemsGo.GetComponent<OnlineRoomUI>();

            if (lobbyUI == null) lobbyUI = lobbySystemsGo.AddComponent<LobbyUIController>();
            if (onlineRoomUI == null) onlineRoomUI = lobbySystemsGo.AddComponent<OnlineRoomUI>();

            // 3. Create Main Canvas
            GameObject canvasGo = UIBuilderUtilities.CreateCanvas("LobbyCanvas");
            Transform canvasTrans = canvasGo.transform;

            // 4. Header / Title Banner
            GameObject headerGo = new GameObject("HeaderPanel", typeof(RectTransform));
            headerGo.transform.SetParent(canvasTrans, false);
            RectTransform headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.85f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = new Vector2(60, 0);
            headerRt.offsetMax = new Vector2(-60, -20);

            Text titleText = UIBuilderUtilities.CreateText(headerGo.transform, "TitleText", "🎱 8-BALL BILLIARDS 3D", 48, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            RectTransform titleRt = titleText.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.45f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            Text subTitleText = UIBuilderUtilities.CreateText(headerGo.transform, "SubTitleText", "", 18, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleLeft, FontStyle.Normal);
            RectTransform subTitleRt = subTitleText.GetComponent<RectTransform>();
            subTitleRt.anchorMin = new Vector2(0f, 0f);
            subTitleRt.anchorMax = new Vector2(1f, 0.45f);
            subTitleRt.offsetMin = Vector2.zero;
            subTitleRt.offsetMax = Vector2.zero;

            // 5. Main Menu Card (Center Left)
            GameObject menuCard = UIBuilderUtilities.CreatePanel(canvasTrans, "MainMenuCard", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgPanel);
            RectTransform menuRt = menuCard.GetComponent<RectTransform>();
            menuRt.anchorMin = new Vector2(0f, 0.5f);
            menuRt.anchorMax = new Vector2(0f, 0.5f);
            menuRt.pivot = new Vector2(0f, 0.5f);
            menuRt.anchoredPosition = new Vector2(80f, -40f);
            menuRt.sizeDelta = new Vector2(380f, 480f);
            StyleCard(menuCard, 380f, 480f, UIBuilderUtilities.ColorAccentCyan, false);

            Text menuHeader = UIBuilderUtilities.CreateText(menuCard.transform, "MenuHeader", "MENU CHÍNH", 22, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform mhRt = menuHeader.GetComponent<RectTransform>();
            mhRt.anchorMin = new Vector2(0f, 1f);
            mhRt.anchorMax = new Vector2(1f, 1f);
            mhRt.pivot = new Vector2(0.5f, 1f);
            mhRt.anchoredPosition = new Vector2(0f, -20f);
            mhRt.sizeDelta = new Vector2(0f, 40f);

            // Vertical Layout for Menu Buttons
            GameObject buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            buttonsContainer.transform.SetParent(menuCard.transform, false);
            RectTransform bcRt = buttonsContainer.GetComponent<RectTransform>();
            bcRt.anchorMin = new Vector2(0f, 0f);
            bcRt.anchorMax = new Vector2(1f, 1f);
            bcRt.offsetMin = new Vector2(25, 25);
            bcRt.offsetMax = new Vector2(-25, -70);

            VerticalLayoutGroup vlg = buttonsContainer.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 15f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = true;

            Button btnPlayAI = HudBtn(buttonsContainer.transform, "BtnPlayAI", "ĐẤU VỚI AI", "play", UIBuilderUtilities.ColorAccentEmerald, 330f, 56f, 19);
            Button btnPlayOnline = HudBtn(buttonsContainer.transform, "BtnPlayOnline", "ĐẤU ONLINE 1v1", "flag", UIBuilderUtilities.ColorAccentBlue, 330f, 56f, 19);
            Button btnRules = HudBtn(buttonsContainer.transform, "BtnRules", "LUẬT THI ĐẤU", "rules", UIBuilderUtilities.ColorAccentGold, 330f, 56f, 17);
            Button btnSettings = HudBtn(buttonsContainer.transform, "BtnSettings", "CÀI ĐẶT", "menu", UIBuilderUtilities.ColorAccentCyan, 330f, 56f, 17);
            Button btnQuit = HudBtn(buttonsContainer.transform, "BtnQuit", "THOÁT GAME", "exit", UIBuilderUtilities.ColorAccentCrimson, 330f, 56f, 17);

            // 6. Modals Root (Centered)
            GameObject modalsRoot = new GameObject("ModalsRoot", typeof(RectTransform));
            modalsRoot.transform.SetParent(canvasTrans, false);
            RectTransform mrRt = modalsRoot.GetComponent<RectTransform>();
            mrRt.anchorMin = Vector2.zero;
            mrRt.anchorMax = Vector2.one;
            mrRt.offsetMin = Vector2.zero;
            mrRt.offsetMax = Vector2.zero;

            // -------------------------------------------------------------------------
            // MODAL 1: Difficulty Modal (Play vs AI)
            // -------------------------------------------------------------------------
            GameObject diffModal = UIBuilderUtilities.CreatePanel(modalsRoot.transform, "DifficultyModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform dmRt = diffModal.GetComponent<RectTransform>();
            dmRt.anchorMin = new Vector2(0.5f, 0.5f);
            dmRt.anchorMax = new Vector2(0.5f, 0.5f);
            dmRt.sizeDelta = new Vector2(580f, 620f);
            StyleCard(diffModal, 580f, 620f, UIBuilderUtilities.ColorAccentEmerald, true);

            Text dmTitle = UIBuilderUtilities.CreateText(diffModal.transform, "Title", "CẤU HÌNH ĐẤU VỚI AI", 26, UIBuilderUtilities.ColorAccentEmerald, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform dmtRt = dmTitle.GetComponent<RectTransform>();
            dmtRt.anchorMin = new Vector2(0f, 1f);
            dmtRt.anchorMax = new Vector2(1f, 1f);
            dmtRt.pivot = new Vector2(0.5f, 1f);
            dmtRt.anchoredPosition = new Vector2(0f, -25f);
            dmtRt.sizeDelta = new Vector2(0f, 40f);

            // Name Input
            Text nameLabel = UIBuilderUtilities.CreateText(diffModal.transform, "NameLabel", "Tên cơ thủ của bạn:", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            RectTransform nlRt = nameLabel.GetComponent<RectTransform>();
            nlRt.anchoredPosition = new Vector2(0f, 195f);
            nlRt.sizeDelta = new Vector2(480f, 25f);

            InputField playerNameInput = UIBuilderUtilities.CreateInputField(diffModal.transform, "PlayerNameInput", "Nhập tên cơ thủ...", 16);
            RectTransform pniRt = playerNameInput.GetComponent<RectTransform>();
            pniRt.anchoredPosition = new Vector2(0f, 165f);
            pniRt.sizeDelta = new Vector2(480f, 42f);

            // Difficulty Cards (Easy, Normal, Hard)
            Text diffLabel = UIBuilderUtilities.CreateText(diffModal.transform, "DiffLabel", "Chọn cấp độ AI:", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            RectTransform dlRt = diffLabel.GetComponent<RectTransform>();
            dlRt.anchoredPosition = new Vector2(0f, 120f);
            dlRt.sizeDelta = new Vector2(480f, 25f);

            GameObject diffButtonsContainer = new GameObject("DiffButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            diffButtonsContainer.transform.SetParent(diffModal.transform, false);
            RectTransform dbcRt = diffButtonsContainer.GetComponent<RectTransform>();
            dbcRt.anchoredPosition = new Vector2(0f, 75f);
            dbcRt.sizeDelta = new Vector2(480f, 55f);
            HorizontalLayoutGroup dbcHz = diffButtonsContainer.GetComponent<HorizontalLayoutGroup>();
            dbcHz.spacing = 15f;
            dbcHz.childForceExpandWidth = true;
            dbcHz.childForceExpandHeight = true;

            Button btnDiffEasy = UIBuilderUtilities.CreateButton(diffButtonsContainer.transform, "BtnDiffEasy", "DỄ\n(EASY)", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorCard, UIBuilderUtilities.ColorTextWhite, 14);
            Button btnDiffNormal = UIBuilderUtilities.CreateButton(diffButtonsContainer.transform, "BtnDiffNormal", "VỪA\n(NORMAL)", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentEmerald, UIBuilderUtilities.ColorTextWhite, 14);
            Button btnDiffHard = UIBuilderUtilities.CreateButton(diffButtonsContainer.transform, "BtnDiffHard", "KHÓ\n(HARD)", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorCard, UIBuilderUtilities.ColorTextWhite, 14);

            Image hlEasy = btnDiffEasy.GetComponent<Image>();
            Image hlNormal = btnDiffNormal.GetComponent<Image>();
            Image hlHard = btnDiffHard.GetComponent<Image>();

            // Race Target Dropdown
            Text raceLabel = UIBuilderUtilities.CreateText(diffModal.transform, "RaceLabel", "Số ván thắng mục tiêu (Race Target):", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            RectTransform rlRt = raceLabel.GetComponent<RectTransform>();
            rlRt.anchoredPosition = new Vector2(0f, 20f);
            rlRt.sizeDelta = new Vector2(480f, 25f);

            Dropdown raceDropdown = UIBuilderUtilities.CreateDropdown(diffModal.transform, "RaceDropdown", new List<string> { "Race to 3", "Race to 5", "Race to 7", "Race to 9" }, 15);
            RectTransform rdRt = raceDropdown.GetComponent<RectTransform>();
            rdRt.anchoredPosition = new Vector2(0f, -10f);
            rdRt.sizeDelta = new Vector2(480f, 40f);

            // Timer Dropdown
            Text timerLabel = UIBuilderUtilities.CreateText(diffModal.transform, "TimerLabel", "Thời gian lượt bắn (Shot Timer):", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            RectTransform tlRt = timerLabel.GetComponent<RectTransform>();
            tlRt.anchoredPosition = new Vector2(0f, -55f);
            tlRt.sizeDelta = new Vector2(480f, 25f);

            Dropdown timerDropdown = UIBuilderUtilities.CreateDropdown(diffModal.transform, "TimerDropdown", new List<string> { "Không giới hạn", "30 Giây", "45 Giây", "60 Giây" }, 15);
            RectTransform tdRt = timerDropdown.GetComponent<RectTransform>();
            tdRt.anchoredPosition = new Vector2(0f, -85f);
            tdRt.sizeDelta = new Vector2(480f, 40f);

            // Action Buttons
            Button btnStartOffline = HudBtn(diffModal.transform, "BtnStartMatch", "BẮT ĐẦU VÁN ĐẤU", "play", UIBuilderUtilities.ColorAccentEmerald, 480f, 50f, 18);
            RectTransform bsoRt = btnStartOffline.GetComponent<RectTransform>();
            bsoRt.anchoredPosition = new Vector2(0f, -165f);
            bsoRt.sizeDelta = new Vector2(480f, 50f);

            Button btnCloseDiff = HudBtn(diffModal.transform, "BtnClose", "ĐÓNG", "close", UIBuilderUtilities.ColorAccentCrimson, 480f, 42f, 15);
            RectTransform bcdRt = btnCloseDiff.GetComponent<RectTransform>();
            bcdRt.anchoredPosition = new Vector2(0f, -230f);
            bcdRt.sizeDelta = new Vector2(480f, 42f);

            // -------------------------------------------------------------------------
            // MODAL 2: Online 1v1 Modal (Photon Room Matchmaking)
            // -------------------------------------------------------------------------
            GameObject onlineModal = UIBuilderUtilities.CreatePanel(modalsRoot.transform, "OnlineModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform omRt = onlineModal.GetComponent<RectTransform>();
            omRt.anchorMin = new Vector2(0.5f, 0.5f);
            omRt.anchorMax = new Vector2(0.5f, 0.5f);
            omRt.sizeDelta = new Vector2(760f, 660f);
            StyleCard(onlineModal, 760f, 660f, UIBuilderUtilities.ColorAccentBlue, true);

            Text omTitle = UIBuilderUtilities.CreateText(onlineModal.transform, "Title", "PHÒNG ĐẤU ONLINE 1v1", 26, UIBuilderUtilities.ColorAccentBlue, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform omtRt = omTitle.GetComponent<RectTransform>();
            omtRt.anchorMin = new Vector2(0f, 1f);
            omtRt.anchorMax = new Vector2(1f, 1f);
            omtRt.pivot = new Vector2(0.5f, 1f);
            omtRt.anchoredPosition = new Vector2(0f, -25f);
            omtRt.sizeDelta = new Vector2(0f, 40f);

            Button btnCloseOnline = HudBtn(onlineModal.transform, "BtnCloseOnline", "", "close", UIBuilderUtilities.ColorAccentCrimson, 40f, 40f, 16);
            RectTransform bcoRt = btnCloseOnline.GetComponent<RectTransform>();
            bcoRt.anchorMin = new Vector2(1f, 1f);
            bcoRt.anchorMax = new Vector2(1f, 1f);
            bcoRt.pivot = new Vector2(1f, 1f);
            bcoRt.anchoredPosition = new Vector2(-20f, -20f);
            bcoRt.sizeDelta = new Vector2(40f, 40f);

            // SubPanel A: LobbySubPanel (Creating / Joining Room)
            GameObject lobbySubPanel = new GameObject("LobbySubPanel", typeof(RectTransform));
            lobbySubPanel.transform.SetParent(onlineModal.transform, false);
            RectTransform lspRt = lobbySubPanel.GetComponent<RectTransform>();
            lspRt.anchorMin = Vector2.zero;
            lspRt.anchorMax = Vector2.one;
            lspRt.offsetMin = new Vector2(40f, 40f);
            lspRt.offsetMax = new Vector2(-40f, -80f);

            Text onlineConnStatus = UIBuilderUtilities.CreateText(lobbySubPanel.transform, "ConnStatus", "Trạng thái: Chưa kết nối máy chủ", 14, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleCenter);
            RectTransform ocsRt = onlineConnStatus.GetComponent<RectTransform>();
            ocsRt.anchoredPosition = new Vector2(0f, 220f);
            ocsRt.sizeDelta = new Vector2(680f, 25f);

            InputField onlineNameInput = UIBuilderUtilities.CreateInputField(lobbySubPanel.transform, "OnlineNameInput", "Tên cơ thủ online...", 16);
            RectTransform oniRt = onlineNameInput.GetComponent<RectTransform>();
            oniRt.anchoredPosition = new Vector2(0f, 175f);
            oniRt.sizeDelta = new Vector2(600f, 42f);

            Button btnOnlineConnect = HudBtn(lobbySubPanel.transform, "BtnConnect", "KẾT NỐI SERVER", "none", UIBuilderUtilities.ColorAccentCyan, 600f, 42f, 15);
            RectTransform bocRt = btnOnlineConnect.GetComponent<RectTransform>();
            bocRt.anchoredPosition = new Vector2(0f, 120f);
            bocRt.sizeDelta = new Vector2(600f, 42f);

            // Create Room Section
            Button btnCreateRoom = HudBtn(lobbySubPanel.transform, "BtnCreateRoom", "TẠO PHÒNG ĐẤU MỚI", "play", UIBuilderUtilities.ColorAccentEmerald, 600f, 50f, 17);
            RectTransform bcrRt = btnCreateRoom.GetComponent<RectTransform>();
            bcrRt.anchoredPosition = new Vector2(0f, 50f);
            bcrRt.sizeDelta = new Vector2(600f, 50f);

            Dropdown onlineRaceDrop = UIBuilderUtilities.CreateDropdown(lobbySubPanel.transform, "OnlineRaceDrop", new List<string> { "Race to 3", "Race to 5", "Race to 7", "Race to 9" }, 14);
            RectTransform ordRt = onlineRaceDrop.GetComponent<RectTransform>();
            ordRt.anchoredPosition = new Vector2(-155f, -10f);
            ordRt.sizeDelta = new Vector2(290f, 38f);

            Dropdown onlineTimerDrop = UIBuilderUtilities.CreateDropdown(lobbySubPanel.transform, "OnlineTimerDrop", new List<string> { "Không giới hạn", "30 Giây", "45 Giây", "60 Giây" }, 14);
            RectTransform otdRt = onlineTimerDrop.GetComponent<RectTransform>();
            otdRt.anchoredPosition = new Vector2(155f, -10f);
            otdRt.sizeDelta = new Vector2(290f, 38f);

            // Join Room Section
            InputField joinCodeInput = UIBuilderUtilities.CreateInputField(lobbySubPanel.transform, "JoinCodeInput", "Nhập mã phòng (Room Code)...", 16);
            RectTransform jciRt = joinCodeInput.GetComponent<RectTransform>();
            jciRt.anchoredPosition = new Vector2(-105f, -80f);
            jciRt.sizeDelta = new Vector2(380f, 45f);

            Button btnJoinRoom = HudBtn(lobbySubPanel.transform, "BtnJoinRoom", "VÀO PHÒNG", "none", UIBuilderUtilities.ColorAccentBlue, 180f, 45f, 15);
            RectTransform bjrRt = btnJoinRoom.GetComponent<RectTransform>();
            bjrRt.anchoredPosition = new Vector2(200f, -80f);
            bjrRt.sizeDelta = new Vector2(180f, 45f);

            Button btnBackMenu = HudBtn(lobbySubPanel.transform, "BtnBackMenu", "QUAY LẠI MENU", "exit", UIBuilderUtilities.ColorAccentCyan, 600f, 45f, 15);
            RectTransform bbmRt = btnBackMenu.GetComponent<RectTransform>();
            bbmRt.anchoredPosition = new Vector2(0f, -170f);
            bbmRt.sizeDelta = new Vector2(600f, 45f);

            // SubPanel B: RoomSubPanel (Inside Room Lobby)
            GameObject roomSubPanel = new GameObject("RoomSubPanel", typeof(RectTransform));
            roomSubPanel.transform.SetParent(onlineModal.transform, false);
            RectTransform rspRt = roomSubPanel.GetComponent<RectTransform>();
            rspRt.anchorMin = Vector2.zero;
            rspRt.anchorMax = Vector2.one;
            rspRt.offsetMin = new Vector2(40f, 40f);
            rspRt.offsetMax = new Vector2(-40f, -80f);
            roomSubPanel.SetActive(false);

            Text roomIdText = UIBuilderUtilities.CreateText(roomSubPanel.transform, "RoomIdTitle", "PHÒNG: #123456", 22, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform ritRt = roomIdText.GetComponent<RectTransform>();
            ritRt.anchoredPosition = new Vector2(-70f, 210f);
            ritRt.sizeDelta = new Vector2(400f, 35f);

            Button btnCopyRoomId = HudBtn(roomSubPanel.transform, "BtnCopyRoomId", "COPY MÃ", "none", UIBuilderUtilities.ColorAccentCyan, 130f, 35f, 13);
            RectTransform bcriRt = btnCopyRoomId.GetComponent<RectTransform>();
            bcriRt.anchoredPosition = new Vector2(200f, 210f);
            bcriRt.sizeDelta = new Vector2(130f, 35f);

            Text roomSettingsText = UIBuilderUtilities.CreateText(roomSubPanel.transform, "RoomSettingsText", "Luật thi đấu: Race to 5 • Shot timer 30s", 14, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            RectTransform rstRt = roomSettingsText.GetComponent<RectTransform>();
            rstRt.anchoredPosition = new Vector2(0f, 170f);
            rstRt.sizeDelta = new Vector2(650f, 25f);

            // Player 1 Slot Card
            GameObject p1Card = UIBuilderUtilities.CreatePanel(roomSubPanel.transform, "Player1Card", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorCard);
            RectTransform p1cRt = p1Card.GetComponent<RectTransform>();
            p1cRt.anchoredPosition = new Vector2(-160f, 70f);
            p1cRt.sizeDelta = new Vector2(300f, 150f);

            Text p1Name = UIBuilderUtilities.CreateText(p1Card.transform, "P1Name", "CƠ THỦ 1 (HOST)", 18, UIBuilderUtilities.ColorAccentEmerald, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform p1nRt = p1Name.GetComponent<RectTransform>();
            p1nRt.anchoredPosition = new Vector2(0f, 25f);
            p1nRt.sizeDelta = new Vector2(280f, 35f);

            Text p1Badge = UIBuilderUtilities.CreateText(p1Card.transform, "P1Badge", "SẴN SÀNG", 16, UIBuilderUtilities.ColorAccentEmerald, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform p1bRt = p1Badge.GetComponent<RectTransform>();
            p1bRt.anchoredPosition = new Vector2(0f, -25f);
            p1bRt.sizeDelta = new Vector2(280f, 30f);

            // Player 2 Slot Card
            GameObject p2Card = UIBuilderUtilities.CreatePanel(roomSubPanel.transform, "Player2Card", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorCard);
            RectTransform p2cRt = p2Card.GetComponent<RectTransform>();
            p2cRt.anchoredPosition = new Vector2(160f, 70f);
            p2cRt.sizeDelta = new Vector2(300f, 150f);

            Text p2Name = UIBuilderUtilities.CreateText(p2Card.transform, "P2Name", "Đang chờ đối thủ...", 18, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform p2nRt = p2Name.GetComponent<RectTransform>();
            p2nRt.anchoredPosition = new Vector2(0f, 25f);
            p2nRt.sizeDelta = new Vector2(280f, 35f);

            Text p2Badge = UIBuilderUtilities.CreateText(p2Card.transform, "P2Badge", "CHỜ ĐỐI THỦ", 16, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform p2bRt = p2Badge.GetComponent<RectTransform>();
            p2bRt.anchoredPosition = new Vector2(0f, -25f);
            p2bRt.sizeDelta = new Vector2(280f, 30f);

            // Room Buttons
            Button btnToggleReady = HudBtn(roomSubPanel.transform, "BtnToggleReady", "SẴN SÀNG", "flag", UIBuilderUtilities.ColorAccentEmerald, 600f, 48f, 17);
            RectTransform btrRt = btnToggleReady.GetComponent<RectTransform>();
            btrRt.anchoredPosition = new Vector2(0f, -40f);
            btrRt.sizeDelta = new Vector2(600f, 48f);
            Text btnToggleReadyText = btnToggleReady.GetComponentInChildren<Text>();

            Button btnStartMatch = HudBtn(roomSubPanel.transform, "BtnStartMatch", "BẮT ĐẦU TRẬN ĐẤU", "play", UIBuilderUtilities.ColorAccentGold, 600f, 48f, 17);
            RectTransform bsmRt = btnStartMatch.GetComponent<RectTransform>();
            bsmRt.anchoredPosition = new Vector2(0f, -100f);
            bsmRt.sizeDelta = new Vector2(600f, 48f);

            Button btnLeaveRoom = HudBtn(roomSubPanel.transform, "BtnLeaveRoom", "RỜI PHÒNG", "exit", UIBuilderUtilities.ColorAccentCrimson, 600f, 42f, 15);
            RectTransform blrRt = btnLeaveRoom.GetComponent<RectTransform>();
            blrRt.anchoredPosition = new Vector2(0f, -160f);
            blrRt.sizeDelta = new Vector2(600f, 42f);

            Text roomStatusMsg = UIBuilderUtilities.CreateText(roomSubPanel.transform, "RoomStatusMsg", "", 14, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            RectTransform rsmRt = roomStatusMsg.GetComponent<RectTransform>();
            rsmRt.anchoredPosition = new Vector2(0f, -210f);
            rsmRt.sizeDelta = new Vector2(650f, 25f);

            // -------------------------------------------------------------------------
            // MODAL 3: Settings Modal
            // -------------------------------------------------------------------------
            GameObject settingsModal = UIBuilderUtilities.CreatePanel(modalsRoot.transform, "SettingsModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform smRt = settingsModal.GetComponent<RectTransform>();
            smRt.anchorMin = new Vector2(0.5f, 0.5f);
            smRt.anchorMax = new Vector2(0.5f, 0.5f);
            smRt.sizeDelta = new Vector2(540f, 520f);
            StyleCard(settingsModal, 540f, 520f, UIBuilderUtilities.ColorAccentCyan, true);

            Text smTitle = UIBuilderUtilities.CreateText(settingsModal.transform, "Title", "CÀI ĐẶT ÂM THANH", 24, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            RectTransform smtRt = smTitle.GetComponent<RectTransform>();
            smtRt.anchoredPosition = new Vector2(0f, 210f);
            smtRt.sizeDelta = new Vector2(480f, 40f);

            // Master Slider
            Text lblMaster = UIBuilderUtilities.CreateText(settingsModal.transform, "LblMaster", "Âm lượng tổng (Master Volume):", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblMaster.rectTransform.anchoredPosition = new Vector2(0f, 140f);
            lblMaster.rectTransform.sizeDelta = new Vector2(440f, 25f);
            Slider sldMaster = UIBuilderUtilities.CreateSlider(settingsModal.transform, "SldMaster", 0f, 1f, 0.8f);
            sldMaster.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 105f);
            sldMaster.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 25f);

            // Music Slider
            Text lblMusic = UIBuilderUtilities.CreateText(settingsModal.transform, "LblMusic", "Nhạc nền (BGM Volume):", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblMusic.rectTransform.anchoredPosition = new Vector2(0f, 55f);
            lblMusic.rectTransform.sizeDelta = new Vector2(440f, 25f);
            Slider sldMusic = UIBuilderUtilities.CreateSlider(settingsModal.transform, "SldMusic", 0f, 1f, 0.6f);
            sldMusic.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 20f);
            sldMusic.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 25f);

            // SFX Slider
            Text lblSFX = UIBuilderUtilities.CreateText(settingsModal.transform, "LblSFX", "Hiệu ứng va chạm (SFX Volume):", 15, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleLeft);
            lblSFX.rectTransform.anchoredPosition = new Vector2(0f, -30f);
            lblSFX.rectTransform.sizeDelta = new Vector2(440f, 25f);
            Slider sldSFX = UIBuilderUtilities.CreateSlider(settingsModal.transform, "SldSFX", 0f, 1f, 0.9f);
            sldSFX.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -65f);
            sldSFX.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 25f);

            Button btnCloseSettings = HudBtn(settingsModal.transform, "BtnCloseSettings", "ĐÓNG", "close", UIBuilderUtilities.ColorAccentCyan, 440f, 45f, 15);
            btnCloseSettings.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -170f);
            btnCloseSettings.GetComponent<RectTransform>().sizeDelta = new Vector2(440f, 45f);

            // -------------------------------------------------------------------------
            // MODAL 4: Rules Modal
            // -------------------------------------------------------------------------
            GameObject rulesModal = UIBuilderUtilities.CreatePanel(modalsRoot.transform, "RulesModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform rmRt = rulesModal.GetComponent<RectTransform>();
            rmRt.anchorMin = new Vector2(0.5f, 0.5f);
            rmRt.anchorMax = new Vector2(0.5f, 0.5f);
            rmRt.sizeDelta = new Vector2(800f, 620f);
            StyleCard(rulesModal, 800f, 620f, UIBuilderUtilities.ColorAccentGold, true);

            Text rmTitle = UIBuilderUtilities.CreateText(rulesModal.transform, "Title", "LUẬT THI ĐẤU WPA 8-BALL QUỐC TẾ", 24, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            rmTitle.rectTransform.anchoredPosition = new Vector2(0f, 260f);
            rmTitle.rectTransform.sizeDelta = new Vector2(740f, 40f);

            string rulesContent = 
                "<b>1. CÚ ĐÁNH KHAI CUỘC (BREAK SHOT):</b>\n" +
                "• Phải đưa ít nhất 1 bi mục tiêu vào lỗ HOẶC ít nhất 4 bi chạm băng.\n" +
                "• Nếu ăn bi hợp lệ, cơ thủ được tiếp tục lượt bắn.\n\n" +
                "<b>2. BÀN MỞ & CHỌN NHÓM BI:</b>\n" +
                "• Bàn ở trạng thái MỞ (Open Table) sau khai cuộc.\n" +
                "• Nhóm bi (Trơn 1-7 hoặc Sọc 9-15) chỉ được xác định khi cơ thủ gọi và ăn bi hợp lệ sau cú khai cuộc.\n\n" +
                "<b>3. LỖI PHẠM QUY (FOULS):</b>\n" +
                "• Bi cái rơi vào lỗ hoặc văng khỏi bàn (Scratch).\n" +
                "• Không chạm đúng nhóm bi của mình đầu tiên.\n" +
                "• Sau va chạm, không có bất kỳ bi nào chạm băng hoặc vào lỗ.\n" +
                "• Hình phạt: Đối thủ nhận quyền ĐẶT BI TỰ DO (Ball-in-Hand) ở bất kỳ đâu trên bàn.\n\n" +
                "<b>4. CÚ ĐÁNH GỌI & KẾT THÚC VÁN (8-BALL):</b>\n" +
                "• Khi đã dọn sạch nhóm bi của mình, cơ thủ phải GỌI LỖ đưa bi số 8 vào.\n" +
                "• Ăn bi 8 vào đúng lỗ đã gọi = CHIẾN THẮNG VÁN.\n" +
                "• Đưa bi 8 vào sai lỗ, rơi bi cái cùng bi 8, hoặc làm văng bi 8 khỏi bàn = XỬ THUA VÁN NGAY LẬP TỨC.";

            Text rmBody = UIBuilderUtilities.CreateText(rulesModal.transform, "RulesBody", rulesContent, 15, UIBuilderUtilities.ColorTextWhite, TextAnchor.UpperLeft);
            rmBody.rectTransform.anchoredPosition = new Vector2(0f, 20f);
            rmBody.rectTransform.sizeDelta = new Vector2(720f, 380f);

            Button btnCloseRules = HudBtn(rulesModal.transform, "BtnCloseRules", "ĐÃ HIỂU & ĐÓNG", "close", UIBuilderUtilities.ColorAccentCyan, 400f, 45f, 15);
            btnCloseRules.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -240f);
            btnCloseRules.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 45f);

            // -------------------------------------------------------------------------
            // MODAL 5: Quit Confirm Modal
            // -------------------------------------------------------------------------
            GameObject quitModal = UIBuilderUtilities.CreatePanel(modalsRoot.transform, "QuitConfirmModal", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorBgModal);
            RectTransform qmRt = quitModal.GetComponent<RectTransform>();
            qmRt.anchorMin = new Vector2(0.5f, 0.5f);
            qmRt.anchorMax = new Vector2(0.5f, 0.5f);
            qmRt.sizeDelta = new Vector2(450f, 240f);
            StyleCard(quitModal, 450f, 240f, UIBuilderUtilities.ColorAccentCrimson, true);

            Text qmText = UIBuilderUtilities.CreateText(quitModal.transform, "ConfirmText", "Bạn có chắc chắn muốn thoát khỏi game?", 18, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            qmText.rectTransform.anchoredPosition = new Vector2(0f, 40f);
            qmText.rectTransform.sizeDelta = new Vector2(400f, 60f);

            Button btnQuitYes = HudBtn(quitModal.transform, "BtnQuitYes", "THOÁT", "exit", UIBuilderUtilities.ColorAccentCrimson, 140f, 42f, 14);
            btnQuitYes.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90f, -45f);
            btnQuitYes.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 42f);

            Button btnQuitNo = HudBtn(quitModal.transform, "BtnQuitNo", "HỦY", "close", UIBuilderUtilities.ColorAccentCyan, 140f, 42f, 14);
            btnQuitNo.GetComponent<RectTransform>().anchoredPosition = new Vector2(90f, -45f);
            btnQuitNo.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 42f);

            // Hide modals by default
            diffModal.SetActive(false);
            onlineModal.SetActive(false);
            settingsModal.SetActive(false);
            rulesModal.SetActive(false);
            quitModal.SetActive(false);

            // -------------------------------------------------------------------------
            // 7. Wire SerializedObject fields for LobbyUIController
            // -------------------------------------------------------------------------
            SerializedObject soLobby = new SerializedObject(lobbyUI);
            soLobby.FindProperty("playVsAIButton").objectReferenceValue = btnPlayAI;
            soLobby.FindProperty("playOnlineButton").objectReferenceValue = btnPlayOnline;
            soLobby.FindProperty("rulesButton").objectReferenceValue = btnRules;
            soLobby.FindProperty("settingsButton").objectReferenceValue = btnSettings;
            soLobby.FindProperty("quitGameButton").objectReferenceValue = btnQuit;

            soLobby.FindProperty("difficultyModalRoot").objectReferenceValue = diffModal;
            soLobby.FindProperty("diffEasyButton").objectReferenceValue = btnDiffEasy;
            soLobby.FindProperty("diffNormalButton").objectReferenceValue = btnDiffNormal;
            soLobby.FindProperty("diffHardButton").objectReferenceValue = btnDiffHard;
            soLobby.FindProperty("diffEasyHighlight").objectReferenceValue = hlEasy;
            soLobby.FindProperty("diffNormalHighlight").objectReferenceValue = hlNormal;
            soLobby.FindProperty("diffHardHighlight").objectReferenceValue = hlHard;
            soLobby.FindProperty("raceTargetDropdown").objectReferenceValue = raceDropdown;
            soLobby.FindProperty("shotTimerDropdown").objectReferenceValue = timerDropdown;
            soLobby.FindProperty("playerNameInput").objectReferenceValue = playerNameInput;
            soLobby.FindProperty("startOfflineMatchButton").objectReferenceValue = btnStartOffline;
            soLobby.FindProperty("closeDifficultyModalButton").objectReferenceValue = btnCloseDiff;

            soLobby.FindProperty("onlineModalRoot").objectReferenceValue = onlineModal;
            soLobby.FindProperty("onlineRoomUI").objectReferenceValue = onlineRoomUI;
            soLobby.FindProperty("createRoomButton").objectReferenceValue = btnCreateRoom;
            soLobby.FindProperty("roomCodeInput").objectReferenceValue = joinCodeInput;
            soLobby.FindProperty("joinRoomButton").objectReferenceValue = btnJoinRoom;
            soLobby.FindProperty("closeOnlineModalButton").objectReferenceValue = btnCloseOnline;
            soLobby.FindProperty("onlineStatusText").objectReferenceValue = onlineConnStatus;

            soLobby.FindProperty("settingsModalRoot").objectReferenceValue = settingsModal;
            soLobby.FindProperty("masterVolumeSlider").objectReferenceValue = sldMaster;
            soLobby.FindProperty("musicVolumeSlider").objectReferenceValue = sldMusic;
            soLobby.FindProperty("sfxVolumeSlider").objectReferenceValue = sldSFX;
            soLobby.FindProperty("closeSettingsModalButton").objectReferenceValue = btnCloseSettings;

            soLobby.FindProperty("rulesModalRoot").objectReferenceValue = rulesModal;
            soLobby.FindProperty("closeRulesModalButton").objectReferenceValue = btnCloseRules;

            soLobby.FindProperty("quitConfirmModalRoot").objectReferenceValue = quitModal;
            soLobby.FindProperty("quitConfirmYesButton").objectReferenceValue = btnQuitYes;
            soLobby.FindProperty("quitConfirmNoButton").objectReferenceValue = btnQuitNo;

            soLobby.ApplyModifiedProperties();

            // -------------------------------------------------------------------------
            // 8. Wire SerializedObject fields for OnlineRoomUI
            // -------------------------------------------------------------------------
            SerializedObject soOnline = new SerializedObject(onlineRoomUI);
            soOnline.FindProperty("onlineRootPanel").objectReferenceValue = onlineModal;
            soOnline.FindProperty("lobbySubPanel").objectReferenceValue = lobbySubPanel;
            soOnline.FindProperty("roomSubPanel").objectReferenceValue = roomSubPanel;

            soOnline.FindProperty("playerNameInput").objectReferenceValue = onlineNameInput;
            soOnline.FindProperty("connectionStatusText").objectReferenceValue = onlineConnStatus;
            soOnline.FindProperty("connectButton").objectReferenceValue = btnOnlineConnect;
            soOnline.FindProperty("raceTargetDropdown").objectReferenceValue = onlineRaceDrop;
            soOnline.FindProperty("shotTimerDropdown").objectReferenceValue = onlineTimerDrop;
            soOnline.FindProperty("createRoomButton").objectReferenceValue = btnCreateRoom;
            soOnline.FindProperty("joinRoomCodeInput").objectReferenceValue = joinCodeInput;
            soOnline.FindProperty("joinRoomButton").objectReferenceValue = btnJoinRoom;
            soOnline.FindProperty("backToMainMenuButton").objectReferenceValue = btnBackMenu;

            soOnline.FindProperty("roomIdDisplayTitle").objectReferenceValue = roomIdText;
            soOnline.FindProperty("copyRoomIdButton").objectReferenceValue = btnCopyRoomId;
            soOnline.FindProperty("roomSettingsInfoText").objectReferenceValue = roomSettingsText;
            soOnline.FindProperty("player1NameText").objectReferenceValue = p1Name;
            soOnline.FindProperty("player1ReadyBadgeText").objectReferenceValue = p1Badge;
            soOnline.FindProperty("player2NameText").objectReferenceValue = p2Name;
            soOnline.FindProperty("player2ReadyBadgeText").objectReferenceValue = p2Badge;
            soOnline.FindProperty("toggleReadyButton").objectReferenceValue = btnToggleReady;
            soOnline.FindProperty("toggleReadyButtonText").objectReferenceValue = btnToggleReadyText;
            soOnline.FindProperty("startMatchButton").objectReferenceValue = btnStartMatch;
            soOnline.FindProperty("leaveRoomButton").objectReferenceValue = btnLeaveRoom;
            soOnline.FindProperty("roomStatusMessageText").objectReferenceValue = roomStatusMsg;

            soOnline.ApplyModifiedProperties();

            ToneDownColors(canvasGo, modalsRoot);

            UIBuilderUtilities.SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("★★★ THÀNH CÔNG: Xây dựng Canvas UI sảnh chờ (Lobby UI) đẹp mắt, chuyên nghiệp và kết nối đầy đủ! ★★★");
        }
    }
}
