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
        private const string BuildPrefix = "THUONG5|";
        private const string MapReadyMessage = "thuong.online.map-ready.v1";
        private ThuongMapCatalog mapCatalog;
        private ThuongMapRuntime mapRuntime;
        private ThuongEventPresentation eventPresentation;
        private ThuongEventPresentationCatalog eventCatalog;
        private bool localMapReady;
        private string mapError = "";
        private uint mapEpoch = 1;
        private uint acknowledgedMapEpoch;
        private GameEventType publicEvent;
        private int publicEventDay;
        private TMP_Text mapLabel;
        private Image mapPreview;
        private Image bloodMoonIcon;
        private Sprite bloodMoonSprite;
        private Texture2D bloodMoonTexture;
        private Button previousMap, nextMap;
        public ThuongMapDefinition SelectedMap => mapRuntime != null ? mapRuntime.Definition : null;
        public bool MapReady => localMapReady;
        public GameEventType VisibleEvent => publicEvent;
        private bool AllMapsReady => localMapReady && serverPlayers.Count > 0 && serverPlayers.Values.All(p => p.MapReady);

        private void PrepareOnlineMaps()
        {
            mapCatalog = Resources.Load<ThuongMapCatalog>(ThuongMapCatalog.ResourcePath);
            eventCatalog = Resources.Load<ThuongEventPresentationCatalog>(ThuongEventPresentationCatalog.ResourcePath);
            mapRuntime = gameObject.AddComponent<ThuongMapRuntime>();
            eventPresentation = gameObject.AddComponent<ThuongEventPresentation>();
            // Disable only the original online world. Offline scenes retain their generator and mood.
            foreach (var root in game.gameObject.scene.GetRootGameObjects())
                if (root.name == "Map" || root.name == "TaskStations") root.SetActive(false);
            foreach (var mood in FindObjectsByType<ThuongVillageMood>()) mood.enabled = false;
            if (mapCatalog == null || eventCatalog == null)
            { mapError = "Thiếu map/event catalog. Chạy Tools/Thuong/Maps/Install Maps And Events."; Debug.LogError(mapError); return; }
            BindOnlineMap(mapCatalog.Find("village"));
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

        private void SelectMap(int direction)
        {
            if (busy || roomLocked || matchStarted || mapCatalog == null || network.IsListening && !network.IsServer) return;
            var maps = mapCatalog.Released;
            if (maps.Length == 0) return;
            int index = Array.IndexOf(maps, SelectedMap);
            if (!BindOnlineMap(maps[(index + direction + maps.Length) % maps.Length])) return;
            mapEpoch++;
            foreach (var member in serverPlayers.Values)
            {
                member.MapReady = member.ClientId == network.LocalClientId && localMapReady;
                member.Position = SpawnPosition(member.PlayerId);
                member.Input = Vector2.zero; member.Ready = false;
            }
            rosterDirty = true;
            if (network.IsServer) { BroadcastRoster(); BroadcastSnapshot(); }
        }

        private void WriteMapState(FastBufferWriter writer, int day)
        {
            FixedString64Bytes id = Fixed64(SelectedMap != null ? SelectedMap.mapId : "");
            writer.WriteValueSafe(id); writer.WriteValueSafe(SelectedMap != null ? SelectedMap.contentRevision : 0);
            writer.WriteValueSafe(mapEpoch); writer.WriteValueSafe(ThuongEventPresentationCatalog.ContentRevision);
            publicEvent = matchStarted && matchResult == null ? EventManager.Instance?.GetEventForDay(day) ?? GameEventType.None : GameEventType.None;
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
            eventPresentation?.Clear(); publicEvent = GameEventType.None; publicEventDay = 0;
            acknowledgedMapEpoch = 0;
            foreach (var member in serverPlayers.Values) member.MapReady = false;
        }

        private void BuildMapSelector(RectTransform parent)
        {
            // Shares the character pane; map controls occupy the lower portion.
            mapLabel = LobbyText("Selected map", parent, "", new Vector2(.03f, .228f), new Vector2(.26f, .268f), 16, TextAlignmentOptions.Center);
            previousMap = LobbyButton("Previous map", parent, "< MAP", new Vector2(.035f, .278f), new Vector2(.135f, .318f), () => SelectMap(-1));
            nextMap = LobbyButton("Next map", parent, "MAP >", new Vector2(.14f, .278f), new Vector2(.26f, .318f), () => SelectMap(1));
            mapPreview = LobbyPanel("Map thumbnail", parent, new Vector2(.075f, .325f), new Vector2(.225f, .365f), false).GetComponent<Image>();
            mapPreview.preserveAspect = true; mapPreview.raycastTarget = false;
        }

        private void RefreshMapSelector()
        {
            if (mapLabel == null) return;
            mapLabel.text = !localMapReady ? "MAP CHƯA SẴN SÀNG" : SelectedMap != null ? SelectedMap.displayName : mapError;
            mapPreview.sprite = localMapReady ? SelectedMap?.thumbnail : null;
            mapPreview.enabled = mapPreview.sprite != null;
            previousMap.interactable = nextMap.interactable = !busy && !roomLocked && network.IsServer;
        }

        private void DrawMapSelector()
        {
            GUILayout.Label("MAP: " + (SelectedMap != null ? SelectedMap.displayName : mapError));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("< Map trước")) SelectMap(-1);
            if (GUILayout.Button("Map sau >")) SelectMap(1);
            GUILayout.EndHorizontal();
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
