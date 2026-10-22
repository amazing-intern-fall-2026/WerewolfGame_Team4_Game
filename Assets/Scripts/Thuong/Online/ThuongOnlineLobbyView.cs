using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed class LobbyRow
        {
            public GameObject Root;
            public Image Avatar, StatusBadge;
            public TMP_Text Name, Status;
        }

        private Canvas lobbyCanvas;
        private GraphicRaycaster lobbyRaycaster;
        private RectTransform lobbyContent;
        private TMP_Text lobbyCountLabel, lobbyStepLabel, lobbyHintLabel, lobbyCodeLabel;
        private TMP_Text lobbyCapacityLabel, lobbyCharacterLabel, lobbyReadyLabel, lobbyRoleLabel;
        private Image lobbyPortrait;
        private Button lobbyReadyButton, lobbyRoleButton, lobbyStartButton, lobbyPreviousButton, lobbyNextButton;
        private Button lobbyDecreaseButton, lobbyIncreaseButton;
        private readonly Dictionary<int, LobbyRow> lobbyRows = new();

        // OK confirms the role locally; Ready need not wait for the round trip.
        private bool CanToggleLobbyReady => connected && !busy && network != null &&
            network.IsListening && !matchStarted && HasLocalRole &&
            localMapReady && (HasReadRole || localRoleConfirmed) && !ShowRoleCard;

        private void RefreshLobbyView()
        {
#if THUONG_MAP_QA
            if(qaPresentationOverride) return;
#endif
            bool visible = connected && !matchStarted && !ShowRoleCard;
            if (lobbyCanvas == null && connected) BuildLobbyView();
            if (lobbyCanvas == null) return;
            lobbyCanvas.enabled = visible;
            lobbyRaycaster.enabled = visible;
            if (!visible) return;
            RefreshMapSelector();

            var local = lobby.FirstOrDefault(member => member.ClientId == network.LocalClientId);
            bool ready = local != null && local.Ready;
            bool canEdit = !busy && !roomLocked;
            int readyCount = lobby.Count(member => member.Ready);
            lobbyCountLabel.text = $"LOBBY  /  {lobby.Count}/{lobbyCapacity} NGƯỜI";
            lobbyCodeLabel.text = string.IsNullOrEmpty(sessionCode) ? $"LAN • {address}:{portText}" : "MÃ PHÒNG: " + sessionCode;
            lobbyStepLabel.text = rolesAssigned
                ? "ĐÃ PHÂN VAI  →  XEM PHÂN VAI  →  OK  →  READY  →  VÀO GAME"
                : "CHỌN NHÂN VẬT  →  CHỦ PHÒNG PHÂN VAI  →  XEM PHÂN VAI  →  READY";
            lobbyCapacityLabel.text = $"SỨC CHỨA: {lobbyCapacity} / 25";
            lobbyDecreaseButton.interactable = canEdit && network.IsServer && lobbyCapacity > Mathf.Max(2, lobby.Count);
            lobbyIncreaseButton.interactable = canEdit && network.IsServer && lobbyCapacity < MaxPlayers;
            lobbyPreviousButton.interactable = lobbyNextButton.interactable = canEdit;
            int characterIndex = local != null ? local.CharacterIndex : selectedCharacterIndex;
            lobbyCharacterLabel.text = $"NHÂN VẬT {characterIndex + 1:00}";
            lobbyPortrait.sprite = LobbyAvatar(characterIndex);
            lobbyPortrait.enabled = lobbyPortrait.sprite != null;
            lobbyReadyButton.interactable = local != null && CanToggleLobbyReady;
            lobbyReadyLabel.text = !rolesAssigned ? "CHỜ PHÂN VAI"
                : !HasLocalRole ? "ĐANG NHẬN ROLE…"
                : !HasReadRole && !localRoleConfirmed ? "XEM PHÂN VAI TRƯỚC"
                : ready ? "HỦY READY" : "READY / SẴN SÀNG";
            lobbyRoleLabel.text = rolesAssigned ? HasLocalRole ? "XEM PHÂN VAI" : "ĐANG NHẬN ROLE…"
                : network.IsServer ? "PHÂN VAI" : "CHỜ PHÂN VAI";
            lobbyRoleButton.interactable = !busy && (rolesAssigned ? HasLocalRole : network.IsServer && lobby.Count >= 2 && AllMapsReady);
            lobbyStartButton.gameObject.SetActive(network.IsServer && rolesAssigned);
            lobbyStartButton.interactable = !busy && rolesAssigned && lobby.Count >= 2 &&
                AllMapsReady && serverPlayers.Values.All(member => member.RevealRead && member.Ready);
            lobbyHintLabel.text = rolesAssigned
                ? $"{readyCount}/{lobby.Count} người Ready. " + (!HasLocalRole ? "Đang nhận thẻ role từ chủ phòng…"
                    : !HasReadRole && !localRoleConfirmed ? "Bấm Xem phân vai, đọc rồi bấm OK. Không giới hạn thời gian."
                    : readyCount == lobby.Count ? "Tất cả đã sẵn sàng. Chờ chủ phòng bấm Bắt đầu game."
                    : ready ? "Đang chờ những người chơi còn lại sẵn sàng." : "Bấm Ready khi bạn sẵn sàng.")
                : network.IsServer ? "Chờ đủ người rồi bấm Phân vai. Danh sách được khóa sau khi phân vai."
                : "Chờ chủ phòng chốt danh sách và phân vai. Bạn có thể đổi nhân vật lúc này.";
            if (!localMapReady) lobbyHintLabel.text = mapError;
            else if (!rolesAssigned && network.IsServer && !AllMapsReady)
                lobbyHintLabel.text = "Đang chờ mọi người xác nhận map. Nếu chưa hoàn tất, hãy kiểm tra tất cả đang dùng cùng bản game.";
            if (lobby.Count == 2)
                lobbyHintLabel.text += "  •  Phòng thử 2 người: tắt thắng do cân bằng quân số.";

            var activeIds = new HashSet<int>();
            foreach (var member in lobby)
            {
                activeIds.Add(member.PlayerId);
                if (!lobbyRows.TryGetValue(member.PlayerId, out LobbyRow row))
                    lobbyRows[member.PlayerId] = row = CreateLobbyRow(member.PlayerId);
                row.Root.SetActive(true);
                row.Avatar.sprite = LobbyAvatar(member.CharacterIndex);
                row.Avatar.enabled = row.Avatar.sprite != null;
                row.Name.text = $"P{member.PlayerId + 1:00}  {member.Name}" +
                    (member.ClientId == network.LocalClientId ? "  (Bạn)" : string.Empty);
                row.Status.text = !member.MapReady ? "ĐANG TẢI MAP" : member.Ready ? "READY" : "NOT READY";
                row.StatusBadge.color = !member.MapReady ? new Color32(139, 91, 40, 255)
                    : member.Ready ? new Color32(57, 105, 65, 255) : new Color32(111, 90, 70, 255);
            }
            foreach (var pair in lobbyRows)
                if (!activeIds.Contains(pair.Key)) pair.Value.Root.SetActive(false);
        }

        private Sprite LobbyAvatar(int index) => characterCast != null && characterCast.Count > 0
            ? characterCast.idleSprites[ClampCharacterIndex(index)] : null;

        private void BuildLobbyView()
        {
            var root = new GameObject("Online Lobby Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            lobbyCanvas = root.GetComponent<Canvas>();
            lobbyCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            lobbyCanvas.sortingOrder = 20;
            lobbyRaycaster = root.GetComponent<GraphicRaycaster>();
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = .5f;
            var shade = LobbyPanel("Lobby shade", root.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(.06f, .04f, .025f, .82f);
            var card = LobbyPanel("Lobby Card", shade, new Vector2(.06f, .06f), new Vector2(.94f, .94f));
            var heading = LobbyPanel("TopBar", card, new Vector2(.015f, .89f), new Vector2(.985f, .98f));
            lobbyCountLabel = LobbyText("Lobby count", heading, "LOBBY", new Vector2(.025f, .08f), new Vector2(.59f, .92f), 30);
            lobbyCodeLabel = LobbyText("Room code", heading, "", new Vector2(.60f, .08f), new Vector2(.98f, .92f), 19, TextAlignmentOptions.MidlineRight);
            lobbyStepLabel = LobbyText("Lobby steps", card, "", new Vector2(.03f, .815f), new Vector2(.97f, .875f), 20);

            var character = LobbyPanel("Character selection", card, new Vector2(.025f, .205f), new Vector2(.265f, .80f));
            LobbyText("Character heading", character, "NHÂN VẬT CỦA BẠN", new Vector2(.025f, .88f), new Vector2(.975f, .98f), 20, TextAlignmentOptions.Center);
            var portrait = LobbyPanel("Portrait frame", character, new Vector2(.22f, .61f), new Vector2(.78f, .85f));
            lobbyPortrait = LobbyPanel("Character avatar", portrait, new Vector2(.12f, .08f), new Vector2(.88f, .92f), false).GetComponent<Image>();
            lobbyPortrait.preserveAspect = true;
            lobbyPortrait.raycastTarget = false;
            lobbyCharacterLabel = LobbyText("Character number", character, "", new Vector2(.03f, .52f), new Vector2(.97f, .60f), 19, TextAlignmentOptions.Center);
            lobbyPreviousButton = LobbyButton("Previous character", character, "< TRƯỚC", new Vector2(.035f, .44f), new Vector2(.475f, .515f), () => SelectCharacter(selectedCharacterIndex - 1));
            lobbyNextButton = LobbyButton("Next character", character, "SAU >", new Vector2(.525f, .44f), new Vector2(.965f, .515f), () => SelectCharacter(selectedCharacterIndex + 1));
            lobbyCapacityLabel = LobbyText("Lobby capacity", character, "", new Vector2(.025f, .36f), new Vector2(.975f, .435f), 18, TextAlignmentOptions.Center);
            lobbyDecreaseButton = LobbyButton("Decrease capacity", character, "−", new Vector2(.12f, .275f), new Vector2(.44f, .35f), () => SetLobbyCapacity(lobbyCapacity - 1));
            lobbyIncreaseButton = LobbyButton("Increase capacity", character, "+", new Vector2(.56f, .275f), new Vector2(.88f, .35f), () => SetLobbyCapacity(lobbyCapacity + 1));
            BuildMapSelector(card);

            var table = LobbyPanel("Lobby table", card, new Vector2(.285f, .205f), new Vector2(.975f, .80f));
            var columns = LobbyPanel("TopBar", table, new Vector2(.015f, .89f), new Vector2(.985f, .99f));
            LobbyText("Avatar column", columns, "AVATAR", new Vector2(.015f, 0), new Vector2(.155f, 1), 18, TextAlignmentOptions.Center);
            LobbyText("Player column", columns, "TÊN NGƯỜI CHƠI", new Vector2(.17f, 0), new Vector2(.72f, 1), 20);
            LobbyText("Ready column", columns, "TRẠNG THÁI", new Vector2(.75f, 0), new Vector2(.985f, 1), 19, TextAlignmentOptions.Center);
            var viewport = LobbyPanel("Players viewport", table, new Vector2(.015f, .025f), new Vector2(.965f, .875f), false);
            viewport.GetComponent<Image>().color = new Color(.30f, .20f, .12f, .12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Lobby players", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            lobbyContent = content.GetComponent<RectTransform>();
            lobbyContent.SetParent(viewport, false);
            lobbyContent.anchorMin = new Vector2(0, 1); lobbyContent.anchorMax = Vector2.one;
            lobbyContent.pivot = new Vector2(.5f, 1); lobbyContent.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 5; layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = table.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = lobbyContent; scroll.horizontal = false;
            scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = LobbyPanel("Players scrollbar", table, new Vector2(.969f, .025f), new Vector2(.99f, .875f), false);
            track.GetComponent<Image>().color = new Color32(96, 66, 42, 255);
            var handle = LobbyPanel("Handle", track, Vector2.zero, Vector2.one, false);
            handle.GetComponent<Image>().color = new Color32(233, 195, 125, 255);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;

            lobbyHintLabel = LobbyText("Lobby hint", card, "", new Vector2(.03f, .135f), new Vector2(.97f, .195f), 18);
            lobbyReadyButton = LobbyButton("Lobby Ready", card, "READY / SẴN SÀNG", new Vector2(.285f, .035f), new Vector2(.465f, .12f), ToggleReady);
            lobbyReadyLabel = lobbyReadyButton.GetComponentInChildren<TMP_Text>();
            lobbyRoleButton = LobbyButton("Lobby Role", card, "PHÂN VAI", new Vector2(.485f, .035f), new Vector2(.665f, .12f), () =>
            {
                if (rolesAssigned) ViewLocalRole(); else AssignLobbyRolesOnServer();
            });
            lobbyRoleLabel = lobbyRoleButton.GetComponentInChildren<TMP_Text>();
            lobbyStartButton = LobbyButton("Lobby Start", card, "BẮT ĐẦU GAME", new Vector2(.685f, .035f), new Vector2(.825f, .12f), StartMatchOnServer);
            LobbyButton("Leave lobby", card, "RỜI PHÒNG", new Vector2(.845f, .035f), new Vector2(.975f, .12f), LeaveSession);
            mapSettingsButton = LobbyButton("Map settings button", card, "MAP: NGẪU NHIÊN", new Vector2(.025f, .035f), new Vector2(.265f, .12f), OpenMapSettings);
        }

        private LobbyRow CreateLobbyRow(int playerId)
        {
            var root = LobbyPanel("Lobby Player " + (playerId + 1), lobbyContent, Vector2.zero, Vector2.one, false);
            root.GetComponent<Image>().color = playerId % 2 == 0 ? new Color32(246, 222, 175, 255) : new Color32(229, 202, 153, 255);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 68;
            var avatar = LobbyPanel("Player Avatar", root, new Vector2(.025f, .08f), new Vector2(.14f, .92f), false).GetComponent<Image>();
            avatar.preserveAspect = true; avatar.raycastTarget = false;
            var name = LobbyText("Player Name", root, "", new Vector2(.17f, .08f), new Vector2(.72f, .92f), 22);
            name.richText = false;
            name.color = ThuongMedievalTheme.Ink;
            var badge = LobbyPanel("Ready Badge", root, new Vector2(.75f, .17f), new Vector2(.985f, .83f), false).GetComponent<Image>();
            var statusText = LobbyText("Player Status", badge.transform, "NOT READY", new Vector2(.015f, 0), new Vector2(.985f, 1), 19, TextAlignmentOptions.Center);
            statusText.color = ThuongMedievalTheme.Cream;
            return new LobbyRow { Root = root.gameObject, Avatar = avatar, Name = name, Status = statusText, StatusBadge = badge };
        }

        private static RectTransform LobbyPanel(string name, Transform parent, Vector2 min, Vector2 max, bool themed = true)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            if (themed) ThuongMedievalTheme.Active?.StylePanel(root.GetComponent<Image>(), name);
            return rect;
        }

        private TMP_Text LobbyText(string name, Transform parent, string value, Vector2 min, Vector2 max, int size,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(parent, false); text.rectTransform.anchorMin = min; text.rectTransform.anchorMax = max;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = sceneHud != null && sceneHud.phaseText != null ? sceneHud.phaseText.font : null;
            text.text = value; text.fontSize = size; text.enableAutoSizing = true;
            text.fontSizeMax = size; text.fontSizeMin = 14;
            text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            ThuongMedievalTheme.Active?.StyleText(text);
            return text;
        }

        private Button LobbyButton(string name, Transform parent, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var rect = LobbyPanel(name, parent, min, max, false);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            ThuongMedievalTheme.Active?.StylePanel(rect.GetComponent<Image>(), name);
            LobbyText("Text", rect, label, new Vector2(.025f, .025f), new Vector2(.975f, .975f), 20, TextAlignmentOptions.Center);
            return button;
        }
    }
}
