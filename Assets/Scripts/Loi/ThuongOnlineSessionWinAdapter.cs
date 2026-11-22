using System;
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
        public Unity.Netcode.NetworkManager GetNetworkManager() => network != null ? network : Unity.Netcode.NetworkManager.Singleton;

        partial void RegisterShamanMessages()
        {
            if (network != null && network.CustomMessagingManager != null)
            {
                NetworkShamanNotificationUI.EnsureInstance().RegisterOnMessagingManager(network.CustomMessagingManager);
            }
            ThuongOnlineShamanAdapter.EnsureInstalled();
        }

        partial void UnregisterShamanMessages()
        {
            if (network != null && network.CustomMessagingManager != null && NetworkShamanNotificationUI.Instance != null)
            {
                NetworkShamanNotificationUI.Instance.UnregisterFromMessagingManager(network.CustomMessagingManager);
            }
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
            if (serverPlayers != null && serverPlayers.TryGetValue(clientId, out var member) && member != null)
            {
                var player = players?.GetplayerByID(member.PlayerId);
                if (player != null)
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
        /// Lấy danh sách ClientId của tất cả người chơi thuộc phe Monster đang trực tuyến.
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
                        if (player != null)
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
