using Thuong.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private RectTransform minimapRoot;
        private Image minimapImage, minimapPlayer;
        private TMP_Text minimapTitle;
        private Button minimapButton;
        private AspectRatioFitter minimapAspect;
        private readonly Image[] minimapTasks = new Image[16];

        private void RefreshMinimap()
        {
            bool visible = connected && matchStarted && matchResult == null && localMapReady &&
                SelectedMap != null && SelectedMap.thumbnail != null && LocalPlayerAlive &&
                (visibleState == GameState.Day || visibleState == GameState.Night) &&
                !NotebookIsOpen && !TutorialIsOpen && !VillagePanelOpen && !IsPlayingTask &&
                !(worldCamera != null && worldCamera.overview) &&
                !Thuong.Emotes.PlayerEmoteWheelUI.IsOpenFor(this) && !leavingScene;
            if (minimapRoot == null && visible) BuildMinimap();
            if (minimapRoot == null) return;
            minimapRoot.gameObject.SetActive(visible);
            if (!visible) return;

            var map = SelectedMap;
            minimapImage.sprite = map.thumbnail;
            minimapAspect.aspectRatio = map.thumbnail.rect.width / map.thumbnail.rect.height;
            minimapTitle.text = map.displayName;
            minimapButton.interactable = visibleState == GameState.Day;
            bool hasAvatar = avatars.TryGetValue(localPlayerId, out var avatar) && avatar.Root != null;
            minimapPlayer.gameObject.SetActive(hasAvatar);
            if (hasAvatar) PlaceMinimapMarker(minimapPlayer, map, avatar.Root.transform.position);
            for (int slot = 0; slot < minimapTasks.Length; slot++)
            {
                bool active = visibleState == GameState.Day && slot < stations.Length &&
                    (activeTaskMask & (1 << slot)) != 0;
                var marker = minimapTasks[slot];
                marker.gameObject.SetActive(active);
                if (!active) continue;
                PlaceMinimapMarker(marker, map, stations[slot].transform.position);
                bool done = (completedTaskMask & (1 << slot)) != 0 || (personalTaskMask & (1 << slot)) != 0;
                marker.color = done ? new Color32(114, 183, 111, 255) : new Color32(255, 207, 65, 255);
            }
            minimapPlayer.transform.SetAsLastSibling();
        }

        private void BuildMinimap()
        {
            minimapRoot = LobbyPanel("Gameplay minimap", sceneHud.transform,
                new Vector2(.82f, .52f), new Vector2(.985f, .795f));
            minimapTitle = LobbyText("Minimap title", minimapRoot, "BẢN ĐỒ",
                new Vector2(.035f, .855f), new Vector2(.965f, .98f), 17, TextAlignmentOptions.Center);
            minimapTitle.richText = false;
            var viewport = LobbyPanel("Minimap viewport", minimapRoot,
                new Vector2(.04f, .275f), new Vector2(.96f, .845f), false);
            viewport.GetComponent<Image>().color = new Color32(26, 33, 25, 255);
            var imageRoot = new GameObject("Minimap image", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            imageRoot.transform.SetParent(viewport, false);
            minimapImage = imageRoot.GetComponent<Image>(); minimapImage.raycastTarget = false;
            minimapAspect = imageRoot.GetComponent<AspectRatioFitter>();
            minimapAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            for (int slot = 0; slot < minimapTasks.Length; slot++)
                minimapTasks[slot] = MinimapMarker("Minimap task " + slot, 7, new Color32(255, 207, 65, 255));
            minimapPlayer = MinimapMarker("Minimap local player", 11, new Color32(71, 233, 246, 255));
            var legend = LobbyText("Minimap legend", minimapRoot, "XANH: BẠN · VÀNG: NHIỆM VỤ",
                new Vector2(.035f, .17f), new Vector2(.965f, .255f), 11, TextAlignmentOptions.Center);
            legend.fontSizeMin = 9; legend.fontSizeMax = 11;
            minimapButton = minimapRoot.gameObject.AddComponent<Button>();
            minimapButton.targetGraphic = minimapRoot.GetComponent<Image>();
            minimapButton.transition = Selectable.Transition.None;
            minimapButton.onClick.AddListener(() =>
            {
                if (worldCamera != null && visibleState == GameState.Day) worldCamera.overview = true;
            });
        }

        private Image MinimapMarker(string name, float size, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            var marker = root.GetComponent<Image>(); marker.transform.SetParent(minimapImage.transform, false);
            marker.rectTransform.sizeDelta = Vector2.one * size;
            marker.color = color; marker.raycastTarget = false;
            var outline = root.GetComponent<Outline>();
            outline.effectColor = new Color32(18, 27, 24, 255); outline.effectDistance = Vector2.one;
            return marker;
        }

        private static void PlaceMinimapMarker(Image marker, ThuongMapDefinition map, Vector2 position)
        {
            Rect area = MinimapWorldRect(map);
            Vector2 anchor = new Vector2(Mathf.InverseLerp(area.xMin, area.xMax, position.x),
                Mathf.InverseLerp(area.yMin, area.yMax, position.y));
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = anchor;
            marker.rectTransform.anchoredPosition = Vector2.zero;
        }

        // Match the three thumbnail capture layouts used by the map installers,
        // including their outer margins. Normalizing only the playable bounds
        // would shift markers away from the buildings shown in the thumbnail.
        private static Rect MinimapWorldRect(ThuongMapDefinition map)
        {
            Vector2 size = map.boundsMax - map.boundsMin;
            float aspect = map.thumbnail.rect.width / map.thumbnail.rect.height;
            bool expanded = map.thumbnail.rect.width >= 640;
            bool original = map.boundsMin == new Vector2(-13, -11) && map.boundsMax == new Vector2(15, 13);
            float halfHeight = expanded ? Mathf.Max(size.y / 2 + 1.5f, size.x / (2 * aspect) + 1.5f)
                : original ? 13 : Mathf.Max(size.y / 2, size.x / (2 * aspect)) + 1;
            Vector2 extent = new Vector2(halfHeight * aspect, halfHeight);
            return new Rect((map.boundsMin + map.boundsMax) / 2 - extent, extent * 2);
        }
    }
}
