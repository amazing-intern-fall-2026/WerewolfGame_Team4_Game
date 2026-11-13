using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private Canvas villageCanvas;
        private GameObject villagePanel, villageWorld;
        private Image blackoutShade;
        private TMP_Text villageStatus, villageHelp, villagePocket, sabotageTargetText;
        private Button villageToggle, bellButton, pairAButton, pairBButton, repairButton, sabotageButton;
        private readonly Button[] itemButtons = new Button[2];
        private GameObject sabotageControls, sabotageMarker, barricadeMarker;
        private Sprite villageMarkerSprite;
        private ThuongSabotageKind selectedSabotageKind = ThuongSabotageKind.Blackout;
        private int selectedSabotageTarget;
        private bool VillagePanelOpen => villagePanel != null && villagePanel.activeSelf;

        private void EnsureVillageUi()
        {
            if (villageCanvas != null || sceneHud == null) return;
            var root = new GameObject("Village actions", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false); villageCanvas = root.GetComponent<Canvas>();
            villageCanvas.renderMode = RenderMode.ScreenSpaceOverlay; villageCanvas.sortingOrder = 25;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            blackoutShade = LobbyPanel("Blackout shade", root.transform, Vector2.zero, Vector2.one, false).GetComponent<Image>();
            blackoutShade.raycastTarget = false;
            villageStatus = LobbyText("Village status", root.transform, "", new Vector2(.28f, .16f), new Vector2(.75f, .20f), 18, TextAlignmentOptions.Center);
            villageStatus.raycastTarget = false; villageStatus.richText = false;
            villageStatus.enableAutoSizing = true; villageStatus.fontSizeMin = 12;
            villageToggle = MatchButton(sceneHud, "LÀNG / TÚI ĐỒ", root.transform,
                new Vector2(.78f, .15f), new Vector2(.98f, .205f), ToggleVillagePanel);
            var shade = LobbyPanel("Village modal shade", root.transform, Vector2.zero, Vector2.one, false);
            villagePanel = shade.gameObject; shade.GetComponent<Image>().color = new Color(0, 0, 0, .65f);
            var modalCanvas = shade.gameObject.AddComponent<Canvas>(); modalCanvas.overrideSorting = true; modalCanvas.sortingOrder = 1900;
            shade.gameObject.AddComponent<GraphicRaycaster>();
            var panel = LobbyPanel("Village action parchment", shade, new Vector2(.23f, .15f), new Vector2(.77f, .85f));
            LobbyText("Village title", panel, "CHUÔNG LÀNG VÀ VẬT PHẨM", new Vector2(.04f, .89f), new Vector2(.96f, .98f), 28, TextAlignmentOptions.Center);
            villageHelp = LobbyText("Village rules", panel, "", new Vector2(.05f, .72f), new Vector2(.95f, .89f), 18);
            villageHelp.richText = false; villageHelp.textWrappingMode = TextWrappingModes.Normal;
            bellButton = VillageButton(panel, "GỌI CHUÔNG", .05f, .265f, .62f, .71f, () => SendVillageUiAction(ThuongOnlineAction.EmergencyMeeting));
            pairAButton = VillageButton(panel, "BẢNG A", .285f, .50f, .62f, .71f, () => SendVillageUiAction(ThuongOnlineAction.PairConsole, 0));
            pairBButton = VillageButton(panel, "BẢNG B", .52f, .735f, .62f, .71f, () => SendVillageUiAction(ThuongOnlineAction.PairConsole, 1));
            repairButton = VillageButton(panel, "SỬA", .755f, .955f, .62f, .71f, () => SendVillageUiAction(ThuongOnlineAction.RepairSabotage));
            villagePocket = LobbyText("Inventory help", panel, "", new Vector2(.05f, .45f), new Vector2(.95f, .61f), 17);
            villagePocket.textWrappingMode = TextWrappingModes.Normal; villagePocket.richText = false;
            for (int slot = 0; slot < 2; slot++)
            {
                int captured = slot;
                itemButtons[slot] = VillageButton(panel, "Ô TRỐNG", .05f + slot * .47f, .48f + slot * .47f, .36f, .45f,
                    () => { if (captured < villageInventory.Count) SendVillageUiAction(ThuongOnlineAction.UseItem, (int)villageInventory[captured]); });
            }
            sabotageControls = new GameObject("Wolf sabotage controls", typeof(RectTransform));
            var sabotageRect = sabotageControls.GetComponent<RectTransform>(); sabotageRect.SetParent(panel, false);
            sabotageRect.anchorMin = new Vector2(.04f, .13f); sabotageRect.anchorMax = new Vector2(.96f, .35f);
            sabotageRect.offsetMin = sabotageRect.offsetMax = Vector2.zero;
            VillageButton(sabotageRect, "ĐỔI KIỂU", 0, .25f, .52f, .98f, () =>
            { selectedSabotageKind = (ThuongSabotageKind)((int)selectedSabotageKind % 3 + 1); selectedSabotageTarget = 0; });
            VillageButton(sabotageRect, "ĐỔI ĐIỂM", .27f, .52f, .52f, .98f, () =>
            { selectedSabotageTarget = (selectedSabotageTarget + 1) % Mathf.Max(1, SabotageTargetCount(selectedSabotageKind)); });
            sabotageButton = VillageButton(sabotageRect, "PHÁ HOẠI", .55f, 1, .52f, .98f, () =>
                SendVillageUiAction(ThuongOnlineAction.Sabotage, (int)selectedSabotageKind, selectedSabotageTarget));
            sabotageTargetText = LobbyText("Sabotage location", sabotageRect, "", Vector2.zero, new Vector2(1, .5f), 17);
            sabotageTargetText.richText = false;
            VillageButton(panel, "ĐÓNG", .35f, .65f, .025f, .115f, CloseVillagePanel);
            villagePanel.SetActive(false);
        }
        private Button VillageButton(Transform parent, string label, float left, float right, float bottom, float top, UnityEngine.Events.UnityAction action)
        {
            var button = MatchButton(sceneHud, label, parent, new Vector2(left, bottom), new Vector2(right, top), action);
            var text = button.GetComponentInChildren<TMP_Text>(); text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 19;
            return button;
        }
        private void ToggleVillagePanel()
        {
            if (!LocalPlayerAlive || !matchStarted || matchResult != null || IsPlayingTask || TutorialIsOpen || NotebookIsOpen) return;
            villagePanel.SetActive(!villagePanel.activeSelf);
            if (villagePanel.activeSelf) { NetworkChatUI.Instance?.SetChatOpen(false); SendMovementInput(Vector2.zero); ResetInteractionInput(); }
        }
        private void CloseVillagePanel() { if (villagePanel != null) villagePanel.SetActive(false); }
        private void SendVillageUiAction(ThuongOnlineAction action, int value = 0, int second = -1)
        {
            SendAction(action, value, second); CloseVillagePanel();
        }
        private bool LocalAtVillagePoint(Vector2 point, float distance = 1.4f) => avatars.TryGetValue(localPlayerId, out var avatar) &&
            avatar.Root != null && Vector2.Distance(avatar.TargetPosition, point) <= distance;
        private void RefreshVillageUi()
        {
            if (matchStarted && sceneHud != null) EnsureVillageUi();
            if (villageCanvas == null) return;
            bool show = connected && matchStarted && matchResult == null;
            villageCanvas.enabled = show;
            if (!show) { CloseVillagePanel(); if (villageWorld != null) villageWorld.SetActive(false); return; }
            bool free = LocalPlayerAlive && !IsPlayingTask && !NotebookIsOpen && !TutorialIsOpen &&
                (visibleState == GameState.Day || visibleState == GameState.Night);
            villageToggle.gameObject.SetActive(free);
            if (!free) CloseVillagePanel();
            bool sabotage = visibleSabotage != ThuongSabotageKind.None && visibleSabotageUntil > VillageNow;
            float dark = sabotage && visibleSabotage == ThuongSabotageKind.Blackout && SelectedMap != null &&
                LocalAtBlackoutArea() && privateFaction != FactionType.Monster ? localLanternUntil > VillageNow ? .15f : .65f : 0;
            blackoutShade.color = new Color(.005f, .01f, .025f, dark);
            villageStatus.text = sabotage
                ? $"{SabotageLabel(visibleSabotage)} · {SabotageTargetLabel(visibleSabotage, visibleSabotageTarget)} · {Math.Ceiling(visibleSabotageUntil - VillageNow):0}s · Tới điểm đỏ để sửa"
                : visiblePairDoneDay == visibleDay ? "Nhiệm vụ đôi hôm nay đã hoàn tất (+5%)."
                : visiblePairFirst >= 0 || visiblePairSecond >= 0 ? $"Bảng A: {(visiblePairFirst >= 0 ? "có người" : "trống")} · Bảng B: {(visiblePairSecond >= 0 ? "có người" : "trống")} · {visiblePairProgress:P0}"
                : $"Họp khẩn {visibleMeetingsUsed}/{roomRules.village.meetingLimit} · Bảng A + B: hai người cùng kích hoạt";
            if (visibleRepairOwner >= 0) villageStatus.text += $" · Đang sửa {visibleRepairProgress:P0}";
            if (villageWorld != null) villageWorld.SetActive(visibleState == GameState.Day);
            RefreshSabotageMarkers(sabotage);
            if (!VillagePanelOpen) return;
            villageHelp.text = $"Chuông: tối đa 1 lần/người, {roomRules.village.meetingLimit} lần/phòng và cách nhau {roomRules.village.meetingCooldown}s. Còi gọi từ xa.\n" +
                "Bảng A + B: hai người khác nhau đứng gần hai bảng trong 3 giây, thưởng 5% mỗi ngày. Di chuyển để hủy.\n" +
                (sabotage ? "Cần khắc phục phá hoại trước khi gọi họp." : $"{(localBellUsed ? "Bạn đã gọi họp trong ván này." : "Bạn còn lượt gọi họp.")} Chờ họp: {Math.Max(0, Math.Ceiling(visibleMeetingAt - VillageNow)):0}s.");
            bellButton.interactable = LocalVillageDay && !localBellUsed && visibleMeetingsUsed < roomRules.village.meetingLimit &&
                !sabotage && VillageNow >= visibleMeetingAt && LocalAtVillagePoint(bellPoint);
            pairAButton.interactable = LocalVillageDay && visiblePairDoneDay != visibleDay && LocalAtVillagePoint(pairPoints[0], 1.1f);
            pairBButton.interactable = LocalVillageDay && visiblePairDoneDay != visibleDay && LocalAtVillagePoint(pairPoints[1], 1.1f);
            repairButton.gameObject.SetActive(privateFaction == FactionType.Villager);
            repairButton.interactable = LocalVillageDay && privateFaction == FactionType.Villager && sabotage &&
                LocalAtVillagePoint(SabotagePoint(visibleSabotage, visibleSabotageTarget));
            villagePocket.text = $"TÚI ĐỒ {villageInventory.Count}/2 · Tối đa 2 phần thưởng/người/ngày\n" +
                $"Đèn: nhìn rõ khu tối {roomRules.village.lanternSeconds}s. Còi: gọi họp từ xa. Bùa: chặn một đòn trong {roomRules.village.wardSeconds}s, không chặn vote.\n" +
                $"Đèn còn {Math.Max(0, Math.Ceiling(localLanternUntil - VillageNow)):0}s · Bùa còn {Math.Max(0, Math.Ceiling(localWardUntil - VillageNow)):0}s";
            for (int slot = 0; slot < 2; slot++)
            {
                bool canUseAlarm = slot >= villageInventory.Count || villageInventory[slot] != ThuongItemKind.Alarm ||
                    LocalVillageDay && !sabotage && !localBellUsed && visibleMeetingsUsed < roomRules.village.meetingLimit && VillageNow >= visibleMeetingAt;
                bool canUseLantern = slot >= villageInventory.Count || villageInventory[slot] != ThuongItemKind.Lantern ||
                    privateFaction == FactionType.Villager && sabotage && visibleSabotage == ThuongSabotageKind.Blackout && LocalAtBlackoutArea();
                itemButtons[slot].interactable = free && slot < villageInventory.Count && canUseAlarm && canUseLantern;
                itemButtons[slot].GetComponentInChildren<TMP_Text>().text = slot < villageInventory.Count ? "DÙNG " + ItemLabel(villageInventory[slot]).ToUpperInvariant() : "Ô TRỐNG";
            }
            sabotageControls.SetActive(privateFaction == FactionType.Monster);
            selectedSabotageTarget = Mathf.Clamp(selectedSabotageTarget, 0, Mathf.Max(0, SabotageTargetCount(selectedSabotageKind) - 1));
            sabotageTargetText.text = SabotageLabel(selectedSabotageKind) + " · " + SabotageTargetLabel(selectedSabotageKind, selectedSabotageTarget);
            sabotageButton.interactable = LocalVillageDay && !sabotage && VillageNow >= visibleSabotageAt && SabotageTargetCount(selectedSabotageKind) > 0;
        }
        private bool LocalAtBlackoutArea() => avatars.TryGetValue(localPlayerId, out var avatar) &&
            avatar.Root != null && BlackoutArea(visibleSabotageTarget).Contains(avatar.TargetPosition);
        private void RebuildVillageMarkers()
        {
            if (villageWorld != null) Destroy(villageWorld);
            if (!villageLandmarksReady) return;
            villageWorld = new GameObject("Village interaction landmarks"); villageWorld.transform.SetParent(transform, false);
            if (villageMarkerSprite == null) villageMarkerSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(.5f, .5f), Texture2D.whiteTexture.width);
            VillageMarker("Chuông làng", bellPoint, new Color(1, .76f, .22f));
            VillageMarker("Bảng A", pairPoints[0], new Color(.2f, .8f, .9f));
            VillageMarker("Bảng B", pairPoints[1], new Color(.2f, .8f, .9f));
            sabotageMarker = VillageMarker("SỬA PHÁ HOẠI", bellPoint, new Color(.95f, .2f, .12f));
            barricadeMarker = new GameObject("Temporary road barricade", typeof(SpriteRenderer));
            barricadeMarker.transform.SetParent(villageWorld.transform, false);
            var sprite = barricadeMarker.GetComponent<SpriteRenderer>(); sprite.sprite = villageMarkerSprite; sprite.sortingOrder = 98; sprite.color = new Color(.72f, .28f, .08f);
            sabotageMarker.SetActive(false); barricadeMarker.SetActive(false); villageWorld.SetActive(false);
        }
        private GameObject VillageMarker(string label, Vector2 point, Color color)
        {
            var root = new GameObject(label, typeof(SpriteRenderer)); root.transform.SetParent(villageWorld.transform, false);
            root.transform.position = point; root.transform.localScale = new Vector3(.55f, .55f, 1);
            var sprite = root.GetComponent<SpriteRenderer>(); sprite.sprite = villageMarkerSprite; sprite.color = color; sprite.sortingOrder = 97;
            var child = new GameObject("World label", typeof(TextMeshPro)); child.transform.SetParent(root.transform, false);
            child.transform.localPosition = Vector3.up * 1.3f; child.transform.localScale = Vector3.one * (1 / .55f);
            var text = child.GetComponent<TextMeshPro>(); text.font = EmoteFont; text.text = label; text.fontSize = 3;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(5, 1); text.color = Color.white;
            text.GetComponent<MeshRenderer>().sortingOrder = 102;
            return root;
        }
        private void RefreshSabotageMarkers(bool active)
        {
            if (sabotageMarker == null || barricadeMarker == null) return;
            sabotageMarker.SetActive(active);
            if (active) sabotageMarker.transform.position = SabotagePoint(visibleSabotage, visibleSabotageTarget);
            bool road = active && visibleSabotage == ThuongSabotageKind.Barricade;
            barricadeMarker.SetActive(road);
            if (road)
            {
                var rect = BarricadeArea(visibleSabotageTarget);
                barricadeMarker.transform.position = rect.center;
                barricadeMarker.transform.localScale = new Vector3(rect.width, rect.height, 1);
            }
        }
        private void DisposeVillageUi()
        {
            if (practiceCanvas != null) Destroy(practiceCanvas.gameObject);
            if (villageCanvas != null) Destroy(villageCanvas.gameObject);
            if (villageWorld != null) Destroy(villageWorld);
            if (villageMarkerSprite != null) Destroy(villageMarkerSprite);
        }
    }
}
