using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkRoleSync : NetworkBehaviour
{
    private bool roleSent;

    private void Update()
    {
        // Chỉ Server xử lý việc lấy Role
        if (!IsServer)
            return;

        // Đã gửi rồi thì không gửi lại
        if (roleSent)
            return;

        if (RoleManager.Instance == null)
            return;

        if (RoleManager.Instance.playerRoles == null)
            return;

        int playerID = (int)OwnerClientId;

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
        Debug.Log(
            "===================================="
        );

        Debug.Log(
            "NETWORK ROLE SYNC | MY ROLE = "
            + roleType
        );

        Debug.Log(
            "===================================="
        );
    }
}