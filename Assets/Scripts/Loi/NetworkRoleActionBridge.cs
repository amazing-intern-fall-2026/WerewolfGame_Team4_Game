using UnityEngine;
using Unity.Netcode;

public class NetworkRoleActionBridge : MonoBehaviour
{
    private bool phaseInitialized;
    private GamePhase lastPhase;

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
                "ROLE ACTION BRIDGE: Không tìm thấy PlayerManager."
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
            "ROLE ACTION BRIDGE | RESET NIGHT ACTION | "
            + resetCount
            + " PlayerData"
        );
    }

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