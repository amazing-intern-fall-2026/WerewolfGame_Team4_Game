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
        private bool deadChatEventsSubscribed;
        private DeathResolver observedDeadChatDeathResolver;
        private VoteManager observedDeadChatVoteManager;

        private void RegisterDeadChatMessages()
        {
            if (deadChatEventsSubscribed) return;
            deadChatEventsSubscribed = true;

            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(WinCheckTestModeMessage);
                network.CustomMessagingManager.RegisterNamedMessageHandler(WinCheckTestModeMessage, ReceiveWinCheckTestMode);
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

            if (network != null)
            {
                network.OnClientConnectedCallback -= HandleWinTestModeClientConnected;
                network.OnClientConnectedCallback += HandleWinTestModeClientConnected;
                network.OnClientDisconnectCallback -= HandleDeadChatClientDisconnected;
                network.OnClientDisconnectCallback += HandleDeadChatClientDisconnected;
            }

            BroadcastWinCheckTestModeOnServer();
        }

        private void UnregisterDeadChatMessages()
        {
            deadChatEventsSubscribed = false;

            if (network != null && network.CustomMessagingManager != null)
            {
                network.CustomMessagingManager.UnregisterNamedMessageHandler(WinCheckTestModeMessage);
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

            if (network != null)
            {
                network.OnClientConnectedCallback -= HandleWinTestModeClientConnected;
                network.OnClientDisconnectCallback -= HandleDeadChatClientDisconnected;
            }

            WinConditionTestConfig.SetServerSyncOverride(null);
            NetworkChatManager.Instance?.ResetDeadChatState();
        }

        private void HandleWinTestModeClientConnected(ulong clientId)
        {
            if (network == null || !network.IsServer || !network.IsListening) return;
            SendWinCheckTestModeToClient(clientId);
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
                            if (target != null && target.isAlive && target.playerID != localPlayerId)
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
