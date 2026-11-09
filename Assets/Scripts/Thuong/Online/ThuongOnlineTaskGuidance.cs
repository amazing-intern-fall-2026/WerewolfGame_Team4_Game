using System;
using System.Collections.Generic;
using System.Linq;
using Thuong.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private Canvas taskGuideCanvas;
        private GameObject taskMapPanel, taskPromptRoot;
        private RectTransform taskMapRect, taskMapLegend;
        private TMP_Text taskPrompt, taskGuideTitle, taskGuideHint;
        private ThuongTaskMapGraphic taskMapGraphic;
        private readonly List<Button> taskMapButtons = new();
        private readonly List<Button> taskMapPins = new();
        private readonly List<TMP_Text> taskZoneLabels = new();
        private readonly List<Vector2> guidePath = new();
        private ThuongMapDefinition guideMap;
        private ThuongMapNavigation guideNavigation;
        private int selectedGuideStation = -1;
        private float nextGuidePathAt, nextGuideRefresh;
        private Vector2 guideOrigin;
        private bool guideRouteFound;

        private bool CanContributeLocally(int slot) => slot >= 0 && slot < stations.Length && slot < 16 &&
            (activeTaskMask & (1 << slot)) != 0 && (completedTaskMask & (1 << slot)) == 0 &&
            (personalTaskMask & (1 << slot)) == 0;

        private bool StationHasRoom(int slot) => slot >= 0 && slot < 16 &&
            taskDone[slot] < taskTargets[slot] && taskOccupancy[slot] < Mathf.Min(taskCapacity[slot], taskTargets[slot] - taskDone[slot]);

        private void PrepareTaskGuidance()
        {
            var root = new GameObject("Task Guidance", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); taskGuideCanvas = root.GetComponent<Canvas>();
            taskGuideCanvas.renderMode = RenderMode.ScreenSpaceOverlay; taskGuideCanvas.sortingOrder = 40;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            // Keep task guidance above the footer HUD. The panel is only a text container;
            // Its default Image is white before the medieval theme initializes, so keep
            // the text container fully transparent in every initialization order.
            var prompt = LobbyPanel("Nearby task prompt", root.transform, new Vector2(.30f, .265f), new Vector2(.72f, .355f));
            var promptImage = prompt.GetComponent<Image>();
            promptImage.color = new Color(1, 1, 1, 0);
            promptImage.raycastTarget = false;
            taskPromptRoot = prompt.gameObject;
            taskPrompt = LobbyText("Interaction", prompt, "", new Vector2(.03f, .48f), new Vector2(.97f, .95f), 21, TextAlignmentOptions.Center);
            taskGuideHint = LobbyText("Route hint", prompt, "", new Vector2(.025f, .03f), new Vector2(.975f, .49f), 16, TextAlignmentOptions.Center);
            var shade = LobbyPanel("Task map shade", root.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, .78f); taskMapPanel = shade.gameObject;
            var card = LobbyPanel("Task map", shade, new Vector2(.13f, .08f), new Vector2(.94f, .92f));
            taskGuideTitle = LobbyText("Map title", card, "", new Vector2(.04f, .91f), new Vector2(.80f, .98f), 26);
            LobbyButton("Close task map", card, "ĐÓNG / TAB", new Vector2(.82f, .91f), new Vector2(.97f, .98f),
                () => { if (worldCamera != null) worldCamera.overview = false; });
            LobbyText("Map instructions", card, "Chọn nhiệm vụ để dẫn đường · Ô xanh là bạn · Mỗi loại đóng góp một lần/ngày",
                new Vector2(.03f, .025f), new Vector2(.97f, .075f), 17, TextAlignmentOptions.Center);
            taskMapRect = new GameObject("Schematic", typeof(RectTransform)).GetComponent<RectTransform>();
            taskMapRect.SetParent(card, false);
            taskMapGraphic = taskMapRect.gameObject.AddComponent<ThuongTaskMapGraphic>(); taskMapGraphic.raycastTarget = false;
            taskMapLegend = new GameObject("Task legend", typeof(RectTransform)).GetComponent<RectTransform>();
            taskMapLegend.SetParent(card, false); taskMapLegend.anchorMin = new Vector2(.72f, .11f); taskMapLegend.anchorMax = new Vector2(.97f, .88f);
            taskMapLegend.offsetMin = taskMapLegend.offsetMax = Vector2.zero;
            taskMapPanel.SetActive(false); taskPromptRoot.SetActive(false); taskGuideCanvas.enabled = false;
        }

        private void RefreshTaskGuidance()
        {
            if (taskGuideCanvas == null) return;
            bool alive = publicPlayers.Any(p => p.PlayerId == localPlayerId && p.Alive);
            bool visible = connected && matchStarted && matchResult == null && localMapReady && alive &&
                visibleState == GameState.Day && !IsPlayingTask && !Thuong.Emotes.PlayerEmoteWheelUI.IsOpenFor(this);
            taskGuideCanvas.enabled = visible;
            if (!visible) return;
            bool overview = worldCamera != null && worldCamera.overview;
            taskMapPanel.SetActive(overview); taskPromptRoot.SetActive(!overview);
            if (Time.unscaledTime < nextGuideRefresh) return;
            nextGuideRefresh = Time.unscaledTime + .15f;
            if (!avatars.TryGetValue(localPlayerId, out var avatar) || avatar.Root == null) return;
            Vector2 position = avatar.Root.transform.position;
            if (guideMap != SelectedMap)
            {
                guideMap = SelectedMap; guideNavigation = null; guidePath.Clear(); selectedGuideStation = -1;
                RebuildTaskMap();
            }
            if (guideMap == null) return;
            if (taskBoardDay != visibleDay)
            { taskPrompt.text = "Đang nhận nhiệm vụ hôm nay…"; taskGuideHint.text = "Tab · Bản đồ nhiệm vụ"; return; }
            if (!CanContributeLocally(selectedGuideStation)) SelectSuggestedTask(position);
            if (selectedGuideStation >= 0 && Time.unscaledTime >= nextGuidePathAt)
            {
                nextGuidePathAt = Time.unscaledTime + 1;
                if (guideNavigation == null) guideNavigation = new ThuongMapNavigation(guideMap);
                if (guidePath.Count == 0 || (position - guideOrigin).sqrMagnitude > .5f)
                {
                    guideOrigin = position;
                    guideRouteFound = guideNavigation.TryPath(position, selectedGuideStation, out var path);
                    guidePath.Clear(); guidePath.AddRange(path);
                }
            }
            int nearby = FindNearbyTask(position, false);
            if (nearby >= 0)
            {
                string title = stations[nearby].task.taskName;
                var entry = minigameCatalog?.Find(stations[nearby].task);
                if (!CanContributeLocally(nearby)) taskPrompt.text = $"✓ {title} · Bạn đã xong / nhóm đã đủ";
                else if (!ThuongMapCollision.HasLine(guideMap, position, stations[nearby].transform.position)) taskPrompt.text = $"{title} · Địa hình đang chắn trạm";
                else if (!StationHasRoom(nearby)) taskPrompt.text = $"{title} · Trạm đang đủ người ({taskOccupancy[nearby]}/{taskCapacity[nearby]})";
                else if (entry != null && visibleRemaining < entry.rules.minimumDayRemaining) taskPrompt.text = $"{title} · Không đủ thời gian bắt đầu";
                else taskPrompt.text = $"E · {title}   |   {taskOccupancy[nearby]}/{taskCapacity[nearby]} đang làm";
            }
            else taskPrompt.text = selectedGuideStation < 0 ? "Bạn đã hoàn thành phần việc hôm nay" : "Tiến tới trạm nhiệm vụ được đánh dấu";
            taskGuideHint.text = selectedGuideStation < 0 ? "Tab · Xem tiến độ của nhóm" :
                $"Tab · Dẫn đường tới {stations[selectedGuideStation].task.taskName} · {ZoneName(position)}" + (guideRouteFound ? "" : " · Chưa tìm được đường");
            taskGuideTitle.text = $"{SelectedMap.displayName}  ·  {ZoneName(position)}";
            taskMapGraphic.Map = guideMap; taskMapGraphic.Player = position;
            taskMapGraphic.Route.Clear(); taskMapGraphic.Route.AddRange(guidePath); taskMapGraphic.SetVerticesDirty();
            for (int slot = 0; slot < taskMapButtons.Count; slot++)
            {
                var button = taskMapButtons[slot]; bool active = (activeTaskMask & (1 << slot)) != 0;
                button.gameObject.SetActive(active);
                taskMapPins[slot].gameObject.SetActive(active);
                if (!active) continue;
                bool mine = (personalTaskMask & (1 << slot)) != 0;
                string state = mine ? "✓ Bạn đã góp" : taskDone[slot] >= taskTargets[slot] ? "✓ Nhóm đã đủ"
                    : $"{taskOccupancy[slot]}/{taskCapacity[slot]} đang làm";
                button.GetComponentInChildren<TMP_Text>().text = $"{(slot == selectedGuideStation ? "> " : "")}{slot + 1}. {stations[slot].task.taskName}\n{taskDone[slot]}/{taskTargets[slot]} lượt · {state}";
                button.interactable = CanContributeLocally(slot);
                button.GetComponent<Image>().color = slot == selectedGuideStation ? new Color32(248, 215, 123, 255) : new Color32(248, 235, 196, 245);
                taskMapPins[slot].interactable = button.interactable;
                taskMapPins[slot].GetComponent<Image>().color = slot == selectedGuideStation ? new Color32(255, 208, 65, 255) :
                    mine || taskDone[slot] >= taskTargets[slot] ? new Color32(115, 148, 108, 255) : new Color32(250, 239, 192, 255);
            }
        }

        private void SelectSuggestedTask(Vector2 position)
        {
            selectedGuideStation = -1; float best = float.MaxValue;
            for (int slot = 0; slot < stations.Length && slot < 16; slot++)
            {
                if (!CanContributeLocally(slot)) continue;
                float score = Vector2.Distance(position, stations[slot].transform.position) + (StationHasRoom(slot) ? 0 : 10000);
                if (score >= best) continue;
                best = score; selectedGuideStation = slot;
            }
            guidePath.Clear(); nextGuidePathAt = 0;
        }

        private int FindNearbyTask(Vector2 position, bool availableOnly)
        {
            int best = -1; float distance = float.MaxValue;
            for (int slot = 0; slot < stations.Length && slot < 16; slot++)
            {
                if ((activeTaskMask & (1 << slot)) == 0 || availableOnly && !CanContributeLocally(slot)) continue;
                float value = Vector2.Distance(position, stations[slot].transform.position);
                if (value > stations[slot].interactionRadius + .25f || value >= distance) continue;
                best = slot; distance = value;
            }
            return best;
        }

        private string ZoneName(Vector2 point)
        {
            var zone = guideMap?.zones?.FirstOrDefault(z => z != null && z.area.Contains(point));
            if (zone != null && !string.IsNullOrWhiteSpace(zone.displayName)) return zone.displayName;
            if (guideMap == null) return "";
            Vector2 normalized = taskMapGraphic.Project(point);
            string[] names = { "Tây Nam", "Phía Nam", "Đông Nam", "Phía Tây", "Trung tâm", "Phía Đông", "Tây Bắc", "Phía Bắc", "Đông Bắc" };
            return names[Mathf.Clamp((int)(normalized.y * 3), 0, 2) * 3 + Mathf.Clamp((int)(normalized.x * 3), 0, 2)];
        }

        private void RebuildTaskMap()
        {
            foreach (var button in taskMapButtons) if (button != null) Destroy(button.gameObject);
            foreach (var button in taskMapPins) if (button != null) Destroy(button.gameObject);
            foreach (var label in taskZoneLabels) if (label != null) Destroy(label.gameObject);
            taskMapButtons.Clear(); taskMapPins.Clear(); taskZoneLabels.Clear();
            if (guideMap == null) return;
            taskMapGraphic.Map = guideMap;
            // Preserve world proportions regardless of the display aspect ratio.
            Canvas.ForceUpdateCanvases();
            Vector2 available = taskMapRect.parent.GetComponent<RectTransform>().rect.size * new Vector2(.64f, .78f);
            float aspect = (guideMap.boundsMax.x - guideMap.boundsMin.x) / (guideMap.boundsMax.y - guideMap.boundsMin.y);
            float width = Mathf.Min(available.x, available.y * aspect), height = width / aspect;
            taskMapRect.anchorMin = taskMapRect.anchorMax = new Vector2(.365f, .49f); taskMapRect.sizeDelta = new Vector2(width, height);
            for (int i = 0; i < 9; i++)
            {
                Vector2 anchor = new Vector2((i % 3 + .5f) / 3, (i / 3 + .5f) / 3);
                Vector2 world = guideMap.boundsMin + Vector2.Scale(anchor, guideMap.boundsMax - guideMap.boundsMin);
                taskZoneLabels.Add(LobbyText("Zone " + i, taskMapRect, ZoneName(world), anchor - new Vector2(.13f, .03f), anchor + new Vector2(.13f, .03f), 14, TextAlignmentOptions.Center));
            }
            for (int slot = 0; slot < stations.Length && slot < 16; slot++)
            {
                int index = slot; Vector2 anchor = taskMapGraphic.Project(stations[slot].transform.position);
                var pin = LobbyButton("Task pin " + slot, taskMapRect, (slot + 1).ToString(), anchor, anchor,
                    () => { selectedGuideStation = index; guidePath.Clear(); nextGuidePathAt = 0; });
                pin.GetComponent<RectTransform>().sizeDelta = new Vector2(34, 34); taskMapPins.Add(pin);
                float y = .86f - slot * .16f;
                var button = LobbyButton("Task marker " + slot, taskMapLegend, "", new Vector2(0, y), new Vector2(1, y + .135f),
                    () => { selectedGuideStation = index; guidePath.Clear(); nextGuidePathAt = 0; });
                var label = button.GetComponentInChildren<TMP_Text>(); label.fontSizeMax = 17; label.fontSizeMin = 12;
                label.textWrappingMode = TextWrappingModes.Normal;
                var icon = new GameObject("Task icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.transform.SetParent(button.transform, false);
                icon.rectTransform.anchorMin = new Vector2(.02f, .18f); icon.rectTransform.anchorMax = new Vector2(.18f, .82f);
                icon.rectTransform.offsetMin = icon.rectTransform.offsetMax = Vector2.zero;
                icon.sprite = guideMap.stations.FirstOrDefault(s => s.maskSlot == slot)?.sprite;
                icon.preserveAspect = true; icon.raycastTarget = false;
                label.rectTransform.anchorMin = new Vector2(.19f, .025f);
                taskMapButtons.Add(button);
            }
        }

        private void DisposeTaskGuidance()
        { if (taskGuideCanvas != null) Destroy(taskGuideCanvas.gameObject); taskGuideCanvas = null; }
    }
}
