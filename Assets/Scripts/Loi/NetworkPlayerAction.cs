using System;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    // =============================================
    // NETWORK ACTION EVENT
    // Dev2 có thể subscribe để nhận Action
    // =============================================

    public static event Action<NetworkActionEvent> OnNetworkActionAccepted;


    [ServerRpc(RequireOwnership = false)]
    public void RequestActionServerRpc(ulong targetPlayerId)
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

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetPlayerId))
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
        // 3. Kiểm tra Player thực hiện Action
        // =========================================

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(OwnerClientId))
        {
            Debug.LogWarning(
                "SERVER: Player gửi request không tồn tại!"
            );

            return;
        }

        NetworkClient requesterClient =
            NetworkManager.Singleton.ConnectedClients[OwnerClientId];

        if (requesterClient.PlayerObject == null)
        {
            Debug.LogWarning(
                "SERVER: Player Object không tồn tại!"
            );

            return;
        }

        PlayerController requester =
            requesterClient.PlayerObject.GetComponent<PlayerController>();

        if (requester == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy PlayerController!"
            );

            return;
        }

        // =========================================
        // 4. Lấy State của Player thực hiện Action
        // =========================================

        NetworkPlayerStateSync requesterState =
            requester.GetComponent<NetworkPlayerStateSync>();

        if (requesterState == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy NetworkPlayerStateSync của Player!"
            );

            return;
        }

        // =========================================
        // 5. Player chết / Spectating không được Action
        // =========================================

        if (requesterState.State.Value == NetworkPlayerStateType.Dead ||
            requesterState.State.Value == NetworkPlayerStateType.Spectating)
        {
            Debug.LogWarning(
                "SERVER: Player " +
                OwnerClientId +
                " không được phép Action vì đã chết!"
            );

            SendActionResultClientRpc(
                false,
                "Player đã chết, không thể Action!",
                CreateTargetParams()
            );

            return;
        }

        // =========================================
        // 6. KIỂM TRA GAME PHASE
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
                "SERVER: Không thể Action ở Phase " +
                currentPhase
            );

            SendActionResultClientRpc(
                false,
                "Không thể Action ở Phase " +
                currentPhase +
                "!",
                CreateTargetParams()
            );

            return;
        }

        Debug.Log(
            "SERVER: Action được phép ở Phase " +
            currentPhase
        );

        // =========================================
        // 7. Lấy Target Player
        // =========================================

        NetworkClient targetClient =
            NetworkManager.Singleton.ConnectedClients[targetPlayerId];

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
            targetClient.PlayerObject.GetComponent<PlayerController>();

        if (target == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy Target PlayerController!"
            );

            return;
        }

        // =========================================
        // 8. Lấy State của Target
        // =========================================

        NetworkPlayerStateSync targetState =
            target.GetComponent<NetworkPlayerStateSync>();

        if (targetState == null)
        {
            Debug.LogWarning(
                "SERVER: Không tìm thấy NetworkPlayerStateSync của Target!"
            );

            return;
        }

        // =========================================
        // 9. Target chết / Spectating
        // =========================================

        if (targetState.State.Value == NetworkPlayerStateType.Dead ||
            targetState.State.Value == NetworkPlayerStateType.Spectating)
        {
            Debug.LogWarning(
                "SERVER: Target Player " +
                targetPlayerId +
                " đã chết!"
            );

            SendActionResultClientRpc(
                false,
                "Target đã chết!",
                CreateTargetParams()
            );

            return;
        }

        // =========================================
        // 10. ACTION HỢP LỆ
        // =========================================

        Debug.Log(
            "SERVER: Action hợp lệ | Player " +
            OwnerClientId +
            " → Player " +
            targetPlayerId
        );

        SendActionResultClientRpc(
            true,
            "Action hợp lệ!",
            CreateTargetParams()
        );

        // =========================================
        // 11. TẠO NETWORK ACTION EVENT
        // =========================================

        NetworkActionEvent actionEvent =
            new NetworkActionEvent(
                OwnerClientId,
                targetPlayerId
            );

        Debug.Log(
            "NETWORK ACTION EVENT | Player " +
            actionEvent.RequesterPlayerId +
            " → Player " +
            actionEvent.TargetPlayerId
        );

        // =========================================
        // 12. GỬI EVENT CHO HỆ THỐNG
        // =========================================

        OnNetworkActionAccepted?.Invoke(actionEvent);
    }


    // =============================================
    // Tạo ClientRpcParams
    // Gửi kết quả về đúng Client
    // =============================================

    private ClientRpcParams CreateTargetParams()
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[]
                {
                    OwnerClientId
                }
            }
        };
    }


    // =============================================
    // Server → Client
    // =============================================

    [ClientRpc]
    private void SendActionResultClientRpc(
        bool success,
        string message,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log(
            "ACTION RESULT | Success = " +
            success +
            " | " +
            message
        );
    }
}