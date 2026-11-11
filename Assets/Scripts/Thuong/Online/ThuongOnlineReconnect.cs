using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private const double ReconnectGraceSeconds = 60;
        private const string LeaveMessage = "thuong.online.leave.v1";
        private readonly Dictionary<ulong, ThuongOnlineServerPlayer> reconnectApprovals = new();
        private Guid reconnectToken;
        private double reconnectOfferUntil;
        private bool clientUsedRelay, clientHadRoles, explicitLeave, relayOperation;
        private bool hostSessionTerminated;

        private static byte[] ConnectionPayload(Guid token, string name, bool resume) =>
            Encoding.UTF8.GetBytes(BuildPrefix + (resume ? "R" : "J") + token.ToString("N") + (resume ? "" : name));

        private static bool TryReadConnectionPayload(string payload, out Guid token, out bool resume, out string name)
        {
            token = Guid.Empty;
            resume = false;
            name = string.Empty;
            int mode = BuildPrefix.Length;
            int nameStart = mode + 33;
            if (payload.Length < nameStart || !payload.StartsWith(BuildPrefix, StringComparison.Ordinal) ||
                (payload[mode] != 'J' && payload[mode] != 'R') ||
                !Guid.TryParseExact(payload.Substring(mode + 1, 32), "N", out token) || token == Guid.Empty) return false;
            resume = payload[mode] == 'R';
            if (resume) return payload.Length == nameStart;
            name = payload.Substring(nameStart);
            return true;
        }

        private bool TryReserveReconnect(ulong clientId, Guid token, double now)
        {
            if (!roomLocked || reconnectApprovals.ContainsKey(clientId) || serverPlayers.ContainsKey(clientId)) return false;
            var member = serverPlayers.Values.FirstOrDefault(candidate => candidate.ReconnectToken == token);
            if (member == null || !member.Offline || member.ReconnectUntil <= now || reconnectApprovals.ContainsValue(member)) return false;
            reconnectApprovals.Add(clientId, member);
            return true;
        }

        private bool AttachReturningClient(ulong clientId, ThuongOnlineServerPlayer member, double now)
        {
            if (!roomLocked || !member.Offline || member.ReconnectUntil <= now ||
                !serverPlayers.TryGetValue(member.ClientId, out var reserved) || reserved != member) return false;
            serverPlayers.Remove(member.ClientId);
            member.ClientId = clientId;
            member.Offline = false;
            member.ReconnectUntil = 0;
            member.LastInputSequence = 0;
            member.LastInputTime = 0;
            member.LastRequestId = 0;
            member.LastRoomStateRevision = 0;
            serverPlayers.Add(clientId, member);
            nextPrivateTime = nextSnapshotTime = 0;
            status = $"{member.Name} đã kết nối lại.";
            return true;
        }

        private void HandleServerPeerLost(ulong clientId)
        {
            reconnectApprovals.Remove(clientId);
            if (!serverPlayers.TryGetValue(clientId, out var member) || member.Offline) return;
            var attempt = minigameAuthority.Get(clientId);
            if (attempt != null)
            {
                minigameAuthority.Cancel(clientId, attempt.attemptId, "Người chơi mất kết nối.");
                attemptOwners.Remove(attempt.attemptId);
            }
            minigameSequences.Remove(clientId);
            minigameBeginTimes.Remove(clientId);
            emoteStates.Remove(member.PlayerId);
            rosterDirty = true;
            if (!rolesAssigned)
            {
                serverPlayers.Remove(clientId);
                return;
            }

            member.Offline = true;
            member.MapReady = false;
            member.Input = Vector2.zero;
            double now = Time.realtimeSinceStartupAsDouble;
            member.ReconnectUntil = member.ReconnectToken == Guid.Empty ? now : now + ReconnectGraceSeconds;
            status = member.ReconnectToken == Guid.Empty ? $"{member.Name} đã rời phòng." :
                $"{member.Name} mất kết nối. Giữ chỗ {ReconnectGraceSeconds:0} giây.";
        }

        private void ExpireReconnects()
        {
            if (!rolesAssigned) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!serverPlayers.Values.Any(member => IsExpired(member, now))) return;
            if (!matchStarted && matchResult == null)
            {
                ResetToRoomLobby("Đã mở lại phòng chờ vì có người rời phòng. Chủ phòng phân vai lại khi sẵn sàng.");
                return;
            }

            using var batch = DeathResolver.Instance?.BeginBatch();
            foreach (var member in serverPlayers.Values)
            {
                if (!IsExpired(member, now)) continue;
                member.ReconnectUntil = 0;
                member.ReconnectToken = Guid.Empty;
                if (matchResult != null) continue;
                var player = players?.GetplayerByID(member.PlayerId);
                if (player == null) continue;
                NightManager.Instance?.CancelDisconnectedPlayerActions(member.PlayerId);
                if (player.isAlive)
                    DeathResolver.Instance?.TryKill(new DeathRequest(player, DeathCause.Disconnect, bypassProtection: true));
                player.isDisconnected = true;
                player.hasHunterTrap = false;
                player.NotifyChanged();
                status = $"{member.Name} đã rời ván.";
            }
        }

        private static bool IsExpired(ThuongOnlineServerPlayer member, double now) =>
            member.Offline && member.ReconnectUntil > 0 && member.ReconnectUntil <= now;

        private void ReceiveVoluntaryLeave(ulong sender, FastBufferReader reader)
        {
            if (!network.IsServer || sender == network.LocalClientId || !serverPlayers.TryGetValue(sender, out var member) ||
                member.Offline || !reader.TryBeginRead(sizeof(uint))) return;
            reader.ReadValueSafe(out uint epoch);
            if (epoch != matchEpoch) return;
            member.ReconnectToken = Guid.Empty;
            network.DisconnectClient(sender);
        }

        private void NotifyVoluntaryLeave()
        {
            if (!connected || network.IsServer || !network.IsConnectedClient) return;
            using var writer = new FastBufferWriter(sizeof(uint), Allocator.Temp);
            writer.WriteValueSafe(matchEpoch);
            network.CustomMessagingManager.SendNamedMessage(LeaveMessage, NetworkManager.ServerClientId, writer);
        }

        private void OfferReconnect()
        {
            if (!explicitLeave && !hostSessionTerminated && connected && clientHadRoles && reconnectToken != Guid.Empty && matchResult == null)
                reconnectOfferUntil = Time.realtimeSinceStartupAsDouble + ReconnectGraceSeconds;
        }

        public void HandleHostSessionTerminated(uint epoch)
        {
            if (network == null || network.IsServer || epoch != matchEpoch || hostSessionTerminated) return;
            hostSessionTerminated = true;
            reconnectOfferUntil = 0;
            if (matchResult != null)
            { status = "Chủ phòng đã rời. Kết quả ván vẫn được giữ lại."; return; }
            LeaveToMainMenu();
        }

        public void LeaveToMainMenu()
        {
            if (IsMainFlowSession) { LeaveMainFlowRoom(); return; }
            _ = LeaveStandaloneToMainMenu();
        }

        private async Task LeaveStandaloneToMainMenu()
        {
            if (busy || explicitLeave || leavingScene) return;
            NotifyVoluntaryLeave();
            if (network.IsServer) NetworkReturnToMenuController.BroadcastHostSessionTerminated(network, matchEpoch);
            busy = explicitLeave = true; reconnectOfferUntil = 0;
            try
            {
                await Task.Delay(200);
                if (NetworkVoiceManager.Instance != null)
                    await Task.WhenAny(NetworkVoiceManager.Instance.LeaveVoiceChannelAsync(), Task.Delay(3000));
                await Task.WhenAny(CloseRelayMembership(), Task.Delay(3000));
            }
            catch (Exception error) { Debug.LogWarning("Rời phiên: " + error.Message); }
            finally
            {
                if (this != null)
                {
                    if (network != null && !network.ShutdownInProgress) network.Shutdown();
                    ThuongOnlineMenu.ReturnToMenu(this);
                }
            }
        }

        private void RetryConnection()
        {
            if (busy || network.IsListening || network.ShutdownInProgress || reconnectOfferUntil <= Time.realtimeSinceStartupAsDouble) return;
            if (IsMainFlowSession)
            {
                busy = true;
                network.NetworkConfig.ConnectionData = ConnectionPayload(reconnectToken, playerName, true);
                if (!network.StartClient()) { busy = false; status = "Không thể kết nối lại vào phòng."; }
                return;
            }
            if (clientUsedRelay) _ = JoinRelayConnection(true);
            else StartLanClient(true);
        }

        private async Task CloseRelayMembership()
        {
            var session = relaySession;
            if (session == null) return;
            if (session.IsMember)
            {
                try { await session.LeaveAsync(); }
                catch (SessionException error) when (error.Error is SessionError.NotInLobby or SessionError.SessionNotFound) { }
            }
            relaySession = null;
        }
    }
}
