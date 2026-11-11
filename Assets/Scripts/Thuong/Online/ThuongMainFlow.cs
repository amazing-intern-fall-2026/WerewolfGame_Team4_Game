using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thuong.Online
{
    // Carries the authenticated lobby into the Thuong scene without reconnecting.
    public static class ThuongMainFlow
    {
        public const string LobbyScene = "Lobby";
        public const string MenuScene = "MainMenu";
        public const string GameplayPath = "Assets/Scenes/Thuong/OnlineSceneGamePlay.unity";
        private static NetworkManager owner;
        private static PlayerLobbyData[] roster = Array.Empty<PlayerLobbyData>();
        public static bool Pending { get; private set; }
        public static string RoomCode { get; private set; }
        public static string LocalName { get; private set; }
        public static Guid ReconnectToken { get; set; }
        public static uint LastMatchEpoch { get; private set; }
        public static void RecordMatchEpoch(uint epoch) => LastMatchEpoch = Math.Max(LastMatchEpoch, epoch);
        public static uint NextMatchEpoch(uint current) => LastMatchEpoch = checked(Math.Max(LastMatchEpoch, current) + 1);
        public static IReadOnlyList<PlayerLobbyData> Roster => roster;
        public static bool BelongsTo(NetworkManager network) => owner != null && owner == network;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            owner = null;
            roster = Array.Empty<PlayerLobbyData>();
            Pending = false;
            RoomCode = string.Empty;
            LocalName = string.Empty;
            ReconnectToken = Guid.Empty;
            LastMatchEpoch = 0;
        }

        public static void Capture(NetworkManager network, PlayerLobbyData[] players, string code)
        {
            owner = network;
            roster = players?.ToArray() ?? Array.Empty<PlayerLobbyData>();
            RoomCode = code ?? string.Empty;
            Pending = true;
            LocalName = PlayerName(network.LocalClientId);
        }

        public static bool CanAdopt(NetworkManager network) => BelongsTo(network) &&
            network.IsListening && network.NetworkConfig.EnableSceneManagement;

        public static void MarkAdopted() => Pending = false;

        public static string PlayerName(ulong clientId)
        {
            foreach (var player in roster)
                if (player.ClientId == clientId) return player.PlayerName.ToString();
            if (owner != null && clientId == owner.LocalClientId) return LocalName;
            return string.Empty;
        }

        public static void RestoreLobbyNames(NetworkManager network)
        {
            if (!BelongsTo(network)) return;
            foreach (var player in roster)
                NetworkGamePlayerNameSetup.Save(player.ClientId, player.PlayerName.ToString());
        }

        public static bool GameplayAvailable => Application.CanStreamedLevelBeLoaded(GameplayPath);

        public static void PrepareLobbyNetwork(NetworkManager network)
        {
            if (network == null || network.IsListening) return;
            network.NetworkConfig.ConnectionApproval = true;
            network.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(ThuongOnlineSession.BuildPrefix + "lobby");
            network.ConnectionApprovalCallback = ApproveLobby;
        }

        private static void ApproveLobby(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = System.Text.Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>()) == ThuongOnlineSession.BuildPrefix + "lobby";
            response.CreatePlayerObject = response.Approved;
            response.Pending = false;
            if (!response.Approved) response.Reason = "Khác phiên bản. Hãy cập nhật bản game trước khi vào phòng.";
        }
    }
}
