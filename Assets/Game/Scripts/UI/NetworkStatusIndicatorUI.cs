using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Game.Billiards.Core;
using Game.Billiards.Network;

namespace Game.Billiards.UI
{
    /// <summary>
    /// Huy hiệu hiển thị tín hiệu sóng Wi-Fi + Ping (ms) và cảnh báo khi mạng yếu/delay khiến game bị khựng.
    /// Chỉ hiển thị trong chế độ Đấu Online 1v1; tự động ẩn hoàn toàn khi Đấu với Máy (AI).
    /// </summary>
    public class NetworkStatusIndicatorUI : MonoBehaviour
    {
        private RectTransform pillRt;
        private CanvasGroup canvasGroup;
        private SkewedPanelGraphic bodyGraphic;
        private BorderProgressGraphic borderGraphic;
        private WifiSignalGraphic wifiIcon;
        private Text statusText;

        private NetworkMatchManager netMatchManager;
        private float nextSearchTime;
        private float currentWidth = 108f;

        private static readonly Color BodyNormal = new Color(0.05f, 0.11f, 0.20f, 0.90f);
        private static readonly Color BodyWarning = new Color(0.34f, 0.07f, 0.09f, 0.96f);

        private static readonly Color ColorGood = new Color(0.18f, 0.88f, 0.48f, 1f);   // Xanh lá (< 120ms)
        private static readonly Color ColorFair = new Color(0.98f, 0.68f, 0.18f, 1f);   // Vàng cam (120 - 220ms)
        private static readonly Color ColorWeak = new Color(1.00f, 0.30f, 0.30f, 1f);   // Đỏ (> 220ms / Delay)

