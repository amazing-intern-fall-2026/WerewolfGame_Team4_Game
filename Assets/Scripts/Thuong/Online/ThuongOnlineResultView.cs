using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private sealed class ResultRow
        {
            public TMP_Text Player, Role, Faction, Status;
            public Image Avatar, Badge;
        }

        private Canvas endgameCanvas;
        private TMP_Text outcomeTitle, outcomeWinner, outcomeReason, outcomeLocal, outcomeVote, outcomeColumn, outcomeVoteButtonLabel;
        private RectTransform outcomeContent;
        private Button outcomeVoteButton;
        private readonly Dictionary<int, ResultRow> outcomeRows = new();
        private bool showFinalVote;

        private void ResetEndgamePresentation()
        {
            showFinalVote = false;
            if (endgameCanvas != null) endgameCanvas.gameObject.SetActive(false);
        }

        private void RefreshEndgamePresentation()
        {
            bool visible = matchResult != null && visibleState == GameState.GameOver;
            if (endgameCanvas == null && visible) BuildEndgamePresentation();
            if (endgameCanvas == null) return;
            endgameCanvas.gameObject.SetActive(visible);
            if (!visible) return;

            var local = matchResult.Players.FirstOrDefault(player => player.Id == localPlayerId);
            bool knownLocal = matchResult.Players.Any(player => player.Id == localPlayerId);
            bool draw = matchResult.Winner == ThuongWinner.Draw;
            bool teamWin = matchResult.Winner == ThuongWinner.Villagers || matchResult.Winner == ThuongWinner.Werewolves;
            Color accent = draw ? new Color32(145, 109, 41, 255)
                : knownLocal && local.Won ? new Color32(42, 108, 66, 255) : new Color32(153, 55, 47, 255);
            outcomeTitle.text = draw ? "HÒA" : !knownLocal ? "VÁN CHƠI KẾT THÚC" : local.Won ? "CHIẾN THẮNG" : "THẤT BẠI";
            outcomeTitle.color = accent;
            outcomeWinner.text = WinnerLabel(matchResult.WinnerId);
            outcomeReason.text = $"NGÀY {matchResult.Day}  •  {EndgamePhaseLabel(matchResult.Phase)}\n{WinReasonLabel(matchResult.Reason)}";
            outcomeLocal.text = knownLocal
                ? $"P{local.Id + 1:00}  {local.Name}  •  {FinalRoleLabel(local)}  •  {FinalFactionLabel(local)}\n" +
                  (draw ? "Ván hòa — không có phe chiến thắng."
                    : local.Won ? local.Alive ? "Bạn đã đạt điều kiện chiến thắng."
                        : teamWin ? "Bạn đã bị loại nhưng vẫn thắng cùng phe." : "Bạn đã đạt điều kiện thắng riêng khi bị loại."
                    : "Bạn chưa đạt điều kiện chiến thắng của phe hoặc vai trò mình.")
                : "Không tìm thấy bạn trong danh sách cuối ván.";
            outcomeLocal.richText = false;
            outcomeVoteButton.gameObject.SetActive(voteResultRevision > 0);
            outcomeVoteButtonLabel.text = showFinalVote ? "VỀ BẢNG KẾT QUẢ" : "XEM VOTE CUỐI";
            outcomeColumn.text = showFinalVote ? "PHIẾU BẦU CUỐI" : "TRẠNG THÁI / KẾT QUẢ";
            outcomeVote.text = showFinalVote
                ? $"VOTE NGÀY {voteResultDay}: {voteResultMessage}"
                : "Kết quả tính theo phe cuối ván. Role chỉ được công khai sau khi kết thúc.";
            outcomeVote.richText = false;

            var ids = new HashSet<int>();
            foreach (var player in matchResult.Players)
            {
                ids.Add(player.Id);
                if (!outcomeRows.TryGetValue(player.Id, out var row))
                    outcomeRows[player.Id] = row = CreateResultRow(player.Id);
                row.Player.transform.parent.gameObject.SetActive(true);
                row.Player.text = $"P{player.Id + 1:00}  {player.Name}" + (player.Id == localPlayerId ? " (Bạn)" : "");
                row.Role.text = FinalRoleLabel(player);
                row.Faction.text = FinalFactionLabel(player);
                row.Avatar.sprite = LobbyAvatar(player.CharacterIndex);
                row.Avatar.enabled = row.Avatar.sprite != null;
                string result = draw ? "HÒA" : player.Won ? "THẮNG" : "THUA";
                row.Status.text = showFinalVote
                    ? $"{(voteTallies.TryGetValue(player.Id, out int votes) ? votes : 0)} phiếu" +
                      (player.Id == voteEliminatedId ? " • BỊ LOẠI" : voteIsTie ? " • HÒA PHIẾU" : "")
                    : (player.Alive ? "Còn sống" : "Đã bị loại") + " • " + result;
                row.Badge.color = draw ? new Color32(122, 102, 60, 255)
                    : player.Won ? new Color32(57, 105, 65, 255) : new Color32(135, 66, 56, 255);
            }
            foreach (var pair in outcomeRows)
                if (!ids.Contains(pair.Key)) pair.Value.Player.transform.parent.gameObject.SetActive(false);
        }

        private void BuildEndgamePresentation()
        {
            var root = new GameObject("Thuong Match Result Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            endgameCanvas = root.GetComponent<Canvas>();
            endgameCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            endgameCanvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            var shade = LobbyPanel("Result shade", root.transform, Vector2.zero, Vector2.one, false);
            shade.GetComponent<Image>().color = new Color(.045f, .03f, .025f, .94f);
            var card = LobbyPanel("Result parchment", shade, new Vector2(.045f, .045f), new Vector2(.955f, .955f));
            outcomeTitle = LobbyText("Personal outcome", card, "", new Vector2(.035f, .87f), new Vector2(.965f, .98f), 50, TextAlignmentOptions.Center);
            outcomeWinner = LobbyText("Winning faction", card, "", new Vector2(.035f, .805f), new Vector2(.965f, .88f), 30, TextAlignmentOptions.Center);
            outcomeReason = LobbyText("Win reason", card, "", new Vector2(.045f, .70f), new Vector2(.955f, .805f), 23, TextAlignmentOptions.Center);
            outcomeReason.textWrappingMode = TextWrappingModes.Normal;
            outcomeLocal = LobbyText("Your result", card, "", new Vector2(.045f, .595f), new Vector2(.955f, .70f), 22, TextAlignmentOptions.Center);
            outcomeLocal.textWrappingMode = TextWrappingModes.Normal;
            outcomeVote = LobbyText("Final vote summary", card, "", new Vector2(.045f, .52f), new Vector2(.955f, .595f), 19, TextAlignmentOptions.Center);
            outcomeVote.textWrappingMode = TextWrappingModes.Normal;

            var table = LobbyPanel("Final player table", card, new Vector2(.025f, .145f), new Vector2(.975f, .515f));
            var columns = LobbyPanel("TopBar", table, new Vector2(.01f, .84f), new Vector2(.99f, .99f));
            LobbyText("Player column", columns, "NGƯỜI CHƠI", new Vector2(.055f, 0), new Vector2(.355f, 1), 19);
            LobbyText("Role column", columns, "VAI TRÒ", new Vector2(.365f, 0), new Vector2(.56f, 1), 19);
            LobbyText("Faction column", columns, "PHE CUỐI VÁN", new Vector2(.57f, 0), new Vector2(.745f, 1), 18);
            outcomeColumn = LobbyText("Outcome column", columns, "", new Vector2(.755f, 0), new Vector2(.99f, 1), 17, TextAlignmentOptions.Center);
            var viewport = LobbyPanel("Final players viewport", table, new Vector2(.01f, .025f), new Vector2(.965f, .82f), false);
            viewport.GetComponent<Image>().color = new Color(.30f, .20f, .12f, .12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Final players", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            outcomeContent = content.GetComponent<RectTransform>(); outcomeContent.SetParent(viewport, false);
            outcomeContent.anchorMin = new Vector2(0, 1); outcomeContent.anchorMax = Vector2.one;
            outcomeContent.pivot = new Vector2(.5f, 1); outcomeContent.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4; layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = table.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = outcomeContent; scroll.horizontal = false;
            scroll.scrollSensitivity = 35; scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = LobbyPanel("Final players scrollbar", table, new Vector2(.969f, .025f), new Vector2(.99f, .82f), false);
            track.GetComponent<Image>().color = new Color32(96, 66, 42, 255);
            var handle = LobbyPanel("Handle", track, Vector2.zero, Vector2.one, false);
            handle.GetComponent<Image>().color = new Color32(233, 195, 125, 255);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop; scroll.verticalScrollbar = scrollbar;

            outcomeVoteButton = LobbyButton("View final vote", card, "XEM VOTE CUỐI", new Vector2(.40f, .035f), new Vector2(.66f, .115f),
                () => showFinalVote = !showFinalVote);
            outcomeVoteButtonLabel = outcomeVoteButton.GetComponentInChildren<TMP_Text>();
            LobbyButton("Leave finished match", card, "RỜI PHÒNG", new Vector2(.69f, .035f), new Vector2(.95f, .115f), LeaveSession);
            LobbyText("Result help", card, "Cuộn để xem đủ người chơi.\nRời phòng để tạo hoặc tham gia ván mới.",
                new Vector2(.035f, .025f), new Vector2(.38f, .125f), 17).textWrappingMode = TextWrappingModes.Normal;
        }

        private ResultRow CreateResultRow(int id)
        {
            var root = LobbyPanel("Final Player " + id, outcomeContent, Vector2.zero, Vector2.one, false);
            root.GetComponent<Image>().color = id % 2 == 0 ? new Color32(246, 222, 175, 255) : new Color32(229, 202, 153, 255);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = 64;
            var avatar = LobbyPanel("Final avatar", root, new Vector2(.005f, .10f), new Vector2(.06f, .90f), false).GetComponent<Image>();
            avatar.preserveAspect = true; avatar.raycastTarget = false;
            var name = LobbyText("Final name", root, "", new Vector2(.065f, .05f), new Vector2(.355f, .95f), 21);
            name.richText = false;
            var role = LobbyText("Final role", root, "", new Vector2(.365f, .05f), new Vector2(.56f, .95f), 18);
            var faction = LobbyText("Final faction", root, "", new Vector2(.57f, .05f), new Vector2(.745f, .95f), 18);
            var badge = LobbyPanel("Final outcome badge", root, new Vector2(.755f, .13f), new Vector2(.99f, .87f), false).GetComponent<Image>();
            var status = LobbyText("Final status", badge.transform, "", Vector2.zero, Vector2.one, 17, TextAlignmentOptions.Center);
            status.color = ThuongMedievalTheme.Cream;
            return new ResultRow { Player = name, Role = role, Faction = faction, Status = status, Avatar = avatar, Badge = badge };
        }

        private static string FinalRoleLabel(ThuongFinalPlayer player) => player.Role switch
        {
            RoleType.Villager => "DÂN LÀNG", RoleType.DogSpirit => "DOG SPIRIT", RoleType.Seer => "TIÊN TRI",
            RoleType.VillageGuardian => "BẢO VỆ LÀNG", RoleType.Mayor => "TRƯỞNG LÀNG", RoleType.Idiot => "KẺ NGỐC",
            RoleType.WhiteHound => player.Awakened ? "BẠCH KHUYỂN THỨC TỈNH" : "BẠCH KHUYỂN",
            RoleType.WhiteWolf => "SÓI TRẮNG", RoleType.SerpentSpirit => "XÀ TINH", RoleType.Ogre => "QUỶ KHỔNG LỒ",
            RoleType.Hunter => "THỢ SĂN", RoleType.Shaman => "PHÁP SƯ", RoleType.WeaverOfFate => "DỆT DUYÊN",
            RoleType.Cursed => "KẺ NGUYỀN", RoleType.Brat => "ĐỨA TRẺ", RoleType.Madman => "KẺ ĐIÊN",
            RoleType.FoxSpirit => "HỒ LY", RoleType.Killer => "SÁT NHÂN", _ => player.Role.ToString()
        };

        private static string FinalFactionLabel(ThuongFinalPlayer player)
        {
            if (player.Awakened && (player.Role == RoleType.WhiteHound || player.Role == RoleType.WhiteWolf)) return "SÓI TRẮNG";
            return player.Faction switch { FactionType.Villager => "DÂN LÀNG", FactionType.Monster => "MA SÓI", _ => "TRUNG LẬP" };
        }

        private static string EndgamePhaseLabel(GamePhase phase) => phase switch
        {
            GamePhase.DayStart or GamePhase.Task => "BAN NGÀY", GamePhase.Night or GamePhase.ResolveNight => "BAN ĐÊM",
            GamePhase.Discussion => "THẢO LUẬN", GamePhase.Voting or GamePhase.ResolveVote or GamePhase.DayResolution => "BỎ PHIẾU",
            _ => "KẾT THÚC"
        };

        private string WinReasonLabel(ThuongWinReason reason) => reason switch
        {
            ThuongWinReason.Tasks => "Dân làng đã hoàn thành 100% tổng nhiệm vụ.",
            ThuongWinReason.NoThreats => "Không còn Ma Sói, Sát nhân hoặc Bạch Khuyển thức tỉnh còn sống.",
            ThuongWinReason.Parity => "Số Ma Sói còn sống đạt thế cân bằng với những người còn lại; không còn phe tấn công đối địch.",
            ThuongWinReason.NoOpponents => "Ma Sói đã loại toàn bộ người chơi ngoài phe; chỉ còn phe Ma Sói sống sót.",
            ThuongWinReason.DayLimit => "Đã hết số ngày quy định, nhiệm vụ chưa hoàn tất và Ma Sói vẫn còn sống.",
            ThuongWinReason.LoversLastTwo => "Cặp đôi có liên kết hai chiều là hai người cuối cùng còn sống.",
            ThuongWinReason.MadmanVoted => "Kẻ Điên đã bị bỏ phiếu loại trong ngày đầu tiên.",
            ThuongWinReason.FoxCharmed => "Hồ Ly còn sống, đã mê hoặc ít nhất hai mục tiêu và toàn bộ mục tiêu mê hoặc đều còn sống.",
            ThuongWinReason.WhiteHoundSurvived => $"Chỉ còn một Bạch Khuyển thức tỉnh, hết Ma Sói / Sát nhân và còn tối đa {winSettings.specialWinVillageLimit} dân.",
            ThuongWinReason.KillerSurvived => $"Chỉ còn một Sát nhân, hết Ma Sói / Bạch Khuyển thức tỉnh và còn tối đa {winSettings.specialWinVillageLimit} dân.",
            ThuongWinReason.NobodyAlive => "Không còn người chơi nào sống sau khi giải quyết toàn bộ hiệu ứng.",
            ThuongWinReason.Stalemate => "Chỉ còn role trung lập không thể hoàn thành điều kiện thắng riêng.",
            _ => "Đã hết số ngày quy định mà không còn phe đủ điều kiện chiến thắng."
        };
    }
}
