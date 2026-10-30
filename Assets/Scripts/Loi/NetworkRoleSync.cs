using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoleSync : NetworkBehaviour
{
    public static NetworkRoleSync LocalInstance;

    public static event Action<RoleType> OnLocalRoleReceived;
    public static event Action<bool> OnLocalWhiteHoundAwakenedChanged;

    public RoleType LocalRole { get; private set; }
    public bool IsWhiteHoundAwakened { get; private set; }

    private bool roleSent;
    private bool awakenedSent;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
        }
    }

    private void Update()
    {
        if (!IsServer)
            return;

        // Nếu có Role Setup từ Lobby thì chờ NetworkRoleActionBridge áp dụng xong Lobby Roles trước
        if (NetworkGameRoleSetup.HasSetup())
        {
            if (NetworkRoleActionBridge.Instance == null || !NetworkRoleActionBridge.Instance.LobbyRolesApplied)
            {
                return;
            }
        }

        if (RoleManager.Instance == null)
            return;

        if (RoleManager.Instance.playerRoles == null)
            return;

        int playerID = (int)OwnerClientId;

        if (!roleSent)
        {
            if (!RoleManager.Instance.playerRoles.TryGetValue(
                playerID,
                out BaseRole role))
            {
                return;
            }

            if (role == null)
                return;

            RoleType roleType = role.roleType;

            Debug.Log(
                "NETWORK ROLE SYNC | Server tìm thấy Role"
                + " | Player "
                + playerID
                + " → "
                + roleType
            );

            SendRoleToOwnerClient(
                roleType,
                OwnerClientId
            );

            roleSent = true;
        }

        // Khi sang ngày hôm sau sau khi WhiteHound bị DogSpirit cắn, đồng bộ chuyển phe DogSpirit và mở kỹ năng tấn công
        if (!awakenedSent)
        {
            bool isDaytimeOrAfterNight = NetworkPhaseSync.Instance == null ||
                                         (NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Night &&
                                          NetworkPhaseSync.Instance.CurrentPhase.Value != GamePhase.Setup);
            if (isDaytimeOrAfterNight)
            {
                PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
                if (pData != null && pData.isWhiteHoundAwakened)
                {
                    pData.faction = FactionType.Monster;
                    if (RoleManager.Instance.playerRoles.TryGetValue(playerID, out BaseRole r) && r != null)
                    {
                        r.faction = FactionType.Monster;
                    }
                    SendWhiteHoundAwakenedToOwnerClient(true, OwnerClientId);
                    awakenedSent = true;
                }
            }
        }
    }

    public void SyncCurrentRole()
    {
        if (!IsServer)
            return;

        if (RoleManager.Instance == null || RoleManager.Instance.playerRoles == null)
            return;

        int playerID = (int)OwnerClientId;

        if (!RoleManager.Instance.playerRoles.TryGetValue(playerID, out BaseRole role) || role == null)
            return;

        RoleType roleType = role.roleType;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        bool awakened = pData != null && pData.isWhiteHoundAwakened;
        if (awakened)
        {
            pData.faction = FactionType.Monster;
            role.faction = FactionType.Monster;
        }

        Debug.Log(
            "NETWORK ROLE SYNC | Server đồng bộ Role mới"
            + " | Player "
            + playerID
            + " → "
            + roleType
            + " | Awakened = "
            + awakened
        );

        SendRoleToOwnerClient(
            roleType,
            OwnerClientId
        );

        SendWhiteHoundAwakenedToOwnerClient(
            awakened,
            OwnerClientId
        );

        roleSent = true;
        awakenedSent = awakened;
    }

    public static void SyncAllRoleSyncs()
    {
        NetworkRoleSync[] syncs = FindObjectsByType<NetworkRoleSync>(FindObjectsSortMode.None);
        foreach (var s in syncs)
        {
            s.SyncCurrentRole();
        }
        Debug.Log($"NETWORK ROLE SYNC: Đã đồng bộ role mới cho {syncs.Length} NetworkRoleSync.");
    }

    private void SendRoleToOwnerClient(
        RoleType roleType,
        ulong clientId)
    {
        ClientRpcParams rpcParams =
            new ClientRpcParams
            {
                Send =
                    new ClientRpcSendParams
                    {
                        TargetClientIds =
                            new List<ulong>
                            {
                                clientId
                            }
                    }
            };

        ReceiveRoleClientRpc(
            roleType,
            rpcParams
        );
    }

    [ClientRpc]
    private void ReceiveRoleClientRpc(
        RoleType roleType,
        ClientRpcParams clientRpcParams = default)
    {
        LocalRole = roleType;

        Debug.Log(
            "NETWORK ROLE SYNC | MY ROLE = "
            + roleType
        );

        OnLocalRoleReceived?.Invoke(roleType);
    }

    private void SendWhiteHoundAwakenedToOwnerClient(
        bool awakened,
        ulong clientId)
    {
        ClientRpcParams rpcParams =
            new ClientRpcParams
            {
                Send =
                    new ClientRpcSendParams
                    {
                        TargetClientIds =
                            new List<ulong>
                            {
                                clientId
                            }
                    }
            };

        ReceiveWhiteHoundAwakenedClientRpc(
            awakened,
            rpcParams
        );
    }

    [ClientRpc]
    private void ReceiveWhiteHoundAwakenedClientRpc(
        bool awakened,
        ClientRpcParams clientRpcParams = default)
    {
        IsWhiteHoundAwakened = awakened;

        int playerID = (int)OwnerClientId;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        if (pData != null)
        {
            pData.isWhiteHoundAwakened = awakened;
            if (awakened)
            {
                pData.faction = FactionType.Monster;
            }
        }

        Debug.Log(
            "NETWORK ROLE SYNC | WHITEHOUND AWAKENED = "
            + awakened
            + " -> Đổi qua phe DogSpirit và mở khóa chức năng tấn công!"
        );

        OnLocalWhiteHoundAwakenedChanged?.Invoke(awakened);
    }

    private void OnDestroy()
    {
        if (LocalInstance == this)
        {
            LocalInstance = null;
        }
    }

    /// <summary>
    /// Cho phép gửi lại Role khi bắt đầu ván mới.
    /// </summary>
    public void ResetForNewMatch()
    {
        roleSent = false;
        awakenedSent = false;
        IsWhiteHoundAwakened = false;
        LocalRole = RoleType.Villager;
        OnLocalWhiteHoundAwakenedChanged?.Invoke(false);
    }

    public static void ResetAllRoleSyncs()
    {
        NetworkRoleSync[] syncs = FindObjectsByType<NetworkRoleSync>(FindObjectsSortMode.None);
        foreach (var s in syncs)
        {
            s.ResetForNewMatch();
        }
        Debug.Log($"NETWORK ROLE SYNC: Đã reset {syncs.Length} NetworkRoleSync.");
    }
}