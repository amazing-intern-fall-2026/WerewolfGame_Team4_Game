using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        public bool IsMainFlowSession { get; private set; }
        private Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> previousApproval;
        private float nextMainFlowHello;
        private bool mainFlowLeaving;
        private const string MainFlowTicket = "thuong.mainflow.ticket.v1";

        private void Start()
        {
            if (!IsMainFlowSession) return;
            // The lobby's NGO transport is already connected. Its callbacks do not fire again on scene load.
            NetworkGamePlayerSetup.Clear();
            NetworkGameManager.LeftPlayerClientIds.Clear();
            sessionCode = ThuongMainFlow.RoomCode;
            matchEpoch = ThuongMainFlow.LastMatchEpoch;
            RegisterMessageHandlers();
            if (network.IsServer)
            {
                int index = 0;
                foreach (var original in ThuongMainFlow.Roster.OrderBy(member => member.ClientId))
                {
                    if (!network.ConnectedClientsIds.Contains(original.ClientId)) continue;
                    serverPlayers[original.ClientId] = new ThuongOnlineServerPlayer
                    {
                        ClientId = original.ClientId,
                        ReconnectToken = Guid.NewGuid(),
                        PlayerId = index,
                        Name = SanitizeName(original.PlayerName.ToString()),
                        CharacterIndex = ClampCharacterIndex(index),
                        Position = SpawnPosition(index++)
                    };
                }
                // These player objects belong to the old Game scene's implementation.
                // Thuong creates its own presentation and server-side PlayerData.
                foreach (var client in network.ConnectedClients.Values.ToArray())
                    if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
                        client.PlayerObject.Despawn(true);
                HandleServerStarted();
            }
            connected = true;
            HandleClientConnected(network.LocalClientId);
            ThuongMainFlow.MarkAdopted();
            status = "Đã giữ kết nối từ lobby. Chọn map, phân vai và Ready để bắt đầu.";
        }

        private void UpdateMainFlowHandshake()
        {
            if (!IsMainFlowSession || !connected || matchStarted || network == null || !network.IsListening ||
                lobby.Any(member => member.ClientId == network.LocalClientId) || Time.unscaledTime < nextMainFlowHello) return;
            // A faster-loading peer can send Hello before the host installs its scene handlers.
            nextMainFlowHello = Time.unscaledTime + .5f;
            SendHello();
        }

        public bool ReturnToMainLobby()
        {
            if (!IsMainFlowSession || !CanRestartMatch || mainFlowLeaving) return false;
            ThuongMainFlow.Capture(network, serverPlayers.Values.Where(member => !member.Offline)
                .Select(member => new PlayerLobbyData(member.ClientId, member.Name)).ToArray(), sessionCode);
            var result = network.SceneManager.LoadScene(ThuongMainFlow.LobbyScene, LoadSceneMode.Single);
            if (result != SceneEventProgressStatus.Started)
            {
                status = "Không thể về lobby: " + result;
                return false;
            }
            mainFlowLeaving = true;
            return true;
        }

        private async void LeaveMainFlowRoom()
        {
            if (mainFlowLeaving) return;
            NotifyVoluntaryLeave();
            if (network.IsServer) NetworkReturnToMenuController.BroadcastHostSessionTerminated(network, matchEpoch);
            explicitLeave = true;
            mainFlowLeaving = true;
            busy = true;
            await System.Threading.Tasks.Task.Delay(200);
            if (this == null || network == null) return;
            if (SceneLoader.Instance == null)
                new GameObject("Main Flow Scene Loader").AddComponent<SceneLoader>();
            SceneLoader.Instance.LeaveRoomToMainMenu();
        }

        private void RegisterMainFlowMessages() => network.CustomMessagingManager.RegisterNamedMessageHandler(MainFlowTicket, ReceiveMainFlowTicket);
        private void UnregisterMainFlowMessages() => network?.CustomMessagingManager?.UnregisterNamedMessageHandler(MainFlowTicket);

        private void SendMainFlowTicket(ulong clientId)
        {
            if (!IsMainFlowSession || !serverPlayers.TryGetValue(clientId, out var member)) return;
            if (clientId == network.LocalClientId)
            { reconnectToken = member.ReconnectToken; ThuongMainFlow.ReconnectToken = reconnectToken; return; }
            using var writer = new Unity.Netcode.FastBufferWriter(80, Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(new Unity.Collections.FixedString64Bytes(member.ReconnectToken.ToString("N")));
            network.CustomMessagingManager.SendNamedMessage(MainFlowTicket, clientId, writer);
        }

        private void ReceiveMainFlowTicket(ulong sender, Unity.Netcode.FastBufferReader reader)
        {
            if (!IsMainFlowSession || network.IsServer || sender != NetworkManager.ServerClientId) return;
            try
            {
                reader.ReadValueSafe(out Unity.Collections.FixedString64Bytes value);
                if (Guid.TryParseExact(value.ToString(), "N", out var token))
                    ThuongMainFlow.ReconnectToken = reconnectToken = token;
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException)
            { Debug.LogWarning("[Thuong] Bỏ qua vé kết nối lại không hợp lệ."); }
        }
    }
}
