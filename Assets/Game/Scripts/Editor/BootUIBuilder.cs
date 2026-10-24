using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Game.Billiards.Editor
{
    public static class BootUIBuilder
    {
        [MenuItem("Tools/Game/Build Boot UI Canvas", false, 9)]
        public static void Build()
        {
            const string scenePath = "Assets/Game/Scenes/Boot.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Remove existing BootCanvas
            GameObject existingCanvas = GameObject.Find("BootCanvas");
            if (existingCanvas != null)
            {
                Undo.DestroyObjectImmediate(existingCanvas);
            }

            // 2. Create BootCanvas
            GameObject canvasGo = UIBuilderUtilities.CreateCanvas("BootCanvas");
            Transform canvasTrans = canvasGo.transform;

            // 3. Dark Background Panel
            GameObject bgPanel = UIBuilderUtilities.CreatePanel(canvasTrans, "SplashBackground", UIBuilderUtilities.PanelSprite, new Color(0.04f, 0.06f, 0.10f, 1f));
            RectTransform bgRt = bgPanel.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // 4. Center Logo / Branding Card
            GameObject logoCard = UIBuilderUtilities.CreatePanel(bgPanel.transform, "LogoCard", UIBuilderUtilities.PanelSprite, UIBuilderUtilities.ColorCard);
            RectTransform lcRt = logoCard.GetComponent<RectTransform>();
            lcRt.anchorMin = new Vector2(0.5f, 0.5f);
            lcRt.anchorMax = new Vector2(0.5f, 0.5f);
            lcRt.sizeDelta = new Vector2(750f, 400f);

            Text logoText = UIBuilderUtilities.CreateText(logoCard.transform, "LogoTitle", "🎱 8-BALL BILLIARDS 3D", 50, UIBuilderUtilities.ColorAccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            logoText.rectTransform.anchoredPosition = new Vector2(0f, 90f);
            logoText.rectTransform.sizeDelta = new Vector2(700f, 65f);

            Text subTitle = UIBuilderUtilities.CreateText(logoCard.transform, "SubTitle", "CHAMPIONSHIP TOURNAMENT EDITION • WPA RULES", 18, UIBuilderUtilities.ColorAccentCyan, TextAnchor.MiddleCenter, FontStyle.Normal);
            subTitle.rectTransform.anchoredPosition = new Vector2(0f, 35f);
            subTitle.rectTransform.sizeDelta = new Vector2(700f, 30f);

            // Divider Line
            GameObject divider = UIBuilderUtilities.CreatePanel(logoCard.transform, "Divider", UIBuilderUtilities.ButtonSprite, new Color(0.2f, 0.4f, 0.6f, 0.5f));
            divider.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 5f);
            divider.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 2f);

            // Loading Text & Bar
            Text loadingText = UIBuilderUtilities.CreateText(logoCard.transform, "LoadingText", "⚡ ĐANG TẢI DỮ LIỆU & KHỞI TẠO BÀN ĐẤU... ⚡", 15, UIBuilderUtilities.ColorTextWhite, TextAnchor.MiddleCenter);
            loadingText.rectTransform.anchoredPosition = new Vector2(0f, -45f);
            loadingText.rectTransform.sizeDelta = new Vector2(600f, 25f);

            GameObject barBg = UIBuilderUtilities.CreatePanel(logoCard.transform, "ProgressBarBg", UIBuilderUtilities.ButtonSprite, new Color(0.08f, 0.12f, 0.18f, 0.95f));
            barBg.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -85f);
            barBg.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 18f);

            GameObject barFill = UIBuilderUtilities.CreatePanel(barBg.transform, "ProgressBarFill", UIBuilderUtilities.ButtonSprite, UIBuilderUtilities.ColorAccentEmerald);
            Image fillImg = barFill.GetComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 0.85f;
            RectTransform bfRt = barFill.GetComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero;
            bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = Vector2.zero;
            bfRt.offsetMax = Vector2.zero;

            Text versionText = UIBuilderUtilities.CreateText(logoCard.transform, "VersionText", "Phiên bản: 1.0 (LTS HDRP) • Sẵn sàng bảo vệ Đồ Án", 12, UIBuilderUtilities.ColorTextMuted, TextAnchor.MiddleCenter);
            versionText.rectTransform.anchoredPosition = new Vector2(0f, -145f);
            versionText.rectTransform.sizeDelta = new Vector2(600f, 20f);

            UIBuilderUtilities.SetLayerRecursively(canvasGo, LayerMask.NameToLayer("UI"));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("★★★ THÀNH CÔNG: Xây dựng Boot Splash Canvas tuyệt đẹp cho Boot.unity! ★★★");
        }
    }
}