        public static NetworkStatusIndicatorUI Ensure(GameplayHUD hud)
        {
            if (hud == null) return null;
            var existing = hud.GetComponentInChildren<NetworkStatusIndicatorUI>(true);
            if (existing != null) return existing;

            var go = new GameObject("NetworkStatusIndicator", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            var indicator = go.AddComponent<NetworkStatusIndicatorUI>();
            indicator.Build();
            return indicator;
        }

        private void Build()
        {
            var rootRt = (RectTransform)transform;
            rootRt.anchorMin = new Vector2(0.5f, 1f);
            rootRt.anchorMax = new Vector2(0.5f, 1f);
            rootRt.pivot = new Vector2(0.5f, 1f);
            rootRt.anchoredPosition = new Vector2(0f, -92f);
            rootRt.sizeDelta = new Vector2(360f, 34f);

            var pillGo = new GameObject("Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(SkewedPanelGraphic), typeof(CanvasGroup));
            pillGo.transform.SetParent(rootRt, false);
            pillRt = (RectTransform)pillGo.transform;
            pillRt.anchorMin = pillRt.anchorMax = new Vector2(0.5f, 0.5f);
            pillRt.pivot = new Vector2(0.5f, 0.5f);
            pillRt.sizeDelta = new Vector2(108f, 28f);
            pillRt.anchoredPosition = Vector2.zero;

            canvasGroup = pillGo.GetComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            bodyGraphic = pillGo.GetComponent<SkewedPanelGraphic>();
            bodyGraphic.SetInsets(0f, 7f, 7f, 0f);
            bodyGraphic.SetBottomDarken(0.35f);
            bodyGraphic.color = BodyNormal;
            bodyGraphic.raycastTarget = false;

            var borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(BorderProgressGraphic));
            borderGo.transform.SetParent(pillRt, false);
            var borderRt = (RectTransform)borderGo.transform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;

            borderGraphic = borderGo.GetComponent<BorderProgressGraphic>();
            borderGraphic.SetInsets(0f, 7f, 7f, 0f);
            borderGraphic.SetBaseColor(new Color(0.25f, 0.70f, 0.95f, 0.55f));
            borderGraphic.ClearProgress();
            borderGraphic.raycastTarget = false;

            var iconGo = new GameObject("WifiIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(WifiSignalGraphic));
            iconGo.transform.SetParent(pillRt, false);
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(22f, 0f);
            iconRt.sizeDelta = new Vector2(20f, 16f);

            wifiIcon = iconGo.GetComponent<WifiSignalGraphic>();
            wifiIcon.raycastTarget = false;

            var textGo = new GameObject("StatusText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(pillRt, false);
            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(36f, 0f);
            textRt.offsetMax = new Vector2(-10f, 0f);

            statusText = textGo.GetComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 13;
            statusText.fontStyle = FontStyle.Bold;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
            statusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;
            statusText.raycastTarget = false;

            RefreshState();
        }

        private void Update()
        {
            RefreshState();
        }

        private void RefreshState()
        {
            if (pillRt == null) return;

            var session = GameSession.Instance;
            bool isOnline = (session != null && session.CurrentGameMode == GameMode.OnlinePvP) || PhotonNetwork.InRoom;
            if (!isOnline)
            {
                if (pillRt.gameObject.activeSelf) pillRt.gameObject.SetActive(false);
                return;
            }

            if (!pillRt.gameObject.activeSelf) pillRt.gameObject.SetActive(true);

            if (netMatchManager == null && Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + 0.5f;
                netMatchManager = FindObjectOfType<NetworkMatchManager>();
            }

            int ping = netMatchManager != null
                ? netMatchManager.CurrentPingMs
                : (PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() : 999);

            bool connectionLost = netMatchManager != null
                ? netMatchManager.IsConnectionLostPending
                : (!PhotonNetwork.IsConnected || Application.internetReachability == NetworkReachability.NotReachable);

            bool isDelaying = netMatchManager != null
                ? netMatchManager.IsExperiencingNetworkDelay
                : (connectionLost || ping > 220);

            int bars;
            Color accent;
            if (connectionLost)
            {
                bars = 0;
                accent = ColorWeak;
            }
            else if (ping > 220 || isDelaying)
            {
                bars = 1;
                accent = ColorWeak;
            }
            else if (ping > 120)
            {
                bars = 2;
                accent = ColorFair;
            }
            else
            {
                bars = 3;
                accent = ColorGood;
            }

            wifiIcon.SetSignal(bars, isDelaying, accent);

            float targetWidth = isDelaying ? 340f : 108f;
            currentWidth = Mathf.Lerp(currentWidth, targetWidth, Time.unscaledDeltaTime * 12f);
            pillRt.sizeDelta = new Vector2(currentWidth, isDelaying ? 30f : 26f);

            if (isDelaying)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
                bodyGraphic.color = Color.Lerp(BodyWarning, new Color(0.48f, 0.09f, 0.12f, 0.98f), pulse);
                borderGraphic.SetBaseColor(Color.Lerp(new Color(1f, 0.30f, 0.30f, 0.65f), new Color(1f, 0.45f, 0.45f, 1f), pulse));

                string msg = connectionLost
                    ? LocalizationManager.Get("net_connection_unstable")
                    : $"{LocalizationManager.Get("net_weak_syncing")} ({Mathf.Clamp(ping, 1, 999)} ms)";
                statusText.text = msg;
                statusText.color = Color.Lerp(new Color(1f, 0.82f, 0.82f, 1f), Color.white, pulse);
            }
            else
            {
                bodyGraphic.color = BodyNormal;
                borderGraphic.SetBaseColor(new Color(accent.r, accent.g, accent.b, 0.55f));
                statusText.text = $"{Mathf.Clamp(ping, 1, 999)} ms";
                statusText.color = accent;
            }
        }
    }

    /// <summary>
    /// Vẽ biểu tượng sóng Wi-Fi vector (chấm tròn gốc + 3 cung tròn đồng tâm + dấu chấm than '!' khi sóng yếu/mất mạng).
    /// </summary>
    public class WifiSignalGraphic : MaskableGraphic
    {
        private int activeBars = 3;
        private bool showWarningExclamation = false;
        private Color activeColor = new Color(0.18f, 0.88f, 0.48f, 1f);
        private static readonly Color DimBarColor = new Color(1f, 1f, 1f, 0.18f);

        public void SetSignal(int bars, bool warning, Color sigColor)
        {
            bars = Mathf.Clamp(bars, 0, 3);
            if (activeBars == bars && showWarningExclamation == warning && activeColor == sigColor) return;
            activeBars = bars;
            showWarningExclamation = warning;
            activeColor = sigColor;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = rectTransform.rect;
            float cx = r.center.x - (showWarningExclamation ? 2.5f : 0f);
            float baseY = r.yMin + 2.5f;

            // 1. Chấm tròn gốc của cột sóng Wi-Fi
            Color dotCol = activeBars >= 1 ? activeColor : DimBarColor;
            AddDisc(vh, new Vector2(cx, baseY), 1.8f, dotCol, 10);

            // 2. Ba cung sóng Wi-Fi (từ thấp lên cao)
            float[] radii = { 5.0f, 8.5f, 12.0f };
            float thickness = 1.7f;
            float halfAngleRad = 44f * Mathf.Deg2Rad;

            for (int i = 0; i < 3; i++)
            {
                Color arcCol = (i < activeBars) ? activeColor : DimBarColor;
                AddArc(vh, new Vector2(cx, baseY), radii[i], thickness,
                    Mathf.PI * 0.5f - halfAngleRad,
                    Mathf.PI * 0.5f + halfAngleRad,
                    arcCol, 12);
            }

            // 3. Dấu chấm than cảnh báo (!) bên phải khi mạng yếu / nghẽn gói
            if (showWarningExclamation)
            {
                float exX = r.xMax - 2.2f;
                Color warnCol = new Color(1f, 0.32f, 0.32f, 1f);
                // Thân dấu chấm than
                AddQuad(vh,
                    new Vector2(exX - 1.1f, baseY + 3.6f),
                    new Vector2(exX + 1.1f, baseY + 3.6f),
                    new Vector2(exX + 1.1f, baseY + 11.2f),
                    new Vector2(exX - 1.1f, baseY + 11.2f),
                    warnCol);
                // Chấm tròn dưới của dấu chấm than
                AddDisc(vh, new Vector2(exX, baseY + 1.0f), 1.25f, warnCol, 8);
            }
        }

        private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color col, int segments)
        {
            int centerIdx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = center;
            vh.AddVert(v);

            for (int i = 0; i <= segments; i++)
            {
                float ang = (i / (float)segments) * Mathf.PI * 2f;
                v.position = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
                vh.AddVert(v);
            }

            for (int i = 1; i <= segments; i++)
            {
                vh.AddTriangle(centerIdx, centerIdx + i, centerIdx + i + 1);
            }
        }

        private static void AddArc(VertexHelper vh, Vector2 center, float radius, float thickness, float startAngle, float endAngle, Color col, int segments)
        {
            float rIn = radius - thickness * 0.5f;
            float rOut = radius + thickness * 0.5f;
            int baseIdx = vh.currentVertCount;

            UIVertex v = UIVertex.simpleVert;
            v.color = col;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float ang = Mathf.Lerp(startAngle, endAngle, t);
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));

                v.position = center + dir * rIn;
                vh.AddVert(v);

                v.position = center + dir * rOut;
                vh.AddVert(v);
            }

            for (int i = 0; i < segments; i++)
            {
                int k = baseIdx + i * 2;
                vh.AddTriangle(k, k + 1, k + 3);
                vh.AddTriangle(k, k + 3, k + 2);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
        {
            int idx = vh.currentVertCount;
            UIVertex v = UIVertex.simpleVert;
            v.color = col;
            v.position = a; vh.AddVert(v);
            v.position = b; vh.AddVert(v);
            v.position = c; vh.AddVert(v);
            v.position = d; vh.AddVert(v);
            vh.AddTriangle(idx, idx + 1, idx + 2);
            vh.AddTriangle(idx, idx + 2, idx + 3);
        }
    }
}
