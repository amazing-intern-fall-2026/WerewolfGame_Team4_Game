using UnityEngine;

public class NetworkRoleActionBridge : MonoBehaviour
{
    private void OnEnable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted += OnActionAccepted;
    }

    private void OnDisable()
    {
        NetworkPlayerAction.OnNetworkActionAccepted -= OnActionAccepted;
    }

    private void OnActionAccepted(NetworkActionEvent actionEvent)
    {
        int requesterID = (int)actionEvent.RequesterPlayerId;
        int targetID = (int)actionEvent.TargetPlayerId;

        Debug.Log(
            "ROLE ACTION BRIDGE | Player "
            + requesterID
            + " → Target "
            + targetID
        );

        // =========================================
        // 1. Tìm RoleManger của Dev2
        // =========================================

        RoleManger roleManager = RoleManger.Instance;

        if (roleManager == null)
        {
            roleManager = FindFirstObjectByType<RoleManger>();
        }

        if (roleManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy RoleManger!"
            );
            return;
        }

        // =========================================
        // 2. Kiểm tra playerRoles
        // =========================================

        if (roleManager.playerRoles == null ||
            roleManager.playerRoles.Count == 0)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: playerRoles đang rỗng!"
            );

            Debug.Log(
                "ROLE ACTION BRIDGE: Gọi RoleManger.AssignRole()"
            );

            roleManager.AssignRole();
        }

        // =========================================
        // 3. Tìm Role của Player
        // =========================================

        if (!roleManager.playerRoles.TryGetValue(
            requesterID,
            out BaseRole role))
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy Role của Player "
                + requesterID
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
        // 4. Tìm NightManager
        // =========================================

        NightManager nightManager = NightManager.Instance;

        if (nightManager == null)
        {
            nightManager = FindFirstObjectByType<NightManager>();

            if (nightManager != null)
            {
                NightManager.Instance = nightManager;

                Debug.Log(
                    "ROLE ACTION BRIDGE | Đã tìm thấy và gán NightManager.Instance"
                );
            }
        }

        if (nightManager == null)
        {
            Debug.LogWarning(
                "ROLE ACTION BRIDGE: Không tìm thấy NightManager!"
            );
            return;
        }

        // =========================================
        // 5. Gọi Ability của Role
        // =========================================

        Debug.Log(
            "ROLE ACTION BRIDGE | UseNightAbility("
            + targetID
            + ")"
        );

        role.UseNightAbility(targetID);
    }
}