using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoleSync : NetworkBehaviour
{
    public static NetworkRoleSync LocalInstance;

    public static event Action<RoleType> OnLocalRoleReceived;
    public static event Action<bool> OnLocalWhiteHoundAwakenedChanged;
    public static event Action<bool, bool> OnLocalShamanCharmsChanged;
    public static event Action<bool> OnLocalHunterRevengeStateChanged;

    public RoleType LocalRole { get; private set; }
    public bool HasAssignedRole { get; private set; }
    public bool IsWhiteHoundAwakened { get; private set; }
    public bool HasUsedSave { get; set; }
    public bool HasUsedKill { get; set; }
    public bool CanHunterRevengeShot { get; private set; }
    public bool HasUsedHunterShot { get; set; }

    [SerializeField] private ulong testOwnerClientId = ulong.MaxValue;
    public ulong TargetPlayerId => testOwnerClientId != ulong.MaxValue ? testOwnerClientId : OwnerClientId;

    public void SetOwnerIdForTest(ulong clientId)
    {
        testOwnerClientId = clientId;
    }

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

        int playerID = (int)TargetPlayerId;

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
        int playerID = (int)TargetPlayerId;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        RoleType roleType = RoleType.Villager;
        bool awakened = false;

        if (pData != null)
        {
            roleType = pData.roleType;
            awakened = pData.isWhiteHoundAwakened;
        }
        else if (RoleManager.Instance?.playerRoles != null &&
                 RoleManager.Instance.playerRoles.TryGetValue(playerID, out BaseRole role) && role != null)
        {
            roleType = role.roleType;
        }

        if (RoleManager.Instance?.playerRoles != null &&
            RoleManager.Instance.playerRoles.TryGetValue(playerID, out BaseRole r) && r != null)
        {
            if (awakened)
            {
                r.faction = FactionType.Monster;
            }
        }

        if (awakened && pData != null)
        {
            pData.faction = FactionType.Monster;
            if (pData.roleType == RoleType.WhiteHound || pData.roleType == RoleType.WhiteWolf)
            {
                pData.roleType = RoleType.DogSpirit;
                roleType = RoleType.DogSpirit;
            }
        }

        LocalRole = roleType;
        HasAssignedRole = true;
        IsWhiteHoundAwakened = awakened;

        bool isShamanRole = roleType == RoleType.Shaman || (pData != null && pData.roleType == RoleType.Shaman);
        if (isShamanRole)
        {
            bool saveUsed = NetworkRoleActionBridge.IsShamanSaveUsed(playerID);
            bool killUsed = NetworkRoleActionBridge.IsShamanKillUsed(playerID);
            if (RoleManager.Instance?.playerRoles != null &&
                RoleManager.Instance.playerRoles.TryGetValue(playerID, out BaseRole br) &&
                br is ShamanRole sr)
            {
                if (!sr.HasGoodCharm) saveUsed = true;
                if (!sr.HasBadCharm) killUsed = true;
            }
            HasUsedSave = saveUsed;
            HasUsedKill = killUsed;
        }

        // Nếu chạy local hoặc unit test không có NetworkManager
        if (NetworkManager.Singleton == null)
        {
            OnLocalRoleReceived?.Invoke(roleType);
            OnLocalWhiteHoundAwakenedChanged?.Invoke(awakened);
            if (isShamanRole)
            {
                OnLocalShamanCharmsChanged?.Invoke(HasUsedSave, HasUsedKill);
            }
            return;
        }

        if (!IsServer)
            return;

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

        if (isShamanRole)
        {
            SendShamanCharmsToOwnerClient(
                HasUsedSave,
                HasUsedKill,
                OwnerClientId
            );
        }

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
        HasAssignedRole = true;

        int playerID = (int)TargetPlayerId;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        if (pData != null)
        {
            pData.roleType = roleType;
            if (roleType == RoleType.DogSpirit && pData.isWhiteHoundAwakened)
            {
                pData.faction = FactionType.Monster;
            }
        }

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

        int playerID = (int)TargetPlayerId;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        if (pData != null)
        {
            pData.isWhiteHoundAwakened = awakened;
            if (awakened)
            {
                pData.faction = FactionType.Monster;
                pData.roleType = RoleType.DogSpirit;
                LocalRole = RoleType.DogSpirit;
            }
        }

        Debug.Log(
            "NETWORK ROLE SYNC | WHITEHOUND AWAKENED = "
            + awakened
            + " -> Đổi qua phe DogSpirit và mở khóa chức năng tấn công!"
        );

        OnLocalWhiteHoundAwakenedChanged?.Invoke(awakened);
    }

    public void SendShamanCharmsToOwnerClient(
        bool hasUsedSave,
        bool hasUsedKill,
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

        ReceiveShamanCharmsClientRpc(
            hasUsedSave,
            hasUsedKill,
            rpcParams
        );
    }

    [ClientRpc]
    private void ReceiveShamanCharmsClientRpc(
        bool hasUsedSave,
        bool hasUsedKill,
        ClientRpcParams clientRpcParams = default)
    {
        HasUsedSave = hasUsedSave;
        HasUsedKill = hasUsedKill;

        Debug.Log(
            $"NETWORK ROLE SYNC | SHAMAN CHARMS: Save={hasUsedSave}, Kill={hasUsedKill}"
        );

        OnLocalShamanCharmsChanged?.Invoke(hasUsedSave, hasUsedKill);
    }

    public static void SyncShamanCharmState(ulong clientId, bool saveUsed, bool killUsed)
    {
        NetworkRoleSync[] syncs = FindObjectsByType<NetworkRoleSync>(FindObjectsSortMode.None);
        foreach (var s in syncs)
        {
            if (s.TargetPlayerId == clientId)
            {
                s.HasUsedSave = saveUsed;
                s.HasUsedKill = killUsed;

                if (NetworkManager.Singleton == null)
                {
                    OnLocalShamanCharmsChanged?.Invoke(saveUsed, killUsed);
                }
                else if (NetworkManager.Singleton.IsServer)
                {
                    s.SendShamanCharmsToOwnerClient(saveUsed, killUsed, clientId);
                }
                break;
            }
        }
    }

    public void SendHunterRevengeStateToOwnerClient(
        bool canShoot,
        bool hasUsedShot,
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

        ReceiveHunterRevengeStateClientRpc(
            canShoot,
            hasUsedShot,
            rpcParams
        );
    }

    [ClientRpc]
    private void ReceiveHunterRevengeStateClientRpc(
        bool canShoot,
        bool hasUsedShot,
        ClientRpcParams clientRpcParams = default)
    {
        CanHunterRevengeShot = canShoot;
        HasUsedHunterShot = hasUsedShot;

        int playerID = (int)TargetPlayerId;
        PlayerData pData = PlayerManager.Instance?.GetplayerByID(playerID);
        if (pData != null)
        {
            pData.hasHunterTrap = canShoot;
            pData.hasUsedHunterShot = hasUsedShot;
        }

        Debug.Log(
            $"NETWORK ROLE SYNC | HUNTER REVENGE: CanShoot={canShoot}, HasUsedShot={hasUsedShot}"
        );

        OnLocalHunterRevengeStateChanged?.Invoke(canShoot);
    }

    public static void SyncHunterRevengeState(ulong clientId, bool canShoot, bool hasUsedShot)
    {
        NetworkRoleSync[] syncs = FindObjectsByType<NetworkRoleSync>(FindObjectsSortMode.None);
        foreach (var s in syncs)
        {
            if (s.TargetPlayerId == clientId)
            {
                s.CanHunterRevengeShot = canShoot;
                s.HasUsedHunterShot = hasUsedShot;

                if (NetworkManager.Singleton == null)
                {
                    OnLocalHunterRevengeStateChanged?.Invoke(canShoot);
                }
                else if (NetworkManager.Singleton.IsServer)
                {
                    s.SendHunterRevengeStateToOwnerClient(canShoot, hasUsedShot, clientId);
                }
                break;
            }
        }
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
        HasUsedSave = false;
        HasUsedKill = false;
        LocalRole = RoleType.Villager;
        HasAssignedRole = false;
        OnLocalWhiteHoundAwakenedChanged?.Invoke(false);
        OnLocalShamanCharmsChanged?.Invoke(false, false);
        CanHunterRevengeShot = false;
        HasUsedHunterShot = false;
        OnLocalHunterRevengeStateChanged?.Invoke(false);
    }

    public static void ResetAllRoleSyncs()
    {
        NetworkRoleSync[] syncs = null;
        try { syncs = FindObjectsByType<NetworkRoleSync>(FindObjectsSortMode.None); } catch { syncs = new NetworkRoleSync[0]; }
        if (syncs != null)
        {
            foreach (var s in syncs)
            {
                s.ResetForNewMatch();
            }
        }
        try { Debug.Log($"NETWORK ROLE SYNC: Đã reset {syncs?.Length ?? 0} NetworkRoleSync."); } catch { }
    }

    /// <summary>
    /// Xóa toàn bộ static event listener, static instance và cache khi rời phòng hoặc vào phòng mới.
    /// Giúp ngăn rò rỉ bộ nhớ, lỗi MissingReferenceException và đảm bảo ván mới nhận đúng role/sprite.
    /// </summary>
    public static void ResetStaticSessionState()
    {
        OnLocalRoleReceived = null;
        OnLocalWhiteHoundAwakenedChanged = null;
        OnLocalShamanCharmsChanged = null;
        LocalInstance = null;

        NetworkGameRoleSetup.Clear();
        RoleInfoDatabase.ClearSpriteCache();

        Debug.Log("NETWORK ROLE SYNC: ResetStaticSessionState hoàn tất.");
    }
}