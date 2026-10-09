using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    public static event Action<NetworkActionEvent> OnNetworkActionAccepted;

    public static event Action<bool, string> OnNetworkActionResult;

    public static event Action<int, string, RoleType> OnSeerResultReceived;

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

        if (NetworkManager.Singleton == null)
            return;

        if (
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(
                targetPlayerId
            )
        )
        {
            SendActionResultToClient(
                false,
                "Target Player không tồn tại!",
                requesterClientId
            );

            return;
        }

        if (requesterClientId == targetPlayerId)
        {
            SendActionResultToClient(
                false,
                "Không thể Action chính mình!",
                requesterClientId
            );

            return;
        }

        if (
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(
                requesterClientId
            )
        )
        {
            Debug.LogWarning(
                "SERVER: Player gửi request không tồn tại!"
            );

            return;
        }

        NetworkClient requesterClient =
            NetworkManager.Singleton.ConnectedClients[
                requesterClientId
            ];

        if (requesterClient.PlayerObject == null)
        {
            SendActionResultToClient(
                false,
                "Player Object không tồn tại!",
                requesterClientId
            );

            return;
        }

        PlayerController requester =
            requesterClient.PlayerObject.GetComponent<PlayerController>();

        if (requester == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy PlayerController!",
                requesterClientId
            );

            return;
        }

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

        NetworkClient targetClient =
            NetworkManager.Singleton.ConnectedClients[
                targetPlayerId
            ];

        if (targetClient.PlayerObject == null)
        {
            SendActionResultToClient(
                false,
                "Target Player Object không tồn tại!",
                requesterClientId
            );

            return;
        }

        PlayerController target =
            targetClient.PlayerObject.GetComponent<PlayerController>();

        if (target == null)
        {
            SendActionResultToClient(
                false,
                "Không tìm thấy Target PlayerController!",
                requesterClientId
            );

            return;
        }

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

        OnNetworkActionAccepted?.Invoke(actionEvent);
    }

    // =========================================================
    // SERVER → CLIENT
    // Gửi kết quả Action cuối cùng về đúng Client
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
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(
                clientId
            )
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
                Send = new ClientRpcSendParams
                {
                    TargetClientIds =
                        new ulong[] { clientId }
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
            !NetworkManager.Singleton.ConnectedClients.ContainsKey(
                clientId
            )
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
                Send = new ClientRpcSendParams
                {
                    TargetClientIds =
                        new ulong[] { clientId }
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