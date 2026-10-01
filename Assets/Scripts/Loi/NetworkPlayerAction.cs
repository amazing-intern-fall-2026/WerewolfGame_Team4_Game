using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    public static event Action<NetworkActionEvent>
        OnNetworkActionAccepted;

    public static event Action<bool, string>
        OnNetworkActionResult;

    public static event Action<int, string, RoleType>
        OnSeerResultReceived;


    // =========================================================
    // REQUEST ACTION
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    public void RequestActionServerRpc(
        ulong targetPlayerId,
        ServerRpcParams rpcParams = default)
    {
        ulong requesterClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "SERVER: Player "
            + requesterClientId
            + " yêu cầu Action → Player "
            + targetPlayerId
        );


        // =====================================================
        // NETWORK MANAGER
        // =====================================================

        if (NetworkManager.Singleton == null)
            return;


        // =====================================================
        // TARGET EXIST
        // =====================================================

        if (
            !NetworkManager.Singleton.ConnectedClients
                .ContainsKey(targetPlayerId)
        )
        {
            SendActionResultToClient(
                false,
                "Target Player không tồn tại!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // SELF TARGET
        // =====================================================

        if (requesterClientId == targetPlayerId)
        {
            SendActionResultToClient(
                false,
                "Không thể Action chính mình!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // REQUESTER EXIST
        // =====================================================

        if (
            !NetworkManager.Singleton.ConnectedClients
                .ContainsKey(requesterClientId)
        )
        {
            Debug.LogWarning(
                "SERVER: Player gửi request không tồn tại!"
            );

            return;
        }


        NetworkClient requesterClient =
            NetworkManager.Singleton
                .ConnectedClients[requesterClientId];


        if (requesterClient.PlayerObject == null)
        {
            SendActionResultToClient(
                false,
                "Player Object không tồn tại!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // REQUESTER PLAYER CONTROLLER
        // =====================================================

        PlayerController requester =
            requesterClient.PlayerObject
                .GetComponent<PlayerController>();


        if (requester == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy PlayerController!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // REQUESTER STATE
        // =====================================================

        NetworkPlayerStateSync requesterState =
            requester.GetComponent<NetworkPlayerStateSync>();


        if (requesterState == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy PlayerState!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // HUNTER SPECIAL ACTION
        // =====================================================

        if (
            IsHunterSpecialAction(
                requesterClientId,
                requesterState
            )
        )
        {
            HandleHunterAction(
                requesterClientId,
                targetPlayerId
            );

            return;
        }


        // =====================================================
        // DEAD / SPECTATING
        // =====================================================

        if (
            requesterState.State.Value ==
                NetworkPlayerStateType.Dead
            ||
            requesterState.State.Value ==
                NetworkPlayerStateType.Spectating
        )
        {
            SendActionResultToClient(
                false,
                "Player đã chết, không thể Action!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // PHASE SYNC
        // =====================================================

        if (NetworkPhaseSync.Instance == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy NetworkPhaseSync!",
                requesterClientId
            );

            return;
        }


        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;


        // =====================================================
        // NORMAL ACTION = NIGHT ONLY
        // =====================================================

        if (currentPhase != GamePhase.Night)
        {
            SendActionResultToClient(
                false,
                "Không thể Action ở Phase "
                + currentPhase
                + "!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // TARGET CLIENT
        // =====================================================

        NetworkClient targetClient =
            NetworkManager.Singleton
                .ConnectedClients[targetPlayerId];


        if (targetClient.PlayerObject == null)
        {
            SendActionResultToClient(
                false,
                "Target Player Object không tồn tại!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // TARGET PLAYER
        // =====================================================

        PlayerController target =
            targetClient.PlayerObject
                .GetComponent<PlayerController>();


        if (target == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy Target PlayerController!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // TARGET STATE
        // =====================================================

        NetworkPlayerStateSync targetState =
            target.GetComponent<NetworkPlayerStateSync>();


        if (targetState == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy Target PlayerState!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // TARGET DEAD
        // =====================================================

        if (
            targetState.State.Value ==
                NetworkPlayerStateType.Dead
            ||
            targetState.State.Value ==
                NetworkPlayerStateType.Spectating
        )
        {
            SendActionResultToClient(
                false,
                "Target đã chết!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // NORMAL ACTION ACCEPTED
        // =====================================================

        Debug.Log(
            "SERVER: Network Action hợp lệ | Player "
            + requesterClientId
            + " → Player "
            + targetPlayerId
        );


        NetworkActionEvent actionEvent =
            new NetworkActionEvent(
                requesterClientId,
                targetPlayerId
            );


        OnNetworkActionAccepted?.Invoke(
            actionEvent
        );
    }


    // =========================================================
    // HUNTER VALIDATION
    // =========================================================

    private bool IsHunterSpecialAction(
        ulong requesterClientId,
        NetworkPlayerStateSync requesterState)
    {
        if (!IsServer)
            return false;


        if (requesterState == null)
            return false;


        // Hunter phải đã chết
        if (
            requesterState.State.Value !=
            NetworkPlayerStateType.Dead
        )
        {
            return false;
        }


        // Phải đang Discussion
        if (NetworkPhaseSync.Instance == null)
            return false;


        if (
            NetworkPhaseSync.Instance.CurrentPhase.Value
            != GamePhase.Discussion
        )
        {
            return false;
        }


        // RoleManager Dev2
        if (RoleManager.Instance == null)
            return false;


        if (RoleManager.Instance.playerRoles == null)
            return false;


        // Lấy role của Hunter
        if (
            !RoleManager.Instance.playerRoles.TryGetValue(
                (int)requesterClientId,
                out BaseRole role
            )
        )
        {
            return false;
        }


        if (role == null)
            return false;


        return role.roleType ==
               RoleType.Hunter;
    }


    // =========================================================
    // HUNTER ACTION
    // =========================================================

    private void HandleHunterAction(
        ulong requesterClientId,
        ulong targetPlayerId)
    {
        Debug.Log(
            "HUNTER NETWORK | Hunter "
            + requesterClientId
            + " chọn Player "
            + targetPlayerId
        );


        // =====================================================
        // ROLE MANAGER
        // =====================================================

        if (RoleManager.Instance == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy RoleManager!",
                requesterClientId
            );

            return;
        }


        if (RoleManager.Instance.playerRoles == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy Player Roles!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // GET ROLE
        // =====================================================

        if (
            !RoleManager.Instance.playerRoles.TryGetValue(
                (int)requesterClientId,
                out BaseRole role
            )
        )
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy Role của Hunter!",
                requesterClientId
            );

            return;
        }


        if (role == null)
        {
            SendActionResultToClient(
                false,
                "Role Hunter không tồn tại!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // CHECK HUNTER
        // =====================================================

        if (role.roleType != RoleType.Hunter)
        {
            SendActionResultToClient(
                false,
                "Player này không phải Hunter!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // GET HUNTER ROLE
        // =====================================================

        HunterRole hunter =
            role as HunterRole;


        if (hunter == null)
        {
            SendActionResultToClient(
                false,
                "Không thể lấy HunterRole!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // CALL DEV2 LOGIC
        // =====================================================

        if (!hunter.TrySetTrap((int)targetPlayerId, out string trapFeedback))
        {
            SendActionResultToClient(
                false,
                trapFeedback,
                requesterClientId
            );
            return;
        }


        Debug.Log(
            "HUNTER NETWORK | "
            + "HunterRole.SetTrap("
            + targetPlayerId
            + ")"
        );


        // =====================================================
        // RESULT
        // =====================================================

        SendActionResultToClient(
            true,
            trapFeedback,
            requesterClientId
        );
    }


    // =========================================================
    // ACTION RESULT
    // =========================================================

    public void SendActionResultToClient(
        bool success,
        string message,
        ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "ACTION RESULT: Chỉ Server mới được gửi kết quả!"
            );

            return;
        }


        if (NetworkManager.Singleton == null)
            return;


        if (
            !NetworkManager.Singleton.ConnectedClients
                .ContainsKey(clientId)
        )
        {
            Debug.LogWarning(
                "ACTION RESULT: Client "
                + clientId
                + " không tồn tại!"
            );

            return;
        }


        ClientRpcParams rpcParams =
            new ClientRpcParams
            {
                Send =
                    new ClientRpcSendParams
                    {
                        TargetClientIds =
                            new ulong[]
                            {
                                clientId
                            }
                    }
            };


        SendActionResultClientRpc(
            success,
            message,
            rpcParams
        );
    }


    [ClientRpc]
    private void SendActionResultClientRpc(
        bool success,
        string message,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log(
            "ACTION RESULT | Success = "
            + success
            + " | "
            + message
        );


        OnNetworkActionResult?.Invoke(
            success,
            message
        );
    }


    // =========================================================
    // SEER RESULT
    // =========================================================

    public void SendSeerResultToClient(
        int targetPlayerId,
        string targetPlayerName,
        RoleType targetRole,
        ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "SEER RESULT: Chỉ Server mới được gửi kết quả!"
            );

            return;
        }


        if (NetworkManager.Singleton == null)
            return;


        if (
            !NetworkManager.Singleton.ConnectedClients
                .ContainsKey(clientId)
        )
        {
            Debug.LogWarning(
                "SEER RESULT: Client không tồn tại!"
            );

            return;
        }


        ClientRpcParams rpcParams =
            new ClientRpcParams
            {
                Send =
                    new ClientRpcSendParams
                    {
                        TargetClientIds =
                            new ulong[]
                            {
                                clientId
                            }
                    }
            };


        SendSeerResultClientRpc(
            targetPlayerId,
            targetPlayerName,
            targetRole,
            rpcParams
        );
    }


    [ClientRpc]
    private void SendSeerResultClientRpc(
        int targetPlayerId,
        string targetPlayerName,
        RoleType targetRole,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log(
            "SEER RESULT | Player "
            + targetPlayerId
            + " | "
            + targetPlayerName
            + " | Role = "
            + targetRole
        );


        OnSeerResultReceived?.Invoke(
            targetPlayerId,
            targetPlayerName,
            targetRole
        );
    }
}