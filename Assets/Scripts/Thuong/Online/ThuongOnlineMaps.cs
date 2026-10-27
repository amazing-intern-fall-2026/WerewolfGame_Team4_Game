using System;
using System.Linq;
using System.Text;
using Thuong.Maps;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const string BuildPrefix = "THUONG6|";
        private const string MapReadyMessage = "thuong.online.map-ready.v1";
        private ThuongMapCatalog mapCatalog;
        private ThuongMapRuntime mapRuntime;
        private ThuongEventPresentation eventPresentation;
        private ThuongEventPresentationCatalog eventCatalog;
        private bool localMapReady;
        private string mapError = "";
        private uint mapEpoch = 1;
        private uint acknowledgedMapEpoch;
        private bool mapDrawComplete;
        private GameEventType selectedMapEvent;
        private GameEventType publicEvent;
        private int publicEventDay;
        private TMP_Text mapLabel, mapVariantLabel;
        private Image mapPreview;
        private Image bloodMoonIcon;
        private Sprite bloodMoonSprite;
        private Texture2D bloodMoonTexture;
        public ThuongMapDefinition SelectedMap => mapRuntime != null ? mapRuntime.Definition : null;
        public GameEventType SelectedMapEvent => selectedMapEvent;
        public bool MapReady => localMapReady;
        public GameEventType VisibleEvent => publicEvent;
        private bool AllMapsReady => mapDrawComplete && localMapReady && serverPlayers.Count > 0 && serverPlayers.Values.All(p => p.MapReady);

        private void PrepareOnlineMaps()
        {
            mapCatalog = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
            eventCatalog = Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath);
            mapRuntime = gameObject.AddComponent<ThuongMapRuntime>();
            eventPresentation = gameObject.AddComponent<ThuongEventPresentation>();
            // Disable only the original online world. Offline scenes retain their generator and mood.
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                if (root.name == "Map" || root.name == "TaskStations" || root.name == "Thuong Online Map Preview") root.SetActive(false);
            foreach (var mood in FindObjectsByType<ThuongVillageMood>()) mood.enabled = false;
            if (mapCatalog == null || eventCatalog == null)
            { mapError = "Thiếu map/event catalog. Chạy Tools/Thuong/Maps/Install Maps And Events."; Debug.LogError(mapError); return; }
            var entry = game.gameObject.scene.GetRootGameObjects()
                .Select(root => root.GetComponent<ThuongOnlineSceneEntry>()).FirstOrDefault(value => value != null);
            string initialMapId = entry != null && entry.InitialMap != null ? entry.InitialMap.mapId : "village_large";
            BindOnlineMap(mapCatalog.Find(initialMapId));
        }

        private bool BindOnlineMap(ThuongMapDefinition definition)
        {
            if (definition == null) { localMapReady = false; mapError = "Bản game chưa có map chủ phòng chọn."; return false; }
            if (SelectedMap == definition && localMapReady) return true;
            ResetMinigames(); eventPresentation.Clear();
            if (!mapRuntime.Bind(definition, out mapError)) { localMapReady = false; return false; }
            stations = mapRuntime.Stations;
            tasks.allTasks = stations.Select(s => s.task).ToList();
            tasks.ResetForNewMatch();
            if (worldCamera != null)
            { worldCamera.boundsMin = definition.boundsMin; worldCamera.boundsMax = definition.boundsMax; worldCamera.Refresh(true); }
            eventPresentation.Bind(mapRuntime, eventCatalog);
            localMapReady = eventCatalog != null && Enumerable.Range(1, 6).All(i => eventCatalog.Find((GameEventType)i) != null);
            mapError = localMapReady ? "" : "Thiếu nội dung sự kiện.";
            return localMapReady;
        }

        private bool EnsureRoomMapSelection()
        {
            if (mapDrawComplete) return true;
            var variants = mapCatalog?.GetVariants(eventCatalog) ?? Array.Empty<ThuongMapVariant>();
            if (variants.Length == 0) { status = "Không có map khả dụng."; return false; }
            var chosen = randomMapSelection ? variants[UnityEngine.Random.Range(0, variants.Length)]
                : variants.FirstOrDefault(v => v.Map.mapId == configuredMapId && v.EventType == configuredMapEvent);
            if (chosen.Map == null) { status = "Map đã chọn không có trong bản game này."; return false; }
            return ApplyMapVariant(chosen);
        }

        private bool ApplyMapVariant(ThuongMapVariant variant)
        {
            if (roomLocked || matchStarted || network.IsListening && !network.IsServer) return false;
            if (variant.EventType != GameEventType.None && eventCatalog?.Find(variant.EventType) == null) return false;
            if (!BindOnlineMap(variant.Map)) return false;
            selectedMapEvent = variant.EventType;
            publicEvent = selectedMapEvent;
            publicEventDay = publicEvent == GameEventType.None ? 0 : Mathf.Max(1, game.currentDay);
            mapDrawComplete = true;
            mapEpoch++;
            foreach (var member in serverPlayers.Values)
            {
                member.MapReady = member.ClientId == network.LocalClientId && localMapReady;
                member.Position = SpawnPosition(member.PlayerId);
                member.Input = Vector2.zero; member.Ready = false;
            }
            rosterDirty = true;
            if (network.IsServer) { BroadcastRoster(); BroadcastSnapshot(); }
            return true;
        }

