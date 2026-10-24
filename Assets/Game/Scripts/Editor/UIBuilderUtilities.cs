using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Game.Billiards.Editor
{
    public static class UIBuilderUtilities
    {
        public static readonly Color ColorBgDark = new Color(0.06f, 0.09f, 0.16f, 0.92f);
        public static readonly Color ColorBgPanel = new Color(0.09f, 0.13f, 0.22f, 0.88f);
        public static readonly Color ColorBgModal = new Color(0.04f, 0.07f, 0.12f, 0.96f);
        public static readonly Color ColorCard = new Color(0.12f, 0.18f, 0.28f, 0.85f);

        public static readonly Color ColorAccentCyan = new Color(0.02f, 0.71f, 0.83f, 1f);
        public static readonly Color ColorAccentEmerald = new Color(0.06f, 0.73f, 0.51f, 1f);
        public static readonly Color ColorAccentGold = new Color(0.96f, 0.62f, 0.04f, 1f);
        public static readonly Color ColorAccentCrimson = new Color(0.94f, 0.27f, 0.27f, 1f);
        public static readonly Color ColorAccentBlue = new Color(0.23f, 0.51f, 0.96f, 1f);

        public static readonly Color ColorTextWhite = new Color(0.97f, 0.98f, 0.99f, 1f);
        public static readonly Color ColorTextMuted = new Color(0.65f, 0.72f, 0.82f, 1f);
        public static readonly Color ColorTextDark = new Color(0.1f, 0.1f, 0.1f, 1f);

        private static Sprite cachedPanelSprite;
        private static Sprite cachedButtonSprite;
        private static Sprite cachedCircleSprite;
        private static Sprite cachedReticleSprite;
        private static Sprite cachedTurnGlowSprite;
        private static Sprite cachedPowerGradSprite;

        public static Sprite PanelSprite => cachedPanelSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_panel.png");
        public static Sprite ButtonSprite => cachedButtonSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_button.png");
        public static Sprite CircleSprite => cachedCircleSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_circle.png");
        public static Sprite ReticleSprite => cachedReticleSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_reticle.png");
        public static Sprite TurnGlowSprite => cachedTurnGlowSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_turn_glow.png");
        public static Sprite PowerGradSprite => cachedPowerGradSprite ??= AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Game/ArtOverrides/UI/ui_power_gradient.png");

        public static Font DefaultFont
        {
            get
            {
                Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return f;
            }
        }

        public static void SetLayerRecursively(GameObject obj, int newLayer)
        {
            if (obj == null) return;
            obj.layer = newLayer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, newLayer);
            }
        }

        public static GameObject CreateCanvas(string name)
        {
            GameObject canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.layer = LayerMask.NameToLayer("UI");
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.localScale = Vector3.one;

            // Ensure EventSystem exists
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvasGo;
        }

        public static GameObject CreatePanel(Transform parent, string name, Sprite sprite, Color color)
        {
            GameObject panelGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGo.layer = LayerMask.NameToLayer("UI");
            panelGo.transform.SetParent(parent, false);
            Image img = panelGo.GetComponent<Image>();
            img.sprite = sprite != null ? sprite : PanelSprite;
            img.type = (img.sprite != null) ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return panelGo;
        }

        public static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            GameObject textGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.layer = LayerMask.NameToLayer("UI");
            textGo.transform.SetParent(parent, false);
            Text t = textGo.GetComponent<Text>();
            t.font = DefaultFont;
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = alignment;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button CreateButton(Transform parent, string name, string labelText, Sprite bgSprite, Color normalColor, Color textColor, int fontSize = 18, FontStyle style = FontStyle.Bold)
        {
            DefaultControls.Resources res = new DefaultControls.Resources();
            res.standard = bgSprite != null ? bgSprite : ButtonSprite;
            GameObject btnGo = DefaultControls.CreateButton(res);
            btnGo.name = name;
            btnGo.layer = LayerMask.NameToLayer("UI");
            btnGo.transform.SetParent(parent, false);

            Image img = btnGo.GetComponent<Image>();
            img.sprite = bgSprite != null ? bgSprite : ButtonSprite;
            img.type = Image.Type.Sliced;
            img.color = normalColor;

            Button btn = btnGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = normalColor;
            cb.highlightedColor = Color.Lerp(normalColor, Color.white, 0.25f);
            cb.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
            cb.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            btn.colors = cb;

            Text t = btnGo.GetComponentInChildren<Text>();
            if (t != null)
            {
                t.font = DefaultFont;
                t.text = labelText;
                t.fontSize = fontSize;
                t.color = textColor;
                t.fontStyle = style;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
            }

            return btn;
        }

        public static Dropdown CreateDropdown(Transform parent, string name, List<string> options, int fontSize = 16)
        {
            DefaultControls.Resources res = new DefaultControls.Resources();
            res.standard = ButtonSprite;
            res.background = PanelSprite;
            res.checkmark = CircleSprite;
            res.dropdown = ButtonSprite;

            GameObject dropGo = DefaultControls.CreateDropdown(res);
            dropGo.name = name;
            dropGo.transform.SetParent(parent, false);

            Dropdown dropdown = dropGo.GetComponent<Dropdown>();
            Image bg = dropGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = ButtonSprite;
                bg.type = Image.Type.Sliced;
                bg.color = ColorCard;
            }

            Text caption = dropGo.GetComponentInChildren<Text>();
            if (caption != null)
            {
                caption.font = DefaultFont;
                caption.fontSize = fontSize;
                caption.color = ColorTextWhite;
                caption.alignment = TextAnchor.MiddleLeft;
            }

            if (options != null)
            {
                dropdown.ClearOptions();
                dropdown.AddOptions(options);
            }

            SetLayerRecursively(dropGo, LayerMask.NameToLayer("UI"));
            return dropdown;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholderText, int fontSize = 16)
        {
            DefaultControls.Resources res = new DefaultControls.Resources();
            res.inputField = ButtonSprite;

            GameObject inputGo = DefaultControls.CreateInputField(res);
            inputGo.name = name;
            inputGo.transform.SetParent(parent, false);

            Image bg = inputGo.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = ButtonSprite;
                bg.type = Image.Type.Sliced;
                bg.color = ColorCard;
            }

            InputField inputField = inputGo.GetComponent<InputField>();
            Text textComponent = inputField.textComponent;
            if (textComponent != null)
            {
                textComponent.font = DefaultFont;
                textComponent.fontSize = fontSize;
                textComponent.color = ColorTextWhite;
                textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;
                textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (inputField.placeholder is Text ph)
            {
                ph.font = DefaultFont;
                ph.fontSize = fontSize;
                ph.color = ColorTextMuted;
                ph.text = placeholderText;
                ph.horizontalOverflow = HorizontalWrapMode.Overflow;
                ph.verticalOverflow = VerticalWrapMode.Overflow;
            }

            SetLayerRecursively(inputGo, LayerMask.NameToLayer("UI"));
            return inputField;
        }

        public static Slider CreateSlider(Transform parent, string name, float min = 0f, float max = 1f, float initial = 0.8f)
        {
            DefaultControls.Resources res = new DefaultControls.Resources();
            res.background = PanelSprite;
            res.knob = CircleSprite;

            GameObject sliderGo = DefaultControls.CreateSlider(res);
            sliderGo.name = name;
            sliderGo.transform.SetParent(parent, false);

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;

            // Style background & fill
            Transform bgTrans = sliderGo.transform.Find("Background");
            if (bgTrans != null && bgTrans.TryGetComponent<Image>(out var bgImg))
            {
                bgImg.sprite = PanelSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(0.1f, 0.14f, 0.2f, 0.8f);
            }

            Transform fillTrans = sliderGo.transform.Find("Fill Area/Fill");
            if (fillTrans != null && fillTrans.TryGetComponent<Image>(out var fillImg))
            {
                fillImg.sprite = ButtonSprite;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = ColorAccentCyan;
            }

            Transform handleTrans = sliderGo.transform.Find("Handle Slide Area/Handle");
            if (handleTrans != null && handleTrans.TryGetComponent<Image>(out var handleImg))
            {
                handleImg.sprite = CircleSprite;
                handleImg.color = Color.white;
            }

            SetLayerRecursively(sliderGo, LayerMask.NameToLayer("UI"));
            return slider;
        }
    }
}
