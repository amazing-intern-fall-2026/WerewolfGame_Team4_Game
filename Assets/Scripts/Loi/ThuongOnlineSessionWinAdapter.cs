using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        /// <summary>
        /// Đồng bộ cấu hình điều kiện thắng Bạch Khuyển thức tỉnh:
        /// Giới hạn số dân làng tối đa để Bạch Khuyển thắng riêng là 1 (parity 1v1 hoặc 0 dân),
        /// ngăn chặn tuyệt đối trường hợp Bạch Khuyển vừa thức tỉnh đã thắng khi còn từ 2 Dân Làng trở lên sống sót.
        /// </summary>
        public void EnforceWhiteHoundWinVillageLimit(int limit = 1)
        {
            if (winSettings != null)
            {
                winSettings.specialWinVillageLimit = Mathf.Clamp(limit, 0, 1);
            }
        }

        /// <summary>
        /// Tra cứu ClientId từ PlayerId thông qua danh sách người chơi trên Server.
        /// </summary>
        public bool TryGetClientIdForPlayer(int playerId, out ulong clientId)
        {
            if (serverPlayers != null)
            {
                foreach (var pair in serverPlayers)
                {
                    if (pair.Value != null && pair.Value.PlayerId == playerId && !pair.Value.Offline)
                    {
                        clientId = pair.Key;
                        return true;
                    }
                }
            }
            clientId = ulong.MaxValue;
            return false;
        }

        /// <summary>
        /// Tra cứu PlayerId của vai trò chỉ định trên Server.
        /// </summary>
        public bool TryGetPlayerIdForRole(RoleType roleType, out int playerId)
        {
            if (serverPlayers != null)
            {
                foreach (var pair in serverPlayers)
                {
                    if (pair.Value != null && !pair.Value.Offline)
                    {
                        var player = players?.GetplayerByID(pair.Value.PlayerId);
                        if (player != null && player.roleType == roleType && player.isAlive)
                        {
                            playerId = pair.Value.PlayerId;
                            return true;
                        }
                    }
                }
                foreach (var pair in serverPlayers)
                {
                    if (pair.Value != null && !pair.Value.Offline)
                    {
                        var player = players?.GetplayerByID(pair.Value.PlayerId);
                        if (player != null && player.roleType == roleType)
                        {
                            playerId = pair.Value.PlayerId;
                            return true;
                        }
                    }
                }
            }
            playerId = -1;
            return false;
        }

        public int GetVisibleDay() => visibleDay;
        public GamePhase GetVisiblePhase() => visiblePhase;
        public RoleType GetPrivateRole() => privateRole;
        public ushort GetPrivateFlags() => privateFlags;
        public bool IsLocalPlayerAliveInMatch() => LocalPlayerAlive;
        public Unity.Netcode.NetworkManager GetNetworkManager() => network != null ? network : Unity.Netcode.NetworkManager.Singleton;

        private const string WinCheckTestModeMessage = "loi.wincheck.testmode.v1";
        private const string MonsterPackTargetMessage = "loi.monster.packtarget.v1";
        private bool deadChatEventsSubscribed;
        private DeathResolver observedDeadChatDeathResolver;
        private VoteManager observedDeadChatVoteManager;
        private NightManager observedMonsterPackNightManager;

        private int syncedMonsterPackTargetId = -1;
        private int syncedMonsterPackSelectorId = -1;
        private RoleType syncedMonsterPackSourceRole = RoleType.DogSpirit;
        private bool syncedMonsterPackLocked;
        private int syncedMonsterPackRevision;
        private int syncedMonsterVotedTargetId = -1;
        private bool syncedMonsterVoteTied;
        private int syncedMonsterVoteHighestCount;
        private bool syncedMonsterCanVote;
        private bool syncedMonsterVoteClosed;
        private bool syncedDogSpiritDecisionActive;
        private bool syncedLocalCanDecideDogSpirit;
        private readonly Dictionary<int, int> syncedMonsterVoteTallies = new();
        private readonly List<int> syncedDogSpiritTiedCandidates = new();
        public event Action MonsterVoteUpdated;

        public int MonsterPackTargetId =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.ActiveMonsterTargetId
                : syncedMonsterPackTargetId;

        public int MonsterPackSelectorId =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.ActiveMonsterSelectorId
                : syncedMonsterPackSelectorId;

        public RoleType MonsterPackSourceRole =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.ActiveMonsterSourceRole
                : syncedMonsterPackSourceRole;

        public bool MonsterPackTargetLocked =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.IsMonsterAttackLockedForCurrentNight()
                : syncedMonsterPackLocked;

        public bool IsMonsterVoteClosed =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.IsMonsterVoteClosed
                : syncedMonsterVoteClosed;

        public bool IsDogSpiritDecisionActive =>
            IsLocalMonsterPackMember &&
            (network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.IsDogSpiritDecisionActive
                : syncedDogSpiritDecisionActive);

        public bool CanLocalPlayerDecideDogSpiritTarget
        {
            get
            {
                if (!IsLocalMonsterPackMember || !IsDogSpiritDecisionActive) return false;
                if (network != null && network.IsServer && NightManager.Instance != null)
                {
                    var localPlayer = players?.GetplayerByID(localPlayerId);
                    return NightManager.CanParticipateInDogSpiritDecision(localPlayer, out _);
                }
                return syncedLocalCanDecideDogSpirit && privateRole == RoleType.DogSpirit &&
                       (privateFlags & 1) != 0 && (privateFlags & 64) == 0;
            }
        }

        public bool IsDogSpiritTiedCandidate(int targetId)
        {
            if (!IsDogSpiritDecisionActive || targetId < 0) return false;
            if (network != null && network.IsServer && NightManager.Instance != null)
                return NightManager.Instance.IsDogSpiritTiedCandidate(targetId);
            return syncedDogSpiritTiedCandidates.Contains(targetId);
        }

        public int MonsterPackStateRevision =>
            network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.MonsterTargetVersion
                : syncedMonsterPackRevision;

        public bool IsLocalMonsterPackMember =>
            HasLocalRole && NightManager.IsMonsterPackRoleType(privateRole, (privateFlags & 128) != 0, privateFaction);

        public int LocalMonsterVotedTargetId
        {
            get
            {
                if (!IsLocalMonsterPackMember) return -1;
                if (network != null && network.IsServer && NightManager.Instance != null)
                {
                    return NightManager.Instance.GetMonsterVotedTarget(localPlayerId);
                }
                return syncedMonsterVotedTargetId;
            }
        }

        public bool IsMonsterVoteTied =>
            IsLocalMonsterPackMember &&
            (network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.IsMonsterVoteTied
                : syncedMonsterVoteTied);

        public int MonsterVoteHighestCount =>
            !IsLocalMonsterPackMember ? 0 :
            (network != null && network.IsServer && NightManager.Instance != null
                ? NightManager.Instance.MonsterVoteHighestCount
                : syncedMonsterVoteHighestCount);

        public bool CanLocalPlayerParticipateInMonsterVote
        {
            get
            {
                if (!IsLocalMonsterPackMember || IsMonsterVoteClosed || MonsterPackTargetLocked) return false;
                if (privateRole == RoleType.SerpentSpirit && visibleDay < 3) return false;
                if (network != null && network.IsServer)
                {
                    var localPlayer = players?.GetplayerByID(localPlayerId);
                    return NightManager.CanParticipateInMonsterPackAttack(
                        localPlayer,
                        NightManager.Instance != null ? NightManager.Instance.CurrentEffectiveNightNumber() : visibleDay,
                        out _);
                }
                return syncedMonsterCanVote && (privateFlags & 1) != 0 && (privateFlags & 64) == 0;
            }
        }

        public int GetMonsterVoteCountForTarget(int targetId)
        {
            if (!IsLocalMonsterPackMember || targetId < 0) return 0;
            if (privateRole == RoleType.SerpentSpirit && visibleDay < 3) return 0;
            if (network != null && network.IsServer && NightManager.Instance != null)
            {
                return NightManager.Instance.GetMonsterVoteCountForTarget(targetId);
            }
            return syncedMonsterVoteTallies.TryGetValue(targetId, out int count) ? count : 0;
        }

        public float VisiblePhaseRemaining => visibleRemaining;

        public Sprite GetOnlinePlayerAvatarSprite(int playerId)
        {
            if (characterCast == null || characterCast.idleSprites == null || characterCast.idleSprites.Length == 0)
                return null;

            int charIdx = -1;
            foreach (var p in publicPlayers)
            {
                if (p.PlayerId == playerId) { charIdx = p.CharacterIndex; break; }
            }
            if (charIdx < 0)
            {
                foreach (var m in lobby)
                {
                    if (m.PlayerId == playerId) { charIdx = m.CharacterIndex; break; }
                }
            }
            if (charIdx < 0) return null;
            int spriteCount = characterCast.idleSprites.Length;
            int safe = ((charIdx % spriteCount) + spriteCount) % spriteCount;
            return characterCast.idleSprites[safe];
        }

        public string FormatMonsterPackStatusSummary()
        {
            if (!IsLocalMonsterPackMember) return string.Empty;
            if (privateRole == RoleType.SerpentSpirit && visibleDay < 3)
            {
                return "Xà Tinh chưa thể tham gia Monster Vote trong 2 đêm đầu.";
            }

            int targetId = MonsterPackTargetId;
            int myVoteId = LocalMonsterVotedTargetId;
            int highest = MonsterVoteHighestCount;
            bool isTie = IsMonsterVoteTied;
            string targetLabel = targetId >= 0 ? FormatOnlinePlayerSlotLabel(targetId) : string.Empty;
            string myVoteLabel = myVoteId >= 0 ? FormatOnlinePlayerSlotLabel(myVoteId) : string.Empty;

            if (IsDogSpiritDecisionActive)
            {
                return CanLocalPlayerDecideDogSpiritTarget
                    ? $"HÒA PHIẾU ({highest} phiếu)! Chó Tinh chọn 1 người trong nhóm hòa để chốt ngay!"
                    : $"HÒA PHIẾU ({highest} phiếu) — Đang chờ Chó Tinh chọn trong nhóm hòa...";
            }

            if (MonsterPackTargetLocked)
            {
                if (targetId >= 0)
                    return $"Đã chốt mục tiêu tấn công: {targetLabel} ({highest} phiếu)!";
                if (isTie)
                    return $"Kết quả Monster Vote: Hòa phiếu ({highest} phiếu) — Không chốt được mục tiêu, không tấn công đêm nay.";
                return "Kết quả Monster Vote: Không có phiếu hợp lệ — Không tấn công đêm nay.";
            }

            string myPart = myVoteId >= 0 ? $"Phiếu của bạn: {myVoteLabel}" : "Bạn chưa bỏ phiếu";
            string leadPart = isTie
                ? $"Đang hòa phiếu ({highest} phiếu) — Nếu hết giờ bằng phiếu, Chó Tinh sẽ chốt"
                : (targetId >= 0 ? $"Dẫn đầu: {targetLabel} ({highest} phiếu)" : "Chưa có phiếu nào");
            return $"{myPart}  •  {leadPart}";
        }

        private string FormatOnlinePlayerSlotLabel(int playerId)
        {
            if (playerId < 0) return string.Empty;
            foreach (var p in publicPlayers)
            {
                if (p.PlayerId == playerId)
                {
                    string prefix = $"P{playerId + 1:00}";
                    string cleanName = string.IsNullOrWhiteSpace(p.Name) ? $"Player {playerId + 1}" : p.Name;
                    return cleanName.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)
                        ? cleanName
                        : $"{prefix}  {cleanName}";
                }
            }
            PlayerData data = players?.GetplayerByID(playerId);
            if (data != null)
            {
                string prefix = $"P{playerId + 1:00}";
                string cleanName = data.DisplayName;
                return cleanName.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase)
                    ? cleanName
                    : $"{prefix}  {cleanName}";
            }
            return $"P{playerId + 1:00}";
        }

        private void RegisterDeadChatMessages()
        {
            if (deadChatEventsSubscribed) return;
            deadChatEventsSubscribed = true;

            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(WinCheckTestModeMessage);
                network.CustomMessagingManager.RegisterNamedMessageHandler(WinCheckTestModeMessage, ReceiveWinCheckTestMode);

                network.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterPackTargetMessage);
                network.CustomMessagingManager.RegisterNamedMessageHandler(MonsterPackTargetMessage, ReceiveMonsterPackTarget);
            }

            if (DeathResolver.Instance != null)
            {
                observedDeadChatDeathResolver = DeathResolver.Instance;
                observedDeadChatDeathResolver.DeathResolved -= HandleDeadChatDeathResolved;
                observedDeadChatDeathResolver.DeathResolved += HandleDeadChatDeathResolved;
            }

            if (VoteManager.Instance != null)
            {
                observedDeadChatVoteManager = VoteManager.Instance;
                observedDeadChatVoteManager.VoteResolved -= HandleDeadChatVoteResolved;
                observedDeadChatVoteManager.VoteResolved += HandleDeadChatVoteResolved;
            }

            if (NightManager.Instance != null)
            {
                observedMonsterPackNightManager = NightManager.Instance;
                observedMonsterPackNightManager.MonsterPackTargetChanged -= HandleServerMonsterPackTargetChanged;
                observedMonsterPackNightManager.MonsterPackTargetChanged += HandleServerMonsterPackTargetChanged;
            }

            if (network != null)
            {
                network.OnClientConnectedCallback -= HandleWinTestModeClientConnected;
                network.OnClientConnectedCallback += HandleWinTestModeClientConnected;
                network.OnClientDisconnectCallback -= HandleDeadChatClientDisconnected;
                network.OnClientDisconnectCallback += HandleDeadChatClientDisconnected;
            }

            BroadcastWinCheckTestModeOnServer();
            BroadcastMonsterPackTargetOnServer();
        }

        private void UnregisterDeadChatMessages()
        {
            deadChatEventsSubscribed = false;

            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(WinCheckTestModeMessage);
                network.CustomMessagingManager.UnregisterNamedMessageHandler(MonsterPackTargetMessage);
            }

            if (observedDeadChatDeathResolver != null)
            {
                observedDeadChatDeathResolver.DeathResolved -= HandleDeadChatDeathResolved;
                observedDeadChatDeathResolver = null;
            }

            if (observedDeadChatVoteManager != null)
            {
                observedDeadChatVoteManager.VoteResolved -= HandleDeadChatVoteResolved;
                observedDeadChatVoteManager = null;
            }

            if (observedMonsterPackNightManager != null)
            {
                observedMonsterPackNightManager.MonsterPackTargetChanged -= HandleServerMonsterPackTargetChanged;
                observedMonsterPackNightManager = null;
            }

            if (network != null)
            {
                network.OnClientConnectedCallback -= HandleWinTestModeClientConnected;
                network.OnClientDisconnectCallback -= HandleDeadChatClientDisconnected;
            }

            syncedMonsterPackTargetId = -1;
            syncedMonsterPackSelectorId = -1;
            syncedMonsterPackSourceRole = RoleType.DogSpirit;
            syncedMonsterPackLocked = false;
            syncedMonsterPackRevision = 0;
            syncedMonsterVotedTargetId = -1;
            syncedMonsterVoteTied = false;
            syncedMonsterVoteHighestCount = 0;
            syncedMonsterCanVote = false;
            syncedMonsterVoteClosed = false;
            syncedDogSpiritDecisionActive = false;
            syncedLocalCanDecideDogSpirit = false;
            syncedMonsterVoteTallies.Clear();
            syncedDogSpiritTiedCandidates.Clear();

            WinConditionTestConfig.SetServerSyncOverride(null);
            NetworkChatManager.Instance?.ResetDeadChatState();
        }

        private void HandleWinTestModeClientConnected(ulong clientId)
        {
            if (network == null || !network.IsServer || !network.IsListening) return;
            SendWinCheckTestModeToClient(clientId);
            SendMonsterPackTargetToClient(clientId);
        }

        private void HandleServerMonsterPackTargetChanged(int targetId, int selectorId, RoleType sourceRole, bool locked)
        {
            if (network == null || !network.IsServer || !network.IsListening) return;
            nextSnapshotTime = 0;
            nextPrivateTime = 0;
            BroadcastMonsterPackTargetOnServer();
        }

        private bool IsServerMonsterPackClient(ulong clientId)
        {
            if (serverPlayers == null || !serverPlayers.TryGetValue(clientId, out var member) || member == null || member.Offline)
                return false;
            PlayerData player = players?.GetplayerByID(member.PlayerId);
            if (!NightManager.IsMonsterPackMember(player)) return false;
            if (!player.isAlive || player.isDisconnected) return false;
            int nightNum = NightManager.Instance != null ? NightManager.Instance.CurrentEffectiveNightNumber() : (game != null ? game.currentDay : 0);
            if (player.roleType == RoleType.SerpentSpirit && nightNum <= 2) return false;
            return true;
        }

        public void BroadcastMonsterPackTargetOnServer()
        {
            if (network == null || !network.IsServer || !network.IsListening) return;

            if (NightManager.Instance != null && observedMonsterPackNightManager != NightManager.Instance)
            {
                if (observedMonsterPackNightManager != null)
                    observedMonsterPackNightManager.MonsterPackTargetChanged -= HandleServerMonsterPackTargetChanged;
                observedMonsterPackNightManager = NightManager.Instance;
                observedMonsterPackNightManager.MonsterPackTargetChanged += HandleServerMonsterPackTargetChanged;
            }

            var night = NightManager.Instance;
            int targetId = night != null ? night.ActiveMonsterTargetId : -1;
            int selectorId = night != null ? night.ActiveMonsterSelectorId : -1;
            RoleType sourceRole = night != null ? night.ActiveMonsterSourceRole : RoleType.DogSpirit;
            bool locked = night != null && night.IsMonsterAttackLockedForCurrentNight();
            int revision = night != null ? night.MonsterTargetVersion : 0;
            bool isTie = night != null && night.IsMonsterVoteTied;
            int highestCount = night != null ? night.MonsterVoteHighestCount : 0;
            bool voteClosed = night != null && night.IsMonsterVoteClosed;
            bool decisionActive = night != null && night.IsDogSpiritDecisionActive;
            int nightNum = night != null ? night.CurrentEffectiveNightNumber() : (game != null ? game.currentDay : 0);

            if (network.IsHost)
            {
                PlayerData localData = localPlayerId >= 0 ? players?.GetplayerByID(localPlayerId) : null;
                bool hostCanSee = NightManager.IsMonsterPackMember(localData) && localData != null && localData.isAlive &&
                                  (localData.roleType != RoleType.SerpentSpirit || nightNum >= 3);
                syncedMonsterVoteTallies.Clear();
                syncedDogSpiritTiedCandidates.Clear();
                if (hostCanSee)
                {
                    syncedMonsterPackTargetId = targetId;
                    syncedMonsterPackSelectorId = selectorId;
                    syncedMonsterPackSourceRole = sourceRole;
                    syncedMonsterPackLocked = locked;
                    syncedMonsterPackRevision = revision;
                    syncedMonsterVotedTargetId = night != null ? night.GetMonsterVotedTarget(localPlayerId) : -1;
                    syncedMonsterVoteTied = isTie;
                    syncedMonsterVoteHighestCount = highestCount;
                    syncedMonsterVoteClosed = voteClosed;
                    syncedDogSpiritDecisionActive = decisionActive;
                    syncedLocalCanDecideDogSpirit = decisionActive &&
                        NightManager.CanParticipateInDogSpiritDecision(localData, out _);
                    syncedMonsterCanVote = !voteClosed && !locked &&
                        NightManager.CanParticipateInMonsterPackAttack(localData, nightNum, out _);
                    if (night != null)
                    {
                        foreach (var kv in night.MonsterVoteTallies)
                            syncedMonsterVoteTallies[kv.Key] = kv.Value;
                        if (decisionActive)
                            syncedDogSpiritTiedCandidates.AddRange(night.DogSpiritTiedCandidateIds);
                    }
                }
                else
                {
                    syncedMonsterPackTargetId = -1;
                    syncedMonsterPackSelectorId = -1;
                    syncedMonsterPackSourceRole = RoleType.DogSpirit;
                    syncedMonsterPackLocked = false;
                    syncedMonsterPackRevision = 0;
                    syncedMonsterVotedTargetId = -1;
                    syncedMonsterVoteTied = false;
                    syncedMonsterVoteHighestCount = 0;
                    syncedMonsterCanVote = false;
                    syncedMonsterVoteClosed = false;
                    syncedDogSpiritDecisionActive = false;
                    syncedLocalCanDecideDogSpirit = false;
                }
                MonsterVoteUpdated?.Invoke();
            }

            foreach (ulong cid in network.ConnectedClientsIds)
            {
                if (cid == network.LocalClientId) continue;
                SendMonsterPackTargetToClient(cid);
            }
        }

        private void SendMonsterPackTargetToClient(ulong clientId)
        {
            if (network == null || !network.IsServer || network.CustomMessagingManager == null ||
                clientId == network.LocalClientId || !network.ConnectedClientsIds.Contains(clientId))
                return;

            var night = NightManager.Instance;
            int nightNum = night != null ? night.CurrentEffectiveNightNumber() : (game != null ? game.currentDay : 0);
            bool allowed = IsServerMonsterPackClient(clientId);
            int targetId = allowed && night != null ? night.ActiveMonsterTargetId : -1;
            int selectorId = allowed && night != null ? night.ActiveMonsterSelectorId : -1;
            RoleType sourceRole = allowed && night != null ? night.ActiveMonsterSourceRole : RoleType.DogSpirit;
            bool locked = allowed && night != null && night.IsMonsterAttackLockedForCurrentNight();
            int revision = allowed && night != null ? night.MonsterTargetVersion : 0;
            bool isTie = allowed && night != null && night.IsMonsterVoteTied;
            int highestCount = allowed && night != null ? night.MonsterVoteHighestCount : 0;
            bool voteClosed = allowed && night != null && night.IsMonsterVoteClosed;
            bool decisionActive = allowed && night != null && night.IsDogSpiritDecisionActive;

            int myVoteId = -1;
            bool canVote = false;
            bool canDecide = false;
            if (allowed && serverPlayers.TryGetValue(clientId, out var member) && member != null)
            {
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                myVoteId = night != null ? night.GetMonsterVotedTarget(member.PlayerId) : -1;
                canVote = !voteClosed && !locked && NightManager.CanParticipateInMonsterPackAttack(player, nightNum, out _);
                canDecide = decisionActive && !locked && NightManager.CanParticipateInDogSpiritDecision(player, out _);
            }

            var tallies = (allowed && night != null) ? night.MonsterVoteTallies : null;
            ushort tallyCount = (ushort)(tallies != null ? tallies.Count : 0);
            var tiedIds = (allowed && decisionActive && night != null) ? night.DogSpiritTiedCandidateIds : null;
            ushort tiedCount = (ushort)(tiedIds != null ? tiedIds.Count : 0);

            using var writer = new FastBufferWriter(80 + tallyCount * 8 + tiedCount * 4, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(matchEpoch);
            writer.WriteValueSafe(nightNum);
            writer.WriteValueSafe(targetId);
            writer.WriteValueSafe(selectorId);
            writer.WriteValueSafe((byte)sourceRole);
            writer.WriteValueSafe(locked);
            writer.WriteValueSafe(isTie);
            writer.WriteValueSafe(highestCount);
            writer.WriteValueSafe(myVoteId);
            writer.WriteValueSafe(canVote);
            writer.WriteValueSafe(revision);
            writer.WriteValueSafe(voteClosed);
            writer.WriteValueSafe(decisionActive);
            writer.WriteValueSafe(canDecide);
            writer.WriteValueSafe(tallyCount);
            if (tallies != null)
            {
                foreach (var pair in tallies)
                {
                    writer.WriteValueSafe(pair.Key);
                    writer.WriteValueSafe(pair.Value);
                }
            }
            writer.WriteValueSafe(tiedCount);
            if (tiedIds != null)
            {
                for (int i = 0; i < tiedIds.Count; i++)
                {
                    writer.WriteValueSafe(tiedIds[i]);
                }
            }
            network.CustomMessagingManager.SendNamedMessage(
                MonsterPackTargetMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced
            );
        }

        private void ReceiveMonsterPackTarget(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out uint epoch);
            reader.ReadValueSafe(out int _nightNum);
            reader.ReadValueSafe(out int targetId);
            reader.ReadValueSafe(out int selectorId);
            reader.ReadValueSafe(out byte sourceRoleByte);
            reader.ReadValueSafe(out bool locked);
            reader.ReadValueSafe(out bool isTie);
            reader.ReadValueSafe(out int highestCount);
            reader.ReadValueSafe(out int myVoteId);
            reader.ReadValueSafe(out bool canVote);
            reader.ReadValueSafe(out int revision);
            reader.ReadValueSafe(out bool voteClosed);
            reader.ReadValueSafe(out bool decisionActive);
            reader.ReadValueSafe(out bool canDecide);
            reader.ReadValueSafe(out ushort tallyCount);

            var incomingTallies = new Dictionary<int, int>(tallyCount);
            for (int i = 0; i < tallyCount; i++)
            {
                reader.ReadValueSafe(out int cid);
                reader.ReadValueSafe(out int cnt);
                incomingTallies[cid] = cnt;
            }
            reader.ReadValueSafe(out ushort tiedCount);
            var incomingTiedIds = new List<int>(tiedCount);
            for (int i = 0; i < tiedCount; i++)
            {
                reader.ReadValueSafe(out int tid);
                incomingTiedIds.Add(tid);
            }

            if (epoch != matchEpoch) return;
            syncedMonsterVoteTallies.Clear();
            syncedDogSpiritTiedCandidates.Clear();
            if (!IsLocalMonsterPackMember)
            {
                syncedMonsterPackTargetId = -1;
                syncedMonsterPackSelectorId = -1;
                syncedMonsterPackSourceRole = RoleType.DogSpirit;
                syncedMonsterPackLocked = false;
                syncedMonsterPackRevision = 0;
                syncedMonsterVotedTargetId = -1;
                syncedMonsterVoteTied = false;
                syncedMonsterVoteHighestCount = 0;
                syncedMonsterCanVote = false;
                syncedMonsterVoteClosed = false;
                syncedDogSpiritDecisionActive = false;
                syncedLocalCanDecideDogSpirit = false;
                MonsterVoteUpdated?.Invoke();
                return;
            }

            syncedMonsterPackTargetId = targetId;
            syncedMonsterPackSelectorId = selectorId;
            syncedMonsterPackSourceRole = Enum.IsDefined(typeof(RoleType), (int)sourceRoleByte)
                ? (RoleType)sourceRoleByte
                : RoleType.DogSpirit;
            syncedMonsterPackLocked = locked;
            syncedMonsterPackRevision = revision;
            syncedMonsterVoteTied = isTie;
            syncedMonsterVoteHighestCount = highestCount;
            syncedMonsterVotedTargetId = myVoteId;
            syncedMonsterCanVote = canVote;
            syncedMonsterVoteClosed = voteClosed;
            syncedDogSpiritDecisionActive = decisionActive;
            syncedLocalCanDecideDogSpirit = canDecide;
            foreach (var pair in incomingTallies)
            {
                syncedMonsterVoteTallies[pair.Key] = pair.Value;
            }
            syncedDogSpiritTiedCandidates.AddRange(incomingTiedIds);
            MonsterVoteUpdated?.Invoke();
        }

        private bool TryExecuteMonsterPackAbilityOnServer(PlayerData actor, int targetId, out string feedback)
        {
            if (actor == null || game == null)
            {
                feedback = "Không tìm thấy người chơi hoặc bộ điều phối.";
                return false;
            }

            var night = NightManager.Instance;
            if (night != null && night.IsDogSpiritDecisionActive)
            {
                if (!night.TrySubmitDogSpiritDecision(actor, targetId, game.currentDay, out feedback))
                {
                    return false;
                }
                BroadcastMonsterPackTargetOnServer();
                SendAllPrivateStates();
                game.CompleteDogSpiritDecisionIfReady();
                return true;
            }

            if (night != null)
            {
                if (night.IsMonsterVoteClosed || night.IsMonsterAttackLockedForCurrentNight())
                {
                    feedback = "Lượt bỏ phiếu của phe Quái vật trong đêm này đã kết thúc.";
                    return false;
                }

                if (!NightManager.CanParticipateInMonsterPackAttack(actor, game.currentDay, out feedback))
                {
                    return false;
                }

                PlayerData target = players?.GetplayerByID(targetId);
                if (!NightManager.IsValidMonsterPackTarget(actor, target, out feedback))
                {
                    return false;
                }

                if (night.GetMonsterVotedTarget(actor.playerID) == targetId)
                {
                    feedback = $"Bạn đã bỏ phiếu cho P{targetId + 1:00} rồi.";
                    return false;
                }
            }

            if (RoleManager.Instance == null)
            {
                feedback = "Không tìm thấy hệ thống quản lý vai trò.";
                return false;
            }

            if (!RoleManager.Instance.UseNightAbility(actor.playerID, targetId, out feedback))
            {
                if (string.IsNullOrWhiteSpace(feedback)) feedback = "Không thể bỏ phiếu cho mục tiêu này.";
                return false;
            }

            BroadcastMonsterPackTargetOnServer();
            SendAllPrivateStates();
            return true;
        }

        public void BroadcastWinCheckTestModeOnServer()
        {
            if (network == null || !network.IsServer || !network.IsListening) return;
            foreach (ulong cid in network.ConnectedClientsIds)
            {
                if (cid == network.LocalClientId) continue;
                SendWinCheckTestModeToClient(cid);
            }
        }

        private void SendWinCheckTestModeToClient(ulong clientId)
        {
            if (network == null || !network.IsServer || network.CustomMessagingManager == null ||
                clientId == network.LocalClientId || !network.ConnectedClientsIds.Contains(clientId))
                return;

            using var writer = new FastBufferWriter(8, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(WinConditionTestConfig.DisableWinConditionForTesting);
            network.CustomMessagingManager.SendNamedMessage(
                WinCheckTestModeMessage,
                clientId,
                writer,
                NetworkDelivery.ReliableSequenced
            );
        }

        private void ReceiveWinCheckTestMode(ulong sender, FastBufferReader reader)
        {
            if (network == null || network.IsServer || sender != NetworkManager.ServerClientId) return;
            reader.ReadValueSafe(out bool disabled);
            WinConditionTestConfig.SetServerSyncOverride(disabled);
        }

        private void OnDeadChatPhaseChanged(GamePhase previous, GamePhase current)
        {
            if (current is GamePhase.Setup or GamePhase.RoleReveal)
            {
                if (ThuongOnlineWinAdapter.Instance != null)
                    ThuongOnlineWinAdapter.Instance.ResetDebugToggleForNewMatch();
                else
                    WinConditionTestConfig.ResetForNewMatch();
            }

            if (current is GamePhase.Setup or GamePhase.RoleReveal || matchResult != null)
            {
                NetworkChatManager.Instance?.ResetDeadChatState();
                NetworkChatManager.RefreshLocalMonsterChatPermission();
                NetworkChatUI.Instance?.RefreshAllTabPermissions();

                if (network != null && network.IsServer && network.IsListening)
                {
                    RefreshServerCommunications();
                    BroadcastDeadChatMembersOnServer();
                    BroadcastWinCheckTestModeOnServer();
                    BroadcastMonsterPackTargetOnServer();
                }
                return;
            }

            EnsureDeadChatServerListeners();

            NetworkChatManager.RefreshLocalDeadChatPermission();
            NetworkChatManager.RefreshLocalMonsterChatPermission();
            NetworkChatUI.Instance?.RefreshAllTabPermissions();

            if (network != null && network.IsServer && network.IsListening)
            {
                BroadcastDeadChatMembersOnServer();
                BroadcastWinCheckTestModeOnServer();
                BroadcastMonsterPackTargetOnServer();
            }
        }

        private void EnsureDeadChatServerListeners()
        {
            if (DeathResolver.Instance != null && observedDeadChatDeathResolver != DeathResolver.Instance)
            {
                if (observedDeadChatDeathResolver != null)
                    observedDeadChatDeathResolver.DeathResolved -= HandleDeadChatDeathResolved;
                observedDeadChatDeathResolver = DeathResolver.Instance;
                observedDeadChatDeathResolver.DeathResolved += HandleDeadChatDeathResolved;
            }

            if (VoteManager.Instance != null && observedDeadChatVoteManager != VoteManager.Instance)
            {
                if (observedDeadChatVoteManager != null)
                    observedDeadChatVoteManager.VoteResolved -= HandleDeadChatVoteResolved;
                observedDeadChatVoteManager = VoteManager.Instance;
                observedDeadChatVoteManager.VoteResolved += HandleDeadChatVoteResolved;
            }
        }

        private void HandleDeadChatDeathResolved(DeathResult result)
        {
            if (network != null && network.IsServer && network.IsListening)
            {
                RefreshServerCommunications();
                BroadcastDeadChatMembersOnServer();
                BroadcastMonsterPackTargetOnServer();
            }
            NetworkChatManager.RefreshLocalDeadChatPermission();
            NetworkChatManager.RefreshLocalMonsterChatPermission();
            NetworkChatUI.Instance?.RefreshAllTabPermissions();
        }

        private void HandleDeadChatVoteResolved(VoteResolution resolution)
        {
            if (network != null && network.IsServer && network.IsListening)
            {
                RefreshServerCommunications();
                BroadcastDeadChatMembersOnServer();
            }
            NetworkChatManager.RefreshLocalDeadChatPermission();
            NetworkChatManager.RefreshLocalMonsterChatPermission();
            NetworkChatUI.Instance?.RefreshAllTabPermissions();
        }

        private void HandleDeadChatClientDisconnected(ulong clientId)
        {
            if (network != null && network.IsServer && network.IsListening)
            {
                RefreshServerCommunications();
                BroadcastDeadChatMembersOnServer();
            }
        }

        /// <summary>
        /// Server xác thực ClientId có đang ở trạng thái Dead hợp lệ trong ván hiện tại hay không.
        /// </summary>
        public bool IsServerPlayerDead(ulong clientId)
        {
            if (!connected || leavingScene || !matchStarted || matchResult != null || network == null || !network.IsListening)
                return false;
            if (serverPlayers == null || !serverPlayers.TryGetValue(clientId, out var member) || member == null || member.Offline)
                return false;
            if (!network.ConnectedClientsIds.Contains(clientId))
                return false;

            PlayerData player = players?.GetplayerByID(member.PlayerId);
            return player != null && !player.isAlive && !player.isDisconnected;
        }

        /// <summary>
        /// Kiểm tra người chơi cục bộ có đang chết trong ván hiện tại (và chưa kết thúc ván) hay không.
        /// </summary>
        public bool IsLocalPlayerDeadInMatch()
        {
            if (!connected || leavingScene || !matchStarted || matchResult != null)
                return false;
            if (CommunicationAudience == ThuongCommunicationAudience.Dead)
                return true;
            if (localPlayerId >= 0 && players != null)
            {
                PlayerData localPlayer = players.GetplayerByID(localPlayerId);
                if (localPlayer != null)
                {
                    return !localPlayer.isAlive && !localPlayer.isDisconnected;
                }
            }
            return HasLocalRole && !LocalPlayerAlive;
        }

        /// <summary>
        /// Thu thập danh sách thành viên kênh Người Chết đang online trên Server.
        /// </summary>
        public List<NetworkChatManager.DeadMemberInfo> GetServerDeadChatMembers()
        {
            var members = new List<NetworkChatManager.DeadMemberInfo>();
            if (!connected || leavingScene || !matchStarted || matchResult != null || serverPlayers == null || network == null)
                return members;

            foreach (var member in serverPlayers.Values.OrderBy(m => m.PlayerId))
            {
                if (member == null || member.Offline || !network.ConnectedClientsIds.Contains(member.ClientId))
                    continue;
                PlayerData player = players?.GetplayerByID(member.PlayerId);
                if (player == null || player.isAlive || player.isDisconnected)
                    continue;

                string cleanName = string.IsNullOrWhiteSpace(member.Name) ? $"Player {member.PlayerId + 1}" : member.Name;
                members.Add(new NetworkChatManager.DeadMemberInfo
                {
                    ClientId = member.ClientId,
                    PlayerId = member.PlayerId,
                    Name = $"P{member.PlayerId + 1:00}  {cleanName}"
                });
            }
            return members;
        }

        /// <summary>
        /// Server phát tán danh sách thành viên Dead Chat:
        /// - Chỉ gửi danh sách thành viên & quyền truy cập cho Client ĐÃ CHẾT.
        /// - Gửi gói thu hồi (hasAccess = false, danh sách rỗng) cho người còn sống.
        /// </summary>
        public void BroadcastDeadChatMembersOnServer()
        {
            if (network == null || !network.IsServer || !network.IsListening || serverPlayers == null)
                return;

            var deadMembers = GetServerDeadChatMembers();
            foreach (var member in serverPlayers.Values)
            {
                if (member == null || member.Offline || !network.ConnectedClientsIds.Contains(member.ClientId))
                    continue;

                bool allowed = IsServerPlayerDead(member.ClientId);
                NetworkChatManager.SendDeadMemberSyncToClient(network, member.ClientId, allowed, deadMembers);
            }
        }

        /// <summary>
        /// Gửi tin nhắn Dead Chat thông qua kênh cô lập của ThuongOnlineSession khi CommunicationAudience đã chuyển sang Dead.
        /// </summary>
        public bool SendDeadChatThroughSession(string text)
        {
            if (!IsLocalPlayerDeadInMatch())
                return false;

            if (CommunicationAudience == ThuongCommunicationAudience.Dead && CanSendChat)
            {
                return SendChatText(text);
            }
            return false;
        }

        partial void RegisterShamanMessages()
        {
            if (network != null && network.CustomMessagingManager != null)
            {
                NetworkShamanNotificationUI.EnsureInstance().RegisterOnMessagingManager(network.CustomMessagingManager);
            }
            ThuongOnlineShamanAdapter.EnsureInstalled();
            RegisterDeadChatMessages();
        }

        partial void UnregisterShamanMessages()
        {
            if (network != null && network.CustomMessagingManager != null && NetworkShamanNotificationUI.Instance != null)
            {
                NetworkShamanNotificationUI.Instance.UnregisterFromMessagingManager(network.CustomMessagingManager);
            }
            UnregisterDeadChatMessages();
        }

        partial void OnOnlinePhaseChanged(GamePhase previous, GamePhase current)
        {
            if (current != GamePhase.Night)
            {
                NetworkShamanNotificationUI.Instance?.HidePrompt();
            }
            if (current == GamePhase.RoleReveal || current == GamePhase.Setup || current == GamePhase.GameOver)
            {
                NetworkShamanNotificationUI.Instance?.ResetForNewMatch();
            }
            OnDeadChatPhaseChanged(previous, current);
        }

        /// <summary>
        /// Tra cứu PlayerId của người chơi tương ứng với ClientId từ serverPlayers.
        /// </summary>
        public bool TryGetPlayerIdForClientId(ulong clientId, out int playerId)
        {
            if (serverPlayers != null && serverPlayers.TryGetValue(clientId, out var member) && member != null)
            {
                playerId = member.PlayerId;
                return true;
            }
            playerId = -1;
            return false;
        }

        /// <summary>
        /// Kiểm tra xem ClientId trên Server có thuộc phe Monster không.
        /// </summary>
        public bool IsServerPlayerMonster(ulong clientId)
        {
            if (serverPlayers != null && serverPlayers.TryGetValue(clientId, out var member) && member != null && !member.Offline)
            {
                var player = players?.GetplayerByID(member.PlayerId);
                if (player != null && player.isAlive && !player.isDisconnected)
                {
                    // Bạch Khuyển không nằm trong kênh chat của Monster theo yêu cầu
                    if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf) return false;
                    if (player.roleType == RoleType.DogSpirit || player.roleType == RoleType.SerpentSpirit || player.roleType == RoleType.Ogre) return true;
                    if (player.faction == FactionType.Monster) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Lấy danh sách ClientId của tất cả người chơi thuộc phe Monster còn sống đang trực tuyến.
        /// </summary>
        public System.Collections.Generic.List<ulong> GetMonsterClientIds()
        {
            var list = new System.Collections.Generic.List<ulong>();
            if (serverPlayers != null)
            {
                foreach (var pair in serverPlayers)
                {
                    if (pair.Value != null && !pair.Value.Offline)
                    {
                        var player = players?.GetplayerByID(pair.Value.PlayerId);
                        if (player != null && player.isAlive && !player.isDisconnected)
                        {
                            // Bạch Khuyển không nằm trong kênh chat của Monster theo yêu cầu
                            if (player.roleType == RoleType.WhiteHound || player.roleType == RoleType.WhiteWolf) continue;
                            bool isMonster = (player.roleType == RoleType.DogSpirit || player.roleType == RoleType.SerpentSpirit || player.roleType == RoleType.Ogre) ||
                                             (player.faction == FactionType.Monster);
                            if (isMonster)
                            {
                                list.Add(pair.Key);
                            }
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Áp dụng giới hạn kỹ năng Xà Tinh trong Thương Online:
        /// Ở Đêm 1 và Đêm 2, xóa cờ HasActiveLocalAbility (bit 8) để RoleAbilityUI ẩn nút CHỌN MỤC TIÊU.
        /// Từ Đêm 3 trở đi, BẬT LẠI cờ HasActiveLocalAbility (bit 8) để hiển thị và sử dụng bình thường.
        /// </summary>
        public void EnforceSerpentSpiritNightRestriction()
        {
            if (privateRole == RoleType.SerpentSpirit)
            {
                int night = ThuongOnlineSerpentSpiritAdapter.GetAuthoritativeNightNumber();
                if (visibleDay < night)
                {
                    visibleDay = night;
                }
                if (night < 3)
                {
                    // Bit 8 là cờ HasActiveLocalAbility, xóa bit 8 để RoleAbilityUI ẩn nút chọn mục tiêu
                    privateFlags = (ushort)(privateFlags & ~8);
                }
                else
                {
                    // Từ Đêm 3 trở đi: BẬT LẠI cờ HasActiveLocalAbility (bit 8) để RoleAbilityUI hiện nút chọn mục tiêu
                    privateFlags = (ushort)(privateFlags | 8);

                    if (players?.players != null)
                    {
                        foreach (var target in players.players)
                        {
                            if (target != null && target.isAlive && target.playerID != localPlayerId &&
                                !NightManager.IsMonsterPackMember(target))
                            {
                                localAllowedTargets.Add(target.playerID);
                            }
                        }
                    }
                }
                matchMaxDay = 7 + night;
                ThuongOnlineSerpentSpiritAdapter.SyncRoleAbilityUIButtons();
            }
        }

        /// <summary>
        /// Dò tìm mục tiêu đang bị Quái vật (Monster/DogSpirit/SerpentSpirit/Ogre/WhiteHound) nhắm tới trong ban đêm.
        /// Kiểm tra cả NightManager.BuildPeekReport() và nhật ký đêm hiện tại (matchJournal).
        /// </summary>
        public bool TryGetMonsterNightTarget(out int victimId)
        {
            victimId = -1;
            if (NightManager.Instance != null)
            {
                string report = NightManager.Instance.BuildPeekReport();
                if (!string.IsNullOrEmpty(report))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        report,
                        @"(?:Sói|Xà|Quỷ|Bạch khuyển|DogSpirit|SerpentSpirit|Ogre|WhiteHound)\s*(?:→|>)\s*P(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int pNum))
                    {
                        victimId = pNum - 1;
                        return true;
                    }
                }
            }

            if (matchJournal != null && players?.players != null)
            {
                int currentDay = game != null ? game.currentDay : visibleDay;
                for (int i = matchJournal.Count - 1; i >= 0; i--)
                {
                    var entry = matchJournal[i];
                    if (entry.Day != currentDay || entry.Phase != GamePhase.Night || string.IsNullOrEmpty(entry.Text))
                    {
                        continue;
                    }

                    bool isMonsterAction =
                        entry.Text.Contains("(DogSpirit)") ||
                        entry.Text.Contains("(SerpentSpirit)") ||
                        entry.Text.Contains("(Ogre)") ||
                        entry.Text.Contains("(WhiteHound)");
                    if (!isMonsterAction)
                    {
                        continue;
                    }

                    const string marker = " lên ";
                    int markerIdx = entry.Text.LastIndexOf(marker, StringComparison.Ordinal);
                    if (markerIdx >= 0)
                    {
                        string targetName = entry.Text.Substring(markerIdx + marker.Length).Trim().TrimEnd('.');
                        foreach (var candidate in players.players)
                        {
                            if (candidate == null || !candidate.isAlive) continue;
                            if (string.Equals(candidate.DisplayName, targetName, StringComparison.Ordinal) ||
                                string.Equals(candidate.playerName, targetName, StringComparison.Ordinal) ||
                                string.Equals($"Player {candidate.playerID + 1}", targetName, StringComparison.Ordinal))
                            {
                                victimId = candidate.playerID;
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
