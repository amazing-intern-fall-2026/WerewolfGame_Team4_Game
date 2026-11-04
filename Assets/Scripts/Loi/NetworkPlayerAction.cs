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
    // PENDING ACTION
    //
    // PlayerID -> NightCycle đang chờ kết quả Action
    //
    // Chỉ khi Action thành công mới chuyển sang
    // playersActionNightCycle.
    // =========================================================

    private static readonly Dictionary<ulong, int>
        pendingActionNightCycle =
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

    public static event Action<int, string>
        OnShamanVictimPromptReceived;

    public static bool HasNetworkActionResultSubscribers()
    {
        return OnNetworkActionResult != null;
    }

    // =========================================================
    // RESET SERVER ACTION DATA
    // =========================================================

    public static void ResetServerActionData()
    {
        playersActionNightCycle.Clear();
        pendingActionNightCycle.Clear();
        // Không null hóa các static event (OnNetworkActionResult, OnNetworkActionAccepted, ...)
        // vì các UI MonoBehaviour (NetworkSkillUI) subscribe khi khởi tạo.
        // Null hóa khi spawn/despawn làm mất kết nối nhận kết quả RPC của Client/Host.

        try { Debug.Log("SERVER ACTION DATA RESET"); } catch { }
    }

    public static void RemoveDisconnectedPlayerAction(ulong clientId)
    {
        playersActionNightCycle.Remove(clientId);
        pendingActionNightCycle.Remove(clientId);
        try { Debug.Log($"SERVER ACTION DATA: Đã xóa action lock của clientId {clientId}"); } catch { }
    }

    // =========================================================
    // NETWORK SPAWN & DESPAWN
    // =========================================================

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            ResetServerActionData();

            Debug.Log(
                "NetworkPlayerAction Spawned | Player="
                + (OwnerClientId + 1)
            );
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            ResetServerActionData();
        }
    }

    // =========================================================
    // REQUEST ACTION
    // =========================================================

    [ServerRpc(RequireOwnership = false)]
    public void RequestActionServerRpc(
        ulong targetPlayerId,
        int subActionId = 0,
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
        // GAME OVER GUARD — Chặn action sau khi ván kết thúc
        // =====================================================

        if (NetworkGameOverManager.GameEnded)
        {
            SendActionResultToClient(
                false,
                "Ván đã kết thúc, không thể thực hiện action.",
                requesterClientId
            );

            Debug.LogWarning(
                "SERVER ACTION BLOCKED | Game Over | Player="
                + (requesterClientId + 1)
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
        // Night 1 -> 1
        // Night 2 -> 2
        // Night 3 -> 3
        // =====================================================

        int currentNightCycle = 1;
        if (NetworkPhaseSync.Instance != null && NetworkPhaseSync.Instance.CurrentNightCycle.Value > 0)
        {
            currentNightCycle = NetworkPhaseSync.Instance.CurrentNightCycle.Value;
        }
        else if (NightManager.Instance != null && NightManager.Instance.CurrentNightNumber > 0)
        {
            currentNightCycle = NightManager.Instance.CurrentNightNumber;
        }
        else if (GameRoleManager.Instance != null && GameRoleManager.Instance.currentDay > 0)
        {
            currentNightCycle = GameRoleManager.Instance.currentDay;
        }

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

        bool isShamanRole = false;
        if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null)
        {
            if (RoleManager.Instance.playerRoles.TryGetValue((int)requesterClientId, out BaseRole requesterRoleCheck) && requesterRoleCheck != null)
            {
                isShamanRole = requesterRoleCheck.roleType == RoleType.Shaman;
            }
        }
        if (!isShamanRole && PlayerManager.Instance != null)
        {
            PlayerData pCheck = PlayerManager.Instance.GetplayerByID((int)requesterClientId);
            if (pCheck != null && pCheck.roleType == RoleType.Shaman)
            {
                isShamanRole = true;
            }
        }

        // Shaman kiểm tra giới hạn bùa theo toàn trận đấu (Save dùng tối đa 1 lần, Kill dùng tối đa 1 lần)
        if (isShamanRole)
        {
            if (subActionId == 1 && NetworkRoleActionBridge.IsShamanSaveUsed((int)requesterClientId))
            {
                SendActionResultToClient(
                    false,
                    "Bùa Lợi (Cứu) đã được sử dụng trước đó!",
                    requesterClientId
                );
                return;
            }

            if (subActionId == 2 && NetworkRoleActionBridge.IsShamanKillUsed((int)requesterClientId))
            {
                SendActionResultToClient(
                    false,
                    "Bùa Hại (Giết) đã được sử dụng trước đó!",
                    requesterClientId
                );
                return;
            }
        }
        else if (
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
            ||
            requesterState.State.Value ==
                NetworkPlayerStateType.Disconnected
            ||
            (PlayerManager.Instance != null && PlayerManager.Instance.GetplayerByID((int)requesterClientId) != null && !PlayerManager.Instance.GetplayerByID((int)requesterClientId).isAlive)
        )
        {
            SendActionResultToClient(
                false,
                "Player đã chết hoặc ngắt kết nối, không thể Action!",
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
        //
        // Guardian được phép tự bảo vệ.
        // Các Role khác không được Action chính mình.
        // =====================================================

        bool isGuardian = false;
        bool isShamanSave = false;

        if (
            RoleManager.Instance != null &&
            RoleManager.Instance.playerRoles != null
        )
        {
            if (
                RoleManager.Instance.playerRoles.TryGetValue(
                    (int)requesterClientId,
                    out BaseRole requesterRole
                )
            )
            {
                if (requesterRole != null)
                {
                    isGuardian =
                        requesterRole.roleType ==
                        RoleType.VillageGuardian;

                    isShamanSave =
                        requesterRole.roleType ==
                        RoleType.Shaman &&
                        subActionId == 1;
                }
            }
        }

        bool allowSelfTarget = isGuardian || isShamanSave;

        if (
            requesterClientId == targetPlayerId &&
            !allowSelfTarget
        )
        {
            SendActionResultToClient(
                false,
                "Không thể Action chính mình!",
                requesterClientId
            );

            return;
        }

        if (
            requesterClientId == targetPlayerId &&
            allowSelfTarget
        )
        {
            Debug.Log(
                "SERVER: Cho phép tự Action chính mình."
                + " | Player="
                + (requesterClientId + 1)
                + " | Guardian="
                + isGuardian
                + " | ShamanSave="
                + isShamanSave
            );
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
            ||
            targetState.State.Value ==
                NetworkPlayerStateType.Disconnected
            ||
            (PlayerManager.Instance != null && PlayerManager.Instance.GetplayerByID((int)targetPlayerId) != null && !PlayerManager.Instance.GetplayerByID((int)targetPlayerId).isAlive)
        )
        {
            SendActionResultToClient(
                false,
                "Target đã chết hoặc ngắt kết nối!",
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
                targetPlayerId,
                subActionId
            );

        Debug.Log(
            "SERVER: Network Action hợp lệ | Player "
            + requesterClientId
            + " → Player "
            + targetPlayerId
        );

        // =====================================================
        // PENDING ACTION
        //
        // Chưa đánh dấu Skill đã dùng.
        // Chờ NetworkRoleActionBridge xử lý Role.
        // =====================================================

        pendingActionNightCycle[
            requesterClientId
        ] = currentNightCycle;

        Debug.Log(
            "SERVER ACTION PENDING"
            + " | Player="
            + (requesterClientId + 1)
            + " | NightCycle="
            + currentNightCycle
        );

        // =====================================================
        // SEND ACTION EVENT
        // =====================================================

        OnNetworkActionAccepted?.Invoke(
            actionEvent
        );

        // =====================================================
        // KHÔNG SAVE Ở ĐÂY
        //
        // Save sẽ được thực hiện trong
        // SendActionResultToClient()
        // khi success == true.
        // =====================================================
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

        PlayerData requesterData = PlayerManager.Instance?.GetplayerByID((int)requesterClientId);
        bool isDead = (requesterState != null && requesterState.State.Value == NetworkPlayerStateType.Dead) ||
                      (requesterData != null && !requesterData.isAlive);

        if (!isDead)
            return false;

        if (NetworkPhaseSync.Instance == null)
            return false;

        GamePhase currentPhase = NetworkPhaseSync.Instance.CurrentPhase.Value;
        if (currentPhase == GamePhase.Setup || currentPhase == GamePhase.Night || currentPhase == GamePhase.GameOver)
        {
            return false;
        }

        bool isHunterRole = false;
        if (requesterData != null && requesterData.roleType == RoleType.Hunter)
        {
            isHunterRole = true;
        }
        else if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null &&
            RoleManager.Instance.playerRoles.TryGetValue((int)requesterClientId, out BaseRole role) &&
            role != null && role.roleType == RoleType.Hunter)
        {
            isHunterRole = true;
        }

        if (!isHunterRole) return false;

        if (requesterData != null)
        {
            if (requesterData.hasUsedHunterShot) return false;

            bool validCause = (requesterData.lastDeathCause == DeathCause.Monster ||
                               requesterData.lastDeathCause == DeathCause.Vote ||
                               requesterData.wasVotedOut);
            if (!validCause)
            {
                Debug.LogWarning($"HUNTER ACTION: Requester chết do {requesterData.lastDeathCause}, không được bắn trả thù.");
                return false;
            }
        }

        return true;
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

        PlayerData requester = PlayerManager.Instance?.GetplayerByID((int)requesterClientId);
        PlayerData target = PlayerManager.Instance?.GetplayerByID((int)targetPlayerId);

        if (requester == null || requester.roleType != RoleType.Hunter || requester.isAlive)
        {
            SendActionResultToClient(
                false,
                "Chỉ Thợ Săn đã chết mới có quyền bắn trả thù!",
                requesterClientId
            );
            return;
        }

        if (requester.hasUsedHunterShot)
        {
            SendActionResultToClient(
                false,
                "Thợ Săn đã sử dụng quyền bắn trả thù!",
                requesterClientId
            );
            return;
        }

        bool validCause = (requester.lastDeathCause == DeathCause.Monster ||
                           requester.lastDeathCause == DeathCause.Vote ||
                           requester.wasVotedOut);
        if (!validCause)
        {
            SendActionResultToClient(
                false,
                "Thợ Săn chỉ có thể bắn trả thù khi bị Ma Sói cắn hoặc bị treo cổ!",
                requesterClientId
            );
            return;
        }

        if ((int)targetPlayerId == (int)requesterClientId)
        {
            SendActionResultToClient(
                false,
                "Thợ Săn không thể tự bắn chính mình!",
                requesterClientId
            );
            return;
        }

        if (target == null || !target.isAlive || target.isDisconnected || NetworkGameManager.IsPlayerLeft(targetPlayerId))
        {
            SendActionResultToClient(
                false,
                "Mục tiêu không tồn tại, đã bị loại hoặc đã ngắt kết nối!",
                requesterClientId
            );
            return;
        }

        // Đánh dấu quyền bắn đã sử dụng (chỉ được dùng 1 lần duy nhất)
        requester.hasHunterTrap = false;
        requester.hasUsedHunterShot = true;
        requester.hunterTargetID = (int)targetPlayerId;

        // Đồng bộ HunterRole nếu có
        if (RoleManager.Instance?.playerRoles != null &&
            RoleManager.Instance.playerRoles.TryGetValue((int)requesterClientId, out BaseRole role) &&
            role is HunterRole hunterRole)
        {
            hunterRole.TrySetTrap((int)targetPlayerId, out _);
        }

        // Đồng bộ trạng thái đã bắn đến Client
        NetworkRoleSync.SyncHunterRevengeState(requesterClientId, false, true);

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

        // Bắn tiêu diệt mục tiêu
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

        Debug.Log(
            "HUNTER NETWORK | Hunter "
            + (requesterClientId + 1)
            + " bắn phát đạn trả thù → Player "
            + (targetPlayerId + 1)
            + " chết ngay."
        );

        SendActionResultToClient(
            true,
            $"Thợ Săn đã bắn tiêu diệt {target.DisplayName}!",
            requesterClientId
        );

        // Sau khi Thợ Săn bắn xong, ngay lập tức kiểm tra lại số người sống và phân định thắng thua
        if (NetworkGameOverManager.Instance != null)
        {
            NetworkGameOverManager.Instance.CheckServerWinCondition();
        }
        if (WinConditionManager.Instance != null)
        {
            WinConditionManager.Instance.CheckWinCondition();
        }
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

        // =====================================================
        // COMMIT ACTION
        //
        // Chỉ save Action nếu Role xử lý thành công.
        //
        // success = true
        //      -> playersActionNightCycle được cập nhật.
        //
        // success = false
        //      -> Skill KHÔNG bị consume.
        // =====================================================

        if (
            pendingActionNightCycle.TryGetValue(
                clientId,
                out int pendingNightCycle
            )
        )
        {
            if (success)
            {
                bool requesterIsShaman = false;
                if (RoleManager.Instance != null && RoleManager.Instance.playerRoles != null &&
                    RoleManager.Instance.playerRoles.TryGetValue((int)clientId, out BaseRole r) && r != null)
                {
                    requesterIsShaman = r.roleType == RoleType.Shaman;
                }
                if (!requesterIsShaman && PlayerManager.Instance != null)
                {
                    PlayerData p = PlayerManager.Instance.GetplayerByID((int)clientId);
                    if (p != null && p.roleType == RoleType.Shaman)
                    {
                        requesterIsShaman = true;
                    }
                }

                if (!requesterIsShaman)
                {
                    playersActionNightCycle[
                        clientId
                    ] = pendingNightCycle;
                }

                Debug.Log(
                    "SERVER ACTION SAVED"
                    + " | Player="
                    + (clientId + 1)
                    + " | NightCycle="
                    + pendingNightCycle
                );
            }
            else
            {
                Debug.Log(
                    "SERVER ACTION NOT SAVED"
                    + " | Player="
                    + (clientId + 1)
                    + " | NightCycle="
                    + pendingNightCycle
                    + " | Reason="
                    + message
                );
            }

            pendingActionNightCycle.Remove(
                clientId
            );
        }

        // =====================================================
        // NETWORK MANAGER
        // =====================================================

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

        // =====================================================
        // SEND RESULT TO CORRECT CLIENT
        // =====================================================

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

    // =========================================================
    // SHAMAN VICTIM PROMPT
    // =========================================================

    public void SendShamanVictimPromptToClient(
        int victimPlayerId,
        string victimPlayerName,
        ulong clientId)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "SHAMAN VICTIM PROMPT: Chỉ Server mới được gửi thông báo!"
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
                "SHAMAN VICTIM PROMPT: Client "
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

        SendShamanVictimPromptClientRpc(
            victimPlayerId,
            victimPlayerName,
            rpcParams
        );
    }

    // =========================================================
    // SHAMAN VICTIM PROMPT CLIENT RPC
    // =========================================================

    [ClientRpc]
    private void SendShamanVictimPromptClientRpc(
        int victimPlayerId,
        string victimPlayerName,
        ClientRpcParams rpcParams = default)
    {
        Debug.Log(
            "SHAMAN VICTIM PROMPT CLIENT RPC | Nạn nhân ID="
            + victimPlayerId
            + " | Tên="
            + victimPlayerName
        );

        OnShamanVictimPromptReceived?.Invoke(
            victimPlayerId,
            victimPlayerName
        );
    }
}
