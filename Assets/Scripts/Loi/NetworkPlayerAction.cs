using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkPlayerAction : NetworkBehaviour
{
    // =========================================================
    // ACTION LOCK
    // PlayerID -> NightCycle đã sử dụng Skill
    // =========================================================

    private static readonly Dictionary<ulong, int>
        playersActionNightCycle =
        new Dictionary<ulong, int>();


    // =========================================================
    // EVENTS
    // =========================================================

    public static event Action<NetworkActionEvent>
        OnNetworkActionAccepted;

    public static event Action<bool, string>
        OnNetworkActionResult;

    public static event Action<int, string, RoleType>
        OnSeerResultReceived;


    // =========================================================
    // RESET SERVER ACTION DATA
    // =========================================================

    private static void ResetServerActionData()
    {
        playersActionNightCycle.Clear();

        Debug.Log(
            "SERVER ACTION DATA RESET"
        );
    }


    // =========================================================
    // NETWORK SPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            Debug.Log(
                "NetworkPlayerAction Spawned | Player="
                + (OwnerClientId + 1)
            );
        }
    }


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
        {
            SendActionResultToClient(
                false,
                "NetworkManager không tồn tại!",
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
                "Không tìm thấy NetworkPlayerStateSync!",
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
            NetworkPhaseSync.Instance
                .CurrentPhase.Value;


        // =====================================================
        // CURRENT NIGHT CYCLE
        //
        // Lấy trực tiếp từ NetworkPhaseSync.
        //
        // Night 1 -> 1
        // Night 2 -> 2
        // Night 3 -> 3
        // =====================================================

        int currentNightCycle =
            NetworkPhaseSync.Instance
                .CurrentNightCycle.Value;


        // =====================================================
        // ACTION CHECK
        // =====================================================

        bool hasStoredNight =
            playersActionNightCycle.ContainsKey(
                requesterClientId
            );


        string storedNightText = "NONE";


        if (hasStoredNight)
        {
            storedNightText =
                playersActionNightCycle[
                    requesterClientId
                ].ToString();
        }


        Debug.Log(
            "SERVER ACTION CHECK"
            + " | Player="
            + (requesterClientId + 1)
            + " | CurrentPhase="
            + currentPhase
            + " | CurrentNight="
            + currentNightCycle
            + " | HasStoredNight="
            + hasStoredNight
            + " | StoredNight="
            + storedNightText
        );


        // =====================================================
        // CHECK SKILL USED IN CURRENT NIGHT
        // =====================================================

        if (
            currentPhase == GamePhase.Night
            &&
            playersActionNightCycle.TryGetValue(
                requesterClientId,
                out int usedNightCycle
            )
        )
        {
            // -------------------------------------------------
            // Đã dùng trong đúng Night hiện tại
            // -------------------------------------------------

            if (
                usedNightCycle ==
                currentNightCycle
            )
            {
                SendActionResultToClient(
                    false,
                    "Skill đã được sử dụng trong Night này!",
                    requesterClientId
                );


                Debug.LogWarning(
                    "SERVER ACTION BLOCKED"
                    + " | Player="
                    + (requesterClientId + 1)
                    + " | NightCycle="
                    + currentNightCycle
                );


                return;
            }


            // -------------------------------------------------
            // Night mới -> cho phép sử dụng lại
            // -------------------------------------------------

            Debug.Log(
                "SERVER ACTION ALLOWED"
                + " | Player="
                + (requesterClientId + 1)
                + " | OldNight="
                + usedNightCycle
                + " | CurrentNight="
                + currentNightCycle
            );
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
                "Không tìm thấy Target NetworkPlayerStateSync!",
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
        // NETWORK ACTION EVENT
        // =====================================================

        NetworkActionEvent actionEvent =
            new NetworkActionEvent(
                requesterClientId,
                targetPlayerId
            );


        Debug.Log(
            "SERVER: Network Action hợp lệ | Player "
            + requesterClientId
            + " → Player "
            + targetPlayerId
        );


        OnNetworkActionAccepted?.Invoke(
            actionEvent
        );


        // =====================================================
        // SAVE CURRENT NIGHT
        // =====================================================

        playersActionNightCycle[
            requesterClientId
        ] = currentNightCycle;


        Debug.Log(
            "SERVER ACTION SAVED"
            + " | Player="
            + (requesterClientId + 1)
            + " | NightCycle="
            + currentNightCycle
        );
    }


    // =========================================================
    // HUNTER SPECIAL VALIDATION
    // =========================================================

    private bool IsHunterSpecialAction(
        ulong requesterClientId,
        NetworkPlayerStateSync requesterState)
    {
        if (!IsServer)
            return false;


        if (requesterState == null)
            return false;


        if (
            requesterState.State.Value !=
            NetworkPlayerStateType.Dead
        )
        {
            return false;
        }


        if (NetworkPhaseSync.Instance == null)
            return false;


        if (
            NetworkPhaseSync.Instance
                .CurrentPhase.Value
            != GamePhase.Discussion
        )
        {
            return false;
        }


        if (RoleManager.Instance == null)
            return false;


        if (RoleManager.Instance.playerRoles == null)
            return false;


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
        // CALL DEV2 HUNTER LOGIC
        // =====================================================

        if (
            !hunter.TrySetTrap(
                (int)targetPlayerId,
                out string trapFeedback
            )
        )
        {
            SendActionResultToClient(
                false,
                trapFeedback,
                requesterClientId
            );

            return;
        }


        Debug.Log(
            "HUNTER NETWORK | HunterRole.TrySetTrap("
            + targetPlayerId
            + ")"
        );


        // =====================================================
        // DEATH RESOLVER
        // =====================================================

        if (DeathResolver.Instance == null)
        {
            Debug.LogError(
                "HUNTER NETWORK | DeathResolver NULL."
            );

            SendActionResultToClient(
                false,
                "DeathResolver đang NULL!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // KILL TARGET
        // =====================================================

        bool killed =
            DeathResolver.Instance.TryKillPlayer(
                (int)targetPlayerId,
                DeathCause.Trap
            );


        Debug.Log(
            "HUNTER NETWORK | TryKillPlayer = "
            + killed
        );


        if (!killed)
        {
            SendActionResultToClient(
                false,
                "Bẫy không thể giết Target!",
                requesterClientId
            );

            return;
        }


        // =====================================================
        // HUNTER RESULT
        // =====================================================

        Debug.Log(
            "HUNTER NETWORK | Hunter "
            + (requesterClientId + 1)
            + " đặt bẫy → Player "
            + (targetPlayerId + 1)
            + " chết ngay."
        );


        SendActionResultToClient(
            true,
            "Đặt bẫy thành công! Target đã chết.",
            requesterClientId
        );
    }


    // =========================================================
    // SEND ACTION RESULT
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


    // =========================================================
    // ACTION RESULT CLIENT RPC
    // =========================================================

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
                "SEER RESULT: Client "
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


        SendSeerResultClientRpc(
            targetPlayerId,
            targetPlayerName,
            targetRole,
            rpcParams
        );
    }


    // =========================================================
    // SEER RESULT CLIENT RPC
    // =========================================================

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