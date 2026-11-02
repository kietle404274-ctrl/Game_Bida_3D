using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Game.Billiards.Network;

namespace Game.Billiards.Core
{
    /// <summary>
    /// Áp dụng Material mặt bàn (Play Field và Cushions) theo chủ đề bàn (TableTheme) đã chọn
    /// mà không làm thay đổi bất kỳ thông số vật lý (TableWrapper / Collider) nào của bàn.
    /// </summary>
    public static class TableThemeApplier
    {
        private static readonly Dictionary<TableTheme, Material> CachedMaterials = new Dictionary<TableTheme, Material>();
        private static readonly Dictionary<TableTheme, Sprite> CachedPreviews = new Dictionary<TableTheme, Sprite>();

        public static Material LoadThemeMaterial(TableTheme theme)
        {
            if (CachedMaterials.TryGetValue(theme, out Material cached) && cached != null)
            {
                return cached;
            }

            string path = $"TableThemes/Materials/TableCloth_{theme}";
            Material mat = Resources.Load<Material>(path);
            if (mat != null)
            {
                CachedMaterials[theme] = mat;
            }
            else
            {
                Debug.LogWarning($"[TableThemeApplier] Không tìm thấy Material tại Resources/{path}");
            }
            return mat;
        }

        public static Sprite LoadThemePreviewSprite(TableTheme theme)
        {
            if (CachedPreviews.TryGetValue(theme, out Sprite cached) && cached != null)
            {
                return cached;
            }

            string path = $"TableThemes/Previews/TablePreview_{theme}";
            Sprite sp = Resources.Load<Sprite>(path);
            if (sp == null)
            {
                Texture2D tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    sp = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            if (sp != null)
            {
                CachedPreviews[theme] = sp;
            }
            return sp;
        }

        public static Color GetThemeAccentColor(TableTheme theme)
        {
            switch (theme)
            {
                case TableTheme.Purple: return new Color(0.52f, 0.44f, 0.86f, 1f);
                case TableTheme.Green:  return new Color(0.28f, 0.65f, 0.47f, 1f);
                case TableTheme.Red:    return new Color(0.86f, 0.42f, 0.38f, 1f);
                case TableTheme.Grey:   return new Color(0.58f, 0.60f, 0.62f, 1f);
                case TableTheme.Blue:
                default:                return new Color(0.36f, 0.66f, 0.72f, 1f);
            }
        }

        /// <summary>
        /// Lấy TableTheme áp dụng cho trận đấu hiện tại (ưu tiên Room Property khi đấu Online).
        /// </summary>
        public static TableTheme ResolveActiveMatchTableTheme()
        {
            if (GameSession.Instance != null &&
                GameSession.Instance.CurrentGameMode == GameMode.OnlinePvP &&
                PhotonNetwork.InRoom &&
                PhotonNetwork.CurrentRoom != null &&
                PhotonNetwork.CurrentRoom.CustomProperties != null &&
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(NetworkConstants.PROP_TABLE_THEME, out object rawTheme))
            {
                int val = System.Convert.ToInt32(rawTheme);
                if (val >= 0 && val <= (int)TableTheme.Grey)
                {
                    return (TableTheme)val;
                }
            }

            return GameSession.SelectedTableTheme;
        }

        /// <summary>
        /// Áp dụng chủ đề mặt bàn cho toàn bộ MeshRenderer của Play Field và Cushions trong Scene hiện tại.
        /// </summary>
        public static void ApplyThemeToCurrentScene(TableTheme theme)
        {
            Material clothMat = LoadThemeMaterial(theme);
            if (clothMat == null) return;

            MeshRenderer[] renderers = Object.FindObjectsOfType<MeshRenderer>(true);
            int appliedCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer mr = renderers[i];
                if (mr == null || mr.gameObject == null) continue;

                string objName = mr.gameObject.name.Trim();
                if (string.Equals(objName, "Play Field", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objName, "Play_Field", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(objName, "Cushions", System.StringComparison.OrdinalIgnoreCase))
                {
                    mr.sharedMaterial = clothMat;
                    appliedCount++;
                }
            }

            Debug.Log($"[TableThemeApplier] Đã áp dụng chủ đề bàn '{GameSession.GetTableThemeName(theme)}' ({theme}) cho {appliedCount} bộ phận mặt bàn.");
        }
    }
}

