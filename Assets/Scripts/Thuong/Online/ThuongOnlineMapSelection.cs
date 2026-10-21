using System;
using System.Linq;
using Thuong.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private bool randomMapSelection = true;
        private string configuredMapId = "village";
        private GameEventType configuredMapEvent;
        private GameObject mapSettingsRoot;
        private Button mapSettingsButton, randomMapOption, manualMapOption;
        private Button previousDraftMap, nextDraftMap, previousDraftEvent, nextDraftEvent;
        private TMP_Text draftMapLabel, draftEventLabel, mapSettingsHint;
        private bool draftRandomMap;
        private string draftMapId;
        private GameEventType draftMapEvent;

        private bool CanConfigureMap => !busy && !roomLocked && !rolesAssigned && !matchStarted &&
            mapCatalog != null && network != null && (!network.IsListening || network.IsServer);

        private GameEventType[] MapEventOptions => mapCatalog.GetVariants(eventCatalog)
            .Select(v => v.EventType).Distinct().ToArray();

        private bool SetHostMapSelection(bool random, string mapId, GameEventType eventType)
        {
            if (!CanConfigureMap) return false;
            var variants = mapCatalog.GetVariants(eventCatalog);
            if (variants.Length == 0) return false;
            var chosen = variants.FirstOrDefault(v => v.Map.mapId == mapId && v.EventType == eventType);
            if (!random && chosen.Map == null) return false;
            if (network.IsListening)
            {
                if (random) chosen = variants[UnityEngine.Random.Range(0, variants.Length)];
                if (!ApplyMapVariant(chosen)) return false;
            }
            else mapDrawComplete = false;
            randomMapSelection = random;
            if (!random) { configuredMapId = mapId; configuredMapEvent = eventType; }
            return true;
        }

        private void OpenMapSettings()
        {
            if (!CanConfigureMap || lobbyCanvas == null) return;
            draftRandomMap = randomMapSelection;
            draftMapId = SelectedMap != null ? SelectedMap.mapId : configuredMapId;
            draftMapEvent = mapDrawComplete ? selectedMapEvent : configuredMapEvent;
            if (mapSettingsRoot == null) BuildMapSettings();
            mapSettingsRoot.SetActive(true);
            mapSettingsRoot.transform.SetAsLastSibling();
            RefreshMapSettings();
        }

        private void CloseMapSettings()
        {
            if (mapSettingsRoot != null) mapSettingsRoot.SetActive(false);
        }

        private void ApplyMapSettings()
        {
            if (SetHostMapSelection(draftRandomMap, draftMapId, draftMapEvent)) CloseMapSettings();
        }

        private void CycleDraftMap(int direction)
        {
            var maps = mapCatalog.Released;
            if (maps.Length == 0 || draftRandomMap) return;
            int index = Array.FindIndex(maps, m => m.mapId == draftMapId);
            draftMapId = maps[(index + direction % maps.Length + maps.Length) % maps.Length].mapId;
            RefreshMapSettings();
        }

        private void CycleDraftEvent(int direction)
        {
            var types = MapEventOptions;
            if (types.Length == 0 || draftRandomMap) return;
            int index = Array.IndexOf(types, draftMapEvent);
            draftMapEvent = types[(index + direction % types.Length + types.Length) % types.Length];
            RefreshMapSettings();
        }

        private void RefreshMapSettings()
        {
            randomMapOption.GetComponentInChildren<TMP_Text>().text = (draftRandomMap ? "[x] " : "[ ] ") + "NGẪU NHIÊN";
            manualMapOption.GetComponentInChildren<TMP_Text>().text = (!draftRandomMap ? "[x] " : "[ ] ") + "CHỌN MAP";
            draftMapLabel.text = mapCatalog.Find(draftMapId)?.displayName ?? "CHƯA CÓ MAP";
            draftEventLabel.text = draftMapEvent == GameEventType.None ? "BÌNH THƯỜNG" : ThuongEventPresentationCatalog.Name(draftMapEvent);
            previousDraftMap.interactable = nextDraftMap.interactable =
                previousDraftEvent.interactable = nextDraftEvent.interactable = !draftRandomMap;
            mapSettingsHint.text = draftRandomMap
                ? "Hệ thống chọn từ 6 map thường và 36 map event."
                : "Chọn Bình thường hoặc loại event muốn chơi.";
        }

        private void BuildMapSettings()
        {
            var shade = LobbyPanel("Map settings shade", lobbyCanvas.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(0, 0, 0, .85f);
            mapSettingsRoot = shade.gameObject;
            var panel = LobbyPanel("Map settings", shade, new Vector2(.18f, .13f), new Vector2(.82f, .87f));
            LobbyText("Map settings title", panel, "THIẾT LẬP MAP", new Vector2(.05f, .91f), new Vector2(.95f, .98f), 28, TextAlignmentOptions.Center);
            randomMapOption = LobbyButton("Random map option", panel, "NGẪU NHIÊN", new Vector2(.05f, .78f), new Vector2(.47f, .87f),
                () => { draftRandomMap = true; RefreshMapSettings(); });
            manualMapOption = LobbyButton("Manual map option", panel, "CHỌN MAP", new Vector2(.53f, .78f), new Vector2(.95f, .87f),
                () => { draftRandomMap = false; RefreshMapSettings(); });
            LobbyText("Map choice heading", panel, "BẢN ĐỒ", new Vector2(.08f, .70f), new Vector2(.92f, .76f), 19, TextAlignmentOptions.Center);
            previousDraftMap = LobbyButton("Previous draft map", panel, "<", new Vector2(.05f, .57f), new Vector2(.18f, .68f), () => CycleDraftMap(-1));
            draftMapLabel = LobbyText("Draft map", panel, "", new Vector2(.20f, .57f), new Vector2(.80f, .68f), 26, TextAlignmentOptions.Center);
            nextDraftMap = LobbyButton("Next draft map", panel, ">", new Vector2(.82f, .57f), new Vector2(.95f, .68f), () => CycleDraftMap(1));
            LobbyText("Map event heading", panel, "LOẠI MAP", new Vector2(.08f, .47f), new Vector2(.92f, .53f), 19, TextAlignmentOptions.Center);
            previousDraftEvent = LobbyButton("Previous draft event", panel, "<", new Vector2(.05f, .35f), new Vector2(.18f, .46f), () => CycleDraftEvent(-1));
            draftEventLabel = LobbyText("Draft event", panel, "", new Vector2(.20f, .35f), new Vector2(.80f, .46f), 22, TextAlignmentOptions.Center);
            nextDraftEvent = LobbyButton("Next draft event", panel, ">", new Vector2(.82f, .35f), new Vector2(.95f, .46f), () => CycleDraftEvent(1));
            mapSettingsHint = LobbyText("Map settings hint", panel, "", new Vector2(.06f, .19f), new Vector2(.94f, .30f), 19, TextAlignmentOptions.Center);
            mapSettingsHint.textWrappingMode = TextWrappingModes.Normal;
            LobbyButton("Cancel map settings", panel, "HỦY", new Vector2(.10f, .055f), new Vector2(.44f, .145f), CloseMapSettings);
            LobbyButton("Apply map settings", panel, "ÁP DỤNG", new Vector2(.56f, .055f), new Vector2(.90f, .145f), ApplyMapSettings);
        }

        private void DrawMapSelectionOptions()
        {
            if (mapCatalog == null) { GUILayout.Label(mapError); return; }
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && CanConfigureMap;
            GUILayout.Label("BẢN ĐỒ");
            int mode = GUILayout.Toolbar(randomMapSelection ? 0 : 1, new[] { "NGẪU NHIÊN", "CHỌN MAP" });
            if ((mode == 0) != randomMapSelection) SetHostMapSelection(mode == 0, configuredMapId, configuredMapEvent);
            if (!randomMapSelection)
            {
                var maps = mapCatalog.Released;
                int index = Array.FindIndex(maps, m => m.mapId == configuredMapId);
                GUILayout.BeginHorizontal();
                if (maps.Length > 0 && GUILayout.Button("< MAP", GUILayout.Width(75)))
                    SetHostMapSelection(false, maps[(index + maps.Length - 1) % maps.Length].mapId, configuredMapEvent);
                GUILayout.Label(mapCatalog.Find(configuredMapId)?.displayName ?? "CHƯA CÓ MAP");
                if (maps.Length > 0 && GUILayout.Button("MAP >", GUILayout.Width(75)))
                    SetHostMapSelection(false, maps[(index + 1) % maps.Length].mapId, configuredMapEvent);
                GUILayout.EndHorizontal();
                var types = MapEventOptions;
                int eventIndex = Array.IndexOf(types, configuredMapEvent);
                GUILayout.BeginHorizontal();
                if (types.Length > 0 && GUILayout.Button("<", GUILayout.Width(45)))
                    SetHostMapSelection(false, configuredMapId, types[(eventIndex + types.Length - 1) % types.Length]);
                GUILayout.Label(configuredMapEvent == GameEventType.None ? "BÌNH THƯỜNG" : ThuongEventPresentationCatalog.Name(configuredMapEvent));
                if (types.Length > 0 && GUILayout.Button(">", GUILayout.Width(45)))
                    SetHostMapSelection(false, configuredMapId, types[(eventIndex + 1) % types.Length]);
                GUILayout.EndHorizontal();
            }
            GUI.enabled = previousEnabled;
        }
    }
}
