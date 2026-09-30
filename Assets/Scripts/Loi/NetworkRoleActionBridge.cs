using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DefaultExecutionOrder(-50)]
public class NetworkRoleActionBridge : MonoBehaviour
{
    private bool phaseInitialized;
    private GamePhase lastPhase;

    // Đảm bảo Role Lobby chỉ được áp dụng một lần
    private bool lobbyRolesApplied;

    private void OnEnable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted +=
            OnActionAccepted;
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted -=
            OnActionAccepted;
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;

        if (!NetworkManager.Singleton.IsServer)
            return;

        // ==========================================
        // APPLY ROLE TỪ LOBBY
        // ==========================================

        if (!lobbyRolesApplied)
        {
            TryApplyLobbyRoles();
        }

        // ==========================================
        // PHASE
        // ==========================================

        if (NetworkPhaseSync.Instance == null)
            return;

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        if (!phaseInitialized)
        {
            phaseInitialized = true;
            lastPhase = currentPhase;

            if (currentPhase == GamePhase.Night)
                ResetNightActions();

            return;
        }

        if (currentPhase == lastPhase)
            return;

        Debug.Log(
            "ROLE ACTION BRIDGE | Phase: "
            + lastPhase
            + " → "
            + currentPhase
        );

        lastPhase = currentPhase;

