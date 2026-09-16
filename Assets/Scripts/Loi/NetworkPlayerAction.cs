using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    [ServerRpc(RequireOwnership = false)]
    public void RequestActionServerRpc(ulong targetPlayerId)
    {
        Debug.Log(
            "SERVER: Player " +
            OwnerClientId +
            " yêu cầu Action lên Player " +
            targetPlayerId
        );

        // =========================
        // 1. Kiểm tra Target tồn tại
        // =========================

        if (!NetworkManager.Singleton.ConnectedClients.ContainsKey(targetPlayerId))
        {
            Debug.LogWarning(
                "SERVER: Target Player không tồn tại!"
            );

            return;
        }

        // =========================
        // 2. Không được tác động chính mình
        // =========================

        if (OwnerClientId == targetPlayerId)
        {
            Debug.LogWarning(
                "SERVER: Không thể Action chính mình!"
            );

            return;
        }

        // =========================
        // 3. Kiểm tra Player thực hiện Action
        // =========================

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

        // =========================
        // 4. Không cho Player đã chết Action
        // =========================

        if (requester.State.Value == PlayerState.Dead ||
            requester.State.Value == PlayerState.Spectating)
        {
            Debug.LogWarning(
                "SERVER: Player " +
                OwnerClientId +
                " không được phép Action vì đã chết!"
            );

            return;
        }

        // =========================
        // 5. Action hợp lệ
        // =========================

        Debug.Log(
            "SERVER: Action hợp lệ | Player " +
            OwnerClientId +
            " → Player " +
            targetPlayerId
        );

        // =====================================
        // Chưa xử lý Role / Skill ở đây
        // Dev 2 sẽ sử dụng kết quả này sau.
        // =====================================
    }
}