#if UNITY_EDITOR || THUONG_MAP_QA
        // Deterministic traversal for content/network checks only; no player controls.
        private void SelectMap(int direction)
        {
            var maps = mapCatalog.Released;
            int index = Array.IndexOf(maps, SelectedMap);
            if (maps.Length > 0) ApplyMapVariant(new ThuongMapVariant(maps[(index + direction % maps.Length + maps.Length) % maps.Length], selectedMapEvent));
        }
#endif

        private void ConfigureMapEventForMatch()
        {
            var events = EventManager.Instance != null ? EventManager.Instance : game.gameObject.AddComponent<EventManager>();
            events.ConfigureMapVariant(selectedMapEvent);
        }

        private void WriteMapState(FastBufferWriter writer, int day)
        {
            FixedString64Bytes id = Fixed64(SelectedMap != null ? SelectedMap.mapId : "");
            writer.WriteValueSafe(id); writer.WriteValueSafe(SelectedMap != null ? SelectedMap.contentRevision : 0);
            writer.WriteValueSafe(mapEpoch); writer.WriteValueSafe(ThuongEventPresentationCatalog.ContentRevision);
            publicEvent = mapDrawComplete && matchResult == null ? selectedMapEvent : GameEventType.None;
            publicEventDay = publicEvent == GameEventType.None ? 0 : day;
            writer.WriteValueSafe((byte)publicEvent); writer.WriteValueSafe(publicEventDay);
            RefreshMapPresentation();
        }

        private void ReadMapState(FastBufferReader reader)
        {
            reader.ReadValueSafe(out FixedString64Bytes id); reader.ReadValueSafe(out int revision);
            reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out int eventsRevision);
            reader.ReadValueSafe(out byte eventType); reader.ReadValueSafe(out publicEventDay);
            publicEvent = Enum.IsDefined(typeof(GameEventType), (int)eventType) ? (GameEventType)eventType : GameEventType.None;
            selectedMapEvent = publicEvent;
            var definition = mapCatalog?.Find(id.ToString());
            bool same = localMapReady && SelectedMap == definition && definition != null && definition.contentRevision == revision && mapEpoch == epoch && acknowledgedMapEpoch == epoch;
            bool compatible = definition != null && definition.contentRevision == revision && eventsRevision == ThuongEventPresentationCatalog.ContentRevision;
            mapEpoch = epoch;
            if (!compatible)
            {
                localMapReady = false; eventPresentation.Clear();
                mapError = "Map hoặc event khác phiên bản chủ phòng. Cần dùng cùng bản game.";
                status = mapError;
            }
            else if (!same) BindOnlineMap(definition);
            mapDrawComplete = compatible && localMapReady;
            if (!same) { SendMapAcknowledgement(id, revision, epoch, localMapReady && compatible); if (compatible && localMapReady) acknowledgedMapEpoch = epoch; }
        }

        private void SendMapAcknowledgement(FixedString64Bytes id, int revision, uint epoch, bool ready)
        {
            using var writer = new FastBufferWriter(100, Allocator.Temp);
            writer.WriteValueSafe(id); writer.WriteValueSafe(revision); writer.WriteValueSafe(epoch);
            writer.WriteValueSafe(ThuongEventPresentationCatalog.ContentRevision); writer.WriteValueSafe(ready);
            network.CustomMessagingManager.SendNamedMessage(MapReadyMessage, NetworkManager.ServerClientId, writer);
        }

        private void ReceiveMapAcknowledgement(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || !serverPlayers.TryGetValue(sender, out var member)) return;
            try
            {
                reader.ReadValueSafe(out FixedString64Bytes id); reader.ReadValueSafe(out int revision);
                reader.ReadValueSafe(out uint epoch); reader.ReadValueSafe(out int eventRevision); reader.ReadValueSafe(out bool ready);
                if (SelectedMap == null || id.ToString() != SelectedMap.mapId || revision != SelectedMap.contentRevision || epoch != mapEpoch) return;
                member.MapReady = ready && eventRevision == ThuongEventPresentationCatalog.ContentRevision;
                if (!member.MapReady) status = "Có người chơi thiếu map/event hoặc khác phiên bản. Cần cập nhật bản game.";
                rosterDirty = true;
            }
            catch (OverflowException) { member.MapReady = false; }
        }

        private void RefreshMapPresentation()
        {
#if THUONG_MAP_QA
            if (qaPresentationOverride) return;
#endif
            eventPresentation?.Apply(publicEventDay == visibleDay ? publicEvent : GameEventType.None,
                visibleDay, visiblePhase, connected && matchStarted && matchResult == null && localMapReady);
        }

        private void ClearMapPresentation()
        {
            CloseMapSettings();
            eventPresentation?.Clear(); publicEvent = GameEventType.None; publicEventDay = 0;
            mapDrawComplete = false; selectedMapEvent = GameEventType.None;
            acknowledgedMapEpoch = 0;
            foreach (var member in serverPlayers.Values) member.MapReady = false;
        }

        private void BuildMapSelector(RectTransform parent)
        {
            // Shares the character pane; map controls occupy the lower portion.
            mapLabel = LobbyText("Selected map", parent, "", new Vector2(.03f, .228f), new Vector2(.26f, .268f), 16, TextAlignmentOptions.Center);
            mapVariantLabel = LobbyText("Map variant", parent, "HỆ THỐNG CHỌN NGẪU NHIÊN", new Vector2(.03f, .278f), new Vector2(.26f, .318f), 14, TextAlignmentOptions.Center);
            mapVariantLabel.enableAutoSizing = true; mapVariantLabel.fontSizeMin = 10;
            mapPreview = LobbyPanel("Map thumbnail", parent, new Vector2(.075f, .325f), new Vector2(.225f, .365f), false).GetComponent<Image>();
            mapPreview.preserveAspect = true; mapPreview.raycastTarget = false;
        }

        private void RefreshMapSelector()
        {
            if (mapLabel == null) return;
            mapLabel.text = !localMapReady ? "MAP CHƯA SẴN SÀNG" : !mapDrawComplete ? "ĐANG NHẬN MAP…" : SelectedMap != null ? SelectedMap.displayName : mapError;
            mapVariantLabel.text = !mapDrawComplete ? "HỆ THỐNG CHỌN NGẪU NHIÊN" : selectedMapEvent == GameEventType.None ? "MAP BÌNH THƯỜNG" : ThuongEventPresentationCatalog.Name(selectedMapEvent);
            mapPreview.sprite = localMapReady && mapDrawComplete ? SelectedMap?.thumbnail : null;
            mapPreview.enabled = mapPreview.sprite != null;
            if (mapSettingsButton != null)
            {
                mapSettingsButton.interactable = CanConfigureMap && network.IsServer;
                mapSettingsButton.GetComponentInChildren<TMP_Text>().text = !network.IsServer ? "MAP ĐÃ CHỌN"
                    : randomMapSelection ? "MAP: NGẪU NHIÊN" : "MAP: TỰ CHỌN";
            }
            if (!CanConfigureMap) CloseMapSettings();
        }

        private void DrawMapSelector()
        {
            DrawMapSelectionOptions();
        }

        private void RefreshEventBadge(GameHUD hud)
        {
            if (hud.phaseText == null) return;
            if (bloodMoonIcon == null)
            {
                var root = new GameObject("Blood moon icon",typeof(RectTransform),typeof(Image));
                var rect = root.GetComponent<RectTransform>(); rect.SetParent(hud.phaseText.transform,false);
                rect.anchorMin = rect.anchorMax = new Vector2(1,.5f); rect.pivot = new Vector2(1,.5f);
                rect.anchoredPosition = new Vector2(-2,0); rect.sizeDelta = new Vector2(22,22);
                bloodMoonIcon = root.GetComponent<Image>(); bloodMoonIcon.raycastTarget = false;
                bloodMoonTexture = new Texture2D(32,32,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point };
                var pixels = new Color[32*32];
                for(int y=0;y<32;y++) for(int x=0;x<32;x++) pixels[y*32+x]=(x-15.5f)*(x-15.5f)+(y-15.5f)*(y-15.5f)<225?Color.white:Color.clear;
                bloodMoonTexture.SetPixels(pixels); bloodMoonTexture.Apply();
                bloodMoonSprite=Sprite.Create(bloodMoonTexture,new Rect(0,0,32,32),new Vector2(.5f,.5f),32);
                bloodMoonIcon.sprite = bloodMoonSprite; bloodMoonIcon.color = new Color(.8f,.16f,.2f);
                hud.phaseText.enableAutoSizing = true; hud.phaseText.fontSizeMin = 16; hud.phaseText.fontSizeMax = 26;
                hud.phaseText.margin = new Vector4(0,0,28,0);
            }
            bloodMoonIcon.gameObject.SetActive(publicEventDay == visibleDay && publicEvent == GameEventType.BloodMoon);
        }
        private void DisposeMapUi()
        { if(bloodMoonSprite!=null) Destroy(bloodMoonSprite); if(bloodMoonTexture!=null) Destroy(bloodMoonTexture); }
    }
}
