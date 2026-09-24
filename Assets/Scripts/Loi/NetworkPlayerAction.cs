using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    // =========================================
    // Events
    // =========================================

    public static event Action<NetworkActionEvent>
        OnNetworkActionAccepted;

    public static event Action<bool, string>
        OnNetworkActionResult;

    // Seer Result
    public static event Action<int, string, RoleType>
        OnSeerResultReceived;


    // =========================================
    // Request Action
    // =========================================

    [ServerRpc(RequireOwnership = false)]
    public void RequestActionServerRpc(
        ulong targetPlayerId)
    {
        Debug.Log(
            "SERVER: Player " +
            OwnerClientId +
            " yêu cầu Action lên Player " +
            targetPlayerId
        );


        // =========================================
        // 1. Kiểm tra Target tồn tại
        // =========================================

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(
            targetPlayerId))
        {
            Debug.LogWarning(
                "SERVER: Target Player không tồn tại!"
            );

            SendActionResultClientRpc(
                false,
                "Target Player không tồn tại!",
                CreateTargetParams()
            );

            return;
        }


        // =========================================
        // 2. Không được Action chính mình
        // =========================================

        if (OwnerClientId == targetPlayerId)
        {
            Debug.LogWarning(
                "SERVER: Không thể Action chính mình!"
            );

            SendActionResultClientRpc(
                false,
                "Không thể Action chính mình!",
                CreateTargetParams()
            );

            return;
        }


        // =========================================
        // 3. Kiểm tra Requester
        // =========================================

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(
            OwnerClientId))
        {
            Debug.LogWarning(
                "SERVER: Player gửi request không tồn tại!"
            );

            return;
        }


        NetworkClient requesterClient =
            NetworkManager.Singleton.ConnectedClients[
                OwnerClientId
            ];


        if (requesterClient.PlayerObject == null)
        {
            Debug.LogWarning(
                "SERVER: Player Object không tồn tại!"
            );

            return;
        }


        PlayerController requester =
            requesterClient.PlayerObject
                .GetComponent<PlayerController>();


        if (requester == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy PlayerController!"
            );

            return;
        }


        // =========================================
        // 4. Kiểm tra State Requester
        // =========================================

        NetworkPlayerStateSync requesterState =
            requester.GetComponent<NetworkPlayerStateSync>();


        if (requesterState == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy "
                + "NetworkPlayerStateSync của Player!"
            );

            return;
        }


        if (
            requesterState.State.Value ==
                NetworkPlayerStateType.Dead
            ||
            requesterState.State.Value ==
                NetworkPlayerStateType.Spectating
        )
        {
            Debug.LogWarning(
                "SERVER: Player "
                + OwnerClientId
                + " không được phép Action vì đã chết!"
            );

            SendActionResultClientRpc(
                false,
                "Player đã chết, không thể Action!",
                CreateTargetParams()
            );

            return;
        }


        // =========================================
        // 5. Kiểm tra Phase
        // =========================================

        if (NetworkPhaseSync.Instance == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy NetworkPhaseSync!"
            );

            return;
        }


        GamePhase currentPhase =
            NetworkPhaseSync.Instance.CurrentPhase.Value;


        if (currentPhase != GamePhase.Night)
        {
            Debug.LogWarning(
                "SERVER: Không thể Action ở Phase "
                + currentPhase
            );

            SendActionResultClientRpc(
                false,
                "Không thể Action ở Phase "
                + currentPhase + "!",
                CreateTargetParams()
            );

            return;
        }


        Debug.Log(
            "SERVER: Action được phép ở Phase "
            + currentPhase
        );


        // =========================================
        // 6. Lấy Target Client
        // =========================================

        NetworkClient targetClient =
            NetworkManager.Singleton.ConnectedClients[
                targetPlayerId
            ];


        if (targetClient.PlayerObject == null)
        {
            Debug.LogWarning(
                "SERVER: Target Player Object không tồn tại!"
            );

            SendActionResultClientRpc(
                false,
                "Target Player Object không tồn tại!",
                CreateTargetParams()
            );

            return;
        }


        PlayerController target =
            targetClient.PlayerObject
                .GetComponent<PlayerController>();


        if (target == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy Target PlayerController!"
            );

            return;
        }


        // =========================================
        // 7. Kiểm tra State Target
        // =========================================

        NetworkPlayerStateSync targetState =
            target.GetComponent<NetworkPlayerStateSync>();


        if (targetState == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy "
                + "NetworkPlayerStateSync của Target!"
            );

            return;
        }


        if (
            targetState.State.Value ==
                NetworkPlayerStateType.Dead
            ||
            targetState.State.Value ==
                NetworkPlayerStateType.Spectating
        )
        {
            Debug.LogWarning(
                "SERVER: Target Player "
                + targetPlayerId
                + " đã chết!"
            );

            SendActionResultClientRpc(
                false,
                "Target đã chết!",
                CreateTargetParams()
            );

            return;
        }


        // =========================================
        // 8. Action hợp lệ
        // =========================================

        Debug.Log(
            "SERVER: Action hợp lệ | Player "
            + OwnerClientId
            + " → Player "
            + targetPlayerId
        );


        // =========================================
        // 9. Gửi Action Result
        // =========================================

        SendActionResultClientRpc(
            true,
            "Action hợp lệ!",
            CreateTargetParams()
        );


        // =========================================
        // 10. Tạo Network Action Event
        // =========================================

        NetworkActionEvent actionEvent =
            new NetworkActionEvent(
                OwnerClientId,
                targetPlayerId
            );


        Debug.Log(
            "NETWORK ACTION EVENT | Player "
            + actionEvent.RequesterPlayerId
            + " → "
            + actionEvent.TargetPlayerId
        );


        OnNetworkActionAccepted?.Invoke(
            actionEvent
        );
    }


    // =========================================
    // ClientRpc Params
    // =========================================

    private ClientRpcParams CreateTargetParams()
    {
        return new ClientRpcParams
        {
            Send =
                new ClientRpcSendParams
                {
                    TargetClientIds =
                        new ulong[]
                        {
                            OwnerClientId
                        }
                }
        };
    }


    // =========================================
    // Action Result
    // =========================================

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


    // =========================================
    // SEER RESULT
    // =========================================

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


    // =========================================
    // Seer Result ClientRpc
    // =========================================

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