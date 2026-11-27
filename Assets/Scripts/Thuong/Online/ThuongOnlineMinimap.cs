using System.Collections.Generic;
using System.Linq;
using TMPro;
using Thuong.Maps;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed class MinimapPlayerMarker
        {
            public Image Image;
            public RectTransform Rect;
        }

        private readonly Dictionary<int, Image> minimapStationMarkers = new();
        private readonly Dictionary<int, MinimapPlayerMarker> minimapPlayerMarkers = new();
        private RectTransform minimapPanel, minimapViewport, minimapMapRect, minimapHeader;
        private Image minimapMapImage;
        private AspectRatioFitter minimapAspect;
        private TMP_Text minimapMapName, minimapToggleLabel;
        private Button minimapHeaderButton, minimapMapButton;
        private ThuongMapDefinition minimapDefinition;
        private Texture2D minimapDotTexture;
        private Sprite minimapDotSprite;
        private Texture2D minimapArrowTexture;
        private Sprite minimapArrowSprite;
        private bool minimapExpanded = true;

        private static readonly Color MinimapPanelColor = new(0.09f, 0.065f, 0.04f, 0.94f);
        private static readonly Color MinimapPlayerColor = new(0.38f, 0.88f, 0.83f, 1f);
        private static readonly Color MinimapLocalColor = new(0.28f, 0.91f, 0.96f, 1f);
        private static readonly Color MinimapTaskColor = new(0.96f, 0.72f, 0.31f, 1f);
        private static readonly Color MinimapDoneColor = new(0.48f, 0.69f, 0.52f, 0.8f);
        private static readonly Color MinimapInactiveTaskColor = new(0.72f, 0.65f, 0.49f, 0.74f);

        private void RefreshMinimap()
        {
            bool canShow = sceneHud != null && connected && matchStarted && matchResult == null &&
                           HasLocalRole && LocalPlayerAlive && localMapReady && SelectedMap != null &&
                           SelectedMap.thumbnail != null && !leavingScene &&
                           (visibleState == GameState.Day || visibleState == GameState.Night) &&
                           !NotebookIsOpen && !TutorialIsOpen && !VillagePanelOpen && !IsPlayingTask &&
                           !(worldCamera != null && worldCamera.overview) &&
                           !Thuong.Emotes.PlayerEmoteWheelUI.IsOpenFor(this);
            if (!canShow)
            {
                if (minimapPanel != null) minimapPanel.gameObject.SetActive(false);
                return;
            }

            EnsureMinimapUi();
            if (minimapPanel == null) return;

            minimapPanel.gameObject.SetActive(true);
            minimapMapButton.interactable = visibleState == GameState.Day;
            if (minimapDefinition != SelectedMap) BindMinimapMap(SelectedMap);
            RefreshMinimapPlayers();
            RefreshMinimapTasks();
        }

        private void EnsureMinimapUi()
        {
            if (minimapPanel != null || sceneHud == null) return;

            var root = new GameObject("Online minimap", typeof(RectTransform), typeof(Image));
            minimapPanel = root.GetComponent<RectTransform>();
            minimapPanel.SetParent(sceneHud.transform, false);
            minimapPanel.anchorMin = new Vector2(.82f, .52f);
            minimapPanel.anchorMax = new Vector2(.985f, .795f);
            minimapPanel.offsetMin = minimapPanel.offsetMax = Vector2.zero;
            Image panelImage = root.GetComponent<Image>();
            panelImage.color = MinimapPanelColor;
            panelImage.raycastTarget = false;
            ThuongMedievalTheme.Active?.StylePanel(panelImage, "Online minimap panel");

            var header = new GameObject("Minimap header", typeof(RectTransform), typeof(Image), typeof(Button));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            minimapHeader = headerRect;
            headerRect.SetParent(minimapPanel, false);
            headerRect.anchorMin = new Vector2(.035f, .855f);
            headerRect.anchorMax = new Vector2(.965f, .98f);
            headerRect.offsetMin = headerRect.offsetMax = Vector2.zero;
            minimapHeaderButton = header.GetComponent<Button>();
            minimapHeaderButton.targetGraphic = header.GetComponent<Image>();
            minimapHeaderButton.onClick.AddListener(ToggleMinimapExpanded);
            ThuongMedievalTheme.Active?.StylePanel(header.GetComponent<Image>(), "Minimap header");

            minimapMapName = CreateMinimapText("Map name", headerRect, "BẢN ĐỒ", 14,
                new Vector2(0.04f, 0.04f), new Vector2(0.74f, 0.96f), TextAlignmentOptions.MidlineLeft);
            minimapToggleLabel = CreateMinimapText("Minimap toggle label", headerRect, "[-]", 14,
                new Vector2(0.76f, 0.04f), new Vector2(0.97f, 0.96f), TextAlignmentOptions.Center);

            var viewport = new GameObject("Minimap viewport", typeof(RectTransform), typeof(Image));
            minimapViewport = viewport.GetComponent<RectTransform>();
            minimapViewport.SetParent(minimapPanel, false);
            minimapViewport.anchorMin = new Vector2(0.04f, 0.275f);
            minimapViewport.anchorMax = new Vector2(0.96f, 0.845f);
            minimapViewport.offsetMin = minimapViewport.offsetMax = Vector2.zero;
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0.035f, 0.045f, 0.035f, 1f);
            minimapMapButton = viewport.AddComponent<Button>();
            minimapMapButton.targetGraphic = viewportImage;
            minimapMapButton.transition = Selectable.Transition.None;
            minimapMapButton.onClick.AddListener(() =>
            {
                if (worldCamera != null && visibleState == GameState.Day) worldCamera.overview = true;
            });

            var mapImageObject = new GameObject("Map image", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            minimapMapRect = mapImageObject.GetComponent<RectTransform>();
            minimapMapRect.SetParent(minimapViewport, false);
            minimapMapRect.anchorMin = Vector2.zero;
            minimapMapRect.anchorMax = Vector2.one;
            minimapMapRect.offsetMin = minimapMapRect.offsetMax = Vector2.zero;
            minimapMapImage = mapImageObject.GetComponent<Image>();
            minimapMapImage.raycastTarget = false;
            minimapAspect = mapImageObject.GetComponent<AspectRatioFitter>();
            minimapAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

            CreateMinimapDotSprite();
            CreateMinimapArrowSprite();
            CreateMinimapLegend();
            SetMinimapExpanded(true);
        }

        private TMP_Text CreateMinimapText(string name, Transform parent, string value, int size,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.font = sceneHud != null && sceneHud.phaseText != null ? sceneHud.phaseText.font : null;
            label.text = value;
            label.fontSize = size;
            label.enableAutoSizing = true;
            label.fontSizeMin = Mathf.Max(10, size - 3);
            label.fontSizeMax = size;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            ThuongMedievalTheme.Active?.StyleText(label);
            return label;
        }

        private void CreateMinimapLegend()
        {
            CreateMinimapLegendEntry("You legend", 0.035f, 0.30f, "BẠN", MinimapLocalColor);
            CreateMinimapLegendEntry("Players legend", 0.365f, 0.67f, "NGƯỜI", MinimapPlayerColor);
            CreateMinimapLegendEntry("Tasks legend", 0.715f, 0.97f, "TRẠM", MinimapTaskColor);
        }

        private void CreateMinimapLegendEntry(string name, float left, float right, string value, Color color)
        {
            var entry = new GameObject(name, typeof(RectTransform));
            RectTransform entryRect = entry.GetComponent<RectTransform>();
            entryRect.SetParent(minimapPanel, false);
            entryRect.anchorMin = new Vector2(left, 0.17f);
            entryRect.anchorMax = new Vector2(right, 0.255f);
            entryRect.offsetMin = entryRect.offsetMax = Vector2.zero;

            var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            RectTransform dotRect = dot.GetComponent<RectTransform>();
            dotRect.SetParent(entryRect, false);
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.02f, 0.5f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            dotRect.anchoredPosition = Vector2.zero;
            dotRect.sizeDelta = new Vector2(9f, 9f);
            Image dotImage = dot.GetComponent<Image>();
            dotImage.sprite = minimapDotSprite;
            dotImage.color = color;
            dotImage.raycastTarget = false;

            CreateMinimapText("Label", entryRect, value, 11,
                new Vector2(0.19f, 0f), Vector2.one, TextAlignmentOptions.MidlineLeft);
        }

        private void CreateMinimapDotSprite()
        {
            if (minimapDotSprite != null) return;
            const int size = 24;
            minimapDotTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Online minimap dot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.46f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            minimapDotTexture.SetPixels(pixels);
            minimapDotTexture.Apply(false, true);
            minimapDotSprite = Sprite.Create(minimapDotTexture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            minimapDotSprite.name = "Online minimap dot";
        }

        private void CreateMinimapArrowSprite()
        {
            if (minimapArrowSprite != null) return;
            const int size = 24;
            minimapArrowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Online minimap direction arrow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 2; y < size - 2; y++)
            {
                float progress = (y - 2f) / (size - 5f);
                float halfWidth = Mathf.Lerp(7f, 0.8f, progress);
                for (int x = 0; x < size; x++)
                {
                    float alpha = Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(x - center));
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            minimapArrowTexture.SetPixels(pixels);
            minimapArrowTexture.Apply(false, true);
            minimapArrowSprite = Sprite.Create(minimapArrowTexture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            minimapArrowSprite.name = "Online minimap direction arrow";
        }

        private void BindMinimapMap(ThuongMapDefinition definition)
        {
            minimapDefinition = definition;
            if (minimapMapName != null)
                minimapMapName.text = string.IsNullOrWhiteSpace(definition.displayName) ? "BẢN ĐỒ" : definition.displayName;
            if (minimapMapImage != null) minimapMapImage.sprite = definition.thumbnail;
            if (minimapAspect != null)
            {
                float width = Mathf.Max(0.01f, definition.boundsMax.x - definition.boundsMin.x);
                float height = Mathf.Max(0.01f, definition.boundsMax.y - definition.boundsMin.y);
                minimapAspect.aspectRatio = definition.thumbnail != null
                    ? definition.thumbnail.rect.width / definition.thumbnail.rect.height : width / height;
            }

            foreach (Image marker in minimapStationMarkers.Values)
                if (marker != null) Destroy(marker.gameObject);
            minimapStationMarkers.Clear();
            foreach (ThuongMapStation station in definition.stations.OrderBy(item => item.maskSlot))
            {
                Image marker = CreateMinimapMarker("Task " + station.stationId, station.position, 9f);
                minimapStationMarkers[station.maskSlot] = marker;
            }

            foreach (MinimapPlayerMarker marker in minimapPlayerMarkers.Values)
                if (marker.Image != null) Destroy(marker.Image.gameObject);
            minimapPlayerMarkers.Clear();
        }

        private Image CreateMinimapMarker(string name, Vector2 position, float size)
        {
            var markerObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.SetParent(minimapMapRect, false);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.sizeDelta = new Vector2(size, size);
            Image marker = markerObject.GetComponent<Image>();
            marker.sprite = minimapDotSprite;
            marker.raycastTarget = false;
            SetMinimapMarkerPosition(markerRect, position, minimapDefinition);
            return marker;
        }

        private void RefreshMinimapTasks()
        {
            if (minimapDefinition == null) return;
            foreach (ThuongMapStation station in minimapDefinition.stations)
            {
                if (!minimapStationMarkers.TryGetValue(station.maskSlot, out Image marker) || marker == null) continue;
                ushort bit = (ushort)(1 << station.maskSlot);
                bool active = visibleState == GameState.Day && (activeTaskMask & bit) != 0;
                marker.gameObject.SetActive(active);
                bool completed = ((completedTaskMask | personalTaskMask) & bit) != 0;
                marker.color = completed ? MinimapDoneColor : active ? MinimapTaskColor : MinimapInactiveTaskColor;
                marker.rectTransform.sizeDelta = active && !completed ? new Vector2(11f, 11f) : new Vector2(8f, 8f);
            }
        }

        private void RefreshMinimapPlayers()
        {
            var visibleIds = new HashSet<int>();
            foreach (ThuongOnlinePublicPlayer player in publicPlayers)
            {
                // Hidden positions are deliberately zeroed in snapshots; never plot them.
                if (player.Hidden || !player.Alive || (visibleState == GameState.Night && player.PlayerId != localPlayerId) ||
                    float.IsNaN(player.Position.x) || float.IsInfinity(player.Position.x) ||
                    float.IsNaN(player.Position.y) || float.IsInfinity(player.Position.y) ||
                    player.Position.x < minimapDefinition.boundsMin.x || player.Position.x > minimapDefinition.boundsMax.x ||
                    player.Position.y < minimapDefinition.boundsMin.y || player.Position.y > minimapDefinition.boundsMax.y) continue;

                Vector2 position = player.Position;
                if (avatars.TryGetValue(player.PlayerId, out ThuongOnlineAvatar avatar) && avatar.Root != null)
                    position = avatar.Root.transform.position;
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y))
                    continue;

                visibleIds.Add(player.PlayerId);
                if (!minimapPlayerMarkers.TryGetValue(player.PlayerId, out MinimapPlayerMarker marker) || marker.Image == null)
                {
                    Image image = CreateMinimapMarker("Player " + player.PlayerId,
                        position, player.PlayerId == localPlayerId ? 14f : 11f);
                    marker = new MinimapPlayerMarker { Image = image, Rect = image.rectTransform };
                    minimapPlayerMarkers[player.PlayerId] = marker;
                }

                bool isLocal = player.PlayerId == localPlayerId;
                marker.Image.sprite = isLocal ? minimapArrowSprite : minimapDotSprite;
                marker.Image.color = isLocal ? MinimapLocalColor : MinimapPlayerColor;
                marker.Rect.sizeDelta = isLocal ? new Vector2(14f, 14f) : new Vector2(11f, 11f);
                marker.Rect.localRotation = isLocal
                    ? Quaternion.Euler(0f, 0f, MinimapHeading(player.PlayerId))
                    : Quaternion.identity;
                SetMinimapMarkerPosition(marker.Rect, position, minimapDefinition);
                marker.Image.transform.SetAsLastSibling();
            }

            foreach (int staleId in minimapPlayerMarkers.Keys.Where(id => !visibleIds.Contains(id)).ToArray())
            {
                MinimapPlayerMarker marker = minimapPlayerMarkers[staleId];
                if (marker.Image != null) Destroy(marker.Image.gameObject);
                minimapPlayerMarkers.Remove(staleId);
            }
        }

        private static void SetMinimapMarkerPosition(RectTransform marker, Vector2 position, ThuongMapDefinition definition)
        {
            if (marker == null || definition == null) return;
            Rect area = MinimapWorldRect(definition);
            float x = Mathf.InverseLerp(area.xMin, area.xMax, position.x);
            float y = Mathf.InverseLerp(area.yMin, area.yMax, position.y);
            Vector2 normalized = new(x, y);
            marker.anchorMin = marker.anchorMax = normalized;
            marker.anchoredPosition = Vector2.zero;
        }

        // Include the margins baked into the original, large and expanded thumbnails.
        private static Rect MinimapWorldRect(ThuongMapDefinition definition)
        {
            Vector2 size = definition.boundsMax - definition.boundsMin;
            float aspect = definition.thumbnail.rect.width / definition.thumbnail.rect.height;
            bool expanded = definition.thumbnail.rect.width >= 640;
            bool original = definition.boundsMin == new Vector2(-13, -11) && definition.boundsMax == new Vector2(15, 13);
            float halfHeight = expanded ? Mathf.Max(size.y / 2 + 1.5f, size.x / (2 * aspect) + 1.5f)
                : original ? 13 : Mathf.Max(size.y / 2, size.x / (2 * aspect)) + 1;
            Vector2 extent = new Vector2(halfHeight * aspect, halfHeight);
            return new Rect((definition.boundsMin + definition.boundsMax) / 2 - extent, extent * 2);
        }

        private float MinimapHeading(int playerId)
        {
            if (avatars.TryGetValue(playerId, out ThuongOnlineAvatar avatar) && avatar.Animator != null)
            {
                Vector2 direction = new(avatar.Animator.GetFloat("LastInputX"), avatar.Animator.GetFloat("LastInputY"));
                if (direction.sqrMagnitude > 0.01f) return Vector2.SignedAngle(Vector2.up, direction);
            }
            return 0f;
        }

        private void ToggleMinimapExpanded() => SetMinimapExpanded(!minimapExpanded);

        private void SetMinimapExpanded(bool expanded)
        {
            minimapExpanded = expanded;
            if (minimapPanel != null)
            {
                minimapPanel.anchorMin = new Vector2(.82f, expanded ? .52f : .745f);
                minimapPanel.anchorMax = new Vector2(.985f, .795f);
                minimapPanel.offsetMin = minimapPanel.offsetMax = Vector2.zero;
            }
            if (minimapHeader != null)
            {
                minimapHeader.anchorMin = new Vector2(.035f, expanded ? .855f : .1f);
                minimapHeader.anchorMax = new Vector2(.965f, expanded ? .98f : .95f);
                minimapHeader.offsetMin = minimapHeader.offsetMax = Vector2.zero;
            }
            if (minimapViewport != null) minimapViewport.gameObject.SetActive(expanded);
            if (minimapToggleLabel != null) minimapToggleLabel.text = expanded ? "[-]" : "[+]";
            if (minimapPanel != null)
                foreach (Transform child in minimapPanel)
                    if (child.name.EndsWith(" legend")) child.gameObject.SetActive(expanded);
        }

        private void ResetMinimap()
        {
            if (minimapPanel != null) minimapPanel.gameObject.SetActive(false);
            minimapDefinition = null;
            foreach (Image marker in minimapStationMarkers.Values)
                if (marker != null) Destroy(marker.gameObject);
            minimapStationMarkers.Clear();
            foreach (MinimapPlayerMarker marker in minimapPlayerMarkers.Values)
                if (marker.Image != null) Destroy(marker.Image.gameObject);
            minimapPlayerMarkers.Clear();
        }

        private void DisposeMinimap()
        {
            ResetMinimap();
            if (minimapPanel != null) Destroy(minimapPanel.gameObject);
            minimapPanel = null;
            if (minimapDotSprite != null) Destroy(minimapDotSprite);
            if (minimapDotTexture != null) Destroy(minimapDotTexture);
            if (minimapArrowSprite != null) Destroy(minimapArrowSprite);
            if (minimapArrowTexture != null) Destroy(minimapArrowTexture);
            minimapDotSprite = null;
            minimapDotTexture = null;
            minimapArrowSprite = null;
            minimapArrowTexture = null;
        }
    }
}
