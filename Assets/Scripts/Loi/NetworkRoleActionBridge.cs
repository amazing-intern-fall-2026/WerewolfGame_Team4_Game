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

            roles[i] = roles[j];

            roles[j] = temp;
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
        Debug.Log(
            "HUNTER DEBUG | OnActionAccepted được gọi."
        );

        int requesterID =
            (int)actionEvent.RequesterPlayerId;

        int targetID =
            (int)actionEvent.TargetPlayerId;

        ulong requesterClientId =
            actionEvent.RequesterPlayerId;

        Debug.Log(
            "HUNTER DEBUG | Requester = "
            + requesterID
            + " | Target = "
            + targetID
        );

        // =====================================================
        // ROLE MANAGER
        // =====================================================

        RoleManager roleManager =
            RoleManager.Instance;

        if (roleManager == null)
        {
            roleManager =
                FindFirstObjectByType<RoleManager>();
        }

        if (roleManager == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | RoleManager NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy RoleManager!"
            );

            return;
        }

        // =====================================================
        // ĐẢM BẢO ROLE ĐÃ ĐƯỢC ASSIGN
        // =====================================================

        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            Debug.LogWarning(
                "HUNTER DEBUG | playerRoles đang rỗng. "
                + "Đang AssignRole()."
            );

            roleManager.AssignRole();

            TryApplyLobbyRoles();
        }

        // =====================================================
        // LẤY ROLE REQUESTER
        // =====================================================

        if (
            !roleManager.playerRoles.TryGetValue(
                requesterID,
                out BaseRole role
            )
        )
        {
            Debug.LogError(
                "HUNTER DEBUG | Không tìm thấy Role của Player "
                + requesterID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Role của Player!"
            );

            return;
        }

        if (role == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Role NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Role đang NULL!"
            );

            return;
        }

        Debug.Log(
            "HUNTER DEBUG | Role của Player "
            + requesterID
            + " = "
            + role.roleType
        );

        // =====================================================
        // PHASE SYNC
        // =====================================================

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | NetworkPhaseSync NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "NetworkPhaseSync đang NULL!"
            );

            return;
        }

        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;

        Debug.Log(
            "HUNTER DEBUG | Current Phase = "
            + currentPhase
        );

        // =====================================================
        // PLAYER MANAGER
        // =====================================================

        PlayerManager playerManager =
            PlayerManager.Instance;

        if (playerManager == null)
        {
            playerManager =
                FindFirstObjectByType<PlayerManager>();
        }

        if (playerManager == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | PlayerManager NULL."
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy PlayerManager!"
            );

            return;
        }

        // =====================================================
        // REQUESTER
        // =====================================================

        PlayerData requester =
            playerManager.GetplayerByID(
                requesterID
            );

        if (requester == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Requester NULL | ID = "
                + requesterID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Requester!"
            );

            return;
        }

        // =====================================================
        // TARGET
        // =====================================================

        PlayerData target =
            playerManager.GetplayerByID(
                targetID
            );

        if (target == null)
        {
            Debug.LogError(
                "HUNTER DEBUG | Target NULL | ID = "
                + targetID
            );

            SendResult(
                requesterClientId,
                false,
                "Không tìm thấy Target!"
            );

            return;
        }

        Debug.Log(
            "HUNTER DEBUG | Requester Alive = "
            + requester.isAlive
            + " | Target Alive = "
            + target.isAlive
        );

        // =====================================================
        // HUNTER
        // =====================================================

        if (role.roleType == RoleType.Hunter)
        {
            Debug.Log(
                "HUNTER DEBUG | Hunter branch được gọi."
            );

            // -------------------------------------------------
            // Hunter phải chết
            // -------------------------------------------------

            if (requester.isAlive)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Hunter vẫn còn sống."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Hunter phải chết mới có thể đặt bẫy!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | Hunter đã chết."
            );

            // -------------------------------------------------
            // Hunter chỉ dùng trong Discussion
            // -------------------------------------------------

            if (currentPhase != GamePhase.Discussion)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Sai phase: "
                    + currentPhase
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Hunter chỉ có thể đặt bẫy trong Discussion!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | Đúng Discussion."
            );

            // -------------------------------------------------
            // Kiểm tra Hunter còn trap
            // -------------------------------------------------

            if (!requester.hasHunterTrap)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Hunter không còn trap."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Hunter không còn bẫy!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | Hunter còn trap."
            );

            // -------------------------------------------------
            // Target phải còn sống
            // -------------------------------------------------

            if (!target.isAlive)
            {
                Debug.LogWarning(
                    "HUNTER DEBUG | Target đã chết."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không thể đặt bẫy lên người đã chết!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | Target còn sống."
            );

            // -------------------------------------------------
            // Lấy HunterRole
            // -------------------------------------------------

            HunterRole hunterRole =
                role as HunterRole;

            if (hunterRole == null)
            {
                Debug.LogError(
                    "HUNTER DEBUG | Role không phải HunterRole."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không tìm thấy HunterRole!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | HunterRole OK."
            );

            // =================================================
            // DEV2: SET TRAP
            // =================================================

            hunterRole.SetTrap(
                targetID
            );

            Debug.Log(
                "HUNTER DEBUG | SetTrap() đã được gọi."
            );

            // =================================================
            // DEATH RESOLVER
            // =================================================

            if (DeathResolver.Instance == null)
            {
                Debug.LogError(
                    "HUNTER DEBUG | DeathResolver NULL."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "DeathResolver đang NULL!"
                );

                return;
            }

            Debug.Log(
                "HUNTER DEBUG | DeathResolver OK."
            );

            // =================================================
            // GIẾT TARGET NGAY
            // =================================================

            bool killed =
                DeathResolver.Instance.TryKillPlayer(
                    targetID,
                    DeathCause.Trap
                );

            Debug.Log(
                "HUNTER DEBUG | TryKillPlayer result = "
                + killed
            );

            if (!killed)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Bẫy không thể giết Target!"
                );

                return;
            }

            // =================================================
            // SUCCESS
            // =================================================

            Debug.Log(
                "Hunter Player "
                + (requesterID + 1)
                + " đặt bẫy → Player "
                + (targetID + 1)
                + " chết ngay."
            );

            SendResult(
                requesterClientId,
                true,
                "Đặt bẫy thành công! Target đã chết."
            );

            return;
        }

        // =====================================================
        // NORMAL NIGHT ACTION
        // =====================================================

        if (currentPhase != GamePhase.Night)
        {
            SendResult(
                requesterClientId,
                false,
                "Không thể Action ngoài Night!"
            );

            return;
        }

        // =====================================================
        // NORMAL DEV2 ACTION
        // =====================================================

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "UseNightAbility("
            + targetID
            + ")"
        );

        bool success;

        // =====================================================
        // GUARDIAN SELF PROTECT
        // =====================================================

        if (
            role.roleType == RoleType.VillageGuardian &&
            requesterID == targetID
        )
        {
            GuardianRole guardianRole =
                role as GuardianRole;

            if (guardianRole == null)
            {
                Debug.LogError(
                    "ROLE ACTION BRIDGE | "
                    + "Không tìm thấy GuardianRole."
                );

                SendResult(
                    requesterClientId,
                    false,
                    "Không tìm thấy GuardianRole!"
                );

                return;
            }

            // =================================================
            // REQUESTER VALIDATION
            // =================================================

            if (requester == null || !requester.isAlive)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Người chơi đã bị loại."
                );

                return;
            }

            if (!role.HasNightAbility)
            {
                SendResult(
                    requesterClientId,
                    false,
                    "Role này không có kỹ năng chủ động ban đêm."
                );

                return;
            }

            // =================================================
            // KHÔNG CHECK requester.hasUseNightAction Ở ĐÂY
            // =================================================
            //
            // NetworkPlayerAction đã quản lý việc dùng Skill
            // theo CurrentNightCycle.
            //
            // Night 1 → dùng được
            // Night 2 → dùng lại được
            // Night 3 → dùng lại được
            //
            // Nếu dùng lại trong cùng Night,
            // NetworkPlayerAction sẽ chặn.

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Guardian tự bảo vệ | Player "
                + (requesterID + 1)
            );

            // =================================================
            // DEV2 GUARDIAN LOGIC
            // =================================================
            //
            // Gọi trực tiếp GuardianRole để giữ nguyên
            // logic Dev2:
            //
            // - Có thể tự bảo vệ
            // - Không thể bảo vệ cùng một người
            //   trong hai đêm liên tiếp
            //

            success =
                guardianRole.TryUseNightAbility(
                    targetID,
                    out string guardianFeedback
                );

            if (!success)
            {
                Debug.LogWarning(
                    "ROLE ACTION BRIDGE | "
                    + "Guardian từ chối tự bảo vệ | "
                    + guardianFeedback
                );

                SendResult(
                    requesterClientId,
                    false,
                    guardianFeedback
                );

                return;
            }

            Debug.Log(
                "ROLE ACTION BRIDGE | "
                + "Guardian tự bảo vệ thành công."
            );

            SendResult(
                requesterClientId,
                true,
                guardianFeedback
            );

            return;
        }

        // =====================================================
        // NORMAL DEV2 ACTION
        // =====================================================

        success =
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
        // SEER RESULT
        // =====================================================

        if (role.roleType == RoleType.Seer)
        {
            SendSeerResult(
                requesterID,
                target
            );
        }
    }

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