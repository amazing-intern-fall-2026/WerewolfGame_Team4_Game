using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Thuong.Online
{
    // The scene keeps its prototype Canvas, portraits, role cards and vote rows.
    // Only this session reads authoritative state or sends gameplay requests.
    public sealed partial class ThuongOnlineSession
    {
        public static ThuongOnlineSession Active { get; private set; }
        public bool MatchStarted => matchStarted;
        public bool HasLocalRole => rolesAssigned && privateStateReceived;
        public bool RolesAssigned => rolesAssigned;
        public bool ShowRoleCard => HasLocalRole && !matchStarted && localRoleCardOpen;
        public int LocalPlayerId => localPlayerId;
        public GameState VisibleState => visibleState;
        public float PhaseTimeRemaining => visibleRemaining;
        public string Feedback => string.IsNullOrEmpty(localActionFeedback) ? status : localActionFeedback;
        public bool HasReadRole => (privateFlags & 2048) != 0;
        public bool HasGoodCharm => (privateFlags & 256) != 0;
        public bool HasBadCharm => (privateFlags & 512) != 0;
        public bool CanSetHunterTrap => HasLocalRole && visibleState == GameState.Discussion && (privateFlags & 16) != 0;

        private GameHUD sceneHud;
        private Canvas sceneHudCanvas;
        private GraphicRaycaster sceneHudRaycaster;
        private bool privateStateReceived;
        private bool localRoleCardOpen;
        private bool localRoleConfirmed;
        private string localActionFeedback = string.Empty;
        private int localVotedTarget = -1;
        private VoteManager observedVoteManager;
        private AbilitySystem observedNetworkAbilities;
        private uint voteResultRevision;
        private readonly Dictionary<int, int> voteTallies = new();
        private string voteResultMessage = string.Empty;
        private int voteResultDay;
        private int voteEliminatedId = -1;
        private bool voteIsTie;
        private float voteResultUntil;
        private Button closeVoteResult;
        private Button reopenVoteResult;

        private void PreparePresentation()
        {
            sceneHud = FindAnyObjectByType<GameHUD>(FindObjectsInactive.Include);
            if (sceneHud == null) return;
            sceneHud.gameObject.SetActive(true);
            sceneHud.enabled = true;
            sceneHudCanvas = sceneHud.GetComponentInParent<Canvas>();
            sceneHudRaycaster = sceneHudCanvas != null ? sceneHudCanvas.GetComponent<GraphicRaycaster>() : null;
            SetGameplayHudVisible(false);
        }

        private void SetGameplayHudVisible(bool visible)
        {
            if (sceneHudCanvas != null) sceneHudCanvas.enabled = visible;
            if (sceneHudRaycaster != null) sceneHudRaycaster.enabled = visible;
        }

        private void DrawLobbyCapacity(bool editable)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"Sức chứa phòng: {lobbyCapacity} người", GUILayout.MinWidth(180));
            GUI.enabled = editable;
            if (GUILayout.Button("−", GUILayout.Width(42))) SetLobbyCapacity(lobbyCapacity - 1);
            if (GUILayout.Button("+", GUILayout.Width(42))) SetLobbyCapacity(lobbyCapacity + 1);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void SetLobbyCapacity(int capacity)
        {
            if (roomLocked || network.IsListening && !network.IsServer) return;
            lobbyCapacity = Mathf.Clamp(capacity, Mathf.Max(2, serverPlayers.Count), MaxPlayers);
            if (network.IsServer) rosterDirty = true;
        }

        public void ViewLocalRole()
        {
            if (!connected || busy || !HasLocalRole || matchStarted) return;
            localRoleCardOpen = true;
        }

        public void ReadLocalRole()
        {
            if (!HasLocalRole || matchStarted || !localRoleCardOpen) return;
            localRoleCardOpen = false;
            localRoleConfirmed = true;
            SendAction(ThuongOnlineAction.RevealRead, -1);
        }
        public void RequestVote(int targetId) => SendAction(ThuongOnlineAction.Vote, targetId);

        public void RequestAbility(int targetId, int secondTargetId = -1, bool shaman = false, bool goodCharm = false, bool trap = false)
        {
            ThuongOnlineAction action = trap ? ThuongOnlineAction.HunterTrap
                : shaman ? (goodCharm ? ThuongOnlineAction.ShamanGoodCharm : ThuongOnlineAction.ShamanBadCharm)
                : privateRole == RoleType.WeaverOfFate ? ThuongOnlineAction.MakeLovers : ThuongOnlineAction.NightAbility;
            SendAction(action, targetId, secondTargetId);
        }

        public bool CanUseLocalAbility(out string reason)
        {
            if (!HasLocalRole) reason = "Đang chờ phân vai trò.";
            else if (visibleState == GameState.GameOver) reason = "Ván chơi đã kết thúc.";
            else if (!matchStarted) reason = "Trở về lobby và bấm Ready để bắt đầu ván.";
            else if (CanSetHunterTrap) { reason = "Chọn mục tiêu cho bẫy."; return true; }
            else if ((privateFlags & 1) == 0) reason = "Bạn đã bị loại.";
            else if ((privateFlags & 8) == 0) reason = "Vai trò này có kỹ năng nội tại.";
            else if (visibleState != GameState.Night) reason = "Kỹ năng chỉ dùng được vào ban đêm.";
            else if ((privateFlags & 64) != 0) reason = "Bạn đang bị khóa kỹ năng.";
            else if ((privateFlags & 4) != 0) reason = "Bạn đã dùng kỹ năng trong đêm này.";
            else if (privateRole == RoleType.WhiteHound && (privateFlags & 128) == 0) reason = "Sói Trắng chưa thức tỉnh.";
            else if (privateRole == RoleType.WeaverOfFate && (privateFlags & 1024) != 0) reason = "Bạn đã dùng Dệt Duyên.";
            else { reason = "Chọn kỹ năng rồi chọn mục tiêu."; return true; }
            return false;
        }

        public bool CanVoteFor(int targetId)
        {
            return HasLocalRole && visibleState == GameState.Voting &&
                (privateFlags & 32) != 0 && (privateFlags & 2) == 0 &&
                publicPlayers.Any(item => item.PlayerId == targetId && item.Alive);
        }

        private bool TryMakeLovers(PlayerData actor, int first, int second, out string feedback)
        {
            feedback = "Không thể dùng Dệt Duyên.";
            if (actor.roleType != RoleType.WeaverOfFate || actor.HasEffect(StatusEffectType.Silenced) ||
                !roles.playerRoles.TryGetValue(actor.playerID, out BaseRole role) || !(role is WeaverOfFateRole weaver)) return false;
            return weaver.TryMakeLovers(first, second, out feedback);
        }

        private void ApplyLocalPrivateData()
        {
            if (!privateStateReceived || network.IsServer) return;
            PlayerData local = players?.GetplayerByID(localPlayerId);
            if (local == null) return; // Private state can arrive before the public roster.
            local.roleType = privateRole;
            local.faction = privateFaction;
            local.roleDefinition = roles != null ? roles.roleDefinitions?.Find(privateRole) : null;
            local.hasVoted = (privateFlags & 2) != 0;
            local.hasUseNightAction = (privateFlags & 4) != 0;
            local.hasHunterTrap = (privateFlags & 16) != 0;
            local.isWhiteHoundAwakened = (privateFlags & 128) != 0;
        }

        private void ObserveVoteResults()
        {
            if (observedNetworkAbilities != AbilitySystem.Instance)
            {
                if (observedNetworkAbilities != null) observedNetworkAbilities.AbilityResolved -= HandleNetworkAbilityResolved;
                observedNetworkAbilities = AbilitySystem.Instance;
                if (observedNetworkAbilities != null) observedNetworkAbilities.AbilityResolved += HandleNetworkAbilityResolved;
            }
            if (observedVoteManager == VoteManager.Instance) return;
            if (observedVoteManager != null) observedVoteManager.VoteResolved -= HandleVoteResolved;
            observedVoteManager = VoteManager.Instance;
            if (observedVoteManager != null) observedVoteManager.VoteResolved += HandleVoteResolved;
        }

        private void HandleNetworkAbilityResolved(AbilityResolution result)
        {
            if (!network.IsServer || !matchStarted || result.Actor == null) return;
            ThuongOnlineServerPlayer owner = serverPlayers.Values.FirstOrDefault(member => member.PlayerId == result.Actor.playerID);
            if (owner != null) Reply(owner, result.Message);
        }

        private void HandleVoteResolved(VoteResolution result)
        {
            if (!network.IsServer || !matchStarted || matchResult != null) return;
            voteResultRevision++;
            voteResultDay = game.currentDay;
            voteEliminatedId = result.Eliminated != null ? result.Eliminated.playerID : -1;
            voteIsTie = result.IsTie;
            voteResultMessage = result.Message;
            voteTallies.Clear();
            foreach (var tally in result.Tallies) voteTallies[tally.Key] = tally.Value;
            voteResultUntil = Time.unscaledTime + 10f;
            // The next snapshot includes this result even if the game has already changed phase.
            nextSnapshotTime = 0;
        }

        private void WriteVoteResult(FastBufferWriter writer)
        {
            writer.WriteValueSafe(voteResultRevision);
            writer.WriteValueSafe(voteResultDay);
            writer.WriteValueSafe(voteEliminatedId);
            writer.WriteValueSafe(voteIsTie);
            FixedString512Bytes message = new(TrimUtf8(voteResultMessage ?? string.Empty, 500));
            writer.WriteValueSafe(message);
            writer.WriteValueSafe((ushort)voteTallies.Count);
            foreach (var tally in voteTallies)
            {
                writer.WriteValueSafe(tally.Key);
                writer.WriteValueSafe(tally.Value);
            }
        }

        private void ReadVoteResult(FastBufferReader reader)
        {
            reader.ReadValueSafe(out uint revision);
            reader.ReadValueSafe(out voteResultDay);
            reader.ReadValueSafe(out voteEliminatedId);
            reader.ReadValueSafe(out voteIsTie);
            reader.ReadValueSafe(out FixedString512Bytes message);
            voteResultMessage = message.ToString();
            reader.ReadValueSafe(out ushort count);
            voteTallies.Clear();
            for (int i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out int playerId);
                reader.ReadValueSafe(out int tally);
                voteTallies[playerId] = tally;
            }
            if (revision > voteResultRevision) voteResultUntil = Time.unscaledTime + 10f;
            voteResultRevision = revision;
        }

        private void ResetVotePresentation()
        {
            voteResultRevision = 0;
            voteTallies.Clear();
            voteResultMessage = string.Empty;
            voteResultUntil = 0;
        }

        private bool ShowingVoteResult => voteResultRevision > 0 && Time.unscaledTime < voteResultUntil &&
            visibleState != GameState.Discussion && visibleState != GameState.Voting && visibleState != GameState.GameOver;

        public string VoteRowLabel(PlayerData player)
        {
            string label = $"P{player.playerID + 1:00}  {player.DisplayName}" +
                (player.playerID == localPlayerId ? " (Bạn)" : string.Empty);
            if (ShowingVoteResult)
                return label + $"  |  {(voteTallies.TryGetValue(player.playerID, out int tally) ? tally : 0)} phiếu" +
                    (player.playerID == voteEliminatedId ? " • BỊ LOẠI" : string.Empty);
            return label + (player.isAlive ? " • Còn sống" : " • Đã bị loại");
        }

        public void RefreshHud(GameHUD hud)
        {
            // Close a task modal before any phase or personal-result UI becomes visible.
            if (LocalMinigameInvalid) CloseLocalMinigame();
            SetGameplayHudVisible(matchStarted);
            RefreshEndgamePresentation();
            if (!matchStarted) return;
            hud.localVoterID = localPlayerId;
            EnsureMatchButtons(hud);
            if (hud.progressSlider != null) { hud.progressSlider.maxValue = 100; hud.progressSlider.value = visibleProgress; }
            if (hud.dailyProgressSlider != null) { hud.dailyProgressSlider.maxValue = 100; hud.dailyProgressSlider.value = visibleDailyProgress; }
            if (hud.progressText != null) hud.progressText.text = $"{visibleProgress:0}%";
            if (hud.dailyProgressText != null) hud.dailyProgressText.text = $"HÔM NAY  |  {visibleDailyProgress:0}%";
            if (hud.phaseText != null) hud.phaseText.text = $"NGÀY {visibleDay}  /  {PhaseName(visibleState)}";
            int seconds = Mathf.CeilToInt(Mathf.Max(0, visibleRemaining));
            if (hud.timerText != null) hud.timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            if (hud.taskListText != null)
            {
                var text = new StringBuilder();
                for (int i = 0; i < stations.Length && i < 16; i++)
                    if ((activeTaskMask & (1 << i)) != 0 && stations[i].task != null)
                        text.AppendLine(((completedTaskMask & (1 << i)) != 0 ? "[x]  " : "[ ]  ") + stations[i].task.taskName);
                hud.taskListText.text = text.Length == 0 ? "Chưa có nhiệm vụ hôm nay." : text.ToString();
            }
            bool meeting = visibleState == GameState.Discussion || visibleState == GameState.Voting;
            if (hud.meetingPanel != null) hud.meetingPanel.SetActive(meeting || ShowingVoteResult);
            if (hud.meetingTitle != null) hud.meetingTitle.text = ShowingVoteResult ? $"KẾT QUẢ VOTE • NGÀY {voteResultDay}"
                : visibleState == GameState.Voting ? "BỎ PHIẾU" : "THẢO LUẬN";
            if (hud.votingStatusText != null)
                hud.votingStatusText.text = ShowingVoteResult ? voteResultMessage
                    : visibleState == GameState.Discussion ? $"Bỏ phiếu bắt đầu sau {seconds} giây."
                    : !HasLocalRole ? "Đang nhận dữ liệu người chơi."
                    : (privateFlags & 1) == 0 ? "Bạn đã bị loại và không thể bỏ phiếu."
                    : (privateFlags & 32) == 0 ? "Bạn đang bị cấm bỏ phiếu."
                    : (privateFlags & 2) != 0 ? $"Đã chọn P{localVotedTarget + 1:00}. Đang chờ kết quả."
                    : "Chọn một người còn sống. Mỗi người bỏ phiếu một lần.";
            if (closeVoteResult != null) closeVoteResult.gameObject.SetActive(ShowingVoteResult);
            if (reopenVoteResult != null) reopenVoteResult.gameObject.SetActive(voteResultRevision > 0 && !meeting && visibleState != GameState.GameOver);
            // Online has a personal result overlay. The offline result panel stays unchanged.
            if (hud.resultPanel != null) hud.resultPanel.SetActive(false);
            hud.SetOnlineFeedback(PhaseLabel(visibleState), Feedback);
        }

        private void EnsureMatchButtons(GameHUD hud)
        {
            if (reopenVoteResult != null) return;
            reopenVoteResult = MatchButton(hud, "Xem kết quả vote", hud.transform,
                new Vector2(.60f, .014f), new Vector2(.76f, .071f), () => voteResultUntil = Time.unscaledTime + 10f);
            MatchButton(hud, "Rời phòng", hud.transform, new Vector2(.77f, .014f), new Vector2(.86f, .071f), LeaveSession);
            if (hud.meetingPanel != null)
            {
                closeVoteResult = MatchButton(hud, "ĐÓNG", hud.meetingPanel.transform,
                    new Vector2(.72f, .84f), new Vector2(.95f, .94f), () => voteResultUntil = 0);
                if (hud.votingStatusText != null)
                    hud.votingStatusText.textWrappingMode = TextWrappingModes.Normal;
                if (hud.meetingTitle != null)
                {
                    hud.meetingTitle.rectTransform.anchorMax = new Vector2(.70f, hud.meetingTitle.rectTransform.anchorMax.y);
                    hud.meetingTitle.enableAutoSizing = true;
                    hud.meetingTitle.fontSizeMin = 18;
                    hud.meetingTitle.fontSizeMax = 32;
                }
            }
            TMP_Text instructions = hud.transform.Find("TaskPanel/Instructions")?.GetComponent<TMP_Text>();
            if (instructions != null) instructions.text = "WASD / MŨI TÊN  Di chuyển\nE  Thực hiện nhiệm vụ\nROLE / KỸ NĂNG  Xem vai trò\n\nThảo luận và bỏ phiếu khi họp.";
            TMP_Text footer = hud.transform.Find("Footer")?.GetComponent<TMP_Text>();
            if (footer != null)
            {
                footer.rectTransform.anchorMax = new Vector2(.58f, footer.rectTransform.anchorMax.y);
                footer.text = $"ONLINE • {publicPlayers.Count} NGƯỜI";
            }
            TMP_Text restart = hud.resultPanel != null ? hud.resultPanel.transform.Find("RestartHint")?.GetComponent<TMP_Text>() : null;
            if (restart != null) restart.text = "Rời phòng để tạo hoặc tham gia ván mới.";
        }

        private static Button MatchButton(GameHUD hud, string label, Transform parent, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
        {
            var root = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var button = root.GetComponent<Button>(); button.targetGraphic = root.GetComponent<Image>();
            button.onClick.AddListener(action);
            ThuongMedievalTheme.Active?.StylePanel(root.GetComponent<Image>(), label);
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(root.transform, false);
            text.rectTransform.anchorMax = Vector2.one; text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            text.font = hud.phaseText != null ? hud.phaseText.font : null;
            text.fontSize = 17; text.text = label; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            ThuongMedievalTheme.Active?.StyleText(text);
            return button;
        }

        private static string PhaseLabel(GameState state) => state switch
        {
            GameState.RoleReveal => "GIỚI THIỆU VAI TRÒ",
            GameState.Day => "BAN NGÀY • TÌM ĐIỂM SÁNG VÀ NHẤN E",
            GameState.Night => "BAN ĐÊM • MỞ ROLE / KỸ NĂNG",
            GameState.Discussion => "THẢO LUẬN",
            GameState.Voting => "BỎ PHIẾU",
            GameState.GameOver => "KẾT THÚC",
            _ => "KIỂM PHIẾU"
        };

        private static string PhaseName(GameState state) => state switch
        {
            GameState.RoleReveal => "VAI TRÒ",
            GameState.Day => "BAN NGÀY",
            GameState.Night => "BAN ĐÊM",
            _ => PhaseLabel(state)
        };

        private static string WinnerLabel(string winner) => winner switch
        {
            "Villagers" => "DÂN LÀNG CHIẾN THẮNG",
            "Werewolves" => "MA SÓI CHIẾN THẮNG",
            "Lovers" => "CẶP ĐÔI CHIẾN THẮNG",
            "White Wolf" => "SÓI TRẮNG CHIẾN THẮNG",
            "Killer" => "SÁT NHÂN CHIẾN THẮNG",
            "Madman" => "KẺ ĐIÊN CHIẾN THẮNG",
            "Fox Spirit" => "HỒ LY CHIẾN THẮNG",
            "Draw" => "VÁN ĐẤU HÒA",
            _ => winner
        };
    }
}
