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
        public bool HasLocalRole => rolesAssigned && privateStateReceived && privateMatchEpoch == matchEpoch;
        public bool RolesAssigned => rolesAssigned;
        public bool ShowRoleCard => HasLocalRole && !matchStarted && localRoleCardOpen;
        public int LocalPlayerId => localPlayerId;
        public GameState VisibleState => visibleState;
        public float PhaseTimeRemaining => visibleRemaining;
        public bool LocalVotedNoElimination => HasLocalRole && (privateFlags & 2) != 0 && localVotedTarget < 0;
        public bool CanVoteNoElimination => HasLocalRole && visibleState == GameState.Voting &&
            (privateFlags & 1) != 0 && (privateFlags & 2) == 0 && (privateFlags & 32) != 0;
        public string Feedback => (string.IsNullOrEmpty(localActionFeedback) ? status : localActionFeedback) +
            (string.IsNullOrEmpty(LocalStatusSummary) ? "" : "\n" + LocalStatusSummary);
        public bool HasReadRole => (privateFlags & 2048) != 0;
        public bool HasGoodCharm => (privateFlags & 256) != 0;
        public bool HasBadCharm => (privateFlags & 512) != 0;
        public bool HasActiveLocalAbility => HasLocalRole && (privateFlags & 8) != 0;
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
        private int voteNoEliminationVotes;
        private bool voteIsTie;
        private bool voteNoEliminationWon;
        private float voteResultUntil;
        private Button closeVoteResult;
        private Button reopenVoteResult;
        private TMP_Text realtimePrompt;

        private void PreparePresentation()
        {
            sceneHud = SceneComponent<GameHUD>();
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
        public void RequestNoEliminationVote() => SendAction(ThuongOnlineAction.NoEliminationVote, -1);

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
            else if (ActiveMatchMode == ThuongMatchMode.RealtimeProximity)
            { reason = "Dùng E khi đứng gần mục tiêu; kỹ năng chọn toàn map đã tắt."; return false; }
            else if (CanSetHunterTrap) { reason = "Chọn mục tiêu cho bẫy."; return true; }
            else if ((privateFlags & 1) == 0) reason = "Bạn đã bị loại.";
            else if ((privateFlags & 8) == 0) reason = "Vai trò này có kỹ năng nội tại.";
            else if (visiblePhase != localAbilityPhase) reason = localAbilityPhase == GamePhase.Night
                ? "Kỹ năng chỉ dùng được vào ban đêm." : "Hiện không phải giai đoạn dùng kỹ năng này.";
            else if ((privateFlags & 64) != 0) reason = "Bạn đang bị khóa kỹ năng.";
            else if ((privateFlags & 4) != 0) reason = "Bạn đã dùng kỹ năng trong đêm này.";
            else if (privateRole == RoleType.WhiteHound && (privateFlags & 128) == 0) reason = "Sói Trắng chưa thức tỉnh.";
            else if (privateRole == RoleType.WeaverOfFate && (privateFlags & 1024) != 0) reason = "Bạn đã dùng Dệt Duyên.";
            else if (privateRole == RoleType.Killer && IsKillerRestrictedThisNight())
                reason = "Sát Nhân chỉ có thể tấn công vào đêm chẵn.";
            else { reason = "Chọn kỹ năng rồi chọn mục tiêu."; return true; }
            return false;
        }

        private bool IsKillerRestrictedThisNight()
        {
            // Remote clients receive the authoritative day in the online
            // snapshot; their local gameplay managers do not advance phases.
            if (network != null && !network.IsServer)
                return visibleDay <= 0 || visibleDay % 2 != 0;

            int night = NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0
                ? NightManager.Instance.CurrentNightNumber
                : (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentNightCycle.Value > 0
                    ? NetworkPhaseSync.Instance.CurrentNightCycle.Value
                    : (game != null ? game.currentDay : 0));
            return night <= 0 || night % 2 != 0;
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
            if (!actor.isAlive || game.currentPhase != GamePhase.Night || actor.hasUseNightAction ||
                actor.roleType != RoleType.WeaverOfFate || actor.HasEffect(StatusEffectType.Silenced) ||
                !roles.playerRoles.TryGetValue(actor.playerID, out BaseRole role) || !(role is WeaverOfFateRole weaver)) return false;
            if (!weaver.TryMakeLovers(first, second, out feedback)) return false;
            actor.NotifyChanged(); players.GetplayerByID(first)?.NotifyChanged(); players.GetplayerByID(second)?.NotifyChanged();
            return true;
        }

        private void ApplyLocalPrivateData()
        {
            if (!HasLocalRole || network.IsServer) return;
            PlayerData local = players?.GetplayerByID(localPlayerId);
            if (local == null) return; // Private state can arrive before the public roster.
            local.roleType = privateRole;
            local.faction = privateFaction;
            local.roleDefinition = roles != null ? roles.roleDefinitions?.Find(privateRole) : null;
            local.hasVoted = (privateFlags & 2) != 0;
            local.hasUseNightAction = (privateFlags & 4) != 0;
            local.hasHunterTrap = (privateFlags & 16) != 0;
            local.isWhiteHoundAwakened = (privateFlags & 128) != 0;
            local.useNetworkCanVote = true; local.networkCanVote = (privateFlags & 32) != 0;
            local.votePower = localBaseVotePower;
            local.effects = localEffects.Select(effect => new StatusEffectInstance(effect.Type, effect.Strength,
                effect.RemainingTicks, effect.TickOnPhase)).ToList();
            local.isCursed = local.HasEffect(StatusEffectType.Cursed);
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
            voteNoEliminationVotes = result.NoEliminationVotes;
            voteNoEliminationWon = result.NoEliminationWon;
            voteResultMessage = result.NoEliminationVotes > 0
                ? $"{result.Message} Không loại ai: {result.NoEliminationVotes} phiếu."
                : result.Message;
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
            writer.WriteValueSafe(voteNoEliminationVotes);
            writer.WriteValueSafe(voteNoEliminationWon);
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
            reader.ReadValueSafe(out voteNoEliminationVotes);
            reader.ReadValueSafe(out voteNoEliminationWon);
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
            voteNoEliminationVotes = 0;
            voteNoEliminationWon = false;
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
            if (!player.isAlive) return label + " • Đã bị loại";
            return label + (lobby.Any(member => member.PlayerId == player.playerID && member.Offline) ? " • Mất kết nối" : " • Còn sống");
        }

        public void RefreshHud(GameHUD hud)
        {
            // Close a task modal before any phase or personal-result UI becomes visible.
            if (LocalMinigameInvalid) CloseLocalMinigame();
            SetGameplayHudVisible(matchStarted);
            RefreshEndgamePresentation();
            if (!matchStarted)
            {
                RefreshSpectatorHud(hud);
                RefreshNotebookHud();
                return;
            }
            hud.localVoterID = localPlayerId;
            EnsureMatchButtons(hud);
            RefreshNotebookHud();
            RefreshRealtimePrompt(hud);
            if (hud.progressSlider != null) { hud.progressSlider.maxValue = 100; hud.progressSlider.value = visibleProgress; }
            if (hud.dailyProgressSlider != null) { hud.dailyProgressSlider.maxValue = 100; hud.dailyProgressSlider.value = visibleDailyProgress; }
            if (hud.progressText != null) hud.progressText.text = $"{visibleProgress:0}%";
            if (hud.dailyProgressText != null) hud.dailyProgressText.text = $"HÔM NAY  |  {visibleDailyProgress:0}%";
            if (hud.phaseText != null)
            {
                int offline = lobby.Count(member => member.Offline);
                hud.phaseText.text = $"NGÀY {visibleDay}  /  {PhaseName(visibleState)}" +
                    (publicEventDay == visibleDay && publicEvent != GameEventType.None ? "  •  " + ThuongEventPresentationCatalog.Name(publicEvent) : "") +
                    (offline == 0 ? "" : $"  •  {offline} mất kết nối");
            }
            RefreshEventBadge(hud);
            int seconds = Mathf.CeilToInt(Mathf.Max(0, visibleRemaining));
            if (hud.timerText != null) hud.timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
            if (hud.taskListText != null)
            {
                var text = new StringBuilder();
                for (int i = 0; i < stations.Length && i < 16; i++)
                    if ((activeTaskMask & (1 << i)) != 0 && stations[i].task != null)
                        text.AppendLine(((completedTaskMask & (1 << i)) != 0 || (personalTaskMask & (1 << i)) != 0 ? "[x]  " : "[ ]  ") +
                            stations[i].task.taskName + (taskTargets[i] > 0 ? $"  {taskDone[i]}/{taskTargets[i]}" : ""));
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
                    : (privateFlags & 2) != 0 ? localVotedTarget < 0
                        ? "Đã chọn Không loại ai. Đang chờ kết quả."
                        : $"Đã chọn P{localVotedTarget + 1:00}. Đang chờ kết quả."
                    : $"Bạn có {LocalVoteWeight} phiếu. Chọn người chơi hoặc Không loại ai.";
            if (closeVoteResult != null) closeVoteResult.gameObject.SetActive(ShowingVoteResult);
            if (reopenVoteResult != null) reopenVoteResult.gameObject.SetActive(voteResultRevision > 0 && !meeting && visibleState != GameState.GameOver);
            // Online has a personal result overlay. The offline result panel stays unchanged.
            if (hud.resultPanel != null) hud.resultPanel.SetActive(false);
            hud.SetOnlineFeedback(PhaseLabel(visibleState), Feedback);
            RefreshSpectatorHud(hud);
        }

        private void EnsureMatchButtons(GameHUD hud)
        {
            if (reopenVoteResult != null) return;
            reopenVoteResult = MatchButton(hud, "Xem kết quả vote", hud.transform,
                new Vector2(.60f, .014f), new Vector2(.76f, .071f), () => voteResultUntil = Time.unscaledTime + 10f);
            MatchButton(hud, "Rời phòng", hud.transform, new Vector2(.77f, .014f), new Vector2(.86f, .071f), LeaveSession);
            MatchButton(hud, "HƯỚNG DẪN", hud.transform, new Vector2(.46f, .014f), new Vector2(.59f, .071f), OpenMatchTutorial);
            EnsureNotebookUi(hud);
            EnsureRealtimePrompt(hud);
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
            if (instructions != null) instructions.text = ActiveMatchMode == ThuongMatchMode.RealtimeProximity
                ? "WASD / MŨI TÊN  Di chuyển\nE ban ngày  Nhiệm vụ hoặc báo xác ở gần\nE ban đêm  Kỹ năng gần của Khuyển tinh, Bảo vệ, Tiên tri\n\nBáo xác mở cuộc họp."
                : "WASD / MŨI TÊN  Di chuyển\nE  Thực hiện nhiệm vụ\nTAB  Bản đồ / chọn đường đi\nROLE / KỸ NĂNG  Xem vai trò\n\nMỗi loại nhiệm vụ góp một lần/ngày.";
            TMP_Text footer = hud.transform.Find("Footer")?.GetComponent<TMP_Text>();
            if (footer != null)
            {
                // Leave a clear gap before the HƯỚNG DẪN button (starts at x=.46).
                footer.rectTransform.anchorMax = new Vector2(.43f, footer.rectTransform.anchorMax.y);
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

        private void EnsureRealtimePrompt(GameHUD hud)
        {
            if (realtimePrompt != null) return;
            var prompt = new GameObject("Nearby interaction prompt", typeof(RectTransform), typeof(TextMeshProUGUI));
            prompt.transform.SetParent(hud.transform, false);
            var rect = prompt.GetComponent<RectTransform>();
            // Keep the action hint above the footer phase/status panel and below task guidance.
            rect.anchorMin = new Vector2(.22f, .205f); rect.anchorMax = new Vector2(.74f, .255f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            realtimePrompt = prompt.GetComponent<TMP_Text>();
            realtimePrompt.font = hud.phaseText != null ? hud.phaseText.font : null;
            realtimePrompt.fontSize = 20; realtimePrompt.alignment = TextAlignmentOptions.Center;
            realtimePrompt.enableAutoSizing = true; realtimePrompt.fontSizeMin = 13; realtimePrompt.fontSizeMax = 20;
            realtimePrompt.raycastTarget = false;
            ThuongMedievalTheme.Active?.StyleText(realtimePrompt);
        }

        private void RefreshRealtimePrompt(GameHUD hud)
        {
            if (realtimePrompt == null) return;
            if (!matchStarted || NotebookIsOpen || TutorialIsOpen || !HasLocalRole)
            { realtimePrompt.gameObject.SetActive(false); return; }
            realtimePrompt.gameObject.SetActive(true);
            if (!LocalPlayerAlive)
            { realtimePrompt.text = "ĐÃ BỊ LOẠI · Dùng bảng quan sát để theo dõi người còn sống"; return; }
            if (InteractionMenuOpen)
            {
                var menuText = new StringBuilder("GIỮ E · CHỌN TƯƠNG TÁC\n");
                for (int i = 0; i < interactionOptions.Count; i++)
                    menuText.AppendLine((i == interactionMenuSelection ? "▶ " : "   ") + interactionOptions[i].Label);
                realtimePrompt.text = menuText.ToString();
                return;
            }
            if (hasSelectedInteraction)
            { realtimePrompt.text = $"ĐÃ CHỌN · {selectedInteraction.Label} · NHẤN E ĐỂ THỰC HIỆN"; return; }
            if (!avatars.TryGetValue(localPlayerId, out ThuongOnlineAvatar localAvatar))
            { realtimePrompt.text = string.Empty; return; }

            if (visibleState == GameState.Day)
            {
                if (ActiveMatchMode == ThuongMatchMode.RealtimeProximity)
                {
                    OnlineCorpse corpse = onlineCorpses.OrderBy(item => Vector2.Distance(localAvatar.Root.transform.position, item.Position))
                        .FirstOrDefault(item => Vector2.Distance(localAvatar.Root.transform.position, item.Position) <= RealtimeInteractionRange &&
                            SelectedMap != null && Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap,
                                localAvatar.Root.transform.position, item.Position));
                    if (corpse != null)
                    {
                        int nearbyTask = NearestAvailableTask(localAvatar.Root.transform.position, out float nearbyTaskDistance);
                        bool taskInRange = nearbyTask >= 0 && nearbyTaskDistance <= stations[nearbyTask].interactionRadius + .25f;
                        realtimePrompt.text = "E · BÁO XÁC" + (taskInRange ? " · GIỮ E ĐỂ CHỌN NHIỆM VỤ" : string.Empty);
                        return;
                    }
                }
                int stationId = NearestAvailableTask(localAvatar.Root.transform.position, out float taskDistance);
                if (stationId >= 0 && taskDistance <= stations[stationId].interactionRadius + .25f)
                    realtimePrompt.text = "E · NHIỆM VỤ · " + stations[stationId].task.taskName;
                else realtimePrompt.text = "BAN NGÀY · Đi đến trạm nhiệm vụ";
                return;
            }
            if (visibleState == GameState.Night)
            {
                if (ActiveMatchMode != ThuongMatchMode.RealtimeProximity)
                { realtimePrompt.text = "BAN ĐÊM · Mở ROLE / KỸ NĂNG để chọn hành động"; return; }
                bool corpseNearby = onlineCorpses.Any(item => Vector2.Distance(localAvatar.Root.transform.position, item.Position) <= RealtimeInteractionRange &&
                    SelectedMap != null && Thuong.Maps.ThuongMapCollision.HasLine(SelectedMap, localAvatar.Root.transform.position, item.Position));
                if (privateRole is not (RoleType.DogSpirit or RoleType.VillageGuardian or RoleType.Seer))
                { realtimePrompt.text = corpseNearby ? "E · BÁO XÁC" : "BAN ĐÊM · Vai trò này không có kỹ năng E trong mode này"; return; }
                int targetId = NearestAlivePlayer(localAvatar.Root.transform.position, out _);
                if (targetId >= 0)
                {
                    ThuongOnlinePublicPlayer target = publicPlayers.First(item => item.PlayerId == targetId);
                    string action = privateRole switch
                    { RoleType.DogSpirit => "TẤN CÔNG", RoleType.VillageGuardian => "BẢO VỆ", _ => "SOI" };
                    realtimePrompt.text = $"E · {action} · {target.Name}" + (corpseNearby ? " · GIỮ E ĐỂ CHỌN BÁO XÁC" : string.Empty);
                }
                else realtimePrompt.text = corpseNearby ? "E · BÁO XÁC" : "BAN ĐÊM · Tiến gần một người chơi, không xuyên tường";
                return;
            }
            realtimePrompt.text = string.Empty;
        }

        private int NearestAvailableTask(Vector2 position, out float distance)
        {
            int best = -1;
            distance = float.MaxValue;
            for (int i = 0; i < stations.Length && i < 16; i++)
            {
                ushort bit = (ushort)(1 << i);
                if ((activeTaskMask & bit) == 0 || (completedTaskMask & bit) != 0 || (personalTaskMask & bit) != 0 || stations[i].task == null) continue;
                float candidate = Vector2.Distance(position, stations[i].transform.position);
                if (candidate >= distance) continue;
                best = i; distance = candidate;
            }
            return best;
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