        if (currentPhase == GamePhase.Night)
            ResetNightActions();
    }

    // =====================================================
    // APPLY ROLE SETUP TỪ LOBBY
    // =====================================================

    private void TryApplyLobbyRoles()
    {
        if (!NetworkGameRoleSetup.HasSetup())
            return;

        RoleManager roleManager =
            RoleManager.Instance;

        if (roleManager == null)
            roleManager =
                FindFirstObjectByType<RoleManager>();

        if (roleManager == null)
            return;

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
            playerManager =
                FindFirstObjectByType<PlayerManager>();

        if (playerManager == null)
            return;

        if (
            playerManager.players == null ||
            playerManager.players.Count == 0
        )
        {
            return;
        }

        // RoleManager phải AssignRole trước.
        // Nếu chưa có role thì chờ frame tiếp theo.
        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            return;
        }

        List<RoleAmountData> roleSetup =
            NetworkGameRoleSetup.GetRoles();

        if (roleSetup == null || roleSetup.Count == 0)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không có Role Setup từ Lobby."
            );

            return;
        }

        int totalRoles = 0;

        foreach (RoleAmountData data in roleSetup)
        {
            totalRoles += data.Amount;
        }

        int playerCount =
            playerManager.players.Count;

        if (totalRoles != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Role Count != Player Count."
                + " Players = "
                + playerCount
                + " | Roles = "
                + totalRoles
            );

            return;
        }

        // ==========================================
        // BUILD ROLE POOL TỪ LOBBY
        // ==========================================

        List<RoleType> selectedRoles =
            new List<RoleType>();

        foreach (RoleAmountData data in roleSetup)
        {
            if (data.Amount <= 0)
                continue;

            for (int i = 0; i < data.Amount; i++)
            {
                selectedRoles.Add(
                    data.Role
                );
            }
        }

        if (selectedRoles.Count != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Không thể tạo Role Pool."
            );

            return;
        }

        // ==========================================
        // SORT PLAYER THEO ID
        // ==========================================

        List<PlayerData> players =
            new List<PlayerData>(
                playerManager.players
            );

        players.RemoveAll(
            player => player == null
        );

        players.Sort(
            (a, b) =>
                a.playerID.CompareTo(
                    b.playerID
                )
        );

        if (players.Count != playerCount)
        {
            Debug.LogError(
                "ROLE ACTION BRIDGE | "
                + "Player list không hợp lệ."
            );

            return;
        }

        // ==========================================
        // SHUFFLE ROLE
        // ==========================================

        ShuffleRoles(
            selectedRoles
        );

        // ==========================================
        // CLEAR ROLE CŨ
        // ==========================================

        roleManager.playerRoles.Clear();

        // ==========================================
        // APPLY ROLE MỚI
        // ==========================================

        for (int i = 0; i < players.Count; i++)
        {
            PlayerData player =
                players[i];

            RoleType roleType =
                selectedRoles[i];

            BaseRole role =
                RoleCatalog.Create(
                    roleType,
                    player
                );

            if (role == null)
            {
                Debug.LogError(
                    "ROLE ACTION BRIDGE | "
                    + "Không tạo được Role "
                    + roleType
                    + " cho Player "
                    + player.playerID
                );

                continue;
            }

            player.roleType =
                roleType;

            player.faction =
                role.faction;

            player.votePower = 1;
            player.hasUseNightAction = false;
            player.hasVoted = false;
            player.isAlive = true;

            roleManager.playerRoles[
                player.playerID
            ] = role;

            role.OnGameStart();

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Lobby Role Applied | "
                + "Player "
                + player.playerID
                + " → "
                + roleType
            );
        }

        lobbyRolesApplied = true;

        // ==========================================
        // DEBUG
        // ==========================================

        Debug.Log(
            "========================================"
        );

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "LOBBY ROLE SETUP APPLIED"
        );

        foreach (PlayerData player in players)
        {
            Debug.Log(
                "Player "
                + player.playerID
                + " → "
                + player.roleType
            );
        }

        Debug.Log(
            "========================================"
        );
    }

    // =====================================================
    // SHUFFLE ROLE
    // =====================================================

    private void ShuffleRoles(
        List<RoleType> roles)
    {
        System.Random random =
            new System.Random();

        for (
            int i = roles.Count - 1;
            i > 0;
            i--
        )
        {
            int j =
                random.Next(
                    i + 1
                );

            RoleType temp =
                roles[i];

            roles[i] =
                roles[j];

            roles[j] =
                temp;
        }
    }

    // =====================================================
    // RESET NIGHT
    // =====================================================

    private void ResetNightActions()
    {
        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
            playerManager =
                FindFirstObjectByType<PlayerManager>();

        if (playerManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy PlayerManager."
            );

            return;
        }

        if (playerManager.players == null)
            return;

        int resetCount = 0;

        foreach (PlayerData player in playerManager.players)
        {
            if (player == null)
                continue;

            player.hasUseNightAction = false;
            resetCount++;
        }

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "RESET NIGHT ACTION | "
            + resetCount
            + " PlayerData"
        );
    }

    // =====================================================
    // ACTION ACCEPTED
    // =====================================================

    private void OnActionAccepted(
        NetworkActionEvent actionEvent)
    {
        int requesterID =
            (int)actionEvent.RequesterPlayerId;

        int targetID =
            (int)actionEvent.TargetPlayerId;

        ulong requesterClientId =
            actionEvent.RequesterPlayerId;

        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " → Target "
            + targetID
        );

        RoleManager roleManager =
            RoleManager.Instance;

        if (roleManager == null)
            roleManager =
                FindFirstObjectByType<RoleManager>();

        if (roleManager == null)
        {
            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy RoleManager!"
            );

            return;
        }

        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            roleManager.AssignRole();

            // Sau AssignRole(), cố gắng áp dụng
            // cấu hình Role từ Lobby.
            TryApplyLobbyRoles();
        }

        if (
            !roleManager.playerRoles.TryGetValue(
                requesterID,
                out BaseRole role
            )
        )
        {
            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Role của Player!"
            );

            return;
        }

        if (role == null)
        {
            SendResult(
                requesterClientId,
                false,
                "Role đang NULL!"
            );

            return;
        }

        if (NetworkPhaseSync.Instance == null)
        {
            SendResult(
                requesterClientId,
                false,
                "NetworkPhaseSync đang NULL!"
            );

            return;
        }

        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Night
        )
        {
            SendResult(
                requesterClientId,
                false,
                "Không thể Action ngoài Night!"
            );

            return;
        }

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
            playerManager =
                FindFirstObjectByType<PlayerManager>();

        if (playerManager == null)
        {
            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy PlayerManager!"
            );

            return;
        }

        PlayerData requester =
            playerManager.GetplayerByID(
                requesterID
            );

        PlayerData target =
            playerManager.GetplayerByID(
                targetID
            );

        if (requester == null)
        {
            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Requester!"
            );

            return;
        }

        if (target == null)
        {
            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Target!"
            );

            return;
        }

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "UseNightAbility("
            + targetID
            + ")"
        );

        // =====================================================
        // GỌI DEV2
        // =====================================================

        bool success =
            roleManager.UseNightAbility(
                requesterID,
                targetID
            );

        // =====================================================
        // DEV2 TỪ CHỐI
        // =====================================================

        if (!success)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Dev2 từ chối Action."
            );

            SendResult(
                requesterClientId,
                false,
                "Action không thành công!"
            );

            return;
        }

        // =====================================================
        // ACTION THÀNH CÔNG
        // =====================================================

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "Action thành công!"
        );

        SendResult(
            requesterClientId,
            true,
            "Action thành công!"
        );

        // =====================================================
        // SEER
        // =====================================================

        if (role.roleType == RoleType.Seer)
        {
            SendSeerResult(
                requesterID,
                target
            );
        }
    }

    // =====================================================
    // SEND ACTION RESULT
    // =====================================================

    private void SendResult(
        ulong clientId,
        bool success,
        string message)
    {
        if (NetworkManager.Singleton == null)
            return;

        if (
            !NetworkManager.Singleton.ConnectedClients
                .TryGetValue(
                    clientId,
                    out NetworkClient client
                )
        )
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không tìm thấy Client "
                + clientId
            );

            return;
        }

        if (client.PlayerObject == null)
            return;

        NetworkPlayerAction networkAction =
            client.PlayerObject.GetComponent<
                NetworkPlayerAction
            >();

        if (networkAction == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE | "
                + "Không tìm thấy NetworkPlayerAction."
            );

            return;
        }

        networkAction.SendActionResultToClient(
            success,
            message,
            clientId
        );
    }

    // =====================================================
    // SEER RESULT
    // =====================================================

    private void SendSeerResult(
        int requesterID,
        PlayerData target)
    {
        if (target == null)
            return;

        NetworkManager networkManager =
            NetworkManager.Singleton;

        if (networkManager == null)
            return;

        if (
            !networkManager.ConnectedClients.TryGetValue(
                (ulong)requesterID,
                out NetworkClient client
            )
        )
        {
            return;
        }

        if (client.PlayerObject == null)
            return;

        NetworkPlayerAction networkAction =
            client.PlayerObject.GetComponent<
                NetworkPlayerAction
            >();

        if (networkAction == null)
            return;

        networkAction.SendSeerResultToClient(
            target.playerID,
            target.playerName,
            target.roleType,
            (ulong)requesterID
        );
    }
}