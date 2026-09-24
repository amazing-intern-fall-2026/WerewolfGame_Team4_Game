using UnityEngine;
using Unity.Netcode;

public class NetworkRoleActionBridge : MonoBehaviour
{
    // =========================================
    // Night Phase Tracking
    // =========================================

    private bool phaseInitialized;

    private GamePhase lastPhase;


    // =========================================
    // Enable / Disable
    // =========================================

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


    // =========================================
    // Update
    // =========================================

    private void Update()
    {
        if (NetworkManager.Singleton == null)
            return;


        if (!NetworkManager.Singleton.IsServer)
            return;


        if (NetworkPhaseSync.Instance == null)
            return;


        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;


        if (!phaseInitialized)
        {
            phaseInitialized = true;

            lastPhase = currentPhase;


            if (currentPhase == GamePhase.Night)
            {
                ResetNightActions();
            }


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
        {
            ResetNightActions();
        }
    }


    // =========================================
    // Reset Night Action
    // =========================================

    private void ResetNightActions()
    {
        PlayerManager playerManager =
            PlayerManager.Instance;


        if (playerManager == null)
        {
            playerManager =
                FindFirstObjectByType<PlayerManager>();
        }


        if (playerManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy PlayerManager "
                + "khi reset Night."
            );

            return;
        }


        if (playerManager.players == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "PlayerManager.players đang NULL."
            );

            return;
        }


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
            + "RESET NIGHT ACTION"
            + " | Reset "
            + resetCount
            + " PlayerData"
        );
    }


    // =========================================
    // Network Action Accepted
    // =========================================

    private void OnActionAccepted(
        NetworkActionEvent actionEvent)
    {
        int requesterID =
            (int)actionEvent.RequesterPlayerId;


        int targetID =
            (int)actionEvent.TargetPlayerId;


        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " → Target "
            + targetID
        );


        // =========================================
        // 1. RoleManager
        // =========================================

        RoleManager roleManager =
            RoleManager.Instance;


        if (roleManager == null)
        {
            roleManager =
                FindFirstObjectByType<RoleManager>();
        }


        if (roleManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy RoleManager!"
            );

            return;
        }


        // =========================================
        // 2. Kiểm tra Role
        // =========================================

        if (
            roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0
        )
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "playerRoles đang rỗng!"
            );


            roleManager.AssignRole();
        }


        if (
            !roleManager.playerRoles.TryGetValue(
                requesterID,
                out BaseRole role
            )
        )
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy Role của Player "
                + requesterID
            );

            return;
        }


        if (role == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Role đang NULL!"
            );

            return;
        }


        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " | Role = "
            + role.roleType
        );


        // =========================================
        // 3. NightManager
        // =========================================

        NightManager nightManager =
            NightManager.Instance;


        if (nightManager == null)
        {
            nightManager =
                FindFirstObjectByType<NightManager>();


            if (nightManager != null)
            {
                NightManager.Instance =
                    nightManager;
            }
        }


        if (nightManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy NightManager!"
            );

            return;
        }


        // =========================================
        // 4. Game Flow
        // =========================================

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "NetworkPhaseSync = NULL"
            );

            return;
        }


        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;


        if (currentPhase != GamePhase.Night)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không thể Action ngoài Night!"
            );

            return;
        }


        // =========================================
        // 5. PlayerManager
        // =========================================

        PlayerManager playerManager =
            PlayerManager.Instance;


        if (playerManager == null)
        {
            playerManager =
                FindFirstObjectByType<PlayerManager>();
        }


        if (playerManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy PlayerManager!"
            );

            return;
        }


        // =========================================
        // 6. Requester
        // =========================================

        PlayerData requester =
            playerManager.GetplayerByID(
                requesterID
            );


        if (requester == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy Requester "
                + requesterID
            );

            return;
        }


        // =========================================
        // 7. Target
        // =========================================

        PlayerData target =
            playerManager.GetplayerByID(
                targetID
            );


        if (target == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Không tìm thấy Target "
                + targetID
            );

            return;
        }


        // =========================================
        // 8. Gọi Dev2
        // =========================================

        Debug.Log(
            "ROLE ACTION BRIDGE | "
            + "UseNightAbility("
            + targetID
            + ")"
        );


        bool success =
            roleManager.UseNightAbility(
                requesterID,
                targetID
            );


        // =========================================
        // 9. Action thất bại
        // =========================================

        if (!success)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: "
                + "Action bị từ chối bởi game flow."
            );

            return;
        }


        // =========================================
        // 10. Action thành công
        // =========================================

        Debug.Log(
            "ROLE ACTION BRIDGE: "
            + "Action thành công!"
        );


        // =========================================
        // 11. SEER RESULT
        // =========================================

        if (role.roleType == RoleType.Seer)
        {
            SendSeerResult(
                requesterID,
                target
            );
        }
    }


    // =========================================
    // Send Seer Result
    // =========================================

    private void SendSeerResult(
        int requesterID,
        PlayerData target)
    {
        if (target == null)
            return;


        Debug.Log(
            "SEER RESULT | Requester = "
            + requesterID
            + " | Target = "
            + target.playerName
            + " | Role = "
            + target.roleType
        );


        NetworkManager networkManager =
            NetworkManager.Singleton;


        if (networkManager == null)
            return;


        if (
            !networkManager.ConnectedClients.ContainsKey(
                (ulong)requesterID
            )
        )
        {
            Debug.LogWarning(
                "SEER RESULT: "
                + "Không tìm thấy Client của Seer."
            );

            return;
        }


        NetworkClient client =
            networkManager.ConnectedClients[
                (ulong)requesterID
            ];


        if (client.PlayerObject == null)
        {
            Debug.LogWarning(
                "SEER RESULT: "
                + "PlayerObject của Seer NULL."
            );

            return;
        }


        NetworkPlayerAction networkAction =
            client.PlayerObject.GetComponent<
                NetworkPlayerAction
            >();


        if (networkAction == null)
        {
            Debug.LogWarning(
                "SEER RESULT: "
                + "Không tìm thấy NetworkPlayerAction."
            );

            return;
        }


        networkAction.SendSeerResultToClient(
            target.playerID,
            target.playerName,
            target.roleType,
            (ulong)requesterID
        );
    }
}